using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.DAL;

namespace GIBFramework.Services.Locations;

public sealed class LocationOptions
{
    public const string Section = "Locations";

    public string TkgmBaseUrl { get; set; } = "https://cbsapi.tkgm.gov.tr/megsiswebapi.v3/api/idariYapi/";

    public bool SyncEnabled { get; set; } = true;

    public int RequestDelaySeconds { get; set; } = 3;

    public int RetryAfterLimitHours { get; set; } = 6;

    public int RequestTimeoutSeconds { get; set; } = 60;
}

public sealed record LocationItem(int Id, string Name);

public sealed record LocationList(IReadOnlyList<LocationItem> Items, bool Complete, string? Message);

public sealed record LocationStatus(int Provinces, int ProvincesWithDistricts, int Districts, int DistrictsWithNeighborhoods, int Neighborhoods, DateTimeOffset? LimitUntil);

public sealed class TkgmLimitException(string message) : Exception(message);

public sealed class TkgmClient(IHttpClientFactory httpFactory, LocationOptions options)
{
    public const string HttpClientName = "Tkgm";

    public Task<IReadOnlyList<LocationItem>> DistrictsAsync(int provinceId, CancellationToken ct) =>
        GetAsync($"ilceListe/{provinceId.ToString(CultureInfo.InvariantCulture)}", ct);

    public Task<IReadOnlyList<LocationItem>> NeighborhoodsAsync(int districtId, CancellationToken ct) =>
        GetAsync($"mahalleListe/{districtId.ToString(CultureInfo.InvariantCulture)}", ct);

    private async Task<IReadOnlyList<LocationItem>> GetAsync(string path, CancellationToken ct)
    {
        var client = httpFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(options.TkgmBaseUrl.EndsWith('/') ? options.TkgmBaseUrl : options.TkgmBaseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.GetAsync(path, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderUnavailableException($"TKGM servisine ulaşılamadı: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ProviderUnavailableException("TKGM servisi zamanında yanıt vermedi.");
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
                || (!response.IsSuccessStatusCode && body.Contains("limit", StringComparison.OrdinalIgnoreCase)))
            {
                throw new TkgmLimitException("TKGM günlük sorgu limiti doldu.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ProviderUnavailableException($"TKGM servisi HTTP {(int)response.StatusCode} döndü.");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            {
                throw new ProviderUnavailableException("TKGM servisinden beklenmeyen yanıt.");
            }

            var items = new List<LocationItem>();
            foreach (var f in features.EnumerateArray())
            {
                if (f.TryGetProperty("properties", out var p)
                    && p.TryGetProperty("id", out var id) && id.TryGetInt32(out var value)
                    && p.TryGetProperty("text", out var text) && text.GetString() is { Length: > 0 } name)
                {
                    items.Add(new LocationItem(value, TitleCase(name)));
                }
            }

            return items;
        }
    }

    internal static string TitleCase(string value)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var trimmed = value.Trim();
        return trimmed.Any(char.IsLower) ? trimmed : tr.TextInfo.ToTitleCase(trimmed.ToLower(tr));
    }
}

public sealed class LocationRepository(SqlConnectionFactory connections)
{
    public async Task<IReadOnlyList<LocationItem>> ProvincesAsync(CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT Id, Name FROM dbo.Provinces ORDER BY Name COLLATE Turkish_CI_AS");
        return await ReadAsync(cmd, ct);
    }

    public async Task<(IReadOnlyList<LocationItem> Items, bool Loaded)> DistrictsAsync(int provinceId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT Id, Name FROM dbo.Districts WHERE ProvinceId = @p ORDER BY Name COLLATE Turkish_CI_AS;
            SELECT CAST(CASE WHEN DistrictsLoadedAt IS NULL THEN 0 ELSE 1 END AS bit) FROM dbo.Provinces WHERE Id = @p;
            """);
        cmd.With("@p", provinceId);
        return await ReadWithFlagAsync(cmd, ct);
    }

    public async Task<(IReadOnlyList<LocationItem> Items, bool Loaded)> NeighborhoodsAsync(int districtId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT Id, Name FROM dbo.Neighborhoods WHERE DistrictId = @d ORDER BY Name COLLATE Turkish_CI_AS;
            SELECT CAST(CASE WHEN NeighborhoodsLoadedAt IS NULL THEN 0 ELSE 1 END AS bit) FROM dbo.Districts WHERE Id = @d;
            """);
        cmd.With("@d", districtId);
        return await ReadWithFlagAsync(cmd, ct);
    }

    public async Task SaveDistrictsAsync(int provinceId, IReadOnlyList<LocationItem> items, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        foreach (var item in items)
        {
            await using var cmd = c.Command("""
                MERGE dbo.Districts AS t USING (SELECT @id AS Id) AS s ON t.Id = s.Id
                WHEN MATCHED THEN UPDATE SET Name = @n, ProvinceId = @p
                WHEN NOT MATCHED THEN INSERT (Id, ProvinceId, Name) VALUES (@id, @p, @n);
                """, tx);
            await cmd.With("@id", item.Id).With("@p", provinceId).With("@n", item.Name).ExecuteNonQueryAsync(ct);
        }

        await using (var mark = c.Command("UPDATE dbo.Provinces SET DistrictsLoadedAt = SYSDATETIMEOFFSET() WHERE Id = @p", tx))
        {
            await mark.With("@p", provinceId).ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task SaveNeighborhoodsAsync(int districtId, IReadOnlyList<LocationItem> items, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        foreach (var item in items)
        {
            await using var cmd = c.Command("""
                MERGE dbo.Neighborhoods AS t USING (SELECT @id AS Id) AS s ON t.Id = s.Id
                WHEN MATCHED THEN UPDATE SET Name = @n, DistrictId = @d
                WHEN NOT MATCHED THEN INSERT (Id, DistrictId, Name) VALUES (@id, @d, @n);
                """, tx);
            await cmd.With("@id", item.Id).With("@d", districtId).With("@n", item.Name).ExecuteNonQueryAsync(ct);
        }

        await using (var mark = c.Command("UPDATE dbo.Districts SET NeighborhoodsLoadedAt = SYSDATETIMEOFFSET() WHERE Id = @d", tx))
        {
            await mark.With("@d", districtId).ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<int?> NextProvinceWithoutDistrictsAsync(CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT TOP 1 Id FROM dbo.Provinces WHERE DistrictsLoadedAt IS NULL ORDER BY PlateCode");
        return await cmd.ExecuteScalarAsync(ct) is int id ? id : null;
    }

    public async Task<int?> NextDistrictWithoutNeighborhoodsAsync(CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT TOP 1 d.Id FROM dbo.Districts d JOIN dbo.Provinces p ON p.Id = d.ProvinceId
             WHERE d.NeighborhoodsLoadedAt IS NULL ORDER BY p.PlateCode, d.Name
            """);
        return await cmd.ExecuteScalarAsync(ct) is int id ? id : null;
    }

    public async Task<IReadOnlyList<string>> ReferenceNamesAsync(CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT Name FROM dbo.TaxOffices WHERE EffectiveTo IS NULL
            UNION ALL SELECT DistrictName FROM dbo.TaxOffices WHERE EffectiveTo IS NULL AND DistrictName IS NOT NULL
            UNION ALL SELECT Name FROM dbo.Provinces
            """);
        var list = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(r.GetString(0));
        }

        return list;
    }

    public async Task<int> RenameAsync(string table, Func<string, string> fix, CancellationToken ct)
    {
        if (table is not ("Districts" or "Neighborhoods"))
        {
            throw new ArgumentOutOfRangeException(nameof(table));
        }

        await using var c = await connections.OpenSystemAsync(ct);
        var changes = new List<(int Id, string Name)>();
        await using (var cmd = c.Command($"SELECT Id, Name FROM dbo.{table}"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var name = r.GetString(1);
                var fixedName = fix(name);
                if (!string.Equals(name, fixedName, StringComparison.Ordinal))
                {
                    changes.Add((r.GetInt32(0), fixedName));
                }
            }
        }

        foreach (var (id, name) in changes)
        {
            await using var update = c.Command($"UPDATE dbo.{table} SET Name = @n WHERE Id = @id");
            await update.With("@n", name).With("@id", id).ExecuteNonQueryAsync(ct);
        }

        return changes.Count;
    }

    public async Task<(int, int, int, int, int)> CountsAsync(CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT (SELECT COUNT(*) FROM dbo.Provinces), (SELECT COUNT(*) FROM dbo.Provinces WHERE DistrictsLoadedAt IS NOT NULL),
                   (SELECT COUNT(*) FROM dbo.Districts), (SELECT COUNT(*) FROM dbo.Districts WHERE NeighborhoodsLoadedAt IS NOT NULL),
                   (SELECT COUNT(*) FROM dbo.Neighborhoods)
            """);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4));
    }

    private static async Task<IReadOnlyList<LocationItem>> ReadAsync(SqlCommand cmd, CancellationToken ct)
    {
        var list = new List<LocationItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new LocationItem(r.GetInt32(0), r.GetString(1)));
        }

        return list;
    }

    private static async Task<(IReadOnlyList<LocationItem>, bool)> ReadWithFlagAsync(SqlCommand cmd, CancellationToken ct)
    {
        var list = new List<LocationItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new LocationItem(r.GetInt32(0), r.GetString(1)));
        }

        var loaded = false;
        if (await r.NextResultAsync(ct) && await r.ReadAsync(ct))
        {
            loaded = r.GetBoolean(0);
        }

        return (list, loaded);
    }
}

public sealed partial class LocationService(LocationRepository repository, TkgmClient tkgm, LocationSyncState state, ILogger<LocationService> logger)
{
    private const string LimitMessage = "TKGM günlük sorgu limiti doldu; liste tamamlandığında burada görünecek.";

    public Task<IReadOnlyList<LocationItem>> ProvincesAsync(CancellationToken ct) => repository.ProvincesAsync(ct);

    public async Task<LocationList> DistrictsAsync(int provinceId, CancellationToken ct)
    {
        var (items, loaded) = await repository.DistrictsAsync(provinceId, ct);
        if (loaded)
        {
            return new LocationList(items, true, null);
        }

        if (!await TryFetchAsync(() => LoadDistrictsAsync(provinceId, ct)))
        {
            return new LocationList(items, false, LimitMessage);
        }

        return new LocationList((await repository.DistrictsAsync(provinceId, ct)).Items, true, null);
    }

    public async Task<LocationList> NeighborhoodsAsync(int districtId, CancellationToken ct)
    {
        var (items, loaded) = await repository.NeighborhoodsAsync(districtId, ct);
        if (loaded)
        {
            return new LocationList(items, true, null);
        }

        if (!await TryFetchAsync(() => LoadNeighborhoodsAsync(districtId, ct)))
        {
            return new LocationList(items, false, LimitMessage);
        }

        return new LocationList((await repository.NeighborhoodsAsync(districtId, ct)).Items, true, null);
    }

    public async Task<LocationStatus> StatusAsync(CancellationToken ct)
    {
        var (p, pl, d, dl, n) = await repository.CountsAsync(ct);
        return new LocationStatus(p, pl, d, dl, n, state.LimitUntil);
    }

    public async Task<bool> SyncStepAsync(CancellationToken ct)
    {
        if (await repository.NextProvinceWithoutDistrictsAsync(ct) is { } provinceId)
        {
            await LoadDistrictsAsync(provinceId, ct);
            return true;
        }

        if (await repository.NextDistrictWithoutNeighborhoodsAsync(ct) is { } districtId)
        {
            await LoadNeighborhoodsAsync(districtId, ct);
            return true;
        }

        return false;
    }

    public async Task<int> FixExistingNamesAsync(CancellationToken ct)
    {
        var fixer = await FixerAsync(ct);
        return await repository.RenameAsync("Districts", fixer.Fix, ct) + await repository.RenameAsync("Neighborhoods", fixer.Fix, ct);
    }

    private async Task LoadDistrictsAsync(int provinceId, CancellationToken ct)
    {
        var items = await tkgm.DistrictsAsync(provinceId, ct);
        var fixer = await FixerAsync(ct);
        await repository.SaveDistrictsAsync(provinceId, [.. items.Select(i => i with { Name = fixer.Fix(i.Name) })], ct);
    }

    private async Task LoadNeighborhoodsAsync(int districtId, CancellationToken ct)
    {
        var items = await tkgm.NeighborhoodsAsync(districtId, ct);
        var fixer = await FixerAsync(ct);
        await repository.SaveNeighborhoodsAsync(districtId, [.. items.Select(i => i with { Name = fixer.Fix(i.Name) })], ct);
    }

    private async Task<TurkishNameFixer> FixerAsync(CancellationToken ct)
    {
        if (state.Fixer is { } cached)
        {
            return cached;
        }

        var names = await repository.ReferenceNamesAsync(ct);
        var fixer = TurkishNameFixer.Build(names);
        if (names.Count > 500)
        {
            state.Fixer = fixer;
        }

        return fixer;
    }

    private async Task<bool> TryFetchAsync(Func<Task> fetch)
    {
        if (state.IsLimited)
        {
            return false;
        }

        try
        {
            await fetch();
            return true;
        }
        catch (TkgmLimitException)
        {
            state.MarkLimited();
            return false;
        }
        catch (ProviderUnavailableException ex)
        {
            Log.FetchFailed(logger, ex.Message);
            return false;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "TKGM verisi alınamadı: {Reason}")]
        public static partial void FetchFailed(ILogger logger, string reason);
    }
}

public sealed class LocationSyncState(LocationOptions options, IClock clock)
{
    private long _limitUntilTicks;

    public DateTimeOffset? LimitUntil
    {
        get
        {
            var ticks = Interlocked.Read(ref _limitUntilTicks);
            return ticks == 0 || ticks <= clock.UtcNow.UtcTicks ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public bool IsLimited => LimitUntil is not null;

    public TurkishNameFixer? Fixer { get; set; }

    public void MarkLimited() =>
        Interlocked.Exchange(ref _limitUntilTicks, clock.UtcNow.AddHours(Math.Max(1, options.RetryAfterLimitHours)).UtcTicks);
}

public sealed partial class LocationSyncWorker(IServiceScopeFactory scopes, LocationOptions options, LocationSyncState state, ILogger<LocationSyncWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var renamed = await scope.ServiceProvider.GetRequiredService<LocationService>().FixExistingNamesAsync(stoppingToken);
            Log.Renamed(logger, renamed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Failed(logger, ex.Message);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(Math.Max(1, options.RequestDelaySeconds));
            if (state.LimitUntil is { } until)
            {
                delay = until - DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1);
            }
            else
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<LocationService>();
                    if (!await service.SyncStepAsync(stoppingToken))
                    {
                        Log.Completed(logger);
                        return;
                    }
                }
                catch (TkgmLimitException)
                {
                    state.MarkLimited();
                    Log.Limited(logger, options.RetryAfterLimitHours);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.Failed(logger, ex.Message);
                    delay = TimeSpan.FromMinutes(5);
                }
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "TKGM il/ilçe/mahalle aktarımı tamamlandı.")]
        public static partial void Completed(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "İl/ilçe/mahalle adlarında {Count} düzeltme yapıldı.")]
        public static partial void Renamed(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Information, Message = "TKGM günlük sorgu limiti doldu; aktarım {Hours} saat sonra devam edecek.")]
        public static partial void Limited(ILogger logger, int hours);

        [LoggerMessage(Level = LogLevel.Warning, Message = "TKGM aktarım adımı başarısız: {Reason}")]
        public static partial void Failed(ILogger logger, string reason);
    }
}

public sealed class TurkishNameFixer
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly Dictionary<string, string> _words;

    private TurkishNameFixer(Dictionary<string, string> words) => _words = words;

    public static TurkishNameFixer Build(IEnumerable<string> referenceNames)
    {
        var counts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var name in referenceNames)
        {
            foreach (var word in Split(name))
            {
                if (word.Length < 3 || !word.Any(c => c is 'ı' or 'I' or 'i' or 'İ'))
                {
                    continue;
                }

                var spelled = Tr.TextInfo.ToTitleCase(word.ToLower(Tr));
                var key = Key(spelled);
                counts.TryAdd(key, new Dictionary<string, int>(StringComparer.Ordinal));
                counts[key][spelled] = counts[key].GetValueOrDefault(spelled) + 1;
            }
        }

        return new TurkishNameFixer(counts.ToDictionary(c => c.Key, c => c.Value.MaxBy(v => v.Value).Key, StringComparer.Ordinal));
    }

    public string Fix(string name)
    {
        var parts = name.Split(' ');
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = FixLarSuffix(parts[i]);
            if (parts[i].Length > 0 && _words.TryGetValue(Key(parts[i]), out var correct) && !string.Equals(correct, parts[i], StringComparison.Ordinal)
                && string.Equals(Key(correct), Key(parts[i]), StringComparison.Ordinal))
            {
                parts[i] = char.IsUpper(parts[i][0]) ? correct : correct.ToLower(Tr);
            }
        }

        return string.Join(' ', parts);
    }

    private static string FixLarSuffix(string word)
    {
        var lower = word.ToLower(Tr);
        var stem = lower.EndsWith("lari", StringComparison.Ordinal) ? lower.Length - 4 : lower.EndsWith("lar", StringComparison.Ordinal) ? lower.Length - 3 : -1;
        if (stem <= 0)
        {
            return word;
        }

        var chars = word.ToCharArray();
        if (lower.EndsWith("lari", StringComparison.Ordinal))
        {
            chars[^1] = char.IsUpper(chars[^1]) ? 'I' : 'ı';
        }

        for (var j = stem - 1; j >= 0; j--)
        {
            if ("aeıioöuü".Contains(lower[j], StringComparison.Ordinal))
            {
                if (lower[j] == 'i')
                {
                    chars[j] = char.IsUpper(chars[j]) ? 'I' : 'ı';
                }

                break;
            }
        }

        return new string(chars);
    }

    private static IEnumerable<string> Split(string value) =>
        value.Split([' ', '-', '/', '(', ')', '.', ','], StringSplitOptions.RemoveEmptyEntries);

    private static string Key(string word) =>
        word.ToLower(Tr).Replace('ı', 'i').Replace("i̇", "i", StringComparison.Ordinal);
}
