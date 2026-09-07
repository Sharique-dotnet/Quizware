namespace QuizApp.Api.Contracts.V1.Topics;

public sealed record TopicResponse(Guid Id, string Name, Guid? ParentTopicId, Guid? ProgramId);

public sealed record CreateTopicRequest(string Name, Guid? ParentTopicId);

public sealed record UpdateTopicRequest(string Name, Guid? ParentTopicId);

public sealed record TagResponse(Guid Id, string Name, Guid? ProgramId);

public sealed record CreateTagRequest(string Name);

public sealed record UpdateTagRequest(string Name);
