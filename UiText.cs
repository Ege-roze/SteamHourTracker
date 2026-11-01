using System.Globalization;
using System.Text.Json;

namespace SteamHourTracker;

// Only application-owned literals/templates are translated. Game/account names,
// saved values, wire messages and parser input are never rewritten.
internal static class UiText
{
    internal const string WorkerLanguageVariable = "SHT_UI_LANGUAGE";
    private static readonly Lazy<Dictionary<string, string>> english = new(() =>
    {
        using var stream = typeof(UiText).Assembly.GetManifestResourceStream("SteamHourTracker.Strings.en.json")
            ?? throw new InvalidOperationException("English language resource is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    });

    internal static string Language { get; private set; } = Normalize(Environment.GetEnvironmentVariable(WorkerLanguageVariable));
    internal static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "en" ? "en-US" : "tr-TR");
    internal static string Normalize(string? language) => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "tr";
    internal static void SetLanguage(string? language) => Language = Normalize(language);
    internal static string Get(string turkish) => Language == "en" && english.Value.TryGetValue(turkish, out string? value) ? value : turkish;
    internal static string Format(FormattableString text) => string.Format(Culture, Get(text.Format), text.GetArguments());
}
