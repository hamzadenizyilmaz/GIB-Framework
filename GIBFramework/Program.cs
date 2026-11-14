using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Rewrite;
using GIBFramework.Base.Extensions;
using GIBFramework.DAL;
using GIBFramework.Infrastructure.Swagger;
using GIBFramework.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
builder.Services.AddGibFramework(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IpAccessPolicy>();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

app.Services.InitializeGibFramework();

var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1,
};
foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
{
    if (!System.Net.IPAddress.TryParse(address, out var proxy))
    {
        throw new InvalidOperationException("ReverseProxy:KnownProxies yalnızca IP adresi kabul eder.");
    }

    forwarded.KnownProxies.Add(proxy);
}
app.UseForwardedHeaders(forwarded);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

var ipAccess = app.Services.GetRequiredService<IpAccessPolicy>();
var deniedBackground = new Lazy<string>(() =>
{
    var assets = Path.Combine(app.Environment.WebRootPath, "app", "assets");
    var photo = Directory.Exists(assets) ? Directory.EnumerateFiles(assets, "office-*.jpg").FirstOrDefault() : null;
    return photo is null ? string.Empty : $"background-image:linear-gradient(#102c2cbb,#102c2cbb),url(data:image/jpeg;base64,{Convert.ToBase64String(File.ReadAllBytes(photo))})";
});
app.Use(async (context, next) =>
{
    if (ipAccess.Allows(context.Connection.RemoteIpAddress))
    {
        await next(context);
        return;
    }

    var log = context.RequestServices.GetRequiredService<ILogger<IpAccessPolicy>>();
    log.LogInformation("IP erişimi reddedildi. IP {ClientIp}, yöntem {Method}, istek {TraceIdentifier}.",
        context.Connection.RemoteIpAddress, context.Request.Method, context.TraceIdentifier);
    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    context.Response.Headers.CacheControl = "no-store";
    if (!context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
    {
        await context.Response.WriteAsJsonAsync(new { code = "IP_ACCESS_DENIED", title = "Bu IP adresinden erişime izin verilmiyor." });
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync($"""
        <!doctype html><html lang="tr"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>403 · Erişim reddedildi</title><body style="margin:0;min-height:100vh;display:grid;place-items:center;background-color:#1c3934;background-position:center;background-size:cover;color:white;font:1rem system-ui;{deniedBackground.Value}">
        <main style="padding:2rem"><h1 style="font-size:clamp(4rem,15vw,9rem);margin:0">403</h1><h2>Erişim reddedildi</h2><p>Bu IP adresinden erişime izin verilmiyor.</p></main></body></html>
        """);
});

app.UseGibFrameworkSwagger();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

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
var database = app.Services.GetRequiredService<DatabaseInitialization>();
app.Use(async (context, next) =>
{
    if (database.Configured && !database.Ready
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
    .WithName("Health_Ready")
    .WithSummary("Veritabanı ve üretim yapılandırması hazırlık denetimi")
    .WithTags("Sistem");
app.MapControllers();

await app.RunAsync();

public partial class Program;
