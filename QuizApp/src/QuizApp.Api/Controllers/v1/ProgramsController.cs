using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Programs;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs")]
[Authorize]
public sealed class ProgramsController : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public ActionResult<IReadOnlyList<ProgramSummaryResponse>> List() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramDetailResponse> GetById(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost]
    [Authorize(Roles = Roles.SuperAdmin)]
    public ActionResult<ProgramDetailResponse> Create([FromBody] CreateProgramRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramDetailResponse> Update(Guid id, [FromBody] UpdateProgramRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramDetailResponse> Clone(Guid id, [FromBody] CloneProgramRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/formats")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramFormatsResponse> GetFormats(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}/formats")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramFormatsResponse> UpdateFormats(Guid id, [FromBody] UpdateProgramFormatsRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/settings")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramSettingsResponse> GetSettings(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}/settings")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramSettingsResponse> UpdateSettings(Guid id, [FromBody] UpdateProgramSettingsRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramValidationResponse> Validate(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramDetailResponse> Activate(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramDetailResponse> Complete(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramDetailResponse> Archive(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/dashboard")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ProgramDashboardResponse> Dashboard(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);
}
