using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SteamHourTracker;

internal sealed record CardGame(uint AppId, string Name, int DropsRemaining, double HoursPlayed, bool DropCountShown = true);
internal sealed record CardInventory(ulong Account, IReadOnlyList<CardGame> Games);
internal interface ICardInventory : IDisposable
{
    Task<CardInventory> ReadAsync(CancellationToken token);
}
internal sealed class CardLoginException(string message) : Exception(message);
internal sealed class CardPageException(string message) : Exception(message);
internal sealed record BadgePage(IReadOnlyList<CardGame> Games, int LastPage);

// Small, bounded reader for the Steam badge document. It never executes downloaded scripts.
internal static class SteamBadgeParser
{
    private static Regex Pattern(string value) => new(value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
    private static readonly Regex LoginId = Pattern("\\bg_steamID\\s*=\\s*['\"]([0-9]{17})['\"]");
    private static readonly Regex Tags = Pattern("<!--.*?-->|<(?<end>/)?(?<name>[a-z][a-z0-9]*)(?<attrs>(?:[^>\"']|\"[^\"]*\"|'[^']*')*)>|(?<text>[^<]+)");
    private static readonly Regex Attributes = Pattern("(?<name>[a-z_][a-z0-9_:.-]*)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s>]+))");
    private static readonly Regex RawContent = Pattern("<(script|style)\\b[^>]*>.*?</\\1\\s*>");
    private static readonly Regex AppId = Pattern("/gamecards/([0-9]+)/");
    private static readonly Regex Drops = Pattern("\\b([0-9][0-9,]*)\\s+card\\s+drops?\\s+remaining\\b");
    private static readonly Regex Hours = Pattern("([0-9][0-9,.]*)\\s+hrs?\\s+on\\s+record");
    private static readonly Regex PageNumber = Pattern("(?:[?&])p=([0-9]+)(?:&|$)");

    private sealed class Node(string tag)
    {
        internal string Tag = tag;
        internal Dictionary<string, string> Attributes = new(StringComparer.OrdinalIgnoreCase);
        internal List<Node> Children = [];
        internal List<object> Content = [];
        internal StringBuilder OwnText = new();
        internal string Attribute(string name) => Attributes.GetValueOrDefault(name, "");
        internal bool HasClass(string value) => Attribute("class").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(value);
        internal IEnumerable<Node> All() { yield return this; foreach (var child in Children) foreach (var node in child.All()) yield return node; }
        internal string Text => string.Concat(Content.Select(c => c is Node child ? child.Text : WebUtility.HtmlDecode((string)c))).Replace('\u00a0', ' ').Trim();
    }

    internal static BadgePage Parse(string html, ulong expectedAccount)
    {
        if (html.Length > 4 * 1024 * 1024) throw new CardPageException(UiText.Get("Steam rozet sayfası beklenenden büyük."));
        var id = LoginId.Match(html);
        if (!id.Success) throw new CardLoginException(UiText.Get("Steam Topluluk oturumu gerekli. Kart modunu yeniden açıp Steam'e giriş yap."));
        if (expectedAccount == 0 || id.Groups[1].Value != expectedAccount.ToString(CultureInfo.InvariantCulture))
            throw new CardLoginException(UiText.Get("Steam web hesabı açık Steam hesabıyla farklı. Giriş penceresinde doğru hesabı kullan."));
        var root = new Node("root"); var stack = new List<Node> { root }; int nodes = 0;
        foreach (Match tag in Tags.Matches(RawContent.Replace(html, "")))
        {
            if (tag.Groups["text"].Success) { stack[^1].OwnText.Append(tag.Value); stack[^1].Content.Add(tag.Value); continue; }
            if (!tag.Groups["name"].Success) continue;
            string name = tag.Groups["name"].Value.ToLowerInvariant();
            if (tag.Groups["end"].Success)
            {
                int at = stack.FindLastIndex(n => n.Tag == name);
                if (at > 0) stack.RemoveRange(at, stack.Count - at);
                continue;
            }
            if (++nodes > 100000 || stack.Count > 128) throw new CardPageException(UiText.Get("Steam rozet sayfasının yapısı okunamadı."));
            var node = new Node(name);
            foreach (Match attribute in Attributes.Matches(tag.Groups["attrs"].Value))
                node.Attributes[attribute.Groups["name"].Value] = WebUtility.HtmlDecode(attribute.Groups["value"].Value);
            stack[^1].Children.Add(node);
            stack[^1].Content.Add(node);
            if (name is not ("area" or "base" or "br" or "col" or "embed" or "hr" or "img" or "input" or "link" or "meta" or "param" or "source" or "track" or "wbr") && !tag.Value.EndsWith("/>")) stack.Add(node);
        }
        var all = root.All().ToArray();
        var sheet = all.FirstOrDefault(n => n.HasClass("badges_sheet"));
        if (sheet == null) throw new CardPageException(UiText.Get("Steam rozet sayfası alınamadı. Eksik sayfa sıfır kart olarak kabul edilmedi."));
        var rows = sheet.All().Where(n => n.HasClass("badge_row")).ToArray();
        if (rows.Length == 0 && !sheet.Text.Contains("no badges", StringComparison.OrdinalIgnoreCase))
            throw new CardPageException(UiText.Get("Steam rozet listesi boş veya eksik geldi. Yeniden dene."));
        var games = new Dictionary<uint, CardGame>();
        foreach (var row in rows)
        {
            var children = row.All().ToArray();
            string href = children.FirstOrDefault(n => n.HasClass("badge_row_overlay"))?.Attribute("href") ?? "";
            var appMatch = AppId.Match(href);
            if (!appMatch.Success || href.Contains("border=1", StringComparison.OrdinalIgnoreCase)) continue;
            if (!uint.TryParse(appMatch.Groups[1].Value, out uint appId) || appId == 0) throw new CardPageException(UiText.Get("Geçersiz oyun kimliği."));
            string title = WebUtility.HtmlDecode(children.FirstOrDefault(n => n.HasClass("badge_title"))?.OwnText.ToString() ?? "").Replace('\u00a0', ' ').Trim();
            if (title.Length == 0) title = "App ID " + appId;
            var dropNode = children.FirstOrDefault(n => n.HasClass("badge_title_stats_drops"));
            if (dropNode == null) throw new CardPageException(UiText.Format($"{title}: kart alanı eksik; tarama durduruldu."));
            string dropText = dropNode.Text;
            bool dropCountShown = dropText.Length != 0;
            var dropMatch = Drops.Match(dropText);
            int count;
            if (dropMatch.Success && int.TryParse(dropMatch.Groups[1].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out count) && count <= 10000) { }
            else if (dropText.Contains("No card drops remaining", StringComparison.OrdinalIgnoreCase)) count = 0;
            // Steam includes earned badges without an active drop entitlement. An empty,
            // present field is distinct from a missing element or an unrecognised response.
            else if (!dropCountShown) count = 0;
            else throw new CardPageException(UiText.Format($"{title}: kalan kart bilgisi okunamadı. Oyun tamamlandı sayılmadı."));
            double hours = 0;
            var timeNode = children.FirstOrDefault(n => n.HasClass("badge_title_stats_playtime"));
            if (count > 0 && timeNode == null) throw new CardPageException(UiText.Format($"{title}: oynama saati alanı eksik."));
            if (timeNode != null)
            {
                string timeText = timeNode.Text;
                var hoursMatch = Hours.Match(timeText);
                if (hoursMatch.Success && double.TryParse(hoursMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out hours) && hours is >= 0 and <= 1000000) { }
                else if (timeText.Length == 0 || timeText.Contains("No playtime recorded", StringComparison.OrdinalIgnoreCase)) hours = 0;
                else throw new CardPageException(UiText.Format($"{title}: oynama saati okunamadı. Süre tahmin edilmedi."));
            }
            if (!games.TryAdd(appId, new(appId, title, count, hours, dropCountShown))) throw new CardPageException(UiText.Get("Steam aynı oyunu iki kez döndürdü. Listeyi tekrar tara."));
        }
        int pages = 1;
        foreach (var link in all.Where(n => n.Tag == "a" && (n.HasClass("pagelink") || n.HasClass("pagebtn"))))
        {
            var page = PageNumber.Match(link.Attribute("href"));
            if (page.Success && int.TryParse(page.Groups[1].Value, out int number)) pages = Math.Max(pages, number);
        }
        if (pages > 500) throw new CardPageException(UiText.Get("Rozet sayfa sayısı beklenen sınırı aşıyor."));
        return new(games.Values.ToArray(), pages);
    }
}

internal sealed class SteamBadgeClient : ICardInventory
{
    private readonly HttpClient client;
    private readonly ulong account;
    internal TimeSpan PageDelay { get; set; } = TimeSpan.FromSeconds(1);
    internal TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    internal CardInventory? CachedFirstRead { get; set; }
    internal SteamBadgeClient(ulong account, HttpMessageHandler handler)
    {
        this.account = account;
        client = new(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SteamHourTracker/1.0");
    }
    internal SteamBadgeClient(ulong account, CookieContainer cookies) : this(account, new HttpClientHandler
    {
        CookieContainer = cookies, AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All
    }) { }
    public async Task<CardInventory> ReadAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (CachedFirstRead is { } cached) { CachedFirstRead = null; return cached; }
        var games = new Dictionary<uint, CardGame>(); int lastPage = 1;
        for (int page = 1; page <= lastPage; page++)
        {
            if (page > 1) await Task.Delay(PageDelay, token);
            // URLs are built from numeric IDs only; never follow downloaded pagination URLs or login redirects.
            var uri = new Uri($"https://steamcommunity.com/profiles/{account}/badges/?l=english&sort=a&p={page}");
            using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            requestDeadline.CancelAfter(RequestTimeout);
            var requestToken = requestDeadline.Token;
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, requestToken);
            if ((int)response.StatusCode is >= 300 and < 400 || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new CardLoginException(UiText.Get("Steam web oturumu sona erdi veya erişim engellendi. Kart modunu yeniden açıp giriş yap."));
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 4 * 1024 * 1024) throw new CardPageException(UiText.Get("Steam yanıtı beklenenden büyük."));
            await using var input = await response.Content.ReadAsStreamAsync(requestToken);
            using var buffer = new MemoryStream(); var block = new byte[16384];
            while (true)
            {
                int n = await input.ReadAsync(block, requestToken); if (n == 0) break;
                if (buffer.Length + n > 4 * 1024 * 1024) throw new CardPageException(UiText.Get("Steam yanıtı beklenenden büyük."));
                buffer.Write(block, 0, n);
            }
            BadgePage parsed;
            try { parsed = SteamBadgeParser.Parse(Encoding.UTF8.GetString(buffer.ToArray()), account); }
            catch (RegexMatchTimeoutException) { throw new CardPageException(UiText.Get("Steam rozet sayfasının yapısı değişmiş olabilir. Tarama durduruldu.")); }
            lastPage = Math.Max(lastPage, parsed.LastPage);
            foreach (var game in parsed.Games)
                if (!games.TryAdd(game.AppId, game)) throw new CardPageException(UiText.Get("Rozet sayfaları değişti veya tekrarlandı. Eksik listeyle devam edilmedi; yeniden tara."));
        }
        return new(account, games.Values.ToArray());
    }
    public void Dispose() => client.Dispose();
}
