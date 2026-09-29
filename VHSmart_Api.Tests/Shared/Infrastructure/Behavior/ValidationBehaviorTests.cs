using FluentValidation;
using VHSmart_Api.Shared.Infrastructure.Behavior;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Behavior;

public class ValidationBehaviorTests
{
    private sealed record TestRequest(string? Name);

    private sealed class TestNameValidator : AbstractValidator<TestRequest>
    {
        public TestNameValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    private sealed class TestCodeValidator : AbstractValidator<TestRequest>
    {
        public TestCodeValidator()
        {
            RuleFor(x => x.Name).Equal("OK").WithMessage("Name must be OK.");
        }
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([]);

        var response = await behavior.Handle(new TestRequest(null), _ => Task.FromResult("handled"), CancellationToken.None);

        Assert.Equal("handled", response);
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new TestNameValidator()]);

        var response = await behavior.Handle(new TestRequest("OK"), _ => Task.FromResult("handled"), CancellationToken.None);

        Assert.Equal("handled", response);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationExceptionWithFieldErrors()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new TestNameValidator()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new TestRequest(string.Empty), _ => Task.FromResult("handled"), CancellationToken.None));

        Assert.Contains(exception.Errors, failure => failure.PropertyName == "Name");
    }

    [Fact]
    public async Task Handle_MultipleValidatorsWithFailures_ThrowsWithAllErrors()
    {
        var behavior = new ValidationBehavior<TestRequest, string>(
        [
            new TestNameValidator(),
            new TestCodeValidator()
        ]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new TestRequest(null), _ => Task.FromResult("handled"), CancellationToken.None));

        Assert.Contains(exception.Errors, failure => failure.ErrorMessage == "'Name' must not be empty.");
        Assert.Contains(exception.Errors, failure => failure.ErrorMessage == "Name must be OK.");
    }
}
