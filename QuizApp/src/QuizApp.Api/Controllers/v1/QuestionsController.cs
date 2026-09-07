using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Questions;
using QuizApp.Api.Contracts.V1.Questions.Formats;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/questions")]
[Authorize]
public sealed class QuestionsController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<IReadOnlyList<QuestionSummaryResponse>> List(Guid programId, [FromQuery] string? formatCode, [FromQuery] byte? difficultyLevelId,
        [FromQuery] Guid? topicId, [FromQuery] Guid? tagId, [FromQuery] string? status, [FromQuery] string? text)
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Polymorphic by format — the response is one of the 10
    /// QuestionResponse subtypes, discriminated by formatCode (§5.8).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> GetById(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("mcq")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateMcq(Guid programId, [FromBody] CreateMcqQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("buzzer")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateBuzzer(Guid programId, [FromBody] CreateBuzzerQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("passing")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreatePassing(Guid programId, [FromBody] CreatePassingQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("card")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateCard(Guid programId, [FromBody] CreateCardQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("choice")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateChoice(Guid programId, [FromBody] CreateChoiceQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("rapid-fire")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateRapidFire(Guid programId, [FromBody] CreateRapidFireQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("tie-breaker")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateTieBreaker(Guid programId, [FromBody] CreateTieBreakerQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("sequence")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateSequence(Guid programId, [FromBody] CreateSequenceQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("audio-visual")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateAudioVisual(Guid programId, [FromBody] CreateAudioVisualQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("visual-rapid-fire")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> CreateVisualRapidFire(Guid programId, [FromBody] CreateVisualRapidFireQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{formatCode}/{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionResponse> Update(Guid programId, string formatCode, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public IActionResult Delete(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<QuestionResponse> Approve(Guid programId, Guid id, [FromBody] ApproveQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<QuestionResponse> Retire(Guid programId, Guid id, [FromBody] RetireQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("import/{formatCode}/validate")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionImportValidateResponse> ImportValidate(Guid programId, string formatCode) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("import/{batchId:guid}/commit")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionImportCommitResponse> ImportCommit(Guid programId, Guid batchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("coverage")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<QuestionCoverageResponse> Coverage(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/usage")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionUsageResponse> Usage(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("media")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public IActionResult UploadMedia(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("duplicates")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<QuestionDuplicatesResponse> Duplicates(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);
}
