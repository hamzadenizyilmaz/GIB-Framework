using GIBFramework.Services.Compliance.Evaluators;
using System.Diagnostics.CodeAnalysis;

namespace GIBFramework.Services.Compliance;

public sealed class RuleEvaluatorRegistry
{
    private readonly Dictionary<string, IRuleEvaluator> _byKind;

    public RuleEvaluatorRegistry(IEnumerable<IRuleEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        _byKind = new(StringComparer.Ordinal);
        foreach (var evaluator in evaluators)
        {
            if (!_byKind.TryAdd(evaluator.Kind, evaluator))
            {
                throw new InvalidOperationException($"Aynı kural türü için birden fazla evaluator: {evaluator.Kind}");
            }
        }
    }

    public static RuleEvaluatorRegistry CreateDefault() => new(
    [
        new EFaturaRoutingEvaluator(),
        new EArchiveRoutingEvaluator(),
        new Vuk230RequiredFieldsEvaluator(),
        new IssuancePeriodEvaluator(),
        new InvoiceObligationThresholdEvaluator(),
    ]);

    public IReadOnlyCollection<string> Kinds => _byKind.Keys;

    public bool TryGet(string kind, [NotNullWhen(true)] out IRuleEvaluator? evaluator) => _byKind.TryGetValue(kind, out evaluator);

    public IRuleEvaluator Get(string kind) =>
        TryGet(kind, out var evaluator) ? evaluator : throw new KeyNotFoundException($"Bilinmeyen kural türü: {kind}");
}
