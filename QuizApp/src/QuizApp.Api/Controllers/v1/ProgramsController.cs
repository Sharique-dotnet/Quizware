using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Programs;
using QuizApp.Application.Authorization;
using QuizApp.Application.Programs.Commands;
using QuizApp.Application.Programs.Dtos;
using QuizApp.Application.Programs.Queries;
using QuizApp.Domain.Enums;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs")]
[Authorize]
public sealed class ProgramsController : ControllerBase
{
    private readonly ISender _sender;

    public ProgramsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public async Task<ActionResult<IReadOnlyList<ProgramSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var programs = await _sender.Send(new ListProgramsQuery(), cancellationToken);
        return Ok(programs.Select(ToSummaryResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var program = await _sender.Send(new GetProgramByIdQuery(id), cancellationToken);
        return Ok(ToDetailResponse(program));
    }

    [HttpPost]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<ActionResult<ProgramDetailResponse>> Create(
        [FromBody] CreateProgramRequest request, CancellationToken cancellationToken)
    {
        var program = await _sender.Send(
            new CreateProgramCommand(request.Code, request.Name, request.Description), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = program.Id }, ToDetailResponse(program));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramDetailResponse>> Update(
        Guid id, [FromBody] UpdateProgramRequest request, CancellationToken cancellationToken)
    {
        var program = await _sender.Send(
            new UpdateProgramCommand(
                id,
                request.Name,
                request.Description,
                request.OrganisationName,
                request.LogoUrl,
                request.ThemePrimaryColor,
                request.ThemeSecondaryColor,
                request.FontFamily),
            cancellationToken);
        return Ok(ToDetailResponse(program));
    }

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramDetailResponse>> Clone(
        Guid id, [FromBody] CloneProgramRequest request, CancellationToken cancellationToken)
    {
        var clone = await _sender.Send(
            new CloneProgramCommand(id, request.NewProgramCode, request.NewProgramName), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = clone.Id }, ToDetailResponse(clone));
    }

    [HttpGet("{id:guid}/formats")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramFormatsResponse>> GetFormats(Guid id, CancellationToken cancellationToken)
    {
        var formats = await _sender.Send(new GetProgramFormatsQuery(id), cancellationToken);
        return Ok(new ProgramFormatsResponse(formats.Select(ToEntry).ToList()));
    }

    [HttpPut("{id:guid}/formats")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramFormatsResponse>> UpdateFormats(
        Guid id, [FromBody] UpdateProgramFormatsRequest request, CancellationToken cancellationToken)
    {
        var entries = new List<ProgramFormatEntryCommand>();
        foreach (var f in request.Formats)
        {
            if (!Enum.TryParse<QuestionFormatCode>(f.FormatCode, ignoreCase: true, out var formatCode))
            {
                ModelState.AddModelError(nameof(f.FormatCode), $"'{f.FormatCode}' is not a recognised question format.");
                return ValidationProblem(ModelState);
            }

            entries.Add(new ProgramFormatEntryCommand(formatCode, f.IsEnabled, f.DisabledReason));
        }

        var formats = await _sender.Send(new UpdateProgramFormatsCommand(id, entries), cancellationToken);
        return Ok(new ProgramFormatsResponse(formats.Select(ToEntry).ToList()));
    }

    [HttpGet("{id:guid}/settings")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramSettingsResponse>> GetSettings(Guid id, CancellationToken cancellationToken)
    {
        var settings = await _sender.Send(new GetProgramSettingsQuery(id), cancellationToken);
        return Ok(new ProgramSettingsResponse(settings.Select(ToEntry).ToList()));
    }

    [HttpPut("{id:guid}/settings")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramSettingsResponse>> UpdateSettings(
        Guid id, [FromBody] UpdateProgramSettingsRequest request, CancellationToken cancellationToken)
    {
        var entries = request.Settings
            .Select(s => new ProgramSettingEntryCommand(s.Category, s.Key, s.Value))
            .ToList();
        var settings = await _sender.Send(new UpdateProgramSettingsCommand(id, entries), cancellationToken);
        return Ok(new ProgramSettingsResponse(settings.Select(ToEntry).ToList()));
    }

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramValidationResponse>> Validate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ValidateProgramQuery(id), cancellationToken);
        return Ok(new ProgramValidationResponse(result.ReadyToGoLive, result.Blockers));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramDetailResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var program = await _sender.Send(new ActivateProgramCommand(id), cancellationToken);
        return Ok(ToDetailResponse(program));
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramDetailResponse>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var program = await _sender.Send(new CompleteProgramCommand(id), cancellationToken);
        return Ok(ToDetailResponse(program));
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramDetailResponse>> Archive(Guid id, CancellationToken cancellationToken)
    {
        var program = await _sender.Send(new ArchiveProgramCommand(id), cancellationToken);
        return Ok(ToDetailResponse(program));
    }

    [HttpGet("{id:guid}/dashboard")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ProgramDashboardResponse>> Dashboard(Guid id, CancellationToken cancellationToken)
    {
        var dashboard = await _sender.Send(new GetProgramDashboardQuery(id), cancellationToken);
        return Ok(new ProgramDashboardResponse(
            dashboard.TeamCount, dashboard.QuestionCount, dashboard.StageCount, dashboard.MatchCount, dashboard.Warnings));
    }

    private static ProgramSummaryResponse ToSummaryResponse(ProgramSummaryDto dto) =>
        new(dto.Id, dto.Code, dto.Name, dto.State, dto.CreatedAtUtc);

    private static ProgramDetailResponse ToDetailResponse(ProgramDto dto) => new(
        dto.Id,
        dto.Code,
        dto.Name,
        dto.State,
        dto.Description,
        dto.OrganisationName,
        dto.LogoUrl,
        dto.ThemePrimaryColor,
        dto.ThemeSecondaryColor,
        dto.FontFamily,
        dto.CreatedAtUtc,
        dto.UpdatedAtUtc);

    private static ProgramFormatEntry ToEntry(ProgramFormatDto dto) =>
        new(dto.FormatCode, dto.IsEnabled, dto.DisplayOrder, dto.DisabledReason);

    private static ProgramSettingEntry ToEntry(ProgramSettingDto dto) =>
        new(dto.Category, dto.Key, dto.Value);
}
