namespace QuizApp.Api.Contracts.V1.Topics;

public sealed record TopicResponse(Guid Id, string Name, Guid? ParentTopicId, Guid? ProgramId);

/// <summary>Shared = true requests a cross-program topic (ProgramId
/// null) — SuperAdmin only. Without this field there would be no way to
/// invoke P6-14's "or shared" half through the API at all.</summary>
public sealed record CreateTopicRequest(string Name, Guid? ParentTopicId, bool Shared = false);

public sealed record UpdateTopicRequest(string Name, Guid? ParentTopicId);

public sealed record TagResponse(Guid Id, string Name, Guid? ProgramId);

public sealed record CreateTagRequest(string Name, bool Shared = false);

public sealed record UpdateTagRequest(string Name);
