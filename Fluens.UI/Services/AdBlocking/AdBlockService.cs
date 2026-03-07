using Fluens.AppCore.Contracts;
using Fluens.AppCore.Enums;
using Fluens.AppCore.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Fluens.UI.Services.AdBlocking;

internal sealed partial class AdBlockService : IAdBlockService, IDisposable
{
    private static readonly Action<ILogger, Exception?> LogReloadAfterSettingsChangeFailedDelegate =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1001, nameof(LogReloadAfterSettingsChangeFailed)),
            "Failed to update adblock snapshot after settings change.");

    private static readonly Action<ILogger, Exception?> LogAdBlockDisabledDelegate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1002, nameof(LogAdBlockDisabled)),
            "Adblock disabled; snapshot cleared.");

    private static readonly Action<ILogger, int, int, Exception?> LogAdBlockSnapshotReloadedDelegate =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1003, nameof(LogAdBlockSnapshotReloaded)),
            "Adblock snapshot reloaded: {BlockCount} block rules, {ExceptionCount} exception rules.");

    private readonly ILocalSettingService LocalSettingService;
    private readonly Func<AdBlockListSelection, CancellationToken, Task<string>> GetSelectedRulesAsync;
    private readonly ILogger<AdBlockService> Logger;
    private readonly SemaphoreSlim ReloadGate = new(1, 1);
    private readonly CompositeDisposable Subscriptions = [];
    private readonly Lazy<Task> EnsureInitializedTask;

    private AdBlockSnapshot Snapshot = AdBlockSnapshot.Empty;
    private AdBlockSettings CurrentSettings;

    public AdBlockService(
        ILocalSettingService localSettingService,
        AdBlockListProvider adBlockListProvider,
        ILogger<AdBlockService> logger)
        : this(localSettingService, logger, adBlockListProvider.GetSelectedRulesAsync)
    {
    }

    internal AdBlockService(
        ILocalSettingService localSettingService,
        ILogger<AdBlockService> logger,
        Func<AdBlockListSelection, CancellationToken, Task<string>> getSelectedRulesAsync)
    {
        ArgumentNullException.ThrowIfNull(localSettingService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(getSelectedRulesAsync);

        LocalSettingService = localSettingService;
        GetSelectedRulesAsync = getSelectedRulesAsync;
        Logger = logger;
        CurrentSettings = localSettingService.CurrentAdBlockSettings;
        EnsureInitializedTask = new(() => ReloadRulesAsync(CurrentSettings, CancellationToken.None));

        IDisposable settingsSubscription = LocalSettingService.AdBlockSettingsChanges
            .DistinctUntilChanged()
            .Skip(1)
            .Subscribe(OnSettingsChanged);

        Subscriptions.Add(settingsSubscription);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken == default)
        {
            return EnsureInitializedTask.Value;
        }

        return EnsureInitializedTask.Value.WaitAsync(cancellationToken);
    }

    public bool ShouldBlock(Uri requestUri, CoreWebView2WebResourceContext resourceContext)
    {
        ArgumentNullException.ThrowIfNull(requestUri);

        if (!CurrentSettings.IsEnabled || !IsSupportedScheme(requestUri))
        {
            return false;
        }

        AdBlockResourceType resourceType = MapResourceType(resourceContext);

        return Snapshot.ShouldBlock(requestUri, resourceType);
    }

    private void OnSettingsChanged(AdBlockSettings settings)
    {
        CurrentSettings = settings;

        Task reloadTask = ReloadRulesAsync(settings, CancellationToken.None);
        reloadTask.ContinueWith(
            t => LogReloadAfterSettingsChangeFailed(Logger, t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task ReloadRulesAsync(AdBlockSettings settings, CancellationToken cancellationToken)
    {
        await ReloadGate.WaitAsync(cancellationToken);

        try
        {
            if (!settings.IsEnabled)
            {
                Snapshot = AdBlockSnapshot.Empty;
                LogAdBlockDisabled(Logger);
                return;
            }

            string selectedRules = await GetSelectedRulesAsync(settings.SelectedLists, cancellationToken);
            IReadOnlyList<AdBlockRule> parsedRules = AdBlockRuleParser.Parse(selectedRules);

            ImmutableArray<AdBlockRule> blockRules = [.. parsedRules.Where(rule => !rule.IsException)];
            ImmutableArray<AdBlockRule> exceptionRules = [.. parsedRules.Where(rule => rule.IsException)];

            Snapshot = new AdBlockSnapshot(blockRules, exceptionRules);

            if (Logger.IsEnabled(LogLevel.Information))
            {
                LogAdBlockSnapshotReloaded(Logger, Snapshot.BlockRuleCount, Snapshot.ExceptionRuleCount);
            }
        }
        finally
        {
            ReloadGate.Release();
        }
    }

    private static bool IsSupportedScheme(Uri requestUri)
    {
        return requestUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || requestUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static AdBlockResourceType MapResourceType(CoreWebView2WebResourceContext resourceContext)
    {
        return resourceContext switch
        {
            CoreWebView2WebResourceContext.Script => AdBlockResourceType.Script,
            CoreWebView2WebResourceContext.Image => AdBlockResourceType.Image,
            CoreWebView2WebResourceContext.Stylesheet => AdBlockResourceType.StyleSheet,
            CoreWebView2WebResourceContext.XmlHttpRequest => AdBlockResourceType.XmlHttpRequest,
            CoreWebView2WebResourceContext.Media => AdBlockResourceType.Media,
            CoreWebView2WebResourceContext.Font => AdBlockResourceType.Font,
            CoreWebView2WebResourceContext.Websocket => AdBlockResourceType.WebSocket,
            CoreWebView2WebResourceContext.Fetch => AdBlockResourceType.Fetch,
            CoreWebView2WebResourceContext.Document => AdBlockResourceType.Document,
            _ => AdBlockResourceType.Other
        };
    }

    public void Dispose()
    {
        Subscriptions.Dispose();
        ReloadGate.Dispose();
    }

    private static void LogReloadAfterSettingsChangeFailed(ILogger logger, Exception? exception)
    {
        LogReloadAfterSettingsChangeFailedDelegate(logger, exception);
    }

    private static void LogAdBlockDisabled(ILogger logger)
    {
        LogAdBlockDisabledDelegate(logger, null);
    }

    private static void LogAdBlockSnapshotReloaded(ILogger logger, int blockCount, int exceptionCount)
    {
        LogAdBlockSnapshotReloadedDelegate(logger, blockCount, exceptionCount, null);
    }
}
