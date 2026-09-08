using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Topics;
using QuizApp.Application.Authorization;
using QuizApp.Application.Tags.Commands;
using QuizApp.Application.Tags.Dtos;
using QuizApp.Application.Tags.Queries;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/tags")]
[Authorize]
public sealed class TagsController : ControllerBase
{
    private readonly ISender _sender;

    public TagsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> List(Guid programId, CancellationToken cancellationToken)
    {
        var tags = await _sender.Send(new ListTagsQuery(programId), cancellationToken);
        return Ok(tags.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TagResponse>> GetById(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var tag = await _sender.Send(new GetTagByIdQuery(programId, id), cancellationToken);
        return Ok(ToResponse(tag));
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<TagResponse>> Create(
        Guid programId, [FromBody] CreateTagRequest request, CancellationToken cancellationToken)
    {
        var tag = await _sender.Send(new CreateTagCommand(programId, request.Name, request.Shared), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = tag.Id }, ToResponse(tag));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<ActionResult<TagResponse>> Update(
        Guid programId, Guid id, [FromBody] UpdateTagRequest request, CancellationToken cancellationToken)
    {
        var tag = await _sender.Send(new UpdateTagCommand(programId, id, request.Name), cancellationToken);
        return Ok(ToResponse(tag));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageQuestions)]
    public async Task<IActionResult> Delete(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteTagCommand(programId, id), cancellationToken);
        return NoContent();
    }

    private static TagResponse ToResponse(TagDto dto) => new(dto.Id, dto.Name, dto.ProgramId);
}
