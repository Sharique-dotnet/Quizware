using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quizware.Application.Abstractions;
using Quizware.Application.Buzzer;
using Quizware.Modules.Buzzer.Abstractions;

namespace Quizware.Modules.Buzzer;

file sealed class BuzzerEntityConfigurationMarker : IEntityConfigurationAssemblyMarker
{
    public Assembly Assembly => typeof(BuzzerEntityConfigurationMarker).Assembly;
}

/// <summary>Registers this module: its persistence contribution and the
/// provider chosen by <c>Buzzer:Provider</c>. Deleting this project removes
/// this one call site too (it is compiled only when the module exists), and
/// the Application layer's null defaults take over.</summary>
public static class BuzzerModuleExtensions
{
    public static IServiceCollection AddBuzzerModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEntityConfigurationAssemblyMarker, BuzzerEntityConfigurationMarker>();

        var section = configuration.GetSection(BuzzerOptions.SectionName);
        services.Configure<BuzzerOptions>(section);
        var options = section.Get<BuzzerOptions>() ?? new BuzzerOptions();

        services.RemoveAll<IBuzzerProvider>();
        switch (options.Provider)
        {
            default:
                services.AddSingleton<IBuzzerProvider>(new NullBuzzerAdapter(
                    options.DeviceCount, $"No buzzer provider is configured (Buzzer:Provider is '{options.Provider}')."));
                break;
        }

        return services;
    }
}
