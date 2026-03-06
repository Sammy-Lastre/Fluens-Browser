using DynamicData;
using Fluens.AppCore.Contracts;
using Fluens.AppCore.Enums;
using Fluens.AppCore.Helpers;
using Fluens.AppCore.Services;
using Fluens.Data.Entities;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Fluens.AppCore.ViewModels;

public partial class AppPageViewModel : ReactiveObject, IDisposable
{
    public IObservableTabView TabView { get; }
    public IObservable<Unit> HasNoTabs => hasNoTabs.AsObservable();
    public int WindowId { get; set; }
    private CompositeDisposable Subscriptions { get; } = [];

    public AppPageViewModel(IObservableTabView tabView)
    {
        TabView = tabView;

        TabView.CollectionEmptied
            .Subscribe(_ => hasNoTabs.OnNext(Unit.Default))
            .DisposeWith(Subscriptions);

        TabView.TabCloseRequested
            .SelectMany(vm => Observable.FromAsync(() => CloseTabAsync(vm)))
            .Subscribe()
            .DisposeWith(Subscriptions);

        TabView.AddTabButtonClick
            .SelectMany(_ => Observable.FromAsync(CreateNewTabAsync))
            .Subscribe()
            .DisposeWith(Subscriptions);

        TabView.Items
            .Subscribe(UpdateTabIndexes)
            .DisposeWith(Subscriptions);

        TabView.Items
            .Select(items => items.Select(vm => vm.KeyboardShortcuts).Merge())
            .Switch()
            .SelectMany(shortcut => Observable.FromAsync(() => HandleKeyboardShortcutAsync(shortcut)))
            .Subscribe()
            .DisposeWith(Subscriptions);

        TabView.SelectedItem
            .WhereNotNull()
            .WithLatestFrom(TabView.Items, (selectedItem, items) => (selectedItem, items))
            .Subscribe(static t => SetSelectedTab(t.selectedItem, t.items))
            .DisposeWith(Subscriptions);
    }

    private readonly TabPersistencyService TabPersistencyService = ServiceLocator.GetRequiredService<TabPersistencyService>();

    private readonly Subject<Unit> hasNoTabs = new();

    public async Task ApplyOnStartupSettingAsync(OnStartupSetting onStartupSetting)
    {
        switch (onStartupSetting)
        {
            case OnStartupSetting.OpenNewTab:
                await CreateNewTabAsync();
                break;
            case OnStartupSetting.RestoreOpenTabs:
                await RecoverStateAsync();
                break;
            //TODO
            //case OnStartupSetting.OpenSpecificTabs:
            //    break;
            case OnStartupSetting.RestoreAndOpenNewTab:
                await RecoverStateAsync();
                await CreateNewTabAsync();
                break;
            default:
                await CreateNewTabAsync();
                break;
        }

        ReadOnlyCollection<AppTabViewModel> items = await TabView.Items.Take(1);

        if (items.Count == 0)
        {
            await CreateNewTabAsync();
        }

    }

    public async Task CloseTabAsync(AppTabViewModel vm)
    {
        TabView.RemoveItem(vm);
        await TabPersistencyService.CloseTabAsync(vm.Id);
        vm.Dispose();
    }

    public bool HasTab(AppTabViewModel tab)
    {
        return TabView.IndexOf(tab) != -1;
    }

    public async Task<AppTabViewModel> CreateTabAsync(Uri? uri = null)
    {
        int id = await TabPersistencyService.CreateTabAsync(WindowId);

        AppTabViewModel vm = new()
        {
            Id = id,
            Url = uri ?? Constants.AboutBlankUri,
            ParentWindowId = WindowId,
            DocumentTitle = Constants.NewTabTitle,
            FaviconUrl = string.Empty
        };

        return vm;
    }

    public async Task CreateNewTabAsync()
    {
        AppTabViewModel vm = await CreateTabAsync();
        TabView.CreateTabViewItem(vm);
        TabView.SelectItem(vm);
    }

    public void CreateTabViewItem(AppTabViewModel vm)
    {
        TabView.CreateTabViewItem(vm);
    }

    public void SelectItem(AppTabViewModel vm)
    {
        TabView.SelectItem(vm);
    }

    public async Task HandleKeyboardShortcutAsync(ShortcutMessage message)
    {
        switch (message)
        {
            case { Ctrl: true, Shift: true, Key: "T" }:
                await RestoreClosedTabAsync();
                break;
            case { Ctrl: true, Key: "T" }:
                await CreateNewTabAsync();
                break;
            case { Ctrl: true, Key: "W" }:
                AppTabViewModel selectedForClose = await TabView.SelectedItem.WhereNotNull().Take(1);
                await CloseTabAsync(selectedForClose);
                break;
            case { Key: "F5" }:
                AppTabViewModel selectedForRefresh = await TabView.SelectedItem.WhereNotNull().Take(1);
                selectedForRefresh.Refresh.Execute().Subscribe();
                break;
        }
    }

    private void UpdateTabIndexes(ReadOnlyCollection<AppTabViewModel> items)
    {
        foreach (AppTabViewModel tab in items)
        {
            tab.Index = TabView.IndexOf(tab);
        }
    }

    private static void SetSelectedTab(AppTabViewModel selectedItem, ReadOnlyCollection<AppTabViewModel> items)
    {
        foreach (AppTabViewModel item in items.Except([selectedItem]))
        {
            item.IsSelected = false;
        }

        selectedItem.IsSelected = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private async Task RecoverStateAsync()
    {
        Tab[] tabs = await TabPersistencyService.RecoverTabsAsync();

        foreach (Tab tab in tabs)
        {
            AppTabViewModel vm = tab.ToAppTabViewModel();
            vm.ParentWindowId = WindowId;
            TabView.CreateTabViewItem(vm);
        }

        ReadOnlyCollection<AppTabViewModel> items = await TabView.Items.Take(1);

        if (items.Count > 0)
        {
            TabView.SelectItem(items.FirstOrDefault(vm => vm.IsSelected, items.First()));
        }
    }

    private async Task RestoreClosedTabAsync()
    {
        Tab? tabData = await TabPersistencyService.GetClosedTabAsync();

        if (tabData == null)
        {
            return;
        }

        AppTabViewModel vm = tabData.ToAppTabViewModel();

        vm.ParentWindowId = WindowId;

        TabView.CreateTabViewItem(vm);
        TabView.SelectItem(vm);
    }

    protected virtual void Dispose(bool dispose)
    {
        if (dispose)
        {
            Subscriptions.Dispose();
            hasNoTabs.OnCompleted();
            hasNoTabs.Dispose();
            TabView.Dispose();
        }
    }
}
