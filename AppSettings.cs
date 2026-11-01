using System.Globalization;
using System.Text.Json;

namespace SteamHourTracker;

internal sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string Language { get; set; } = "tr";
    public bool MultiGame { get; set; }
    public bool Invisible { get; set; }
    public bool AutoStop { get; set; }
    public int Minutes { get; set; } = 60;
    public CardFarmOptions CardMode { get; set; } = new();
    public int CardDefaultsVersion { get; set; }
    public Dictionary<string,GameSelectionSettings> Accounts { get; set; } = [];

    internal AppSettings Normalize()
    {
        var clean = new AppSettings
        {
            Language = UiText.Normalize(Language),
            MultiGame = MultiGame, Invisible = Invisible, AutoStop = AutoStop,
            Minutes = Math.Clamp(Minutes, 1, 10080), CardMode = (CardMode ?? new()).Normalize(), CardDefaultsVersion = 1
        };
        // One-time change of the former default; later explicit choices remain untouched.
        if (CardDefaultsVersion == 0 && clean.CardMode.HoursBeforeDrops == 3)
            clean.CardMode = clean.CardMode with { HoursBeforeDrops = 2 };
        foreach (var pair in Accounts ?? [])
        {
            if (pair.Value == null || (pair.Key != SettingsStore.LocalAccount &&
                (!ulong.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) || id == 0))) continue;
            clean.Accounts[pair.Key] = new GameSelectionSettings
            {
                SourceIndex = pair.Value.SourceIndex == 1 ? 1 : 0,
                CurrentGame = CleanGame(pair.Value.CurrentGame),
                Weekly = (pair.Value.Weekly ?? new()).Normalize(DateTimeOffset.UtcNow),
                CardExcludedAppIds = (pair.Value.CardExcludedAppIds ?? []).Where(id => id > 0).ToHashSet(),
                TargetProgress = (pair.Value.TargetProgress ?? []).Where(p => p.Key > 0 && p.Value != null &&
                    p.Value.TargetMinutes is >= 1 and <= 10080 && double.IsFinite(p.Value.CompletedSeconds))
                    .ToDictionary(p => p.Key, p => p.Value with { CompletedSeconds = Math.Clamp(p.Value.CompletedSeconds, 0, p.Value.TargetMinutes * 60d) }),
                SelectedGames = (pair.Value.SelectedGames ?? []).Select(CleanGame).OfType<SavedGame>()
                    .DistinctBy(game => game.AppId).ToList()
            };
        }
        return clean;
    }

    private static SavedGame? CleanGame(SavedGame? game)
    {
        if (game == null || game.AppId == 0) return null;
        string name = string.IsNullOrWhiteSpace(game.Name) ? $"App ID {game.AppId}" : game.Name.Trim();
        return new SavedGame(game.AppId, name.Length > 512 ? name[..512] : name,
            game.TargetMinutes is >= 1 and <= 10080 ? game.TargetMinutes : null);
    }
}

internal sealed class GameSelectionSettings
{
    public HashSet<uint> CardExcludedAppIds { get; set; } = [];
    public WeeklyPlaytime Weekly { get; set; } = new();
    public Dictionary<uint, GameTargetProgress> TargetProgress { get; set; } = [];
    public int SourceIndex { get; set; }
    public SavedGame? CurrentGame { get; set; }
    public List<SavedGame> SelectedGames { get; set; } = [];
}

internal sealed record GameTargetProgress(int TargetMinutes, double CompletedSeconds, bool UsesGeneral = false)
{
    internal static double Read(IReadOnlyDictionary<uint, GameTargetProgress>? progress, SavedGame game, int target) =>
        target > 0 && progress != null && progress.TryGetValue(game.AppId, out var saved) &&
        saved.TargetMinutes == target && saved.UsesGeneral == (game.TargetMinutes == null) && double.IsFinite(saved.CompletedSeconds)
            ? Math.Clamp(saved.CompletedSeconds, 0, target * 60d) : 0;
}

internal record SettingsLoadResult(AppSettings Settings, bool CanSave, string? Warning);

internal sealed class SettingsStore(string directory)
{
    private byte[]? lastSavedBytes;
    private DateTime lastSavedWriteTime;
    internal const string LocalAccount = "local";
    private const int MaxBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 16 };
    public string FilePath { get; } = Path.Combine(directory, "settings.json");

    public SettingsLoadResult Load()
    {
        lastSavedBytes = null;
        if (!File.Exists(FilePath)) return new(new AppSettings(), true, null);
        try
        {
            if (new FileInfo(FilePath).Length > MaxBytes) throw new InvalidDataException(UiText.Get("Ayar dosyası çok büyük."));
            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions)
                ?? throw new InvalidDataException(UiText.Get("Ayar dosyası boş."));
            if (loaded.Version != 1)
                return new(new AppSettings(), false, UiText.Get("Ayar dosyasının sürümü desteklenmiyor. Dosya korundu; ayarlar kaydedilmeyecek."));
            return new(loaded.Normalize(), true, null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            try
            {
                File.Copy(FilePath, FilePath + ".corrupt-" + Guid.NewGuid().ToString("N") + ".bak");
                return new(new AppSettings(), true, UiText.Get("Ayar dosyası okunamadı; yedeği alındı ve varsayılan ayarlar açıldı."));
            }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException)
            {
                return new(new AppSettings(), false, UiText.Get("Ayar dosyası okunamadı ve yedeklenemedi. Dosyayı korumak için kayıt kapatıldı."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(new AppSettings(), false, UiText.Get("Ayarlar okunamadı. Bu açılışta ayarlar kaydedilmeyecek: ") + ex.Message);
        }
    }

    public void Save(AppSettings settings)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(settings.Normalize(), JsonOptions);
        if (bytes.Length > MaxBytes) throw new IOException(UiText.Get("Ayar dosyası boyut sınırını aştı."));
        if (lastSavedBytes != null && bytes.AsSpan().SequenceEqual(lastSavedBytes))
        {
            var saved = new FileInfo(FilePath);
            if (saved.Exists && saved.Length == bytes.Length && saved.LastWriteTimeUtc == lastSavedWriteTime) return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            // Same-directory replacement keeps the previous complete file on write failure.
            File.Move(temporary, FilePath, overwrite: true);
            lastSavedBytes = bytes;
            lastSavedWriteTime = File.GetLastWriteTimeUtc(FilePath);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
