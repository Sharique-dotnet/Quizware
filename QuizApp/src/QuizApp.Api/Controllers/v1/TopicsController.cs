using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Topics;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/topics")]
[Authorize]
public sealed class TopicsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<TopicResponse>> List(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}")]
    public ActionResult<TopicResponse> GetById(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<TopicResponse> Create(Guid programId, [FromBody] CreateTopicRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public ActionResult<TopicResponse> Update(Guid programId, Guid id, [FromBody] UpdateTopicRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public IActionResult Delete(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);
}
