using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using GIBFramework.Models.Integrations;

namespace GIBFramework.Services.Integrations;

public sealed record MarketplaceOrder(InboundOrder Order, string Raw);

public sealed record MarketplaceInvoiceLink(string ExternalId, string? OrderNumber, string Link, string DocumentNumber, DateTimeOffset IssuedAt);

public sealed class MarketplaceException(string message, bool transient = false) : Exception(message)
{
    public bool Transient { get; } = transient;
}

public interface IMarketplaceClient
{
    IntegrationKind Kind { get; }

    Task<IReadOnlyList<MarketplaceOrder>> FetchAsync(Integration integration, IReadOnlyDictionary<string, string> secrets, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

    Task<string> SendInvoiceLinkAsync(Integration integration, IReadOnlyDictionary<string, string> secrets, MarketplaceInvoiceLink link, CancellationToken ct);
}

public abstract class MarketplaceClientBase(IHttpClientFactory httpFactory)
{
    public const string HttpClientName = "Marketplaces";
    protected static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    protected async Task<(int Status, string Body)> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        HttpResponseMessage response;
        try
        {
            response = await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new MarketplaceException("Pazaryeri API'sine bağlanılamadı: " + ex.Message, transient: true);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MarketplaceException("Pazaryeri API'si zamanında yanıt vermedi.", transient: true);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;
            switch (status)
            {
                case 401 or 403:
                    throw new MarketplaceException($"Pazaryeri kimlik doğrulaması başarısız (HTTP {status}); API bilgilerini ve User-Agent değerini kontrol edin.");
                case 429:
                    throw new MarketplaceException("Pazaryeri istek sınırı aşıldı (HTTP 429).", transient: true);
                case >= 500:
                    throw new MarketplaceException($"Pazaryeri API'si HTTP {status} döndürdü.", transient: true);
            }

            return (status, body);
        }
    }

    protected static void Basic(HttpRequestMessage request, string user, string password) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));

    protected static string Require(IReadOnlyDictionary<string, string> secrets, string key, string label) =>
        secrets.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : throw new MarketplaceException($"{label} girilmemiş.");

    protected static string Require(Integration integration, string key, string label) =>
        integration.Setting(key) ?? throw new MarketplaceException($"{label} girilmemiş.");

    protected static JsonElement Obj(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : default;

    protected static IEnumerable<JsonElement> Arr(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    protected static string? Str(JsonElement e, params string[] names)
    {
        foreach (var name in names)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v))
            {
                continue;
            }

            var s = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(s) && s != "null")
            {
                return s.Trim();
            }
        }

        return null;
    }

    protected static decimal Dec(JsonElement e, params string[] names)
    {
        foreach (var name in names)
        {
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v))
            {
                if (v.ValueKind == JsonValueKind.Object && Str(v, "amount", "value") is { } inner
                    && decimal.TryParse(inner, NumberStyles.Number, Inv, out var nested))
                {
                    return nested;
                }

                if (Str(e, name) is { } s && decimal.TryParse(s, NumberStyles.Number, Inv, out var d))
                {
                    return d;
                }
            }
        }

        return 0;
    }

    protected static bool Has(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

    protected static string? Join(params string?[] parts)
    {
        var s = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return s.Length == 0 ? null : s;
    }

    protected static decimal Rate(JsonElement line, Integration integration, params string[] names)
    {
        foreach (var name in names)
        {
            if (Has(line, name))
            {
                return InboundMappers.Snap(Dec(line, name));
            }
        }

        return decimal.TryParse(integration.Setting("defaultVatRate"), NumberStyles.Number, Inv, out var r) ? r : 20;
    }

    protected static InboundCustomer Customer(string? taxNumber, string? identity, bool corporate, string? name, string? company, string? taxOffice,
        string? email, string? phone, string? address, string? district, string? city, string? postal, string? country)
    {
        var vkn = Digits(taxNumber);
        var tckn = Digits(identity);
        var taxId = corporate && vkn is { Length: 10 or 11 } ? vkn : tckn is { Length: 11 } ? tckn : vkn is { Length: 10 } ? vkn : null;
        return new InboundCustomer(taxId, name, corporate || taxId?.Length == 10 ? company ?? name : company, taxOffice, email, phone, address, district, city, postal, country);
    }

    protected static string? Digits(string? value)
    {
        var d = new string([.. (value ?? string.Empty).Where(char.IsAsciiDigit)]);
        return d.Length == 0 ? null : d;
    }

    protected static JsonDocument Parse(string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new MarketplaceException("Pazaryeri API'sinden okunamayan yanıt: " + (body.Length > 200 ? body[..200] : body));
        }
    }
}

public sealed class TrendyolClient(IHttpClientFactory httpFactory, MarketplaceOptions options) : MarketplaceClientBase(httpFactory), IMarketplaceClient
{
    public IntegrationKind Kind => IntegrationKind.Trendyol;

    private string Base(Integration i) => !string.IsNullOrWhiteSpace(options.TrendyolBaseUrl) ? options.TrendyolBaseUrl.TrimEnd('/')
        : i.Setting("environment") == "stage" ? "https://stageapigw.trendyol.com" : "https://apigw.trendyol.com";

    private static HttpRequestMessage Request(HttpMethod method, string url, Integration i, IReadOnlyDictionary<string, string> secrets)
    {
        var sellerId = Require(i, "sellerId", "Satıcı ID");
        var request = new HttpRequestMessage(method, url);
        Basic(request, Require(secrets, "apiKey", "API Key"), Require(secrets, "apiSecret", "API Secret"));
        request.Headers.TryAddWithoutValidation("User-Agent", $"{sellerId} - {i.Setting("integratorName") ?? "SelfIntegration"}");
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    public async Task<IReadOnlyList<MarketplaceOrder>> FetchAsync(Integration integration, IReadOnlyDictionary<string, string> secrets, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integration);
        var sellerId = Require(integration, "sellerId", "Satıcı ID");
        var statuses = integration.Setting("statuses") ?? "Created";
        var list = new List<MarketplaceOrder>();
        for (var page = 0; page < 50; page++)
        {
            var url = $"{Base(integration)}/integration/order/sellers/{Uri.EscapeDataString(sellerId)}/v2/orders?status={Uri.EscapeDataString(statuses)}"
                + $"&startDate={from.ToUnixTimeMilliseconds()}&endDate={to.ToUnixTimeMilliseconds()}&orderByField=PackageLastModifiedDate&orderByDirection=ASC&page={page}&size=200";
            using var request = Request(HttpMethod.Get, url, integration, secrets);
            var (status, body) = await SendAsync(request, ct);
            if (status >= 400)
            {
                throw new MarketplaceException($"Trendyol sipariş listesi HTTP {status}: {Short(body)}");
            }

            using var doc = Parse(body);
            var root = doc.RootElement;
            foreach (var p in Arr(root, "content"))
            {
                list.Add(new MarketplaceOrder(Map(integration, p), p.GetRawText()));
            }

            var totalPages = (int)Dec(root, "totalPages");
            if (page + 1 >= totalPages)
            {
                break;
            }
        }

        return list;
    }

    private static InboundOrder Map(Integration integration, JsonElement p)
    {
        var id = Str(p, "shipmentPackageId", "id") ?? throw new MarketplaceException("Trendyol paket numarası yok.");
        var invoice = Obj(p, "invoiceAddress");
        var corporate = Str(p, "commercial") == "true";
        var customer = Customer(
            Str(invoice, "taxNumber") ?? Str(p, "taxNumber"),
            Str(p, "identityNumber", "customerTckn", "tcIdentityNumber"),
            corporate,
            Str(invoice, "fullName") ?? Join(Str(invoice, "firstName"), Str(invoice, "lastName")) ?? Join(Str(p, "customerFirstName"), Str(p, "customerLastName")),
            Str(invoice, "company"),
            Str(invoice, "taxOffice"),
            Str(p, "customerEmail"),
            Str(invoice, "phone"),
            Join(Str(invoice, "address1"), Str(invoice, "address2")) ?? Str(invoice, "fullAddress"),
            Str(invoice, "district"),
            Str(invoice, "city"),
            Str(invoice, "postalCode"),
            Str(invoice, "countryCode"));
        var lines = Arr(p, "lines").Select(l =>
        {
            var qty = Math.Max(1, Dec(l, "quantity"));
            var unit = Has(l, "lineUnitPrice") ? Dec(l, "lineUnitPrice") : Has(l, "price") ? Dec(l, "price") : Dec(l, "amount") / qty;
            var discount = Has(l, "lineSellerDiscount") ? Dec(l, "lineSellerDiscount") : Dec(l, "discount");
            return new InboundLine(Str(l, "productName") ?? "Ürün", qty, unit, Rate(l, integration, "vatRate", "vatBaseAmount"), discount, null);
        }).ToList();
        var orderNumber = Str(p, "orderNumber") ?? id;
        return new InboundOrder(id, orderNumber, Str(p, "currencyCode") ?? "TRY", true, customer, lines, [$"Trendyol sipariş no: {orderNumber}", $"Trendyol paket no: {id}"]);
    }

    public async Task<string> SendInvoiceLinkAsync(Integration integration, IReadOnlyDictionary<string, string> secrets, MarketplaceInvoiceLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integration);
        ArgumentNullException.ThrowIfNull(link);
        var sellerId = Require(integration, "sellerId", "Satıcı ID");
        using var request = Request(HttpMethod.Post, $"{Base(integration)}/integration/sellers/{Uri.EscapeDataString(sellerId)}/seller-invoice-links", integration, secrets);
        var body = JsonSerializer.Serialize(new
        {
            invoiceLink = link.Link,
            shipmentPackageId = long.TryParse(link.ExternalId, NumberStyles.Integer, Inv, out var packageId) ? packageId : 0,
            invoiceDateTime = link.IssuedAt.ToUnixTimeMilliseconds(),
            invoiceNumber = link.DocumentNumber,
        });
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var (status, response) = await SendAsync(request, ct);
        return status switch
        {
            200 or 201 or 202 or 204 => $"HTTP {status}",
            409 => "HTTP 409: paket için fatura zaten kayıtlı",
            _ => throw new MarketplaceException($"Trendyol fatura linki HTTP {status}: {Short(response)}"),
        };
    }

    internal static string Short(string body) => body.Length > 300 ? body[..300] : body;
}

public sealed class HepsiburadaClient(IHttpClientFactory httpFactory, MarketplaceOptions options) : MarketplaceClientBase(httpFactory), IMarketplaceClient
{
    public IntegrationKind Kind => IntegrationKind.Hepsiburada;

    private string Base(Integration i) => !string.IsNullOrWhiteSpace(options.HepsiburadaBaseUrl) ? options.HepsiburadaBaseUrl.TrimEnd('/')
        : i.Setting("environment") == "sit" ? "https://oms-external-sit.hepsiburada.com" : "https://oms-external.hepsiburada.com";

    private static HttpRequestMessage Request(HttpMethod method, string url, Integration i, IReadOnlyDictionary<string, string> secrets)
    {
        var merchantId = Require(i, "merchantId", "Merchant ID");
        var request = new HttpRequestMessage(method, url);
        Basic(request, i.Setting("username") ?? merchantId, Require(secrets, "password", "Servis anahtarı / şifre"));
        request.Headers.TryAddWithoutValidation("User-Agent", Require(i, "userAgent", "User-Agent (entegratör kullanıcı adı)"));
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    public async Task<IReadOnlyList<MarketplaceOrder>> FetchAsync(Integration integration, IReadOnlyDictionary<string, string> secrets, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integration);
        var merchantId = Require(integration, "merchantId", "Merchant ID");
        var list = new List<MarketplaceOrder>();
        for (var offset = 0; offset < 5000; offset += 50)
        {
            using var request = Request(HttpMethod.Get, $"{Base(integration)}/packages/merchantid/{Uri.EscapeDataString(merchantId)}/missing-invoice?offset={offset}&limit=50", integration, secrets);
            var (status, body) = await SendAsync(request, ct);
            if (status == 404)
            {
                break;
            }

            if (status >= 400)
            {
                throw new MarketplaceException($"Hepsiburada paket listesi HTTP {status}: {TrendyolClient.Short(body)}");
            }

            using var doc = Parse(body);
            var root = doc.RootElement;
            var items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList()
                : new[] { "items", "packages", "content", "data" }.SelectMany(n => Arr(root, n)).ToList();
            foreach (var p in items)
            {
                if (Map(integration, p) is { } order)
                {
                    list.Add(new MarketplaceOrder(order, p.GetRawText()));
                }
            }

            if (items.Count < 50)
            {
                break;
            }
        }

        return list;
    }

    private static InboundOrder? Map(Integration integration, JsonElement p)
    {
        var packageNumber = Str(p, "packageNumber", "PackageNumber");
        if (packageNumber is null)
        {
            return null;
        }

        var lines = Arr(p, "items").Concat(Arr(p, "lineItems")).ToList();
        var taxNumber = Str(p, "taxNumber");
        var company = Str(p, "companyName");
        var customer = Customer(taxNumber, Str(p, "identityNo", "turkishIdentityNumber"), Digits(taxNumber)?.Length == 10, Str(p, "customerName", "recipientName") ?? company,
            company, Str(p, "taxOffice"), Str(p, "email"), Str(p, "phoneNumber"), Str(p, "billingAddress"), Str(p, "billingTown"), Str(p, "billingCity"),
            Str(p, "billingPostalCode"), Str(p, "billingCountryCode", "shippingCountryCode"));
        var mapped = lines.Select(l =>
        {
            var qty = Math.Max(1, Dec(l, "quantity"));
            var unit = Has(l, "merchantUnitPrice") ? Dec(l, "merchantUnitPrice")
                : Has(l, "price") ? Dec(l, "price")
                : Has(l, "unitPrice") ? Dec(l, "unitPrice")
                : Dec(l, "totalPrice") / qty;
            return new InboundLine(Str(l, "productName", "name") ?? "Ürün", qty, unit, Rate(l, integration, "vatRate"), 0, null);
        }).ToList();
        var orderNumber = Str(p, "orderNumber") ?? lines.Select(l => Str(l, "orderNumber")).FirstOrDefault(o => o is not null) ?? packageNumber;
        var currency = lines.Select(l => Str(Obj(l, "price"), "currency")).FirstOrDefault(c => c is not null) ?? "TRY";
        return new InboundOrder(packageNumber, orderNumber, currency, true, customer, mapped, [$"Hepsiburada sipariş no: {orderNumber}", $"Hepsiburada paket no: {packageNumber}"]);
    }

    public async Task<string> SendInvoiceLinkAsync(Integration integration, IReadOnlyDictionary<string, string> secrets, MarketplaceInvoiceLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integration);
        ArgumentNullException.ThrowIfNull(link);
        var merchantId = Require(integration, "merchantId", "Merchant ID");
        using var request = Request(HttpMethod.Put,
            $"{Base(integration)}/packages/merchantid/{Uri.EscapeDataString(merchantId)}/packagenumber/{Uri.EscapeDataString(link.ExternalId)}/invoice", integration, secrets);
        var serial = link.DocumentNumber.Length > 3 ? link.DocumentNumber[..3] : link.DocumentNumber;
        var row = link.DocumentNumber.Length > 3 ? link.DocumentNumber[3..] : link.DocumentNumber;
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            invoiceLink = link.Link,
            arrangementDate = TurkeyTime.ToTurkey(link.IssuedAt).ToString("yyyy-MM-dd'T'HH:mm:ss", Inv),
            serialNumber = serial,
            rowNumber = row,
        }), Encoding.UTF8, "application/json");
        var (status, response) = await SendAsync(request, ct);
        return status is >= 200 and < 300 ? $"HTTP {status}" : throw new MarketplaceException($"Hepsiburada fatura linki HTTP {status}: {TrendyolClient.Short(response)}");
    }
}

public sealed class N11Client(IHttpClientFactory httpFactory, MarketplaceOptions options) : MarketplaceClientBase(httpFactory), IMarketplaceClient
{
    public IntegrationKind Kind => IntegrationKind.N11;

    private string Rest => options.N11RestUrl.TrimEnd('/');

    private string InvoiceSoap => options.N11InvoiceSoapUrl;

    public async Task<IReadOnlyList<MarketplaceOrder>> FetchAsync(Integration integration, IReadOnlyDictionary<string, string> secrets, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integration);
        var key = Require(secrets, "appKey", "App Key");
        var secret = Require(secrets, "appSecret", "App Secret");
        var status = integration.Setting("status") ?? "Created";
        var list = new List<MarketplaceOrder>();
        for (var page = 0; page < 100; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{Rest}/delivery/v1/shipmentPackages?status={Uri.EscapeDataString(status)}&startDate={from.ToUnixTimeMilliseconds()}&endDate={to.ToUnixTimeMilliseconds()}&orderByField=true&orderByDirection=ASC&page={page}&size=100");
            request.Headers.TryAddWithoutValidation("appKey", key);
            request.Headers.TryAddWithoutValidation("appSecret", secret);
            request.Headers.Accept.ParseAdd("application/json");
            var (code, body) = await SendAsync(request, ct);
            if (code >= 400)
            {
                throw new MarketplaceException($"n11 paket listesi HTTP {code}: {TrendyolClient.Short(body)}");
            }

            using var doc = Parse(body);
            var content = Arr(doc.RootElement, "content").ToList();
            foreach (var p in content)
            {
                if (Map(integration, p) is { } order)
                {
                    list.Add(new MarketplaceOrder(order, p.GetRawText()));
                }
            }

            var totalPages = (int)Dec(doc.RootElement, "totalPages");
            if (content.Count == 0 || page + 1 >= totalPages)
            {
                break;
            }
        }

        return list;
    }

    private static InboundOrder? Map(Integration integration, JsonElement p)
    {
        var id = Str(p, "id");
        var orderNumber = Str(p, "orderNumber");
        if (id is null && orderNumber is null)
        {
            return null;
        }

        var billing = Obj(p, "billingAddress");
        var corporate = Str(billing, "invoiceType") == "2";
        var customer = Customer(
            Str(billing, "taxId") ?? Str(p, "taxId"),
            Str(billing, "tcId") ?? Str(p, "tcIdentityNumber"),
            corporate,
            Str(billing, "fullName") ?? Str(p, "customerfullName"),
            corporate ? Str(p, "customerfullName") ?? Str(billing, "fullName") : null,
            Str(billing, "taxHouse") ?? Str(p, "taxOffice"),
            Str(p, "customerEmail"),
            Str(billing, "gsm"),
            Join(Str(billing, "neighborhood"), Str(billing, "address")),
            Str(billing, "district"),
            Str(billing, "city"),
            Str(billing, "postalCode"),
            Str(billing, "countryCode") is { } cc and not "null" ? cc : "TR");
        var lines = Arr(p, "lines").Select(l =>
        {
            var qty = Math.Max(1, Dec(l, "quantity"));
            var price = Dec(l, "price");
            var discount = Has(l, "sellerInvoiceAmount") ? Math.Max(0, (price * qty) - Dec(l, "sellerInvoiceAmount"))
                : Has(l, "totalSellerDiscountPrice") ? Dec(l, "totalSellerDiscountPrice") + Dec(l, "sellerCouponDiscount")
                : Dec(l, "sellerDiscount") + Dec(l, "sellerCouponDiscount");
            return new InboundLine(Str(l, "productName") ?? "Ürün", qty, price, Rate(l, integration, "vatRate"), discount, null);
        }).ToList();
        var externalId = id ?? orderNumber!;
        return new InboundOrder(externalId, orderNumber ?? externalId, "TRY", true, customer, lines, [$"n11 sipariş no: {orderNumber ?? externalId}", $"n11 paket no: {externalId}"]);
    }

    public async Task<string> SendInvoiceLinkAsync(Integration integration, IReadOnlyDictionary<string, string> secrets, MarketplaceInvoiceLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integration);
        ArgumentNullException.ThrowIfNull(link);
        if (!link.Link.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || (!link.Link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && options.N11InvoiceSoapUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            throw new MarketplaceException("n11 fatura linki HTTPS olmalı ve .pdf ile bitmelidir; App:PublicUrl değerini HTTPS alan adınızla ayarlayın.");
        }

        static string X(string v) => SecurityElement.Escape(v) ?? string.Empty;
        var envelope = $"""
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:sch="http://www.n11.com/ws/schemas">
              <soapenv:Header/>
              <soapenv:Body>
                <sch:SaveLinkSellerInvoiceRequest>
                  <auth><appKey>{X(Require(secrets, "appKey", "App Key"))}</appKey><appSecret>{X(Require(secrets, "appSecret", "App Secret"))}</appSecret></auth>
                  <url>{X(link.Link)}</url>
                  <orderNumber>{X(link.OrderNumber ?? link.ExternalId)}</orderNumber>
                  <invoiceDateTime>{TurkeyTime.ToTurkey(link.IssuedAt).ToUnixTimeMilliseconds().ToString(Inv)}</invoiceDateTime>
                  <invoiceNumber>{X(link.DocumentNumber)}</invoiceNumber>
                </sch:SaveLinkSellerInvoiceRequest>
              </soapenv:Body>
            </soapenv:Envelope>
            """;
        using var request = new HttpRequestMessage(HttpMethod.Post, InvoiceSoap) { Content = new StringContent(envelope, Encoding.UTF8, "text/xml") };
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");
        var (code, body) = await SendAsync(request, ct);
        XDocument xml;
        try
        {
            xml = XDocument.Parse(body);
        }
        catch (System.Xml.XmlException)
        {
            throw new MarketplaceException($"n11 fatura linki HTTP {code}: {TrendyolClient.Short(body)}");
        }

        var status = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "status")?.Value;
        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return "success";
        }

        var message = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "errorMessage")?.Value
            ?? xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value ?? $"HTTP {code}";
        throw new MarketplaceException("n11 fatura linki reddedildi: " + WebUtility.HtmlDecode(message));
    }
}
