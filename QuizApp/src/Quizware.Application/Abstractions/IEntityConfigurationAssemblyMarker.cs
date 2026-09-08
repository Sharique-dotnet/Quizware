using System.Reflection;

namespace Quizware.Application.Abstractions;

/// <summary>
/// Lets an optional module (e.g. Quizware.Modules.Buzzer) contribute its own
/// EF Core entity configurations to Infrastructure's AppDbContext without
/// Infrastructure ever referencing that module — and without this project
/// taking a dependency on EF Core. Infrastructure resolves every registered
/// marker and applies configurations from its Assembly (ADR-005/ADR-010:
/// the solution must build with the module deleted).
/// </summary>
public interface IEntityConfigurationAssemblyMarker
{
    Assembly Assembly { get; }
}
