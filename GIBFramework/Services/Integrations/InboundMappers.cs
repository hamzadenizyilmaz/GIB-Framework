using System.Globalization;
using System.Text.Json;
using GIBFramework.Models.Integrations;

namespace GIBFramework.Services.Integrations;

public sealed record MapResult(InboundOrder? Order, string? IgnoredReason)
{
    public static MapResult Ignore(string reason) => new(null, reason);
}

public static class InboundMappers
{
    public static MapResult Map(Integration integration, JsonElement root, string? topic)
    {
        ArgumentNullException.ThrowIfNull(integration);
        return integration.Kind switch
        {
            IntegrationKind.WooCommerce => WooCommerce(integration, root),
            IntegrationKind.Shopify => Shopify(integration, root, topic),
            _ => Generic(integration, root),
        };
    }

    public static MapResult WooCommerce(Integration integration, JsonElement o)
    {
        ArgumentNullException.ThrowIfNull(integration);
        var status = Str(o, "status") ?? string.Empty;
        var allowed = (integration.Setting("triggerStatuses") ?? "processing,completed")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return MapResult.Ignore($"Sipariş durumu \"{status}\" faturalanacak durumlar arasında değil.");
        }

        var id = Str(o, "id") ?? throw Invalid("Sipariş kimliği (id) yok.");
        var billing = Obj(o, "billing");
        var meta = o.TryGetProperty("meta_data", out var m) && m.ValueKind == JsonValueKind.Array ? m : default;
        string? Meta(string? key) => key is null || meta.ValueKind != JsonValueKind.Array ? null
            : meta.EnumerateArray().Where(x => Str(x, "key") == key).Select(x => Str(x, "value")).FirstOrDefault();

        var name = Join(Str(billing, "first_name"), Str(billing, "last_name"));
        var customer = new InboundCustomer(
            Meta(integration.Setting("taxIdMetaKey")),
            name,
            Str(billing, "company"),
            Meta(integration.Setting("taxOfficeMetaKey")),
            Str(billing, "email"),
            Str(billing, "phone"),
            Join(Str(billing, "address_1"), Str(billing, "address_2")),
            null,
            Str(billing, "city") ?? Str(billing, "state"),
            Str(billing, "postcode"),
            Str(billing, "country"));

        var lines = new List<InboundLine>();
        foreach (var l in Arr(o, "line_items"))
        {
            var qty = Dec(l, "quantity", 1);
            var total = Dec(l, "total");
            var tax = Dec(l, "total_tax");
            var subtotal = Dec(l, "subtotal", total);
            var discount = Math.Max(0, subtotal - total);
            lines.Add(new InboundLine(Str(l, "name") ?? "Ürün", qty, qty == 0 ? 0 : subtotal / qty, Rate(tax, total, integration), discount, null));
        }

        foreach (var s in Arr(o, "shipping_lines"))
        {
            var total = Dec(s, "total");
            if (total > 0)
            {
                lines.Add(new InboundLine(Str(s, "method_title") ?? "Kargo", 1, total, Rate(Dec(s, "total_tax"), total, integration), 0, null));
            }
        }

        foreach (var f in Arr(o, "fee_lines"))
        {
            var total = Dec(f, "total");
            if (total > 0)
            {
                lines.Add(new InboundLine(Str(f, "name") ?? "Ek ücret", 1, total, Rate(Dec(f, "total_tax"), total, integration), 0, null));
            }
        }

        var number = Str(o, "number") ?? id;
        var notes = new List<string> { $"WooCommerce sipariş no: {number}" };
        if (Str(o, "customer_note") is { } note)
        {
            notes.Add(note);
        }

        return new MapResult(new InboundOrder(id, number, Str(o, "currency") ?? "TRY", false, customer, lines, notes), null);
    }

    public static MapResult Shopify(Integration integration, JsonElement o, string? topic)
    {
        ArgumentNullException.ThrowIfNull(integration);
        if (topic is not null && !topic.Equals("orders/paid", StringComparison.OrdinalIgnoreCase) && !topic.Equals("orders/create", StringComparison.OrdinalIgnoreCase))
        {
            return MapResult.Ignore($"Shopify konusu \"{topic}\" faturalanmıyor.");
        }

        if (topic?.Equals("orders/create", StringComparison.OrdinalIgnoreCase) == true && Str(o, "financial_status") is not ("paid" or null))
        {
            return MapResult.Ignore("Sipariş henüz ödenmedi.");
        }

        var id = Str(o, "id") ?? throw Invalid("Sipariş kimliği (id) yok.");
        var billing = Obj(o, "billing_address");
        var buyer = Obj(o, "customer");
        var included = Bool(o, "taxes_included");
        var attrName = integration.Setting("taxIdAttribute");
        var taxId = attrName is null ? null : Arr(o, "note_attributes").Where(a => Str(a, "name") == attrName).Select(a => Str(a, "value")).FirstOrDefault();
        var customer = new InboundCustomer(
            taxId,
            Join(Str(billing, "first_name") ?? Str(buyer, "first_name"), Str(billing, "last_name") ?? Str(buyer, "last_name")),
            Str(billing, "company"),
            null,
            Str(o, "email") ?? Str(buyer, "email"),
            Str(billing, "phone") ?? Str(o, "phone") ?? Str(buyer, "phone"),
            Join(Str(billing, "address1"), Str(billing, "address2")),
            null,
            Str(billing, "city") ?? Str(billing, "province"),
            Str(billing, "zip"),
            Str(billing, "country"));

        var lines = new List<InboundLine>();
        foreach (var l in Arr(o, "line_items"))
        {
            var rate = Arr(l, "tax_lines").Select(t => Dec(t, "rate")).FirstOrDefault() * 100;
            if (rate == 0 && Bool(l, "taxable"))
            {
                rate = DefaultRate(integration);
            }

            lines.Add(new InboundLine(Str(l, "title") ?? Str(l, "name") ?? "Ürün", Dec(l, "quantity", 1), Dec(l, "price"), Snap(rate), Dec(l, "total_discount"), null));
        }

        foreach (var s in Arr(o, "shipping_lines"))
        {
            var price = Dec(s, "price");
            if (price > 0)
            {
                var rate = Arr(s, "tax_lines").Select(t => Dec(t, "rate")).FirstOrDefault() * 100;
                lines.Add(new InboundLine(Str(s, "title") ?? "Kargo", 1, price, Snap(rate), 0, null));
            }
        }

        var name = Str(o, "name") ?? Str(o, "order_number") ?? id;
        return new MapResult(new InboundOrder(id, name.TrimStart('#'), Str(o, "currency") ?? "TRY", included, customer, lines, [$"Shopify sipariş no: {name}"]), null);
    }

    public static MapResult Generic(Integration integration, JsonElement o)
    {
        ArgumentNullException.ThrowIfNull(integration);
        if (Str(o, "event") is { } ev && ev is not ("order.paid" or "invoice.paid"))
        {
            return MapResult.Ignore($"\"{ev}\" olayı faturalanmıyor.");
        }

        var id = Str(o, "externalId") ?? Str(o, "orderNumber") ?? throw Invalid("externalId alanı zorunludur.");
        var c = Obj(o, "customer");
        var customer = new InboundCustomer(
            Str(c, "taxId"), Str(c, "name"), Str(c, "company"), Str(c, "taxOffice"), Str(c, "email"), Str(c, "phone"),
            Str(c, "address"), Str(c, "district"), Str(c, "city"), Str(c, "postalCode"), Str(c, "country"));
        var lines = Arr(o, "lines").Select(l => new InboundLine(
            Str(l, "name") ?? "Hizmet",
            Dec(l, "quantity", 1),
            Dec(l, "unitPrice"),
            l.TryGetProperty("vatRate", out _) ? Snap(Dec(l, "vatRate")) : DefaultRate(integration),
            Dec(l, "discount"),
            Str(l, "unitCode"))).ToList();
        var notes = Arr(o, "notes").Select(n => n.ToString()).Where(n => n.Length > 0).ToList();
        var label = integration.Kind switch
        {
            IntegrationKind.Whmcs => "WHMCS fatura no",
            IntegrationKind.WiseCp => "WISECP fatura no",
            _ => "Sipariş no",
        };
        var number = Str(o, "orderNumber") ?? id;
        notes.Insert(0, $"{label}: {number}");
        return new MapResult(new InboundOrder(id, number, Str(o, "currency") ?? "TRY", Bool(o, "pricesIncludeTax"), customer, lines, notes), null);
    }

    public static decimal Snap(decimal rate)
    {
        decimal[] allowed = [0, 1, 8, 10, 18, 20];
        return allowed.OrderBy(a => Math.Abs(a - rate)).First();
    }

    private static decimal DefaultRate(Integration integration) =>
        decimal.TryParse(integration.Setting("defaultVatRate"), NumberStyles.Number, CultureInfo.InvariantCulture, out var r) ? r : 20;

    private static decimal Rate(decimal tax, decimal net, Integration integration) =>
        net > 0 ? Snap(Math.Round(tax / net * 100, 2)) : DefaultRate(integration);

    private static InvalidDataException Invalid(string message) => new(message);

    private static string? Join(params string?[] parts)
    {
        var joined = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return joined.Length == 0 ? null : joined;
    }

    private static JsonElement Obj(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : default;

    private static IEnumerable<JsonElement> Arr(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    private static string? Str(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v))
        {
            return null;
        }

        var s = v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static bool Bool(JsonElement e, string name) =>
        Str(e, name) is { } s && (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1");

    private static decimal Dec(JsonElement e, string name, decimal fallback = 0) =>
        Str(e, name) is { } s && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : fallback;
}
