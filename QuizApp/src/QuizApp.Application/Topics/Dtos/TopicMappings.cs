using QuizApp.Domain.QuestionBank;

namespace QuizApp.Application.Topics.Dtos;

internal static class TopicMappings
{
    public static TopicDto ToDto(this Topic topic) => new(topic.Id, topic.Name, topic.ParentTopicId, topic.ProgramId);
}
