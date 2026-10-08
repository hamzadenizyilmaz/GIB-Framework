namespace GIBFramework.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var incoming = context.Request.Headers[GibFrameworkHeaders.CorrelationId].ToString();
        var id = incoming.Length is > 0 and <= 64 && incoming.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = id;
        context.Response.Headers[GibFrameworkHeaders.CorrelationId] = id;
        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = id }))
        {
            await next(context);
        }
    }
}
