using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Teams;
using Quizware.Application.Authorization;
using Quizware.Application.Teams.Commands;
using Quizware.Application.Teams.Dtos;
using Quizware.Application.Teams.Queries;
using Quizware.Domain.Enums;
using Quizware.Infrastructure.Imports;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/teams")]
[Authorize]
public sealed class TeamsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly AppDbContext _dbContext;

    public TeamsController(ISender sender, AppDbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamSummaryResponse>>> List(
        Guid programId, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        TeamStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<TeamStatus>(status, ignoreCase: true, out var value))
            {
                ModelState.AddModelError(nameof(status), $"'{status}' is not a recognised team status.");
                return ValidationProblem(ModelState);
            }

            parsedStatus = value;
        }

        var teams = await _sender.Send(new ListTeamsQuery(programId, parsedStatus), cancellationToken);
        return Ok(teams.Select(ToSummaryResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TeamDetailResponse>> GetById(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var team = await _sender.Send(new GetTeamByIdQuery(programId, id), cancellationToken);
        return Ok(ToDetailResponse(team));
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<TeamDetailResponse>> Create(
        Guid programId, [FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var team = await _sender.Send(
            new CreateTeamCommand(programId, request.Code, request.SchoolName, request.DisplayName, request.MemberNames),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = team.Id }, ToDetailResponse(team));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<TeamDetailResponse>> Update(
        Guid programId, Guid id, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var team = await _sender.Send(
            new UpdateTeamCommand(
                programId, id, request.SchoolName, request.DisplayName, request.ShortName,
                request.ContactName, request.ContactPhone, request.ContactEmail),
            cancellationToken);
        return Ok(ToDetailResponse(team));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<IActionResult> Delete(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteTeamCommand(programId, id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<TeamDetailResponse>> ChangeStatus(
        Guid programId, Guid id, [FromBody] ChangeTeamStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TeamStatus>(request.Status, ignoreCase: true, out var status))
        {
            ModelState.AddModelError(nameof(request.Status), $"'{request.Status}' is not a recognised team status.");
            return ValidationProblem(ModelState);
        }

        var team = await _sender.Send(new ChangeTeamStatusCommand(programId, id, status, request.Reason), cancellationToken);
        return Ok(ToDetailResponse(team));
    }

    [HttpPost("{id:guid}/images")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<TeamDetailResponse>> UploadImages(
        Guid programId, Guid id, [FromBody] SetTeamImagesRequest request, CancellationToken cancellationToken)
    {
        var team = await _sender.Send(
            new SetTeamImagesCommand(programId, id, request.ScoreImageUrl, request.SelectionImageUrl), cancellationToken);
        return Ok(ToDetailResponse(team));
    }

    /// <summary>Every data row becomes exactly one ImportBatchRow, valid or
    /// not — this is what "no silent row skipping" means (01-Analysis-
    /// Findings.md's own complaint about the legacy importer).</summary>
    [HttpPost("import/validate")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<TeamImportValidateResponse>> ImportValidate(
        Guid programId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Programs.AnyAsync(p => p.Id == programId, cancellationToken))
        {
            return NotFound();
        }

        var program = await _dbContext.Programs.SingleAsync(p => p.Id == programId, cancellationToken);

        List<TeamImportRawRow> rawRows;
        await using (var stream = file.OpenReadStream())
        {
            rawRows = TeamExcelParser.Parse(stream).ToList();
        }

        var existingCodes = new HashSet<string>(
            await _dbContext.Teams.Where(t => t.ProgramId == programId).Select(t => t.Code).ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);
        var existingCount = existingCodes.Count;
        var codesSeenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ImportType = "Teams",
            FileName = file.FileName,
            TotalRows = rawRows.Count,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "unknown",
        };

        var validSoFar = 0;
        var rows = new List<ImportBatchRow>();
        var results = new List<TeamImportRowResult>();

        foreach (var raw in rawRows)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(raw.Code))
            {
                errors.Add("Code is required.");
            }

            if (string.IsNullOrWhiteSpace(raw.SchoolName))
            {
                errors.Add("SchoolName is required.");
            }

            if (string.IsNullOrWhiteSpace(raw.DisplayName))
            {
                errors.Add("DisplayName is required.");
            }

            if (!string.IsNullOrWhiteSpace(raw.Code))
            {
                if (existingCodes.Contains(raw.Code))
                {
                    errors.Add($"Code '{raw.Code}' is already used by an existing team in this program.");
                }
                else if (!codesSeenInFile.Add(raw.Code))
                {
                    errors.Add($"Code '{raw.Code}' is duplicated elsewhere in this file.");
                }
            }

            if (errors.Count == 0 && program.MaxTeams is int maxTeams && existingCount + validSoFar >= maxTeams)
            {
                errors.Add($"Would exceed the program's configured team limit ({maxTeams}).");
            }

            var isValid = errors.Count == 0;
            if (isValid)
            {
                validSoFar++;
            }

            rows.Add(new ImportBatchRow
            {
                Id = Guid.NewGuid(),
                ImportBatchId = batch.Id,
                RowNumber = raw.RowNumber,
                RawDataJson = JsonSerializer.Serialize(raw),
                IsValid = isValid,
                ValidationErrorsJson = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null,
            });
            results.Add(new TeamImportRowResult(raw.RowNumber, isValid, errors));
        }

        batch.ValidRows = validSoFar;
        batch.InvalidRows = batch.TotalRows - validSoFar;
        batch.State = ImportBatchState.Validated;

        _dbContext.ImportBatches.Add(batch);
        _dbContext.ImportBatchRows.AddRange(rows);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new TeamImportValidateResponse(batch.Id, batch.TotalRows, batch.ValidRows, batch.InvalidRows, results));
    }

    [HttpPost("import/{batchId:guid}/commit")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<TeamImportCommitResponse>> ImportCommit(
        Guid programId, Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _dbContext.ImportBatches
            .SingleOrDefaultAsync(b => b.Id == batchId && b.ProgramId == programId, cancellationToken);
        if (batch is null)
        {
            return NotFound();
        }

        if (batch.State != ImportBatchState.Validated)
        {
            return Conflict(new
            {
                type = "https://quizapp/errors/conflict-state",
                title = "Batch is not ready to commit",
                status = 409,
                detail = $"Import batch '{batchId}' is {batch.State}, not Validated.",
                errorCode = "CONFLICT_STATE",
            });
        }

        var validRows = await _dbContext.ImportBatchRows
            .Where(r => r.ImportBatchId == batchId && r.IsValid)
            .OrderBy(r => r.RowNumber)
            .ToListAsync(cancellationToken);

        var createdCount = 0;
        foreach (var row in validRows)
        {
            var raw = JsonSerializer.Deserialize<TeamImportRawRow>(row.RawDataJson)!;
            try
            {
                var team = await _sender.Send(
                    new CreateTeamCommand(programId, raw.Code!, raw.SchoolName!, raw.DisplayName!, []), cancellationToken);
                row.CreatedEntityId = team.Id;
                createdCount++;
            }
            catch (Exception ex) when (ex is KeyNotFoundException or Quizware.Domain.Common.Exceptions.InvalidStateTransitionException)
            {
                row.IsValid = false;
                var errors = new List<string> { $"No longer valid at commit time: {ex.Message}" };
                row.ValidationErrorsJson = JsonSerializer.Serialize(errors);
            }
        }

        batch.ImportedRows = createdCount;
        batch.State = ImportBatchState.Committed;
        batch.UpdatedAtUtc = DateTime.UtcNow;
        batch.UpdatedBy = User.Identity?.Name ?? "unknown";

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new TeamImportCommitResponse(createdCount));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<TeamHistoryResponse>> History(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var history = await _sender.Send(new GetTeamHistoryQuery(programId, id), cancellationToken);
        return Ok(new TeamHistoryResponse(
            history.Select(h => new TeamHistoryEntry(h.MatchId, h.MatchName, h.Score, h.PlayedAtUtc)).ToList()));
    }

    private static TeamSummaryResponse ToSummaryResponse(TeamSummaryDto dto) =>
        new(dto.Id, dto.Code, dto.SchoolName, dto.DisplayName, dto.Status);

    private static TeamDetailResponse ToDetailResponse(TeamDto dto) => new(
        dto.Id,
        dto.Code,
        dto.SchoolName,
        dto.DisplayName,
        dto.ShortName,
        dto.ScoreImageUrl,
        dto.SelectionImageUrl,
        dto.ContactName,
        dto.ContactPhone,
        dto.ContactEmail,
        dto.Status,
        dto.StatusReason,
        dto.StatusChangedAtUtc,
        dto.Members
            .Select(m => new Quizware.Api.Contracts.V1.Teams.TeamMemberDto(
                m.Id, m.FullName, m.RollNumber, m.ClassName, m.IsCaptain, m.PhotoUrl))
            .ToList());
}
