using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Rewrite;
using GIBFramework.Base.Extensions;
using GIBFramework.DAL;
using GIBFramework.Infrastructure.Swagger;
using GIBFramework.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
builder.Services.AddGibFramework(builder.Configuration, builder.Environment);

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

app.UseGibFrameworkSwagger();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRewriter(new RewriteOptions().AddRewrite("^$", "app/index.html", skipRemainingRules: true));
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store";
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
