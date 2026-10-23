using FluentAssertions;
using NetArchTest.Rules;

namespace Quizware.Architecture.Tests;

/// <summary>
/// Enforces the dependency rule from ADR-010: arrows point inward. Domain
/// depends on nothing; Application depends only on Domain; Infrastructure
/// and Api depend on Application and Domain; nothing depends on Api.
/// </summary>
public class DependencyRuleTests
{
    private const string DomainNamespace = "Quizware.Domain";
    private const string ApplicationNamespace = "Quizware.Application";
    private const string InfrastructureNamespace = "Quizware.Infrastructure";
    private const string ApiNamespace = "Quizware.Api";
    private const string BuzzerModuleNamespace = "Quizware.Modules.Buzzer";

    [Fact]
    public void Domain_Should_Not_DependOn_AnyOtherProject()
    {
        var result = Types.InAssembly(typeof(Quizware.Domain.Common.BaseEntity).Assembly)
            .Should()
            .NotHaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace, BuzzerModuleNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Application_Should_Only_DependOn_Domain()
    {
        var result = Types.InAssembly(typeof(Quizware.Application.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureNamespace, ApiNamespace, BuzzerModuleNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_DependOn_Api()
    {
        var result = Types.InAssembly(typeof(Quizware.Infrastructure.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

#if BUZZER_MODULE
    /// <summary>ADR-005: the buzzer module plugs into Application's ports
    /// only — never Infrastructure or the Api — and it never touches scoring:
    /// a buzz says who pressed first, the answer endpoint does the rest.</summary>
    [Fact]
    public void BuzzerModule_Should_DependOn_ApplicationPortsOnly_AndNeverOnScoring()
    {
        var result = Types.InAssembly(typeof(Quizware.Modules.Buzzer.BuzzerModuleExtensions).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                InfrastructureNamespace, ApiNamespace, "Quizware.Application.Scoring", "Quizware.Domain.Scoring")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }
#endif

    [Fact]
    public void Nothing_Should_DependOn_Api()
    {
        var offendingAssemblies = new[]
        {
            typeof(Quizware.Domain.Common.BaseEntity).Assembly,
            typeof(Quizware.Application.DependencyInjection).Assembly,
            typeof(Quizware.Infrastructure.DependencyInjection).Assembly,
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
