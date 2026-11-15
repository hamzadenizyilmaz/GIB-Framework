using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.ResponseCompression;
using GIBFramework.Base.Extensions;
using GIBFramework.Controllers;
using GIBFramework.DAL;
using GIBFramework.Infrastructure.Swagger;
using GIBFramework.Middleware;
using GIBFramework.Services.Auth;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
if (string.IsNullOrWhiteSpace(builder.Configuration["AllowedHosts"])
    || (!builder.Environment.IsDevelopment()
        && builder.Configuration["AllowedHosts"]!.Split(';', StringSplitOptions.TrimEntries).Contains("*")))
{
    if (Uri.TryCreate(builder.Configuration["App:PublicUrl"], UriKind.Absolute, out var publicUrl)
        && publicUrl.Scheme == Uri.UriSchemeHttps && publicUrl.UserInfo.Length == 0)
        builder.Configuration["AllowedHosts"] = publicUrl.IdnHost;
    else
        builder.Configuration["AllowedHosts"] = "localhost;127.0.0.1;[::1]";
}
if (builder.Configuration.GetValue<bool>("ForwardedHeaders_Enabled")
    || builder.Configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED")
    || builder.Configuration.GetValue<bool>("DOTNET_FORWARDEDHEADERS_ENABLED"))
{
    throw new InvalidOperationException("Otomatik forwarded headers ayarını kapatın ve ReverseProxy altında güvenilen vekilleri tanımlayın.");
}
builder.Services.AddGibFramework(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IpAccessPolicy>();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ["text/css", "text/javascript", "application/javascript", "image/svg+xml"];
});

var app = builder.Build();

app.Services.InitializeGibFramework();

var forwardLimit = builder.Configuration.GetValue("ReverseProxy:ForwardLimit", 1);
var forwardedForHeader = builder.Configuration["ReverseProxy:ForwardedForHeaderName"] ?? "X-Forwarded-For";
if (forwardLimit is < 1 or > 5 || forwardedForHeader is not ("X-Forwarded-For" or "CF-Connecting-IP")
    || (forwardedForHeader == "CF-Connecting-IP" && forwardLimit != 1))
{
    throw new InvalidOperationException("ReverseProxy:ForwardLimit 1–5 olmalıdır. CF-Connecting-IP yalnızca tek vekil için kullanılabilir.");
}
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = forwardLimit,
    ForwardedForHeaderName = forwardedForHeader,
};
forwarded.KnownIPNetworks.Clear();
forwarded.KnownProxies.Clear();
foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
{
    if (!System.Net.IPAddress.TryParse(address, out var proxy))
    {
        throw new InvalidOperationException("ReverseProxy:KnownProxies yalnızca IP adresi kabul eder.");
    }

    forwarded.KnownProxies.Add(proxy);
}
foreach (var range in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
{
    if (!System.Net.IPNetwork.TryParse(range, out var network) || network.PrefixLength == 0)
    {
        throw new InvalidOperationException("ReverseProxy:KnownNetworks yalnızca CIDR ağları kabul eder.");
    }

    forwarded.KnownIPNetworks.Add(network);
}
if (forwarded.KnownProxies.Count > 0 || forwarded.KnownIPNetworks.Count > 0)
    app.UseForwardedHeaders(forwarded);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

var ipAccess = app.Services.GetRequiredService<IpAccessPolicy>();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path.Equals(new PathString("/robots.txt"), StringComparison.OrdinalIgnoreCase))
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "robots.txt"), context.RequestAborted);
        return;
    }
    await next(context);
});
var deniedBackground = new Lazy<string>(() =>
{
    var assets = Path.Combine(app.Environment.WebRootPath, "app", "assets");
    var photo = Directory.Exists(assets) ? Directory.EnumerateFiles(assets, "office-*.jpg").FirstOrDefault() : null;
    return photo is null ? string.Empty : $"background-image:linear-gradient(#102c2cbb,#102c2cbb),url(data:image/jpeg;base64,{Convert.ToBase64String(File.ReadAllBytes(photo))})";
});
app.Use(async (context, next) =>
{
    var rejection = ipAccess.RejectionReason(context.Connection.RemoteIpAddress);
    if (rejection is null)
    {
        await next(context);
        return;
    }

    var log = context.RequestServices.GetRequiredService<ILogger<IpAccessPolicy>>();
    var checkedIp = IpAccessPolicy.FormatAddress(context.Connection.RemoteIpAddress);
    var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string;
    log.LogInformation("IP erişimi reddedildi. IP {ClientIp}, kural {Rule}, yöntem {Method}, istek {CorrelationId}.",
        checkedIp, rejection, context.Request.Method, correlationId);
    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    context.Response.Headers.CacheControl = "no-store";
    if (!context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
    {
        const string title = "Bu IP adresinden erişime izin verilmiyor.";
        if (context.Request.Path.StartsWithSegments("/api/v1/hooks", StringComparison.OrdinalIgnoreCase))
            await context.Response.WriteAsJsonAsync(new { code = "IP_ACCESS_DENIED", title, clientIp = checkedIp, reason = rejection, correlationId });
        else
            await context.Response.WriteAsJsonAsync(new { code = "IP_ACCESS_DENIED", title });
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; img-src data:; frame-ancestors 'none'; sandbox";
    await context.Response.WriteAsync($"""
        <!doctype html><html lang="tr"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>403 · Erişim reddedildi</title><body style="margin:0;min-height:100vh;display:grid;place-items:center;background-color:#1c3934;background-position:center;background-size:cover;color:white;font:1rem system-ui;{deniedBackground.Value}">
        <main style="padding:2rem"><h1 style="font-size:clamp(4rem,15vw,9rem);margin:0">403</h1><h2>Erişim reddedildi</h2><p>Bu IP adresinden erişime izin verilmiyor.</p></main></body></html>
        """);
});

app.UseGibFrameworkSwagger();

var database = app.Services.GetRequiredService<DatabaseInitialization>();
var installOptions = app.Services.GetRequiredService<InstallOptions>();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/"
        && database.Ready && installOptions.IsConfigured)
    {
        var bootstrap = context.RequestServices.GetRequiredService<UserBootstrapper>();
        if (await bootstrap.IsWebInstallAvailableAsync(context.RequestAborted))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Redirect("/install");
            return;
        }
    }
    await next(context);
});

app.UseRewriter(new RewriteOptions().AddRewrite("^(?:app/?)?$", "app/index.html", skipRemainingRules: true));
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/app/assets"), assets => assets.UseResponseCompression());
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store";
        }
        else if (ctx.Context.Request.Path.StartsWithSegments("/app/assets"))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    },
});

app.UseRouting();
app.Use(async (context, next) =>
{
    if (database.Configured && !database.Ready && !context.Request.Path.Equals(new PathString("/api/v1/install"), StringComparison.OrdinalIgnoreCase)
        && (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/f")))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new { code = "DATABASE_NOT_READY", title = "Veritabanı başlangıç işlemi tamamlanmadı." }, context.RequestAborted);
        return;
    }
    await next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<OperationAuditMiddleware>();
app.UseMiddleware<PasswordChangeMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .WithName("Health_Get")
    .WithSummary("Uygulamanın canlılık denetimi")
    .WithTags("Sistem");
app.MapGet("/health/ready", async (ReadinessCheck check, HttpContext context, CancellationToken ct) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        var result = await check.CheckAsync(ct);
        return result.Ready
            ? Results.Ok(new { status = "ready" })
            : Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    })
    .AllowAnonymous()
    .RequireRateLimiting(PublicInvoiceController.RateLimitPolicy)
    .WithName("Health_Ready")
    .WithSummary("Veritabanı ve üretim yapılandırması hazırlık denetimi")
    .WithTags("Sistem");
app.MapControllers();

await app.RunAsync();

public partial class Program;
