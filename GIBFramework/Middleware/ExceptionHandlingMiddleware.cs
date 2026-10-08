using GIBFramework.Infrastructure.GibPortal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GIBFramework.Middleware;

public sealed partial class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted && ex is not OperationCanceledException)
        {
            var (status, code, details) = ex switch
            {
                ValidationFailedException v => (StatusCodes.Status422UnprocessableEntity, v.Code, v.Details),
                DomainException d => (StatusCodes.Status422UnprocessableEntity, d.Code, (IReadOnlyList<string>)[]),
                NotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND", []),
                ConflictException c => (StatusCodes.Status409Conflict, c.Code, []),
                ForbiddenOperationException f => (StatusCodes.Status403Forbidden, f.Code, []),
                ProviderUnavailableException => (StatusCodes.Status503ServiceUnavailable, "PROVIDER_UNAVAILABLE", []),
                GibPortalException g => (StatusCodes.Status502BadGateway, g.SessionExpired ? "GIB_PORTAL_SESSION_EXPIRED" : "GIB_PORTAL_ERROR", []),
                SqlException { Number: >= 51000 and < 52000 } s => (StatusCodes.Status409Conflict, "DB_INTEGRITY_" + s.Number, []),
                _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", []),
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                Log.Unhandled(logger, ex);
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = status == StatusCodes.Status500InternalServerError && !environment.IsDevelopment() ? "Beklenmeyen bir hata oluştu." : ex.Message,
                Type = $"https://gibframework.local/errors/{code}",
                Instance = context.Request.Path,
            };
            problem.Extensions["code"] = code;
            problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.ItemKey];
            if (details.Count > 0)
            {
                problem.Extensions["details"] = details;
            }

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem, JsonDefaults.Options, contentType: "application/problem+json");
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "İşlenmeyen hata")]
        public static partial void Unhandled(ILogger logger, Exception exception);
    }
}
