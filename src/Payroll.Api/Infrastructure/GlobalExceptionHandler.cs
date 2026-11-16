using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payroll.Application.Common;
using Payroll.Domain.Common;

namespace Payroll.Api.Infrastructure;

/// <summary>
/// Translates exceptions into RFC 9457 ProblemDetails responses so clients always get a
/// consistent, machine-readable error shape — and never a stack trace.
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail, code) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Message, "not_found"),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message, "conflict"),
            DbUpdateException => (StatusCodes.Status409Conflict, "Conflict", "The change conflicts with existing data.", "conflict"),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Message, e.Code),
            ValidationException e => (StatusCodes.Status400BadRequest, "Validation failed", e.Message, "validation"),
            BadHttpRequestException e => (e.StatusCode, "Bad request", "The request body or parameters could not be read.", "bad_request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.", "internal"),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request failed with {Status} ({Code}): {Message}", status, code, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["code"] = code },
            },
        });
    }
}
