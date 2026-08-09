using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Common;

namespace PermitToWork.Api.ExceptionHandling;

/// <summary>
/// Turns the three exception types this application throws on purpose into the right HTTP
/// status codes, and everything else into a 500 that says nothing useful to an attacker.
/// <para>
/// Written as an <see cref="IExceptionHandler"/> so controllers never need a try/catch.
/// A controller that catches <see cref="DomainException"/> to return a 400 is a controller
/// that will eventually forget to, in exactly one place, and return a 500 to a user who
/// simply typed a date wrong.
/// </para>
/// <list type="bullet">
///   <item><see cref="NotFoundException"/> → 404. Also covers "scoped out of your view".</item>
///   <item><see cref="ConflictException"/> → 409. Duplicate badge number, email in use.</item>
///   <item><see cref="DomainException"/> → 422. The request was understood and is
///   well-formed, but a business rule forbids it — reinstating someone who was never
///   suspended, a second team leader. Distinct from a 400, which means malformed input.</item>
/// </list>
/// </summary>
internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException notFound =>
                (StatusCodes.Status404NotFound, "Not found", notFound.Message),

            ConflictException conflict =>
                (StatusCodes.Status409Conflict, "Conflict", conflict.Message),

            DomainException domain =>
                (StatusCodes.Status422UnprocessableEntity, "Rule violated", domain.Message),

            // Anything else is a bug. The message goes to the log, not to the caller.
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error",
                "Something went wrong handling this request.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("{Title} on {Method} {Path}: {Detail}",
                title, httpContext.Request.Method, httpContext.Request.Path, detail);
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
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
            }
        });
    }
}
