using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Questions;
using Quizware.Api.Contracts.V1.Questions.Formats;
using Quizware.Application.Authorization;
using Quizware.Application.Media.Commands;
using Quizware.Application.QuestionBank.Commands;
using Quizware.Application.QuestionBank.Dtos;
using Quizware.Application.QuestionBank.Queries;
using Quizware.Domain.Enums;
using Quizware.Infrastructure.Imports;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/questions")]
[Authorize]
public sealed class QuestionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly AppDbContext _dbContext;

    public QuestionsController(ISender sender, AppDbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<IReadOnlyList<QuestionSummaryResponse>>> List(
        Guid programId, [FromQuery] string? formatCode, [FromQuery] byte? difficultyLevelId,
        [FromQuery] Guid? topicId, [FromQuery] Guid? tagId, [FromQuery] string? status, [FromQuery] string? text,
        CancellationToken cancellationToken)
    {
        QuestionFormatCode? parsedFormat = null;
        if (formatCode is not null)
        {
            if (!Enum.TryParse<QuestionFormatCode>(formatCode, ignoreCase: true, out var f))
            {
                return BadRequest($"'{formatCode}' is not a recognised question format.");
            }

            parsedFormat = f;
        }

        QuestionStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<QuestionStatus>(status, ignoreCase: true, out var s))
            {
                return BadRequest($"'{status}' is not a recognised question status.");
            }

            parsedStatus = s;
        }

        var questions = await _sender.Send(
            new ListQuestionsQuery(programId, parsedFormat, difficultyLevelId, topicId, parsedStatus, text), cancellationToken);

        return Ok(questions.Select(q => new QuestionSummaryResponse(q.Id, q.FormatCode, q.DifficultyLevel, q.TopicName, q.Status, q.QuestionText)).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> GetById(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var question = await _sender.Send(new GetQuestionByIdQuery(programId, id), cancellationToken);
        return Ok(QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("mcq")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateMcq(
        Guid programId, [FromBody] CreateMcqQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("buzzer")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateBuzzer(
        Guid programId, [FromBody] CreateBuzzerQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("passing")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreatePassing(
        Guid programId, [FromBody] CreatePassingQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("card")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateCard(
        Guid programId, [FromBody] CreateCardQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("choice")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateChoice(
        Guid programId, [FromBody] CreateChoiceQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("rapid-fire")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateRapidFire(
        Guid programId, [FromBody] CreateRapidFireQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("tie-breaker")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateTieBreaker(
        Guid programId, [FromBody] CreateTieBreakerQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("sequence")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateSequence(
        Guid programId, [FromBody] CreateSequenceQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("audio-visual")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateAudioVisual(
        Guid programId, [FromBody] CreateAudioVisualQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("visual-rapid-fire")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> CreateVisualRapidFire(
        Guid programId, [FromBody] CreateVisualRapidFireQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = ToCommand(programId, request, replacesQuestionId: null);
        var question = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = question.Id }, QuestionResponseMapper.ToResponse(question));
    }

    /// <summary>P6-17: PUT never mutates in place — it always creates a new
    /// version via the same per-format Create command, linking back via
    /// ReplacesQuestionId. The body is the same per-format create shape;
    /// formatCode picks which one to deserialize into.</summary>
    [HttpPut("{formatCode}/{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionResponse>> Update(
        Guid programId, string formatCode, Guid id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<QuestionFormatCode>(formatCode, ignoreCase: true, out var format))
        {
            return BadRequest($"'{formatCode}' is not a recognised question format.");
        }

        var json = body.GetRawText();
        IRequest<QuestionDto> command = format switch
        {
            QuestionFormatCode.Mcq => ToCommand(programId, Deserialize<CreateMcqQuestionRequest>(json), id),
            QuestionFormatCode.Buzzer => ToCommand(programId, Deserialize<CreateBuzzerQuestionRequest>(json), id),
            QuestionFormatCode.Passing => ToCommand(programId, Deserialize<CreatePassingQuestionRequest>(json), id),
            QuestionFormatCode.Card => ToCommand(programId, Deserialize<CreateCardQuestionRequest>(json), id),
            QuestionFormatCode.Choice => ToCommand(programId, Deserialize<CreateChoiceQuestionRequest>(json), id),
            QuestionFormatCode.RapidFire => ToCommand(programId, Deserialize<CreateRapidFireQuestionRequest>(json), id),
            QuestionFormatCode.TieBreaker => ToCommand(programId, Deserialize<CreateTieBreakerQuestionRequest>(json), id),
            QuestionFormatCode.Sequence => ToCommand(programId, Deserialize<CreateSequenceQuestionRequest>(json), id),
            QuestionFormatCode.AudioVisual => ToCommand(programId, Deserialize<CreateAudioVisualQuestionRequest>(json), id),
            QuestionFormatCode.VisualRapidFire => ToCommand(programId, Deserialize<CreateVisualRapidFireQuestionRequest>(json), id),
            _ => throw new NotSupportedException(),
        };

        var question = await _sender.Send(command, cancellationToken);
        return Ok(QuestionResponseMapper.ToResponse(question));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<IActionResult> Delete(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteQuestionCommand(programId, id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<QuestionResponse>> Approve(
        Guid programId, Guid id, [FromBody] ApproveQuestionRequest request, CancellationToken cancellationToken)
    {
        var question = await _sender.Send(new ApproveQuestionCommand(programId, id), cancellationToken);
        return Ok(QuestionResponseMapper.ToResponse(question));
    }

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<QuestionResponse>> Retire(
        Guid programId, Guid id, [FromBody] RetireQuestionRequest request, CancellationToken cancellationToken)
    {
        var question = await _sender.Send(new RetireQuestionCommand(programId, id, request.Reason), cancellationToken);
        return Ok(QuestionResponseMapper.ToResponse(question));
    }

    /// <summary>Scoped to MCQ (P6-19's reference format — see
    /// McqQuestionExcelParser). Mirrors TeamsController's import
    /// validate/commit pattern exactly: ImportBatch/ImportBatchRow are
    /// Infrastructure-only, so this bypasses Application/MediatR for the
    /// batch bookkeeping, but reuses CreateMcqQuestionCommand at commit
    /// time for the actual question creation.</summary>
    [HttpPost("import/{formatCode}/validate")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionImportValidateResponse>> ImportValidate(
        Guid programId, string formatCode, IFormFile file, CancellationToken cancellationToken)
    {
        if (!string.Equals(formatCode, "mcq", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only the 'mcq' format is supported for import in this phase.");
        }

        if (!await _dbContext.Programs.AnyAsync(p => p.Id == programId, cancellationToken))
        {
            return NotFound();
        }

        List<McqImportRawRow> rawRows;
        await using (var stream = file.OpenReadStream())
        {
            rawRows = McqQuestionExcelParser.Parse(stream).ToList();
        }

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ImportType = "Questions.Mcq",
            FileName = file.FileName,
            TotalRows = rawRows.Count,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "unknown",
        };

        var rows = new List<ImportBatchRow>();
        var validCount = 0;
        var errorMessages = new List<string>();

        foreach (var raw in rawRows)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(raw.QuestionText))
            {
                errors.Add("QuestionText is required.");
            }

            if (!byte.TryParse(raw.DifficultyLevelId, out var difficulty) || difficulty is < 1 or > 5)
            {
                errors.Add("DifficultyLevelId must be a number between 1 and 5.");
            }

            var options = new[]
            {
                (raw.Option1, raw.Option1Correct), (raw.Option2, raw.Option2Correct),
                (raw.Option3, raw.Option3Correct), (raw.Option4, raw.Option4Correct),
            }.Where(o => !string.IsNullOrWhiteSpace(o.Item1)).ToList();

            if (options.Count < 2)
            {
                errors.Add("At least 2 options are required.");
            }

            var correctCount = options.Count(o => string.Equals(o.Item2, "true", StringComparison.OrdinalIgnoreCase) || o.Item2 == "1");
            if (options.Count >= 2 && correctCount != 1)
            {
                errors.Add("Exactly one option must be marked correct.");
            }

            var isValid = errors.Count == 0;
            if (isValid)
            {
                validCount++;
            }
            else
            {
                errorMessages.AddRange(errors.Select(e => $"Row {raw.RowNumber}: {e}"));
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
        }

        batch.ValidRows = validCount;
        batch.InvalidRows = batch.TotalRows - validCount;
        batch.State = ImportBatchState.Validated;

        _dbContext.ImportBatches.Add(batch);
        _dbContext.ImportBatchRows.AddRange(rows);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new QuestionImportValidateResponse(batch.Id, batch.TotalRows, batch.ValidRows, errorMessages));
    }

    [HttpPost("import/{batchId:guid}/commit")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionImportCommitResponse>> ImportCommit(
        Guid programId, Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _dbContext.ImportBatches.SingleOrDefaultAsync(b => b.Id == batchId && b.ProgramId == programId, cancellationToken);
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
            var raw = JsonSerializer.Deserialize<McqImportRawRow>(row.RawDataJson)!;
            var options = new List<McqOptionInput>();
            var candidates = new[]
            {
                (raw.Option1, raw.Option1Correct), (raw.Option2, raw.Option2Correct),
                (raw.Option3, raw.Option3Correct), (raw.Option4, raw.Option4Correct),
            };
            var order = 1;
            foreach (var (text, correct) in candidates)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var isCorrect = string.Equals(correct, "true", StringComparison.OrdinalIgnoreCase) || correct == "1";
                options.Add(new McqOptionInput(text, isCorrect, order++, null));
            }

            try
            {
                var command = new CreateMcqQuestionCommand(
                    programId, raw.QuestionText!, byte.Parse(raw.DifficultyLevelId!), null,
                    raw.Language ?? "ur", null, null, [], options, false, true, false);
                await _sender.Send(command, cancellationToken);
                createdCount++;
            }
            catch (Exception ex) when (ex is KeyNotFoundException or Quizware.Domain.Common.Exceptions.InvalidStateTransitionException)
            {
                row.IsValid = false;
                row.ValidationErrorsJson = JsonSerializer.Serialize(new[] { $"No longer valid at commit time: {ex.Message}" });
            }
        }

        batch.ImportedRows = createdCount;
        batch.State = ImportBatchState.Committed;
        batch.UpdatedAtUtc = DateTime.UtcNow;
        batch.UpdatedBy = User.Identity?.Name ?? "unknown";

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new QuestionImportCommitResponse(createdCount));
    }

    [HttpGet("coverage")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<QuestionCoverageResponse>> Coverage(Guid programId, CancellationToken cancellationToken)
    {
        var coverage = await _sender.Send(new GetQuestionCoverageQuery(programId), cancellationToken);
        return Ok(new QuestionCoverageResponse(
            coverage.ReadyToRun,
            coverage.FormatsInUse,
            coverage.FormatsNotUsed,
            coverage.ByStage.Select(s => new QuestionCoverageByStage(
                s.StageName, 0, s.Requirements.Select(r => new QuestionCoverageRequirement(r.Format, [], r.RequiredTotal, r.Available, r.Status, r.Shortfall)).ToList())).ToList(),
            coverage.Blockers));
    }

    [HttpGet("{id:guid}/usage")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionUsageResponse>> Usage(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var usage = await _sender.Send(new GetQuestionUsageQuery(programId, id), cancellationToken);
        return Ok(new QuestionUsageResponse(usage.Select(u => new QuestionUsageEntry(u.MatchId, u.MatchName, u.PlayedAtUtc, false)).ToList()));
    }

    [HttpPost("media")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<MediaAssetResponse>> UploadMedia(
        Guid programId, IFormFile file, [FromForm] bool shared, CancellationToken cancellationToken)
    {
        byte[] content;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream, cancellationToken);
            content = stream.ToArray();
        }

        var media = await _sender.Send(
            new UploadMediaCommand(programId, file.FileName, file.Length, content, shared), cancellationToken);

        return Ok(new MediaAssetResponse(media.Id, media.FileName, media.MediaType, media.MimeType, media.FileSizeBytes, media.IsValidated, media.ProgramId, media.WasDeduplicated));
    }

    [HttpGet("duplicates")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<QuestionDuplicatesResponse>> Duplicates(Guid programId, CancellationToken cancellationToken)
    {
        var pairs = await _sender.Send(new GetQuestionDuplicatesQuery(programId), cancellationToken);
        return Ok(new QuestionDuplicatesResponse(pairs.Select(p => new DuplicateQuestionPair(p.QuestionId, p.DuplicateOfQuestionId, p.SimilarityScore)).ToList()));
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static IReadOnlyList<McqOptionInput> ToOptionInputs(List<OptionDto> options) =>
        options.Select(o => new McqOptionInput(o.Text, o.IsCorrect, o.DisplayOrder, o.MediaAssetId)).ToList();

    private static CreateMcqQuestionCommand ToCommand(Guid programId, CreateMcqQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        ToOptionInputs(r.Options), r.AllowMultipleCorrect, r.ShuffleOptions, r.NegativeMarkingEnabled, replacesQuestionId);

    private static CreateBuzzerQuestionCommand ToCommand(Guid programId, CreateBuzzerQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        ToOptionInputs(r.Options), r.BuzzWindowSeconds, r.LockoutOnWrongAnswer, r.AllowStealAfterWrong, r.StealWindowSeconds, replacesQuestionId);

    private static CreatePassingQuestionCommand ToCommand(Guid programId, CreatePassingQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        ToOptionInputs(r.Options), r.MaxPassCount, r.PassDirection, r.RevealAnswerIfAllPass, replacesQuestionId);

    private static CreateCardQuestionCommand ToCommand(Guid programId, CreateCardQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        ToOptionInputs(r.Options), replacesQuestionId);

    private static CreateChoiceQuestionCommand ToCommand(Guid programId, CreateChoiceQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        ToOptionInputs(r.Options), r.TopicLabel, r.IsExclusiveTopic, replacesQuestionId);

    private static CreateRapidFireQuestionCommand ToCommand(Guid programId, CreateRapidFireQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        r.IsHostRead, r.AnswerText, replacesQuestionId);

    private static CreateTieBreakerQuestionCommand ToCommand(Guid programId, CreateTieBreakerQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        r.AnswerMode, ToOptionInputs(r.Options), r.AnswerText, r.NumericAnswer, replacesQuestionId);

    private static CreateSequenceQuestionCommand ToCommand(Guid programId, CreateSequenceQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        r.Items.Select(i => new SequenceItemInput(i.Text, i.MediaAssetId, i.CorrectPosition, i.DisplayOrder)).ToList(),
        r.PartialCreditEnabled, r.PointsPerCorrectPosition, r.ItemKind, replacesQuestionId);

    private static CreateAudioVisualQuestionCommand ToCommand(Guid programId, CreateAudioVisualQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.QuestionText, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        r.MediaAssetId, r.MediaKind, r.AnswerText, r.AcceptableAnswers, r.PlaybackStartSeconds, r.PlaybackDurationSeconds,
        r.AutoPlay, r.ReplayAllowed, r.RevealMediaAssetId, replacesQuestionId);

    private static CreateVisualRapidFireQuestionCommand ToCommand(Guid programId, CreateVisualRapidFireQuestionRequest r, Guid? replacesQuestionId) => new(
        programId, r.DifficultyLevelId, r.TopicId, r.Language, r.TimeLimitSeconds, r.Source, r.TagIds,
        r.Items.Select(i => new VrfItemInput(i.MediaAssetId, i.AnswerText, i.AcceptableAnswers, i.DisplayOrder)).ToList(),
        r.RevealSecondsPerImage, r.GridColumns, r.ScorePerImage, replacesQuestionId);
}
