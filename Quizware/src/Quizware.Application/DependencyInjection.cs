using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Application.Common.Behaviors;
using Quizware.Application.Rules.Services;
using Quizware.Application.Selection;

namespace Quizware.Application;

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
        services.AddScoped<IQuestionSelector, QuestionSelector>();

        return services;
    }
}
