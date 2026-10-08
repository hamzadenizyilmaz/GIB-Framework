namespace GIBFramework.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    private const string AppPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; "
        + "connect-src 'self'; frame-src 'self'; worker-src 'self' blob:; manifest-src 'self'; media-src 'self'; object-src 'none'; "
        + "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private const string DocsPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
        + "img-src 'self' data: blob: https:; font-src 'self' data: https://fonts.gstatic.com; connect-src 'self'; worker-src 'self' blob:; "
        + "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private const string PermissionsPolicy =
        "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), fullscreen=(self), geolocation=(), gyroscope=(), "
        + "magnetometer=(), microphone=(), midi=(), payment=(), picture-in-picture=(), publickey-credentials-get=(self), screen-wake-lock=(), "
        + "sync-xhr=(), usb=(), xr-spatial-tracking=()";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.Request;
        var isDocs = request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/redoc", StringComparison.OrdinalIgnoreCase);
        var isApi = request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            headers.ContentSecurityPolicy = (isDocs ? DocsPolicy : AppPolicy) + (request.IsHttps ? "; upgrade-insecure-requests" : string.Empty);
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.XXSSProtection = "0";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers["Origin-Agent-Cluster"] = "?1";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["X-DNS-Prefetch-Control"] = "off";
            headers["X-Download-Options"] = "noopen";
            if (!isDocs)
            {
                headers["Cross-Origin-Embedder-Policy"] = "require-corp";
            }

            if (request.IsHttps && !environment.IsDevelopment())
            {
                headers.StrictTransportSecurity = "max-age=63072000; includeSubDomains; preload";
            }

            if (isApi)
            {
                headers.CacheControl = "no-store, max-age=0";
                headers.Pragma = "no-cache";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
