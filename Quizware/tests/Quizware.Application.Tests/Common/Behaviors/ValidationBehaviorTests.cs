using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using Quizware.Application.Common.Behaviors;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    public sealed record SampleRequest(string Name) : IRequest<string>;

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([]);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next.Invoke(Arg.Any<CancellationToken>()).Returns("ok");

        var result = await behavior.Handle(new SampleRequest(""), next, CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var validator = Substitute.For<IValidator<SampleRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<SampleRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var behavior = new ValidationBehavior<SampleRequest, string>([validator]);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next.Invoke(Arg.Any<CancellationToken>()).Returns("ok");

        var result = await behavior.Handle(new SampleRequest("valid"), next, CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsWithAllFailures_NeverCallsNext()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required."),
            new("Name", "Name must be at least 3 characters."),
        };

        var validator = Substitute.For<IValidator<SampleRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<SampleRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var behavior = new ValidationBehavior<SampleRequest, string>([validator]);
        var next = Substitute.For<RequestHandlerDelegate<string>>();

        var act = async () => await behavior.Handle(new SampleRequest(""), next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors["Name"].Should().HaveCount(2);
        await next.DidNotReceive().Invoke(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultipleValidators_AggregatesAllFailures()
    {
        var validator1 = Substitute.For<IValidator<SampleRequest>>();
        validator1.ValidateAsync(Arg.Any<ValidationContext<SampleRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Name", "From validator 1")]));

        var validator2 = Substitute.For<IValidator<SampleRequest>>();
        validator2.ValidateAsync(Arg.Any<ValidationContext<SampleRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Email", "From validator 2")]));

        var behavior = new ValidationBehavior<SampleRequest, string>([validator1, validator2]);
        var next = Substitute.For<RequestHandlerDelegate<string>>();

        var act = async () => await behavior.Handle(new SampleRequest(""), next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Keys.Should().BeEquivalentTo("Name", "Email");
    }
}
