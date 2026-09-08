using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Application.Common.Behaviors;
using QuizApp.Application.Rules.Services;

namespace QuizApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IRuleService, RuleService>();

        return services;
    }
}
