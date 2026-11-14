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
        if (!IsSafeName(stage))
        {
            throw new ArgumentException("Kanıt aşamasının adı geçersiz.", nameof(stage));
        }
        if (items.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count || items.Any(i => !IsSafeName(i.Name)))
        {
            throw new ArgumentException("Kanıt dosya adları benzersiz ve güvenli olmalıdır.", nameof(items));
        }

        var documentDir = DocumentDirectory(tenantId, documentId);
        Directory.CreateDirectory(documentDir);

        await using var documentLock = await LockDocumentAsync(documentDir, cancellationToken);
        var existing = await ListAsync(tenantId, documentId, cancellationToken);
        var previous = existing.LastOrDefault();
        var sequence = (previous?.Sequence ?? 0) + 1;
        while (Directory.EnumerateDirectories(documentDir, $"{sequence:D4}-*").Any())
        {
            sequence++;
        }

        var stageDir = Path.Combine(documentDir, $"{sequence:D4}-{stage}");
        var pending = Path.Combine(documentDir, ".pending");
        DeletePending(pending);
        Directory.CreateDirectory(pending);
        var artifacts = new List<EvidenceArtifact>();
        try
        {
            foreach (var item in items)
            {
                await File.WriteAllBytesAsync(Path.Combine(pending, item.Name), item.Content, cancellationToken);
                artifacts.Add(new EvidenceArtifact(item.Name, item.ContentType, item.Content.LongLength, Hashing.Sha256Hex(item.Content)));
            }

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
            await File.WriteAllBytesAsync(Path.Combine(pending, ManifestFile), JsonDefaults.SerializeToUtf8(manifest, indented: true), cancellationToken);
            foreach (var file in Directory.EnumerateFiles(pending))
            {
                File.SetAttributes(file, FileAttributes.ReadOnly);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(pending, stageDir);
            return manifest;
        }
        finally
        {
            DeletePending(pending);
        }
    }

    private static async Task<FileStream> LockDocumentAsync(string directory, CancellationToken ct)
    {
        var path = Path.Combine(directory, ".write.lock");
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 11 or 32 or 33 && wait.Elapsed < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(50, ct);
            }
        }
    }

    private static void DeletePending(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        Directory.Delete(directory);
    }

    public async Task<IReadOnlyList<EvidenceManifest>> ListAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken)
    {
        var dir = DocumentDirectory(tenantId, documentId);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        var list = new List<EvidenceManifest>();
        foreach (var stageDir in Directory.EnumerateDirectories(dir, "*-*"))
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

        var documentDir = DocumentDirectory(tenantId, documentId);
        if (!Directory.Exists(documentDir))
        {
            return null;
        }

        var stageDir = Directory.EnumerateDirectories(documentDir, $"{sequence:D4}-*")
            .FirstOrDefault(dir => File.Exists(Path.Combine(dir, ManifestFile)));
        var path = stageDir is null ? null : Path.Combine(stageDir, name);
        if (path is null || !File.Exists(path))
            return null;

        var manifest = JsonDefaults.Deserialize<EvidenceManifest>(
            await File.ReadAllTextAsync(Path.Combine(stageDir!, ManifestFile), cancellationToken));
        var expected = manifest.Artifacts.FirstOrDefault(artifact => artifact.Name == name);
        if (manifest.TenantId != tenantId || manifest.DocumentId != documentId || manifest.Sequence != sequence
            || manifest.ManifestHash != manifest.ComputeHash() || expected is null)
        {
            throw new InvalidDataException("Belgenin kanıt kaydı doğrulanamadı.");
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        if (content.LongLength != expected.Size || Hashing.Sha256Hex(content) != expected.Sha256)
            throw new InvalidDataException("Kanıt dosyasının bütünlüğü doğrulanamadı.");

        return content;
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
                try
                {
                    if (await ReadAsync(tenantId, documentId, m.Sequence, a.Name, cancellationToken) is null)
                        problems.Add($"#{m.Sequence} {a.Name}: dosya eksik.");
                }
                catch (InvalidDataException ex)
                {
                    problems.Add($"#{m.Sequence} {a.Name}: {ex.Message}");
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
