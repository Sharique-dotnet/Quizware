using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Topics;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/tags")]
[Authorize]
public sealed class TagsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<TagResponse>> List(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}")]
    public ActionResult<TagResponse> GetById(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<TagResponse> Create(Guid programId, [FromBody] CreateTagRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<TagResponse> Update(Guid programId, Guid id, [FromBody] UpdateTagRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public IActionResult Delete(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);
}
