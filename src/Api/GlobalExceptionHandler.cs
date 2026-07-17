using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;

namespace ShiftLedger.Api;

// Maps known exceptions to the problem-details shape: 400 validation, 409 concurrency, 422 business-rule.
// Unhandled types fall through to the default handler (500).
public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string title;
        IDictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case ValidationException validation:
                status = StatusCodes.Status400BadRequest;
                title = "Validation failed";
                errors = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;
            case DbUpdateConcurrencyException:
                status = StatusCodes.Status409Conflict;
                title = "The record was changed by someone else. Reload and try again.";
                break;
            case BusinessRuleException business:
                status = StatusCodes.Status422UnprocessableEntity;
                title = business.Message;
                break;
            default:
                return false;
        }

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title };
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
