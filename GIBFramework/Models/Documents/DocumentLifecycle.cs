using System.Collections.Frozen;

namespace GIBFramework.Models.Documents;

public enum DocumentStatus
{
    Draft,
    Validating,
    Validated,
    AwaitingApproval,
    Approved,
    Signing,
    Signed,
    Queued,
    Transmitting,

    InDoubt,
    Sent,
    Acknowledged,
    Delivered,
    Accepted,
    Rejected,
    Cancelled,
    Objected,

    Failed,
}

public static class DocumentLifecycle
{
    private static readonly FrozenDictionary<DocumentStatus, DocumentStatus[]> Transitions = new Dictionary<DocumentStatus, DocumentStatus[]>
    {
        [DocumentStatus.Draft] = [DocumentStatus.Validating],
        [DocumentStatus.Validating] = [DocumentStatus.Validated, DocumentStatus.Draft],
        [DocumentStatus.Validated] = [DocumentStatus.AwaitingApproval, DocumentStatus.Draft],
        [DocumentStatus.AwaitingApproval] = [DocumentStatus.Approved, DocumentStatus.Draft],
        [DocumentStatus.Approved] = [DocumentStatus.Signing],
        [DocumentStatus.Signing] = [DocumentStatus.Signed, DocumentStatus.Approved],
        [DocumentStatus.Signed] = [DocumentStatus.Queued],
        [DocumentStatus.Queued] = [DocumentStatus.Transmitting],
        [DocumentStatus.Transmitting] = [DocumentStatus.Sent, DocumentStatus.InDoubt, DocumentStatus.Failed],
        [DocumentStatus.InDoubt] = [DocumentStatus.Sent, DocumentStatus.Acknowledged, DocumentStatus.Delivered, DocumentStatus.Failed],
        [DocumentStatus.Sent] = [DocumentStatus.Acknowledged, DocumentStatus.Delivered, DocumentStatus.Rejected, DocumentStatus.InDoubt],
        [DocumentStatus.Acknowledged] = [DocumentStatus.Delivered, DocumentStatus.Rejected],
        [DocumentStatus.Delivered] = [DocumentStatus.Accepted, DocumentStatus.Rejected, DocumentStatus.Cancelled, DocumentStatus.Objected],
        [DocumentStatus.Accepted] = [DocumentStatus.Objected],
        [DocumentStatus.Rejected] = [],
        [DocumentStatus.Cancelled] = [],
        [DocumentStatus.Objected] = [],
        [DocumentStatus.Failed] = [DocumentStatus.Queued, DocumentStatus.Sent, DocumentStatus.Delivered],
    }.ToFrozenDictionary();

    public static bool CanTransition(DocumentStatus from, DocumentStatus to) => Transitions[from].Contains(to);

    public static IReadOnlyList<DocumentStatus> AllowedFrom(DocumentStatus from) => Transitions[from];

    public static void EnsureTransition(DocumentStatus from, DocumentStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new DomainException("DOC_STATUS_TRANSITION", $"{from} durumundan {to} durumuna geçiş yapılamaz.");
        }
    }

    public static IReadOnlyList<DocumentStatus> FindReconciliationPath(DocumentStatus from, DocumentStatus to)
    {
        DocumentStatus[] intermediates = [DocumentStatus.InDoubt, DocumentStatus.Sent, DocumentStatus.Acknowledged, DocumentStatus.Delivered];
        DocumentStatus[] outcomes = [DocumentStatus.Accepted, DocumentStatus.Rejected, DocumentStatus.Cancelled, DocumentStatus.Objected, DocumentStatus.Failed];
        if (from == to || !(intermediates.Contains(to) || outcomes.Contains(to)))
        {
            return [];
        }

        var previous = new Dictionary<DocumentStatus, DocumentStatus> { [from] = from };
        var queue = new Queue<DocumentStatus>([from]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in Transitions[current])
            {
                if (previous.ContainsKey(next) || (next != to && !intermediates.Contains(next)))
                {
                    continue;
                }

                previous[next] = current;
                if (next == to)
                {
                    var path = new List<DocumentStatus> { to };
                    for (var s = current; s != from; s = previous[s])
                    {
                        path.Add(s);
                    }

                    path.Reverse();
                    return path;
                }

                queue.Enqueue(next);
            }
        }

        return [];
    }

    public static bool MayHaveReachedProvider(DocumentStatus status) => status is DocumentStatus.Transmitting or DocumentStatus.InDoubt
        or DocumentStatus.Sent or DocumentStatus.Acknowledged or DocumentStatus.Delivered or DocumentStatus.Accepted
        or DocumentStatus.Rejected or DocumentStatus.Cancelled or DocumentStatus.Objected;
}
