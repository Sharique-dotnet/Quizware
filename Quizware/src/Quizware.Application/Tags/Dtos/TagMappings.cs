using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Tags.Dtos;

internal static class TagMappings
{
    public static TagDto ToDto(this Tag tag) => new(tag.Id, tag.Name, tag.ProgramId);
}
