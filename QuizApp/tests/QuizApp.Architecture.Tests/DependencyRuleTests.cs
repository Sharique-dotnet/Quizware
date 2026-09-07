using FluentAssertions;
using NetArchTest.Rules;

namespace QuizApp.Architecture.Tests;

/// <summary>
/// Enforces the dependency rule from ADR-010: arrows point inward. Domain
/// depends on nothing; Application depends only on Domain; Infrastructure
/// and Api depend on Application and Domain; nothing depends on Api.
/// </summary>
public class DependencyRuleTests
{
    private const string DomainNamespace = "QuizApp.Domain";
    private const string ApplicationNamespace = "QuizApp.Application";
    private const string InfrastructureNamespace = "QuizApp.Infrastructure";
    private const string ApiNamespace = "QuizApp.Api";
    private const string BuzzerModuleNamespace = "QuizApp.Modules.Buzzer";

    [Fact]
    public void Domain_Should_Not_DependOn_AnyOtherProject()
    {
        var result = Types.InAssembly(typeof(QuizApp.Domain.Common.BaseEntity).Assembly)
            .Should()
            .NotHaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace, BuzzerModuleNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Application_Should_Only_DependOn_Domain()
    {
        var result = Types.InAssembly(typeof(QuizApp.Application.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureNamespace, ApiNamespace, BuzzerModuleNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_DependOn_Api()
    {
        var result = Types.InAssembly(typeof(QuizApp.Infrastructure.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    // QuizApp.Modules.Buzzer has no types yet (P14 is deliberately last) —
    // once it does, add a test here asserting it depends on Application's
    // ports only, never on Infrastructure or Api directly (ADR-005).

    [Fact]
    public void Nothing_Should_DependOn_Api()
    {
        var offendingAssemblies = new[]
        {
            typeof(QuizApp.Domain.Common.BaseEntity).Assembly,
            typeof(QuizApp.Application.DependencyInjection).Assembly,
            typeof(QuizApp.Infrastructure.DependencyInjection).Assembly,
        };

        foreach (var assembly in offendingAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOn(ApiNamespace)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(FailureSummary(result));
        }
    }

    private static string FailureSummary(TestResult result) =>
        result.FailingTypeNames is null
            ? "unknown failure"
            : string.Join(", ", result.FailingTypeNames);
}
