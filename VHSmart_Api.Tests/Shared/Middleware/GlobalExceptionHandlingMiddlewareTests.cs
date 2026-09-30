using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Middleware;

namespace VHSmart_Api.Tests.Shared.Middleware;

public class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoException_DoesNotWriteProblem()
    {
        var context = await RunAsync(() => Task.CompletedTask);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(string.Empty, await ReadBodyAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400WithFieldErrors()
    {
        var context = await RunAsync(() => throw new ValidationException(
        [
            new ValidationFailure("Name", "'Name' is required."),
            new ValidationFailure("Code", "'Code' is required.")
        ]));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        using var problem = await ReadProblemAsync(context);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("One or more validation errors occurred.", problem.RootElement.GetProperty("title").GetString());

        var errors = problem.RootElement.GetProperty("errors");
        Assert.Equal("'Name' is required.", errors.GetProperty("Name")[0].GetString());
        Assert.Equal("'Code' is required.", errors.GetProperty("Code")[0].GetString());
    }

    [Fact]
    public async Task InvokeAsync_Unauthorized_Returns401WithMessage()
    {
        var context = await RunAsync(() => throw new UnauthorizedException("Invalid email or password."));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        using var problem = await ReadProblemAsync(context);
        Assert.Equal("Invalid email or password.", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_NotFound_Returns404WithMessage()
    {
        var context = await RunAsync(() => throw new NotFoundException("Record not found."));

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        using var problem = await ReadProblemAsync(context);
        Assert.Equal("Record not found.", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_Forbidden_Returns403WithMessage()
    {
        var context = await RunAsync(() => throw new ForbiddenException("You do not have permission."));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        using var problem = await ReadProblemAsync(context);
        Assert.Equal("You do not have permission.", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_Conflict_Returns409WithSpecMessage()
    {
        const string specMessage = "Data document sequence exist. Please check the existing data.";
        var context = await RunAsync(() => throw new ConflictException(specMessage));

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        using var problem = await ReadProblemAsync(context);
        Assert.Equal(specMessage, problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_BusinessRule_Returns422WithMessage()
    {
        var context = await RunAsync(() => throw new BusinessRuleException("At least one Recommendation is required."));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        using var problem = await ReadProblemAsync(context);
        Assert.Equal("At least one Recommendation is required.", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500WithoutLeakingDetails()
    {
        var context = await RunAsync(() => throw new InvalidOperationException("connection string Password=secret"));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        using var problem = await ReadProblemAsync(context);
        Assert.Equal(
            "An unexpected error occurred. Please try again later.",
            problem.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("secret", await ReadBodyAsync(context));
    }

    private static async Task<HttpContext> RunAsync(Func<Task> action)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => action(),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;

        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context) =>
        await new StreamReader(context.Response.Body).ReadToEndAsync();

    private static async Task<JsonDocument> ReadProblemAsync(HttpContext context) =>
        JsonDocument.Parse(await ReadBodyAsync(context));
}
