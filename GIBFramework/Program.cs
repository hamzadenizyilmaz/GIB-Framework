using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Rewrite;
using GIBFramework.Base.Extensions;
using GIBFramework.Infrastructure.Swagger;
using GIBFramework.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
builder.Services.AddGibFramework(builder.Configuration, builder.Environment);

var app = builder.Build();

await app.Services.InitializeGibFrameworkAsync();

var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
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
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<PasswordChangeMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .WithName("Health_Get")
    .WithSummary("GIB-Framework Özel Yazılmış Yapıdır - Hamza Deniz Yılmaz - Telif Hakları 2019-2026 - v5.2.1")
    .WithTags("Sistem");
app.MapControllers();

await app.RunAsync();

public partial class Program;
