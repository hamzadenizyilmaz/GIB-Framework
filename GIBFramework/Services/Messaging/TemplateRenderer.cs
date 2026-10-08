using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GIBFramework.Services.Messaging;

public sealed record RenderedEmail(string Subject, string Html, string Text);

public sealed record EmailBranding(string CompanyName, string? CompanyLines, string? Footer, string BrandColor, bool HasLogo);

public static partial class TemplateRenderer
{
    public const string LogoContentId = "logo@gibframework";

    public static string Render(string template, IReadOnlyDictionary<string, string?> values, bool html, IReadOnlyCollection<string>? keep = null) =>
        VariablePattern().Replace(template ?? string.Empty, m =>
        {
            var name = m.Groups[1].Value;
            if (keep is not null && keep.Contains(name))
            {
                return m.Value;
            }

            var value = values.TryGetValue(name, out var v) ? v ?? string.Empty : string.Empty;
            return html ? WebUtility.HtmlEncode(value) : value;
        });

    public static string FillSecrets(string text, IReadOnlyDictionary<string, string> secrets, bool html) =>
        VariablePattern().Replace(text ?? string.Empty, m =>
            secrets.TryGetValue(m.Groups[1].Value, out var v) ? (html ? WebUtility.HtmlEncode(v) : v) : m.Value);

    public static IReadOnlyList<string> UnknownVariables(string template, IEnumerable<string> known)
    {
        var set = new HashSet<string>(known, StringComparer.Ordinal) { "marka.renk" };
        return [.. VariablePattern().Matches(template ?? string.Empty).Select(m => m.Groups[1].Value).Where(n => !set.Contains(n)).Distinct(StringComparer.Ordinal)];
    }

    public static string Layout(string bodyHtml, EmailBranding brand)
    {
        ArgumentNullException.ThrowIfNull(brand);
        var color = SafeColor(brand.BrandColor);
        var name = WebUtility.HtmlEncode(brand.CompanyName);
        var header = brand.HasLogo
            ? $"<img src=\"cid:{LogoContentId}\" alt=\"{name}\" style=\"max-height:56px;max-width:220px;display:block\">"
            : $"<span style=\"font-size:20px;font-weight:700;color:#1f2933\">{name}</span>";
        var lines = string.IsNullOrWhiteSpace(brand.CompanyLines) ? string.Empty
            : $"<div style=\"margin-top:6px\">{WebUtility.HtmlEncode(brand.CompanyLines).Replace("\n", "<br>", StringComparison.Ordinal)}</div>";
        var footer = string.IsNullOrWhiteSpace(brand.Footer) ? string.Empty
            : $"<div style=\"margin-top:10px\">{WebUtility.HtmlEncode(brand.Footer).Replace("\n", "<br>", StringComparison.Ordinal)}</div>";
        return $$"""
            <!doctype html>
            <html lang="tr">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{name}}</title></head>
            <body style="margin:0;padding:0;background:#f4f7fb;font-family:'Segoe UI',Arial,Helvetica,sans-serif;color:#1f2933">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f7fb;padding:28px 12px">
            <tr><td align="center">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 2px 12px rgba(15,40,80,.06)">
            <tr><td style="height:5px;background:{{color}}"></td></tr>
            <tr><td style="padding:24px 32px 8px">{{header}}</td></tr>
            <tr><td style="padding:12px 32px 28px;font-size:15px;line-height:1.6">{{bodyHtml}}</td></tr>
            <tr><td style="padding:18px 32px;background:#f8fafc;border-top:1px solid #eef1f5;font-size:12px;color:#6b7684;line-height:1.5">
            <strong style="color:#1f2933">{{name}}</strong>{{lines}}{{footer}}
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """;
    }

    public static string ToPlainText(string html)
    {
        var text = BreakPattern().Replace(html ?? string.Empty, "\n");
        text = LinkPattern().Replace(text, m => $"{m.Groups[2].Value} ({m.Groups[1].Value})");
        text = TagPattern().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        var sb = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = WhitespacePattern().Replace(line, " ").Trim();
            if (trimmed.Length > 0 || (sb.Length > 0 && sb[^1] != '\n'))
            {
                sb.Append(trimmed).Append('\n');
            }
        }

        return sb.ToString().Trim();
    }

    public static string SafeColor(string? color) => color is not null && ColorPattern().IsMatch(color) ? color : "#0778E6";

    [GeneratedRegex(@"\{\{\s*([a-z0-9_.]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();

    [GeneratedRegex(@"<\s*(br|/p|/tr|/div|/h[1-6]|/li)\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakPattern();

    [GeneratedRegex(@"<a\s[^>]*href=""([^""]+)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"[ \t\r\f\v]+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
