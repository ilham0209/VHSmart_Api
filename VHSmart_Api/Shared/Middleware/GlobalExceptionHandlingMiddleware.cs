using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Shared.Middleware;

// Converts the exceptions of CodingRules 9 into RFC 7807 ProblemDetails.
// Only the user-facing message travels to the client; an unhandled exception always answers a
// fixed generic 500 so internals are never leaked.
public class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    private const string ProblemContentType = "application/problem+json";
    private const string GenericServerError = "An unexpected error occurred. Please try again later.";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
                throw; // the body is already on the wire, a second write would corrupt it

            context.Response.Clear();
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problem) = MapProblem(context, exception);

        context.Response.StatusCode = statusCode;

        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogDebug(exception, "Handled {ExceptionType}", exception.GetType().Name);

        // Declared type object so the runtime type is serialized: ValidationProblemDetails
        // carries the field errors, which ProblemDetails itself does not declare.
        await context.Response.WriteAsJsonAsync(
            (object)problem,
            options: null,
            contentType: ProblemContentType,
            cancellationToken: context.RequestAborted);
    }

    private static (int StatusCode, ProblemDetails Problem) MapProblem(HttpContext context, Exception exception)
    {
        var instance = context.Request.Path.Value;

        switch (exception)
        {
            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).ToArray(),
                        StringComparer.Ordinal);

                return (StatusCodes.Status400BadRequest, new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Instance = instance
                });

            case UnauthorizedException unauthorized:
                return Problem(StatusCodes.Status401Unauthorized, "Unauthorized", unauthorized.Message, instance);

            case NotFoundException notFound:
                return Problem(StatusCodes.Status404NotFound, "Not Found", notFound.Message, instance);

            case ForbiddenException forbidden:
                return Problem(StatusCodes.Status403Forbidden, "Forbidden", forbidden.Message, instance);

            case ConflictException conflict:
                return Problem(StatusCodes.Status409Conflict, "Conflict", conflict.Message, instance);

            case BusinessRuleException businessRule:
                return Problem(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", businessRule.Message, instance);

            default:
                return Problem(StatusCodes.Status500InternalServerError, "Internal Server Error", GenericServerError, instance);
        }
    }

    private static (int StatusCode, ProblemDetails Problem) Problem(int statusCode, string title, string detail, string? instance) =>
        (statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = instance
        });
}
