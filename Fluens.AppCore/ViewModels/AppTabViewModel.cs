using Fluens.AppCore.Contracts;
using Fluens.AppCore.Helpers;
using Fluens.AppCore.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Fluens.AppCore.ViewModels;

public partial class AppTabViewModel : ReactiveObject, IDisposable
{
    private const string HttpsPrefix = "https://";
    private const string HttpPrefix = "http://";

    public IObservable<ShortcutMessage> KeyboardShortcuts => KeyboardShortcutsSource.AsObservable();
    private Subject<ShortcutMessage> KeyboardShortcutsSource { get; } = new();
    private CompositeDisposable Subscriptions { get; } = [];
    private SerialDisposable WebViewSubscriptions { get; } = new();
    private int? CurrentPlaceId { get; set; }

    [Reactive]
    public partial int Id { get; set; }

    [Reactive]
    public partial IObservableWebView? ObservableWebView { get; set; }

    [Reactive]
    public partial string DocumentTitle { get; set; } = string.Empty;

    [Reactive]
    public partial string FaviconUrl { get; set; } = string.Empty;

    [Reactive]
    public partial bool IsLoading { get; set; }

    [Reactive]
    public partial bool CanStop { get; set; }

    [Reactive]
    public partial bool CanRefresh { get; set; }

    [Reactive]
    public partial int? Index { get; set; }

    [Reactive]
    public partial bool IsSelected { get; set; }

    [Reactive]
    public partial Uri Url { get; set; } = Constants.AboutBlankUri;

    [Reactive]
    public partial string SearchBarText { get; set; } = string.Empty;

    [Reactive]
    public partial bool SettingsDialogIsOpen { get; set; }

    [Reactive]
    public partial int ParentWindowId { get; set; }

    public ReactiveCommand<Unit, Unit> Refresh { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> GoBack { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> GoForward { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> Stop { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ToggleSettingsDialogCommand { get; private set; } = null!;

    private TabPersistencyService TabPersistencyService { get; } = ServiceLocator.GetRequiredService<TabPersistencyService>();
    private VisitsService VisitsService { get; } = ServiceLocator.GetRequiredService<VisitsService>();
    private PlacesService PlacesService { get; } = ServiceLocator.GetRequiredService<PlacesService>();
    private ITabPageManager TabPageManager { get; } = ServiceLocator.GetRequiredService<ITabPageManager>();

    public AppTabViewModel()
    {
        GoBack = ReactiveCommand.Create(() => ObservableWebView?.GoBack());
        GoForward = ReactiveCommand.Create(() => ObservableWebView?.GoForward());
        Refresh = ReactiveCommand.Create(() => ObservableWebView?.Refresh());
        Stop = ReactiveCommand.Create(() => ObservableWebView?.StopNavigation());
        ToggleSettingsDialogCommand = ReactiveCommand.Create(() => { SettingsDialogIsOpen = !SettingsDialogIsOpen; });

        this.WhenAnyValue(x => x.Index, x => x.Id, (index, id) => new { index, id })
            .Where(i => i.index != null && i.id > 0)
            .SelectMany(i => Observable.FromAsync(() => TabPersistencyService.UpdateTabInfoAsync(i.id, index: i.index)))
            .Subscribe()
            .DisposeWith(Subscriptions);

        this.WhenAnyValue(x => x.ParentWindowId, x => x.Id, (parentWindowId, id) => new { parentWindowId, id })
            .Where(i => i.id > 0 && i.parentWindowId > 0)
            .SelectMany(i => Observable.FromAsync(() => TabPersistencyService.UpdateTabInfoAsync(i.id, windowId: i.parentWindowId)))
            .Subscribe()
            .DisposeWith(Subscriptions);

        this.WhenAnyValue(x => x.IsSelected, x => x.Id, (IsSelected, id) => new { IsSelected, id })
            .Where(i => i.id > 0)
            .SelectMany(i => Observable.FromAsync(() => TabPersistencyService.UpdateTabInfoAsync(i.id, isSelected: i.IsSelected)))
            .Subscribe()
            .DisposeWith(Subscriptions);

        this.WhenAnyValue(x => x.Url)
            .WhereNotNull()
            .Subscribe(_ => UpdateSearchBar())
            .DisposeWith(Subscriptions);

        this.WhenAnyValue(x => x.ObservableWebView)
            .WhereNotNull()
            .Subscribe(BindWebView);

        this.WhenAnyValue(x => x.IsSelected, x => x.ObservableWebView, x => x.Url, (isSelected, web, url) => isSelected && web != null && url != Constants.AboutBlankUri)
            .DistinctUntilChanged()
            .Where(ready => ready)
            .Subscribe(_ => Activate())
            .DisposeWith(Subscriptions);
    }

    private void BindWebView(IObservableWebView webView)
    {
        CompositeDisposable webViewBindings = [];

        webViewBindings.Add(webView.IsNavigating
            .Subscribe(SetStopRefreshVisibility));

        webViewBindings.Add(webView.Url
            .Subscribe(url => Url = url));

        webViewBindings.Add(webView.Url
            .Where(url => url != Constants.AboutBlankUri && Id > 0)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(url => Observable.FromAsync(ct => PersistNavigationAsync(url, ct)))
            .Subscribe());

        webViewBindings.Add(webView.DocumentTitle
            .Subscribe(documentTitle => DocumentTitle = documentTitle));

        webViewBindings.Add(this.WhenAnyValue(x => x.DocumentTitle)
            .DistinctUntilChanged()
            .Where(title => !string.IsNullOrWhiteSpace(title) && title != Constants.NewTabTitle && CurrentPlaceId is not null)
            .SelectMany(title => Observable.FromAsync(() => PlacesService.UpdatePlaceAsync(CurrentPlaceId!.Value, title: title)))
            .Subscribe());

        webViewBindings.Add(webView.FaviconUrl
            .Subscribe(faviconUrl => FaviconUrl = faviconUrl));

        webViewBindings.Add(this.WhenAnyValue(x => x.FaviconUrl)
            .DistinctUntilChanged()
            .Where(faviconUrl => !string.IsNullOrWhiteSpace(faviconUrl) && faviconUrl != Constants.LoadingFaviconUri && CurrentPlaceId is not null)
            .SelectMany(faviconUrl => Observable.FromAsync(() => PlacesService.UpdatePlaceAsync(CurrentPlaceId!.Value, faviconUrl: faviconUrl)))
            .Subscribe());

        webViewBindings.Add(webView.OpenNewTab
            .SelectMany(newTabRequest => Observable.FromAsync(async () =>
            {
                IViewFor<AppPageViewModel> page = TabPageManager.GetParentTabPage(this);
                AppTabViewModel vm = await page.ViewModel!.CreateTabAsync(newTabRequest.Url);
                page.ViewModel.CreateTabViewItem(vm);

                if (newTabRequest.ShouldNavigate)
                {
                    page.ViewModel.SelectItem(vm);
                }

                await vm.ActivateAsync();
            }))
            .Subscribe());

        webViewBindings.Add(webView.KeyboardShortcuts
            .Subscribe(KeyboardShortcutsSource.OnNext));

        WebViewSubscriptions.Disposable = webViewBindings;
    }

    public void ShortcutMessageInvoked(ShortcutMessage shortcutMessage)
    {
        KeyboardShortcutsSource.OnNext(shortcutMessage);
    }

    [ReactiveCommand]
    private async Task NavigateToInput()
    {
        // Normalize input
        string search = (SearchBarText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(search))
        {
            return;
        }

        Uri url = BuildNavigationUri(search);

        await (ObservableWebView?.NavigateToUrlAsync(url) ?? Task.CompletedTask);
    }

    public void Activate()
    {
        _ = ActivateAsync();
    }

    public async Task ActivateAsync()
    {
        IObservableWebView? webView = ObservableWebView;

        if (webView is null)
        {
            return;
        }

        await webView.ActivateAsync();

        if (Url == Constants.AboutBlankUri && webView.Source is null)
        {
            return;
        }

        if (Url != webView.Source)
        {
            await webView.NavigateToUrlAsync(Url);
        }
    }

    private static Uri BuildNavigationUri(string search)
    {
        bool containsDot = search.Contains('.', StringComparison.Ordinal);
        bool startsOrEndsWithDot = search.StartsWith('.') || search.EndsWith('.');
        bool hasScheme = search.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase)
                      || search.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase);

        // Follow original logic: only use the raw input as a URL if it contains a dot, does not start/end with a dot,
        // and already begins with http(s). Otherwise prepend https://

        string candidate;

        if (containsDot && !startsOrEndsWithDot && !hasScheme)
        {
            candidate = HttpsPrefix + search;
        }
        else
        {
            candidate = search;
        }

        // Try to make an absolute Uri; if that fails, fall back to a search query (DuckDuckGo)
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? url))
        {
            string query = Uri.EscapeDataString(search);
            url = new Uri($"https://duckduckgo.com/?q={query}");
        }

        return url;
    }

    private async Task PersistNavigationAsync(Uri url, CancellationToken cancellationToken)
    {
        int placeId = await PlacesService.GetOrCreatePlaceAsync(url, cancellationToken);
        CurrentPlaceId = placeId;

        await VisitsService.AddEntryAsync(placeId, cancellationToken);
        await TabPersistencyService.UpdateTabInfoAsync(Id, placeId: placeId, cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(FaviconUrl) && FaviconUrl != Constants.LoadingFaviconUri)
        {
            await PlacesService.UpdatePlaceAsync(placeId, faviconUrl: FaviconUrl, cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(DocumentTitle) && DocumentTitle != Constants.NewTabTitle)
        {
            await PlacesService.UpdatePlaceAsync(placeId, title: DocumentTitle, cancellationToken: cancellationToken);
        }
    }

    private void UpdateSearchBar()
    {
        string text = Url.ToString();

        SearchBarText = text.Equals(Constants.AboutBlankUri.ToString(), StringComparison.Ordinal)
            ? string.Empty
            : text;
    }

    private void SetStopRefreshVisibility(bool showStopBtn)
    {
        if (showStopBtn)
        {
            CanStop = true;
            CanRefresh = false;
        }
        else
        {
            CanStop = false;
            CanRefresh = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool dispose)
    {
        if (dispose)
        {
            WebViewSubscriptions.Dispose();
            Subscriptions.Dispose();
            KeyboardShortcutsSource.OnCompleted();
            KeyboardShortcutsSource.Dispose();
            GoBack.Dispose();
            GoForward.Dispose();
            Refresh.Dispose();
            Stop.Dispose();
            ToggleSettingsDialogCommand.Dispose();
            ObservableWebView?.Dispose();
        }
    }
}
