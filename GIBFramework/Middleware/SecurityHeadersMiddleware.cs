namespace GIBFramework.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    private const string AppPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; font-src 'self' data:; "
        + "connect-src 'self'; frame-src 'self' blob:; worker-src 'self' blob:; manifest-src 'self'; media-src 'self'; object-src 'none'; "
        + "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private const string DocsPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
        + "img-src 'self' data: blob: https:; font-src 'self' data: https://fonts.gstatic.com; connect-src 'self'; worker-src 'self' blob:; "
        + "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    // The hash covers the inline bridge script in wwwroot/preview/frame.html.
    private const string PreviewFramePolicy =
        "default-src 'none'; script-src 'sha256-Os+r7YYcyjuI5F8akxHBfoJRIHHEmbezZEz+2AkRZYU='; style-src 'unsafe-inline'; img-src data:; font-src data:; "
        + "frame-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'self'; sandbox allow-scripts";

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
        var isPreviewFrame = request.Path.Equals("/preview/frame.html", StringComparison.OrdinalIgnoreCase);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            if (!headers.ContainsKey("Content-Security-Policy"))
                headers.ContentSecurityPolicy = (isPreviewFrame ? PreviewFramePolicy : isDocs ? DocsPolicy : AppPolicy)
                    + (request.IsHttps ? "; upgrade-insecure-requests" : string.Empty);
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = isPreviewFrame ? "SAMEORIGIN" : "DENY";
            headers.XXSSProtection = "0";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers["Origin-Agent-Cluster"] = "?1";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["X-DNS-Prefetch-Control"] = "off";
            headers["X-Download-Options"] = "noopen";
            headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
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
