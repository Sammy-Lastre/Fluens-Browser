using Fluens.AppCore.Contracts;
using Fluens.AppCore.Helpers;
using Fluens.StaticPages;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace Fluens.UI.Helpers;

public sealed partial class ObservableWebView : IObservableWebView
{
    private readonly WebView2 WebView;
    private readonly StaticPagesHost StaticPagesHost;
    private readonly Subject<bool> IsNavigatingSource = new();
    public IObservable<bool> IsNavigating => IsNavigatingSource.AsObservable();
    private readonly Subject<string> DocumentTitleSource = new();
    public IObservable<string> DocumentTitle => DocumentTitleSource.AsObservable();
    private readonly Subject<string> FaviconUrlSource = new();
    public IObservable<string> FaviconUrl => FaviconUrlSource.AsObservable();
    private readonly Subject<Uri> UrlSource = new();
    public IObservable<Uri> Url => UrlSource.AsObservable();
    private readonly Subject<NewTabRequest> OpenNewTabSource = new();
    public IObservable<NewTabRequest> OpenNewTab => OpenNewTabSource.AsObservable();
    private readonly Subject<ShortcutMessage> KeyboardShortcutsSource = new();
    public IObservable<ShortcutMessage> KeyboardShortcuts => KeyboardShortcutsSource.AsObservable();

    private bool IsInitialized { get; set; }
    private bool IsDisposed { get; set; }

    public Uri? Source => WebView.Source;

    private readonly Lazy<Task> EnsureInitializedCoreWebView2Async;

    public ObservableWebView(WebView2 webView)
    {
        ArgumentNullException.ThrowIfNull(webView);

        WebView = webView;
        StaticPagesHost = ServiceLocator.GetRequiredService<StaticPagesHost>();
        EnsureInitializedCoreWebView2Async = new(EnsureCoreWebView2Async);
    }

    private async Task AddPageListenersAsync()
    {
        string script = """
if (!window.__fluensListenersRegistered) {
  window.__fluensListenersRegistered = true;

  document.addEventListener('click', function (e) {
    if (e.defaultPrevented || e.button !== 0) {
      return;
    }

    const anchor = e.target?.closest?.('a[target="_blank"]');
    if (!anchor || !anchor.href) {
      return;
    }

    e.preventDefault();
    window.chrome.webview.postMessage({ type: 'openNewTab', url: anchor.href, shouldActivate: true });
  }, true);

window.addEventListener('keydown', function (e) {
  const combo = `${e.code}|ctrl:${e.ctrlKey }|shift:${e.shiftKey}`;
  switch (combo) {
    case 'KeyT|ctrl:true|shift:true':
      e.preventDefault();
      window.chrome.webview.postMessage({ key: 'T', ctrl: true, shift: true });
      break;

    case 'KeyT|ctrl:true|shift:false':
      e.preventDefault();
      window.chrome.webview.postMessage({ key: 'T', ctrl: true, shift: false });
      break;

    case 'KeyW|ctrl:true|shift:false':
    case 'KeyW|ctrl:true|shift:true':
      e.preventDefault();
      window.chrome.webview.postMessage({ key: 'W', ctrl: true, shift: e.shiftKey });
      break;
  }
});
}
""";

        await WebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    //TODO: this stop doesn't work on SPA, i.e. Youtube, it doens't stops scripts
    //https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.stop?view=webview2-dotnet-1.0.3351.48#remarks
    public void StopNavigation()
    {
        WebView.CoreWebView2?.Stop();
    }

    public void Refresh()
    {
        WebView.CoreWebView2?.Reload();
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        if (WebView.CoreWebView2 is not null)
        {
            DetachCoreEvents(WebView.CoreWebView2);
        }

        IsNavigatingSource.OnCompleted();
        DocumentTitleSource.OnCompleted();
        FaviconUrlSource.OnCompleted();
        UrlSource.OnCompleted();
        OpenNewTabSource.OnCompleted();
        KeyboardShortcutsSource.OnCompleted();

        IsNavigatingSource.Dispose();
        DocumentTitleSource.Dispose();
        FaviconUrlSource.Dispose();
        UrlSource.Dispose();
        OpenNewTabSource.Dispose();
        KeyboardShortcutsSource.Dispose();
    }

    public async Task NavigateToUrlAsync(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url == Constants.AboutBlankUri && WebView.Source is null)
        {
            return;
        }

        if (url == Constants.SettingsUri && StaticPagesHost.IsHostedSettingsUri(WebView.Source))
        {
            return;
        }

        if (url == WebView.Source)
        {
            return;
        }

        await EnsureInitializedCoreWebView2Async.Value;

        WebView.Source = url;
    }

    public void GoBack()
    {
        if (WebView.CanGoBack)
        {
            WebView.GoBack();
        }
    }

    public void GoForward()
    {
        if (WebView.CanGoForward)
        {
            WebView.GoForward();
        }
    }

    public async Task ActivateAsync()
    {
        await EnsureInitializedCoreWebView2Async.Value;
    }

    private async Task EnsureCoreWebView2Async()
    {
        await WebView.EnsureCoreWebView2Async();

        if (IsInitialized)
        {
            return;
        }

        IsInitialized = true;
        AttachCoreEvents(WebView.CoreWebView2);
        await AddPageListenersAsync();
    }

    private void AttachCoreEvents(CoreWebView2 coreWebView)
    {
        coreWebView.NavigationStarting += OnNavigationStarting;
        coreWebView.NavigationCompleted += OnNavigationCompleted;
        coreWebView.DocumentTitleChanged += OnDocumentTitleChanged;
        coreWebView.FaviconChanged += OnFaviconChanged;
        //coreWebView.HistoryChanged += OnHistoryChanged;
        coreWebView.NewWindowRequested += OnNewWindowRequested;
        coreWebView.WebMessageReceived += OnWebMessageReceived;
    }

    private void DetachCoreEvents(CoreWebView2 coreWebView)
    {
        coreWebView.NavigationStarting -= OnNavigationStarting;
        coreWebView.NavigationCompleted -= OnNavigationCompleted;
        coreWebView.DocumentTitleChanged -= OnDocumentTitleChanged;
        coreWebView.FaviconChanged -= OnFaviconChanged;
        //coreWebView.HistoryChanged -= OnHistoryChanged;
        coreWebView.NewWindowRequested -= OnNewWindowRequested;
        coreWebView.WebMessageReceived -= OnWebMessageReceived;
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        IsNavigatingSource.OnNext(true);

        if (IsSettingsUri(args.Uri))
        {
            _ = NavigateToSettingsPageAsync();
            return;
        }
        FaviconUrlSource.OnNext(Constants.LoadingFaviconUri);
    }

    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        IsNavigatingSource.OnNext(false);
        await AddPageListenersAsync();

        if (!args.IsSuccess)
        {
            return;
        }

        if (StaticPagesHost.IsHostedSettingsUri(WebView.Source))
        {
            UrlSource.OnNext(Constants.SettingsUri);
            // FaviconUrlSource.OnNext(SettingsIcon);
        }
        else
        {
            UrlSource.OnNext(WebView.Source);
        }

        FaviconUrlSource.OnNext(sender.FaviconUri);
    }

    private void OnDocumentTitleChanged(CoreWebView2 sender, object args)
    {
        if (StaticPagesHost.IsHostedSettingsUri(WebView.Source))
        {
            DocumentTitleSource.OnNext(Constants.SettingsTitle);
            return;
        }
        DocumentTitleSource.OnNext(sender.DocumentTitle);
    }

    private void OnFaviconChanged(CoreWebView2 sender, object args)
    {
        if (!string.IsNullOrWhiteSpace(sender.FaviconUri))
        {
            FaviconUrlSource.OnNext(sender.FaviconUri);
        }
    }

    //private void OnHistoryChanged(CoreWebView2 sender, object args)
    //{
    //    if (!Uri.TryCreate(sender.Source, UriKind.Absolute, out Uri? uri))
    //    {
    //        return;
    //    }

    //    if (uri == Constants.AboutBlankUri)
    //    {
    //        return;
    //    }

    //    if (StaticPagesHost.IsHostedSettingsUri(uri))
    //    {
    //        return;
    //    }

    //    UrlSource.OnNext(uri);
    //}

    private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;

        if (Uri.TryCreate(args.Uri, UriKind.Absolute, out Uri? uri))
        {
            OpenNewTabSource.OnNext(new NewTabRequest(uri, ShouldNavigate: false));
        }
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(args.WebMessageAsJson);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("type", out JsonElement typeElement)
                && string.Equals(typeElement.GetString(), "openNewTab", StringComparison.Ordinal)
                && root.TryGetProperty("url", out JsonElement urlElement)
                && Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out Uri? url))
            {
                bool shouldActivate = root.TryGetProperty("shouldActivate", out JsonElement activateElement)
                    && activateElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                    && activateElement.GetBoolean();

                OpenNewTabSource.OnNext(new NewTabRequest(url, shouldActivate));
                return;
            }

            ShortcutMessage? message = JsonSerializer.Deserialize<ShortcutMessage>(args.WebMessageAsJson);

            if (message is not null)
            {
                KeyboardShortcutsSource.OnNext(message);
            }
        }
        catch (JsonException)
        {
            Debug.WriteLine($"Failed to deserialize shortcut message: {args.WebMessageAsJson}");
        }
    }

    private static bool IsSettingsUri(string? rawUri)
    {
        if (!Uri.TryCreate(rawUri, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        bool isFluensSettings = uri.Scheme.Equals("fluens", StringComparison.OrdinalIgnoreCase)
            && uri.Host.Equals("settings", StringComparison.OrdinalIgnoreCase);

        bool isChromeSettings = uri.Scheme.Equals("chrome", StringComparison.OrdinalIgnoreCase)
            && uri.Host.Equals("settings", StringComparison.OrdinalIgnoreCase);

        return isFluensSettings || isChromeSettings;
    }

    private async Task NavigateToSettingsPageAsync()
    {
        await EnsureInitializedCoreWebView2Async.Value;
        Uri settingsUri = await StaticPagesHost.GetSettingsUriAsync();

        WebView.Source = settingsUri;

        UrlSource.OnNext(Constants.SettingsUri);
    }
}
