using Fluens.AppCore.Contracts;
using Fluens.AppCore.Enums;
using Fluens.AppCore.Helpers;
using Fluens.UI.Services.AdBlocking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Web.WebView2.Core;
using System.Reactive.Subjects;
using Xunit;

namespace Fluens.UI.Tests.AdBlocking;

public class AdBlockServiceTests
{
    [Fact]
    public async Task WhenUsingRealEasyListThenKnownAdUrlIsBlocked()
    {
        TestLocalSettingService settings = new(new AdBlockSettings(true, AdBlockListSelection.EasyList));
        string listRules = await DownloadRealListRulesAsync(AdBlockListSelection.EasyList);
        Uri blockedUri = BuildUriFromFirstHostBlockRule(listRules);

        AdBlockService service = CreateService(
            settings,
            listRules,
            out _);

        await service.InitializeAsync();

        bool shouldBlock = service.ShouldBlock(
            blockedUri,
            CoreWebView2WebResourceContext.Script);

        Assert.True(shouldBlock);
    }

    [Fact]
    public async Task WhenInitializedWithBlockingRuleThenShouldBlockReturnsTrueForMatchingRequest()
    {
        TestLocalSettingService settings = new(new AdBlockSettings(true, AdBlockListSelection.EasyList));

        AdBlockService service = CreateService(
            settings,
            "||ads.example.com^",
            out Func<int> providerCalls);

        await service.InitializeAsync();

        bool shouldBlock = service.ShouldBlock(
            new Uri("https://ads.example.com/banner.js"),
            CoreWebView2WebResourceContext.Script);

        Assert.True(shouldBlock);
        Assert.Equal(1, providerCalls());
    }

    [Fact]
    public async Task WhenRuleMatchesDifferentResourceTypeThenShouldBlockReturnsFalse()
    {
        TestLocalSettingService settings = new(new AdBlockSettings(true, AdBlockListSelection.EasyList));

        AdBlockService service = CreateService(
            settings,
            "||ads.example.com^$image",
            out _);

        await service.InitializeAsync();

        bool shouldBlock = service.ShouldBlock(
            new Uri("https://ads.example.com/banner.js"),
            CoreWebView2WebResourceContext.Script);

        Assert.False(shouldBlock);
    }

    [Fact]
    public async Task WhenAdBlockIsDisabledThenShouldBlockAlwaysReturnsFalse()
    {
        TestLocalSettingService settings = new(new AdBlockSettings(false, AdBlockListSelection.EasyList));

        AdBlockService service = CreateService(
            settings,
            "||ads.example.com^",
            out _);

        await service.InitializeAsync();

        bool shouldBlock = service.ShouldBlock(
            new Uri("https://ads.example.com/banner.js"),
            CoreWebView2WebResourceContext.Script);

        Assert.False(shouldBlock);
    }

    [Fact]
    public async Task WhenSettingsChangeToEnabledThenServiceReloadsAndBlocksMatchingRequest()
    {
        TestLocalSettingService settings = new(new AdBlockSettings(false, AdBlockListSelection.EasyList));

        AdBlockService service = CreateService(
            settings,
            "||ads.example.com^",
            out _);

        await service.InitializeAsync();

        settings.Publish(new AdBlockSettings(true, AdBlockListSelection.EasyList));
        await Task.Delay(100);

        bool shouldBlock = service.ShouldBlock(
            new Uri("https://ads.example.com/banner.js"),
            CoreWebView2WebResourceContext.Script);

        Assert.True(shouldBlock);
    }

    [Fact]
    public async Task WhenInitializeIsCalledMultipleTimesThenRulesProviderIsCalledOnlyOnce()
    {
        TestLocalSettingService settings = new(new AdBlockSettings(true, AdBlockListSelection.EasyList));

        AdBlockService service = CreateService(
            settings,
            "||ads.example.com^",
            out Func<int> providerCalls);

        await service.InitializeAsync();
        await service.InitializeAsync();

        Assert.Equal(1, providerCalls());
    }

    private static AdBlockService CreateService(
        TestLocalSettingService settings,
        string rules,
        out Func<int> providerCalls)
    {
        Counter counter = new();

        AdBlockService service = new(
            settings,
            NullLogger<AdBlockService>.Instance,
            (_, _) =>
            {
                counter.Increment();
                return Task.FromResult(rules);
            });

        providerCalls = counter.Get;
        return service;
    }

    private static async Task<string> DownloadRealListRulesAsync(AdBlockListSelection selectedLists)
    {
        IReadOnlyList<AdBlockListDefinition> selectedDefinitions = AdBlockListCatalog.GetSelected(selectedLists);

        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        List<string> contents = [];

        foreach (AdBlockListDefinition definition in selectedDefinitions)
        {
            string content = await httpClient.GetStringAsync(definition.SourceUrl);
            contents.Add(content);
        }

        return string.Join(Environment.NewLine, contents);
    }

    private static Uri BuildUriFromFirstHostBlockRule(string rules)
    {
        AdBlockRule hostRule = AdBlockRuleParser.Parse(rules)
            .First(rule => !rule.IsException
                && rule.MatchMode == AdBlockRuleMatchMode.Host
                && !string.IsNullOrWhiteSpace(rule.Host));

        return new Uri($"https://{hostRule.Host}/probe.js");
    }

    private sealed class Counter
    {
        private int value;

        public void Increment()
        {
            value++;
        }

        public int Get()
        {
            return value;
        }
    }

    private sealed class TestLocalSettingService : ILocalSettingService
    {
        private readonly BehaviorSubject<AdBlockSettings> adBlockSettingsSubject;

        public TestLocalSettingService(AdBlockSettings initialSettings)
        {
            adBlockSettingsSubject = new BehaviorSubject<AdBlockSettings>(initialSettings);
        }

        public IObservable<OnStartupSetting> OnStartupSettingChanges => throw new NotSupportedException();
        public IObservable<string> AccentColorChanges => throw new NotSupportedException();
        public IObservable<AdBlockSettings> AdBlockSettingsChanges => adBlockSettingsSubject;

        public OnStartupSetting CurrentOnStartupSetting => OnStartupSetting.OpenNewTab;
        public string CurrentAccentColor => "#000000";
        public AdBlockSettings CurrentAdBlockSettings => adBlockSettingsSubject.Value;

        public void Publish(AdBlockSettings settings)
        {
            adBlockSettingsSubject.OnNext(settings);
        }

        public void SetStartupConfig(OnStartupSetting onStartupSetting)
        {
            throw new NotSupportedException();
        }

        public void SetAccentColor(string accentColor)
        {
            throw new NotSupportedException();
        }

        public void SetAdBlockEnabled(bool isEnabled)
        {
            AdBlockSettings next = adBlockSettingsSubject.Value with { IsEnabled = isEnabled };
            adBlockSettingsSubject.OnNext(next);
        }

        public void SetSelectedAdBlockLists(AdBlockListSelection selectedLists)
        {
            AdBlockSettings next = adBlockSettingsSubject.Value with { SelectedLists = selectedLists };
            adBlockSettingsSubject.OnNext(next);
        }

        public void Dispose()
        {
            adBlockSettingsSubject.Dispose();
        }
    }
}
