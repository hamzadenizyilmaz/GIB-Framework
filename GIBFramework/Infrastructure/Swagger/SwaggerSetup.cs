using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace GIBFramework.Infrastructure.Swagger;

public sealed class SwaggerOptions
{
    public const string Section = "Swagger";

    public bool? Enabled { get; set; }
}

public static class SwaggerSetup
{
    public const string DocumentName = "v1";

    public static readonly (string Controller, string Tag)[] Tags =
    [
        ("Auth", "Kimlik Doğrulama"),
        ("ForgotPassword", "Kimlik Doğrulama"),
        ("Invoices", "Faturalar"),
        ("GibPortal", "GİB e-Arşiv Portal"),
        ("IncomingInvoices", "Gelen Faturalar"),
        ("Compliance", "Mevzuat & Uyum"),
        ("TaxOffices", "Vergi Daireleri"),
        ("Taxpayers", "Mükellef"),
        ("Documents", "Belge & Sertifika"),
        ("Audit", "Denetim İzi"),
        ("Customers", "Cari Kartlar"),
        ("Products", "Ürün & Hizmetler"),
        ("Settings", "Firma Ayarları"),
        ("SecurityPolicy", "Güvenlik Politikası"),
        ("System", "Sistem"),
        ("Locations", "İl / İlçe / Mahalle"),
        ("ApiKeys", "API Anahtarları"),
        ("Integrations", "Entegrasyonlar"),
        ("Hooks", "Entegrasyonlar"),
        ("Messaging", "E-posta & SMS"),
        ("InvoiceDelivery", "Faturalar"),
        ("TenantLogo", "Firmalar"),
        ("Announcements", "Duyurular"),
        ("Users", "Kullanıcılar"),
        ("PasswordResetRequests", "Kullanıcılar"),
        ("Roles", "Roller & Yetkiler"),
        ("Tenants", "Firmalar"),
        ("DevAuth", "Geliştirme"),
        ("-", "Sistem"),
    ];

    public static IServiceCollection AddGibFrameworkSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "GIB Framework API",
                Version = "v1",
                Contact = new OpenApiContact { Name = "Hamza Deniz Yılmaz", Url = new Uri("https://github.com/hamzadenizyilmaz/GIB-Framework") },
            });
            var tagByController = Tags.ToDictionary(t => t.Controller, t => t.Tag, StringComparer.Ordinal);
            o.TagActionsBy(api => [api.ActionDescriptor.RouteValues.TryGetValue("controller", out var c) && c is not null && tagByController.TryGetValue(c, out var tag) ? tag : "Sistem"]);
            o.OrderActionsBy(api =>
            {
                var c = api.ActionDescriptor.RouteValues.TryGetValue("controller", out var name) ? name ?? string.Empty : string.Empty;
                var index = Array.FindIndex(Tags, t => t.Controller == c);
                return $"{(index < 0 ? 99 : index):D2}_{api.RelativePath}_{api.HttpMethod}";
            });
            o.CustomOperationIds(api => api.ActionDescriptor is ControllerActionDescriptor d
                ? $"{d.ControllerName}_{d.ActionName}"
                : api.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>().FirstOrDefault()?.EndpointName);
            o.SupportNonNullableReferenceTypes();
            o.UseAllOfToExtendReferenceSchemas();
            o.CustomSchemaIds(t => SchemaId(t));

            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "POST /api/v1/auth/login yanıtındaki accessToken.",
            });

            o.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Api-Key",
                In = ParameterLocation.Header,
                Description = "Üçüncü taraf uygulamalar için firma API anahtarı (Ayarlar > API anahtarları).",
            });

            o.DocumentFilter<TagDescriptionsDocumentFilter>();
            o.OperationFilter<SecurityOperationFilter>();
            o.OperationFilter<ProblemResponsesOperationFilter>();
            o.SchemaFilter<ExamplesSchemaFilter>();
        });
        return services;
    }

    public static WebApplication UseGibFrameworkSwagger(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var enabled = app.Configuration.GetSection(SwaggerOptions.Section).Get<SwaggerOptions>()?.Enabled ?? app.Environment.IsDevelopment();
        if (!enabled)
        {
            return app;
        }

        app.UseSwagger(o => o.RouteTemplate = "swagger/{documentName}/swagger.json");
        app.UseSwaggerUI(o =>
        {
            o.SwaggerEndpoint($"/swagger/{DocumentName}/swagger.json", "GIB Framework API v1");
            o.RoutePrefix = "swagger";
            o.DocumentTitle = "GIB Framework API";
            o.DocExpansion(DocExpansion.None);
            o.DefaultModelsExpandDepth(0);
            o.DefaultModelExpandDepth(2);
            o.DisplayRequestDuration();
            o.DisplayOperationId();
            o.EnableFilter();
            o.EnableDeepLinking();
            o.EnableTryItOutByDefault();
            o.ShowExtensions();
            o.ShowCommonExtensions();
            o.InjectStylesheet("/swagger-ui/gibframework.css?v=2");
            o.InjectJavascript("/swagger-ui/gibframework-login.js?v=2");
        });
        app.UseReDoc(o =>
        {
            o.RoutePrefix = "redoc";
            o.SpecUrl = $"/swagger/{DocumentName}/swagger.json";
            o.DocumentTitle = "GIB Framework API — Dokümantasyon";
            o.ExpandResponses("200,201");
            o.RequiredPropsFirst();
            o.SortPropsAlphabetically();
        });
        return app;
    }

    private static string SchemaId(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.IsNested ? $"{type.DeclaringType!.Name}{type.Name}" : type.Name;
        }

        var name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];
        return name + "Of" + string.Join("And", type.GetGenericArguments().Select(SchemaId));
    }
}

internal sealed class TagDescriptionsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags = [.. SwaggerSetup.Tags.Select(t => t.Tag).Distinct(StringComparer.Ordinal).Select(t => new OpenApiTag { Name = t })];
    }
}

internal sealed class SecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
            },
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }] = [],
            },
        ];

        var policies = metadata.OfType<IAuthorizeData>().Select(a => a.Policy).Where(p => p is not null).Distinct().ToList();
        if (policies.Count > 0)
        {
            var lines = policies.Select(p => Policies.Map.TryGetValue(p!, out var roles) ? $"`{p}`: {string.Join(", ", roles)}" : $"`{p}`");
            operation.Description = (operation.Description is { Length: > 0 } d ? d + "\n\n" : string.Empty)
                + "**Yetki:** " + string.Join(" · ", lines);
            operation.Extensions["x-gibframework-policies"] = new OpenApiArray { Capacity = policies.Count };
            foreach (var p in policies)
            {
                ((OpenApiArray)operation.Extensions["x-gibframework-policies"]).Add(new OpenApiString(p));
            }
        }

        operation.Parameters ??= [];
        if (!operation.Parameters.Any(p => p.Name == GibFrameworkHeaders.TenantId))
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = GibFrameworkHeaders.TenantId,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Yalnızca PlatformSuperAdmin: işlem yapılacak firma (kiracı) kimliği.",
                Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
            });
        }

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Token yok, geçersiz veya oturum sonlandırılmış." });
        operation.Responses.TryAdd("403", ProblemResponsesOperationFilter.Problem(context, "Yetki yok (rol/politika), dört göz ihlali veya şifre değiştirme zorunlu."));
    }
}

internal sealed class ProblemResponsesOperationFilter : IOperationFilter
{
    public static OpenApiResponse Problem(OperationFilterContext context, string description)
    {
        var schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository);
        return new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = schema } },
        };
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.ApiDescription.HttpMethod ?? "GET";
        var hasBody = context.ApiDescription.ParameterDescriptions.Any(p => p.Source?.Id is "Body" or "Form");
        if (hasBody)
        {
            operation.Responses.TryAdd("400", Problem(context, "İstek modeli geçersiz (alan doğrulaması)."));
        }

        if (context.ApiDescription.RelativePath?.Contains('{', StringComparison.Ordinal) == true)
        {
            operation.Responses.TryAdd("404", Problem(context, "Kayıt bulunamadı (veya başka bir firmaya ait)."));
        }

        if (method is "POST" or "PATCH" or "PUT")
        {
            operation.Responses.TryAdd("409", Problem(context, "Çakışma: eşzamanlı değişiklik, mükerrer kayıt veya veritabanı bütünlük kuralı."));
            operation.Responses.TryAdd("422", Problem(context, "İş kuralı ihlali (mevzuat, vergi hesabı, durum makinesi); ayrıntılar `details` alanında."));
        }

        operation.Responses.TryAdd("500", Problem(context, "Beklenmeyen hata (correlationId ile destek kaydı açın)."));
        foreach (var response in operation.Responses.Values)
        {
            response.Headers ??= new Dictionary<string, OpenApiHeader>();
            response.Headers.TryAdd(GibFrameworkHeaders.CorrelationId, new OpenApiHeader { Description = "İstek korelasyon kimliği.", Schema = new OpenApiSchema { Type = "string" } });
        }
    }
}

internal sealed class ExamplesSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        schema.Example = context.Type.Name switch
        {
            "LoginRequest" => new OpenApiObject { ["userCode"] = new OpenApiString("admin"), ["password"] = new OpenApiString("GibFrameworkGelistirme1"), ["totpCode"] = new OpenApiNull() },
            "GibPortalConnectRequest" => new OpenApiObject { ["environment"] = new OpenApiString("Test"), ["userCode"] = new OpenApiString("33333301"), ["password"] = new OpenApiString("1") },
            "GibPortalSignRequest" => new OpenApiObject { ["smsCode"] = new OpenApiString("123456") },
            "ReasonRequest" => new OpenApiObject { ["reason"] = new OpenApiString("Tutar hatalı düzenlendi") },
            "InvoiceLineDto" => new OpenApiObject
            {
                ["name"] = new OpenApiString("Danışmanlık hizmeti"),
                ["quantity"] = new OpenApiDouble(1),
                ["unitCode"] = new OpenApiString("HUR"),
                ["unitPrice"] = new OpenApiDouble(1000),
                ["discountAmount"] = new OpenApiDouble(0),
                ["vatRate"] = new OpenApiDouble(20),
            },
            "PartyDto" => new OpenApiObject
            {
                ["taxId"] = new OpenApiString("10000000146"),
                ["kind"] = new OpenApiString("NaturalPerson"),
                ["regime"] = new OpenApiString("NotATaxpayer"),
                ["title"] = new OpenApiString("Ayşe Yılmaz"),
                ["district"] = new OpenApiString("Çankaya"),
                ["city"] = new OpenApiString("Ankara"),
                ["email"] = new OpenApiString("ayse@example.com"),
            },
            "CreateInvoiceRequest" => new OpenApiObject
            {
                ["deliveryDate"] = new OpenApiString("2026-10-07"),
                ["profile"] = new OpenApiString("TEMELFATURA"),
                ["currency"] = new OpenApiString("TRY"),
                ["customer"] = new OpenApiObject
                {
                    ["taxId"] = new OpenApiString("10000000146"),
                    ["kind"] = new OpenApiString("NaturalPerson"),
                    ["regime"] = new OpenApiString("NotATaxpayer"),
                    ["title"] = new OpenApiString("Ayşe Yılmaz"),
                    ["district"] = new OpenApiString("Çankaya"),
                    ["city"] = new OpenApiString("Ankara"),
                },
                ["lines"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["name"] = new OpenApiString("Danışmanlık hizmeti"),
                        ["quantity"] = new OpenApiDouble(1),
                        ["unitCode"] = new OpenApiString("HUR"),
                        ["unitPrice"] = new OpenApiDouble(1000),
                        ["vatRate"] = new OpenApiDouble(20),
                    },
                },
                ["notes"] = new OpenApiArray { new OpenApiString("Ödeme 30 gün vadelidir.") },
            },
            _ => schema.Example,
        };
    }
}
