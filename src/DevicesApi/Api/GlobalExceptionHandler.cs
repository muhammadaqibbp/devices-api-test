using System.Diagnostics;
using DevicesApi.Application;
using DevicesApi.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevicesApi.Api;

/// <summary>Translates exceptions into RFC 7807 problem responses.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        switch (exception)
        {
            case DomainValidationException validation:
                problem = new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [validation.Field] = [validation.Message]
                })
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred."
                };
                break;

            case DeviceNotFoundException notFound:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Device not found.",
                    Detail = notFound.Message
                };
                break;

            case BusinessRuleViolationException rule:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The request conflicts with the current state of the device.",
                    Detail = rule.Message
                };
                break;

            case DbUpdateConcurrencyException:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The device was modified or deleted by another request.",
                    Detail = "Fetch the device again and retry."
                };
                break;

            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred."
                };
                break;
        }

        if (problem.Status is not StatusCodes.Status500InternalServerError)
        {
            logger.LogInformation("Request rejected with {Status}: {Message}", problem.Status, exception.Message);
        }

        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync<object>(
            problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
