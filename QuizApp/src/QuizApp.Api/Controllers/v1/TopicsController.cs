using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Topics;
using QuizApp.Application.Authorization;
using QuizApp.Application.Topics.Commands;
using QuizApp.Application.Topics.Dtos;
using QuizApp.Application.Topics.Queries;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/topics")]
[Authorize]
public sealed class TopicsController : ControllerBase
{
    private readonly ISender _sender;

    public TopicsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TopicResponse>>> List(Guid programId, CancellationToken cancellationToken)
    {
        var topics = await _sender.Send(new ListTopicsQuery(programId), cancellationToken);
        return Ok(topics.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TopicResponse>> GetById(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var topic = await _sender.Send(new GetTopicByIdQuery(programId, id), cancellationToken);
        return Ok(ToResponse(topic));
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<TopicResponse>> Create(
        Guid programId, [FromBody] CreateTopicRequest request, CancellationToken cancellationToken)
    {
        var topic = await _sender.Send(
            new CreateTopicCommand(programId, request.Name, request.ParentTopicId, request.Shared), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = topic.Id }, ToResponse(topic));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<TopicResponse>> Update(
        Guid programId, Guid id, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        var topic = await _sender.Send(
            new UpdateTopicCommand(programId, id, request.Name, request.ParentTopicId), cancellationToken);
        return Ok(ToResponse(topic));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<IActionResult> Delete(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteTopicCommand(programId, id), cancellationToken);
        return NoContent();
    }

    private static TopicResponse ToResponse(TopicDto dto) => new(dto.Id, dto.Name, dto.ParentTopicId, dto.ProgramId);
}
