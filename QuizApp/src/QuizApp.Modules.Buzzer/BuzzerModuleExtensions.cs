using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Application.Abstractions;

namespace QuizApp.Modules.Buzzer;

file sealed class BuzzerEntityConfigurationMarker : IEntityConfigurationAssemblyMarker
{
    public Assembly Assembly => typeof(BuzzerEntityConfigurationMarker).Assembly;
}

/// <summary>Registers this module's persistence contribution. Deleting this
/// project means deleting this one call site too — nothing else changes.</summary>
public static class BuzzerModuleExtensions
{
    public static IServiceCollection AddBuzzerModule(this IServiceCollection services)
    {
        services.AddSingleton<IEntityConfigurationAssemblyMarker, BuzzerEntityConfigurationMarker>();
        return services;
    }
}
