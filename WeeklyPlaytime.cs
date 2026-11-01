using System.Globalization;

namespace SteamHourTracker;

internal sealed record WeeklyGameTime(string Name, double Seconds);

// Only one aggregate per game in the current local calendar week is retained.
internal sealed class WeeklyPlaytime
{
    private const double MaximumSeconds = 8 * 24 * 60 * 60;
    public string WeekStart { get; set; } = "";
    public Dictionary<uint, WeeklyGameTime> Games { get; set; } = [];

    internal static DateOnly Monday(DateTimeOffset time, TimeZoneInfo zone)
    {
        DateOnly day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, zone).DateTime);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }

    private bool TryWeek(out DateOnly start) => DateOnly.TryParseExact(WeekStart, "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out start) && start.DayOfWeek == DayOfWeek.Monday &&
        start <= DateOnly.MaxValue.AddDays(-7);

    internal void EnsureWeek(DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        DateOnly current = Monday(now, zone ?? TimeZoneInfo.Local);
        // Moving the computer clock backwards must not erase an already recorded week.
        if (TryWeek(out var saved) && saved >= current) return;
        WeekStart = current.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Games.Clear();
    }

    internal WeeklyPlaytime Normalize(DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        var clean = new WeeklyPlaytime { WeekStart = WeekStart };
        if (TryWeek(out _))
            foreach (var pair in Games ?? [])
                if (pair.Key > 0 && pair.Value != null && double.IsFinite(pair.Value.Seconds) && pair.Value.Seconds > 0)
                {
                    string name = string.IsNullOrWhiteSpace(pair.Value.Name) ? $"App ID {pair.Key}" : pair.Value.Name.Trim();
                    clean.Games[pair.Key] = new(name.Length > 512 ? name[..512] : name, Math.Min(MaximumSeconds, pair.Value.Seconds));
                }
        clean.EnsureWeek(now, zone);
        return clean;
    }

    internal void Add(SavedGame game, DateTimeOffset from, DateTimeOffset to, double seconds, TimeZoneInfo? zone = null)
    {
        if (game.AppId == 0 || !double.IsFinite(seconds) || seconds <= 0) return;
        zone ??= TimeZoneInfo.Local;
        EnsureWeek(to, zone);
        if (!TryWeek(out var start)) return;
        DateTime StartUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        DateTimeOffset startUtc = new(StartUtc(start));
        DateTimeOffset endUtc = new(StartUtc(start.AddDays(7)));
        double credited;
        if (to <= from)
        {
            // The elapsed stopwatch stays authoritative after a backwards clock adjustment.
            credited = to >= startUtc && to < endUtc ? seconds : 0;
        }
        else
        {
            DateTimeOffset overlapFrom = from > startUtc ? from : startUtc;
            DateTimeOffset overlapTo = to < endUtc ? to : endUtc;
            credited = Math.Max(0, (overlapTo - overlapFrom).TotalSeconds) / (to - from).TotalSeconds * seconds;
        }
        if (credited <= 0) return;
        double previous = Games.TryGetValue(game.AppId, out var saved) ? saved.Seconds : 0;
        Games[game.AppId] = new(game.Name, Math.Min(MaximumSeconds, previous + credited));
    }

    internal static string Duration(double seconds)
    {
        long whole = (long)Math.Max(0, Math.Floor(seconds));
        if (whole >= 3600) return UiText.Format($"{whole / 3600} saat {whole / 60 % 60} dk");
        if (whole >= 60) return UiText.Format($"{whole / 60} dk {whole % 60} sn");
        return whole == 0 && seconds > 0 ? UiText.Get("1 sn'den az") : UiText.Format($"{whole} sn");
    }
}
