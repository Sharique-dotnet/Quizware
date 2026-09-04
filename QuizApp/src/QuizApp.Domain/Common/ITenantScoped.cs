namespace QuizApp.Domain.Common;

/// <summary>Marks an entity as belonging to exactly one Program (multi-tenancy boundary).</summary>
public interface ITenantScoped
{
    Guid ProgramId { get; }
}
