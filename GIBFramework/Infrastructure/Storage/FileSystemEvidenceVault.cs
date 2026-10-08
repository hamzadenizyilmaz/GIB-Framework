using System.Globalization;
using GIBFramework.Models.Evidence;

namespace GIBFramework.Infrastructure.Storage;

public sealed class FileSystemEvidenceVault(string rootPath, IClock clock) : IEvidenceVault
{
    private const string ManifestFile = "manifest.json";

    public async Task<EvidenceManifest> StoreAsync(
        Guid tenantId,
        Guid documentId,
        int fiscalYear,
        string stage,
        IReadOnlyList<EvidenceItem> items,
        RetentionClass retentionClass,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count || items.Any(i => !IsSafeName(i.Name)))
        {
            throw new ArgumentException("Kanıt dosya adları benzersiz ve güvenli olmalıdır.", nameof(items));
        }

        var documentDir = DocumentDirectory(tenantId, documentId);
        Directory.CreateDirectory(documentDir);

        var existing = await ListAsync(tenantId, documentId, cancellationToken);
        var sequence = existing.Count == 0 ? 1 : existing[^1].Sequence + 1;
        string stageDir;
        while (true)
        {
            try
            {
                using (new FileStream(Path.Combine(documentDir, $"{sequence:D4}.lock"), FileMode.CreateNew, FileAccess.Write))
                {
                }

                stageDir = Path.Combine(documentDir, $"{sequence:D4}-{stage}");
                Directory.CreateDirectory(stageDir);
                break;
            }
            catch (IOException) when (File.Exists(Path.Combine(documentDir, $"{sequence:D4}.lock")))
            {
                sequence++;
            }
        }

        var artifacts = new List<EvidenceArtifact>();
        foreach (var item in items)
        {
            var path = Path.Combine(stageDir, item.Name);
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(item.Content, cancellationToken);
            }

            File.SetAttributes(path, FileAttributes.ReadOnly);
            artifacts.Add(new EvidenceArtifact(item.Name, item.ContentType, item.Content.LongLength, Hashing.Sha256Hex(item.Content)));
        }

        var previous = existing
            .Where(m => m.Sequence < sequence)
            .OrderBy(m => m.Sequence)
            .LastOrDefault();
        var manifest = new EvidenceManifest
        {
            TenantId = tenantId,
            DocumentId = documentId,
            Sequence = sequence,
            Stage = stage,
            CreatedAt = clock.UtcNow,
            RetentionClass = retentionClass,
            RetainUntil = RetentionPolicy.RetainUntil(retentionClass, fiscalYear),
            Artifacts = artifacts,
            PreviousManifestHash = previous?.ManifestHash,
        };
        manifest = manifest with { ManifestHash = manifest.ComputeHash() };

        var manifestPath = Path.Combine(stageDir, ManifestFile);
        await File.WriteAllBytesAsync(manifestPath, JsonDefaults.SerializeToUtf8(manifest, indented: true), cancellationToken);
        File.SetAttributes(manifestPath, FileAttributes.ReadOnly);
        return manifest;
    }

    public async Task<IReadOnlyList<EvidenceManifest>> ListAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken)
    {
        var dir = DocumentDirectory(tenantId, documentId);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        var list = new List<EvidenceManifest>();
        foreach (var stageDir in Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.Ordinal))
        {
            var path = Path.Combine(stageDir, ManifestFile);
            if (File.Exists(path))
            {
                list.Add(JsonDefaults.Deserialize<EvidenceManifest>(await File.ReadAllTextAsync(path, cancellationToken)));
            }
        }

        return [.. list.OrderBy(m => m.Sequence)];
    }

    public async Task<byte[]?> ReadAsync(Guid tenantId, Guid documentId, int sequence, string name, CancellationToken cancellationToken)
    {
        if (!IsSafeName(name))
        {
            return null;
        }

        var stageDir = Directory.GetDirectories(DocumentDirectory(tenantId, documentId), $"{sequence:D4}-*").FirstOrDefault();
        var path = stageDir is null ? null : Path.Combine(stageDir, name);
        return path is not null && File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public async Task<EvidenceVerification> VerifyAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken)
    {
        var manifests = await ListAsync(tenantId, documentId, cancellationToken);
        var problems = new List<string>();
        string? previousHash = null;

        foreach (var m in manifests)
        {
            if (m.ComputeHash() != m.ManifestHash)
            {
                problems.Add($"#{m.Sequence} {m.Stage}: manifest içeriği değiştirilmiş.");
            }

            if (m.PreviousManifestHash != previousHash)
            {
                problems.Add($"#{m.Sequence} {m.Stage}: manifest zinciri kopuk.");
            }

            foreach (var a in m.Artifacts)
            {
                var content = await ReadAsync(tenantId, documentId, m.Sequence, a.Name, cancellationToken);
                if (content is null)
                {
                    problems.Add($"#{m.Sequence} {a.Name}: dosya eksik.");
                }
                else if (Hashing.Sha256Hex(content) != a.Sha256)
                {
                    problems.Add($"#{m.Sequence} {a.Name}: dosya içeriği değiştirilmiş (SHA-256 uyuşmuyor).");
                }
            }

            previousHash = m.ManifestHash;
        }

        return new EvidenceVerification(documentId, manifests.Count, problems.Count == 0, problems);
    }

    private string DocumentDirectory(Guid tenantId, Guid documentId) =>
        Path.Combine(rootPath, tenantId.ToString("N", CultureInfo.InvariantCulture), documentId.ToString("N", CultureInfo.InvariantCulture));

    private static bool IsSafeName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !name.Contains("..", StringComparison.Ordinal)
        && !string.Equals(name, ManifestFile, StringComparison.OrdinalIgnoreCase);
}
