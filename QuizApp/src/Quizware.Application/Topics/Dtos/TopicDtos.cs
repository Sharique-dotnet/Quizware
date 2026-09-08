namespace Quizware.Application.Topics.Dtos;

public sealed record TopicDto(Guid Id, string Name, Guid? ParentTopicId, Guid? ProgramId);
