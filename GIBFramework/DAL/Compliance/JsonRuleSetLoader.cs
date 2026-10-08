using GIBFramework.Services.Compliance;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GIBFramework.DAL.Compliance;

public static class JsonRuleSetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static RuleSet LoadFromDirectory(string databaseRoot, RuleEvaluatorRegistry registry)
    {
        var sourceFiles = ListJson(Path.Combine(databaseRoot, "legal-sources"));
        var ruleFiles = ListJson(Path.Combine(databaseRoot, "legal-rules"));

        var hashInput = new StringBuilder();
        var sources = new List<LegalSource>();
        foreach (var file in sourceFiles)
        {
            var json = File.ReadAllText(file);
            hashInput.Append("source:").Append(Path.GetFileName(file)).Append(':').AppendLine(Hashing.Sha256Hex(json));
            sources.AddRange(ParseSources(json, file));
        }

        var rules = new List<RuleDto>();
        foreach (var file in ruleFiles)
        {
            var json = File.ReadAllText(file);
            hashInput.Append("rules:").Append(Path.GetFileName(file)).Append(':').AppendLine(Hashing.Sha256Hex(json));
            rules.AddRange(Deserialize<RuleFileDto>(json, file).Rules);
        }

        return Build(sources, rules, Hashing.Sha256Hex(hashInput.ToString()), registry);
    }

    public static RuleSet LoadFromJson(string sourcesJson, string rulesJson, RuleEvaluatorRegistry registry)
    {
        var sources = ParseSources(sourcesJson, "<sources>");
        var rules = Deserialize<RuleFileDto>(rulesJson, "<rules>").Rules;
        return Build(sources, rules, Hashing.Sha256Hex(sourcesJson + "\n" + rulesJson), registry);
    }

    private static RuleSet Build(List<LegalSource> sources, List<RuleDto> dtos, string hash, RuleEvaluatorRegistry registry)
    {
        var byCode = sources.GroupBy(s => s.Code, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var errors = new List<string>();
        var rules = new List<LegalRule>();

        foreach (var dto in dtos)
        {
            var basis = new List<LegalBasisReference>();
            foreach (var b in dto.LegalBasis ?? [])
            {
                if (byCode.TryGetValue(b.Source, out var source))
                {
                    basis.Add(new LegalBasisReference(source, b.Article));
                }
                else
                {
                    errors.Add($"{dto.Code}:v{dto.Version}: tanımsız hukuki kaynak '{b.Source}'.");
                }
            }

            try
            {
                rules.Add(new LegalRule
                {
                    Code = dto.Code,
                    Version = dto.Version,
                    Kind = dto.Kind,
                    Title = dto.Title,
                    Description = dto.Description,
                    Period = new EffectivePeriod(dto.EffectiveFrom, dto.EffectiveTo),
                    Severity = dto.Severity,
                    Blocking = dto.Blocking,
                    Priority = dto.Priority,
                    LegalBasis = basis,
                    Parameters = new RuleParameters(dto.Parameters ?? []),
                    Review = new RuleReview(dto.Review.Status, dto.Review.ReviewedBy, dto.Review.ApprovedBy, dto.Review.ApprovedAt, dto.Review.Notes),
                });
            }
            catch (DomainException ex)
            {
                errors.Add($"{dto.Code}:v{dto.Version}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new RuleSetValidationException(errors);
        }

        return RuleSet.Create(sources, rules, hash, registry);
    }

    private static List<LegalSource> ParseSources(string json, string origin) =>
        [.. Deserialize<SourceFileDto>(json, origin).Sources.Select(s =>
            new LegalSource(s.Code, s.Title, s.Kind, s.Uri is null ? null : new Uri(s.Uri), s.Sha256, s.Notes))];

    private static string[] ListJson(string directory) =>
        Directory.Exists(directory)
            ? [.. Directory.GetFiles(directory, "*.json").OrderBy(f => f, StringComparer.Ordinal)]
            : throw new DirectoryNotFoundException($"Kural dizini bulunamadı: {directory}");

    private static T Deserialize<T>(string json, string origin) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidDataException($"Boş JSON: {origin}");

    private sealed record SourceFileDto(List<SourceDto> Sources);

    private sealed record SourceDto(string Code, string Title, LegalSourceKind Kind, string? Uri, string? Sha256, string? Notes);

    private sealed record RuleFileDto(List<RuleDto> Rules);

    private sealed record RuleDto(
        string Code,
        int Version,
        string Kind,
        string Title,
        string? Description,
        DateOnly EffectiveFrom,
        DateOnly? EffectiveTo,
        FindingSeverity Severity,
        bool Blocking,
        int Priority,
        List<BasisDto>? LegalBasis,
        Dictionary<string, JsonElement>? Parameters,
        ReviewDto Review);

    private sealed record BasisDto(string Source, string? Article);

    private sealed record ReviewDto(RuleReviewStatus Status, string? ReviewedBy, string? ApprovedBy, DateTimeOffset? ApprovedAt, string? Notes);
}
