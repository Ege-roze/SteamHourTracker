using System.Net;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SteamHourTracker;

internal sealed class SteamWebLoginDialog : Form
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    private readonly Label message = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8), Text = UiText.Get("Steam giriş ekranı hazırlanıyor…") };
    private readonly CancellationTokenSource closing = new();
    private bool checking;
    private bool connectionReady;
    private bool disposed;
    internal ICardInventory? Inventory { get; private set; }

    internal SteamWebLoginDialog(ulong account, string dataDirectory)
    {
        Text = UiText.Get("Steam Topluluk bağlantısı • steamcommunity.com");
        ClientSize = new Size(900, 720); MinimumSize = new Size(640, 500);
        Font = SystemFonts.MessageBoxFont; StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(5) };
        var retry = new Button { Text = UiText.Get("Giriş yaptım, kontrol et"), AutoSize = true, Enabled = false };
        var reset = new Button { Text = UiText.Get("Web oturumunu sıfırla"), AutoSize = true, Enabled = false };
        var cancel = new Button { Text = UiText.Get("Vazgeç"), AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.AddRange([retry, reset, cancel]);
        layout.Controls.Add(message, 0, 0); layout.Controls.Add(browser, 0, 1); layout.Controls.Add(actions, 0, 2);
        Controls.Add(layout); CancelButton = cancel;
        var certificates = new SteamCertificateGuard(() =>
        {
            connectionReady = false;
            closing.Cancel(); // Also cancels any badge check already in flight.
            if (disposed || IsDisposed) return;
            retry.Enabled = reset.Enabled = browser.Enabled = false;
            message.Text = UiText.Get("Steam bağlantısının güvenlik sertifikası doğrulanamadı. Giriş iptal edildi. ") +
                UiText.Get("Ağını ve bilgisayarın tarih/saatini kontrol edip pencereyi yeniden aç.");
            browser.CoreWebView2?.Stop();
        });
        async Task CheckAsync()
        {
            if (!connectionReady || checking || closing.IsCancellationRequested || browser.CoreWebView2 == null) return;
            checking = true; retry.Enabled = reset.Enabled = false;
            SteamBadgeClient? candidate = null;
            try
            {
                var cookies = await browser.CoreWebView2.CookieManager.GetCookiesAsync("https://steamcommunity.com/");
                closing.Token.ThrowIfCancellationRequested();
                if (!cookies.Any(c => c.Name == "steamLoginSecure"))
                { message.Text = UiText.Get("Steam'in kendi sayfasında giriş yap. Şifren uygulama kodunda okunmaz veya kaydedilmez."); return; }
                var jar = new CookieContainer(100, 20, 16384);
                foreach (var cookie in cookies)
                    if (cookie.Domain.TrimStart('.').Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase) &&
                        cookie.Name is "steamLoginSecure" or "sessionid" or "steamParental")
                        jar.Add(new Cookie(cookie.Name, cookie.Value, "/", "steamcommunity.com") { Secure = true, HttpOnly = true });
                message.Text = UiText.Get("Hesap ve bütün rozet sayfaları doğrulanıyor…");
                candidate = new SteamBadgeClient(account, jar);
                var snapshot = await candidate.ReadAsync(closing.Token);
                closing.Token.ThrowIfCancellationRequested();
                candidate.CachedFirstRead = snapshot;
                Inventory = candidate; candidate = null;
                DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException) when (closing.IsCancellationRequested) { }
            catch (CardLoginException ex) { if (!closing.IsCancellationRequested) message.Text = ex.Message; }
            catch (CardPageException ex) { if (!closing.IsCancellationRequested) message.Text = ex.Message; }
            catch (Exception ex)
            {
                if (!closing.IsCancellationRequested)
                    message.Text = UiText.Get("Steam bağlantısı kurulamadı. Tekrar dene. (") + ex.GetType().Name +
                        (ex is HttpRequestException http ? $" / {http.HttpRequestError} / {http.StatusCode}" : "") + ")";
            }
            finally
            {
                candidate?.Dispose(); checking = false;
                if (!IsDisposed && !closing.IsCancellationRequested) retry.Enabled = reset.Enabled = connectionReady;
            }
        }
        retry.Click += async (_, _) => await CheckAsync();
        reset.Click += (_, _) =>
        {
            if (!connectionReady || closing.IsCancellationRequested) return;
            browser.CoreWebView2?.CookieManager.DeleteAllCookies();
            if (browser.CoreWebView2 != null) browser.CoreWebView2.Navigate(LoginUrl);
        };
        Shown += async (_, _) =>
        {
            try
            {
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(dataDirectory, "SteamWebView", account.ToString()));
                if (closing.IsCancellationRequested) return;
                await browser.EnsureCoreWebView2Async(environment);
                if (closing.IsCancellationRequested) return;
                browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                browser.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
                browser.CoreWebView2.DownloadStarting += (_, e) => e.Cancel = true;
                browser.CoreWebView2.NavigationStarting += (_, e) => { if (!Allowed(e.Uri)) e.Cancel = true; };
                browser.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; if (Allowed(e.Uri)) browser.CoreWebView2.Navigate(e.Uri); };
                // Attach before navigating; certificate-failed NavigationCompleted fires before
                // ServerCertificateErrorDetected, so the guard must run before the cookie check.
                await certificates.AttachAsync(browser.CoreWebView2);
                if (closing.IsCancellationRequested) return;
                browser.CoreWebView2.NavigationCompleted += async (_, e) => { if (e.IsSuccess) await CheckAsync(); };
                connectionReady = true;
                retry.Enabled = reset.Enabled = true;
                browser.CoreWebView2.Navigate(LoginUrl);
            }
            catch (WebView2RuntimeNotFoundException) { message.Text = UiText.Get("Microsoft Edge WebView2 Runtime bulunamadı. Microsoft'un resmi sitesinden WebView2 Runtime kurup tekrar dene."); }
            catch (Exception) { if (!closing.IsCancellationRequested) message.Text = UiText.Get("Steam giriş penceresi açılamadı. Pencereyi kapatıp tekrar dene."); }
        };
        FormClosing += (_, _) => closing.Cancel();
    }
    private const string LoginUrl = "https://steamcommunity.com/login/home/?goto=my%2Fbadges%2F%3Fl%3Denglish";
    internal static bool Allowed(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort &&
        uri.Host is "steamcommunity.com" or "login.steampowered.com" or "store.steampowered.com";
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed) { disposed = true; closing.Cancel(); browser.Dispose(); closing.Dispose(); }
        base.Dispose(disposing);
    }
}

// No trust exceptions: a certificate failure blocks this login window until it is reopened.
internal sealed class SteamCertificateGuard(Action onBlocked)
{
    internal bool IsBlocked { get; private set; }

    internal async Task AttachAsync(CoreWebView2 core)
    {
        core.NavigationCompleted += (_, e) =>
        {
            if (!e.IsSuccess && IsCertificateError(e.WebErrorStatus)) Block();
        };
        core.ServerCertificateErrorDetected += (_, e) =>
        {
            e.Action = CoreWebView2ServerCertificateErrorAction.Cancel;
            Block();
        };
        // Remove any earlier allow decisions from another WebView sharing this session.
        await core.ClearServerCertificateErrorActionsAsync();
    }

    internal static bool IsCertificateError(CoreWebView2WebErrorStatus status) => status is
        CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect or CoreWebView2WebErrorStatus.CertificateExpired or
        CoreWebView2WebErrorStatus.ClientCertificateContainsErrors or CoreWebView2WebErrorStatus.CertificateRevoked or
        CoreWebView2WebErrorStatus.CertificateIsInvalid;

    private void Block()
    {
        if (IsBlocked) return;
        IsBlocked = true;
        onBlocked();
    }
}
