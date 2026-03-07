using Fluens.AppCore.Contracts;
using Fluens.AppCore.Enums;
using Fluens.AppCore.Helpers;
using Fluens.AppCore.Services;
using Fluens.AppCore.ViewModels.Settings;
using Fluens.AppCore.ViewModels.Settings.History;
using Fluens.AppCore.ViewModels.Settings.OnStartup;
using Fluens.Data;
using Fluens.Data.Entities;
using Fluens.StaticPages;
using Fluens.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using ReactiveUI.Builder;
using System.Reactive.Linq;
using System.Reflection;
using Toimik.UrlNormalization;
using Windows.Graphics;

namespace Fluens.UI;

public partial class App : Application
{
    private IHost _host = null!;

    public App()
    {
        InitializeComponent();

        IReactiveUIInstance app = RxAppBuilder.CreateReactiveUIBuilder()
            .WithWinUI()
            .WithViewsFromAssembly(Assembly.GetExecutingAssembly())
            .BuildApp();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddDebug(); // shows in Debug output
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                services.AddSingleton<OnStartupConfigViewModel>()
                    .AddSingleton<HistoryPageViewModel>()
                    .AddSingleton<SettingsViewModel>()
                    .AddSingleton<WindowsManager>()
                    .AddSingleton<ITabPageManager, TabViewsManager>()
                    .AddSingleton<TabPersistencyService>()
                    .AddSingleton<BrowserWindowService>()
                    .AddSingleton<StaticPagesHost>()
                    .AddSingleton<VisitsService>()
                    .AddSingleton<ILocalSettingService, LocalSettingService>()
                    .AddSingleton<HttpUrlNormalizer>()
                    .AddSingleton<PlacesService>()
                    .AddPooledDbContextFactory<BrowserDbContext>(opts =>
                    {
                        opts.UseSqlite("Data Source=BrowserStorage.db")
                            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                    });
            })
            .Build();


        ServiceLocator.SetLocator(_host.Services);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();

        await ApplyDbMigrations();

        await ApplyOnStartupSetting();
    }

    private static async Task ApplyDbMigrations()
    {
        IDbContextFactory<BrowserDbContext> dbContextFactory = ServiceLocator.GetRequiredService<IDbContextFactory<BrowserDbContext>>();
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        await dbContext.Database.MigrateAsync();
    }

    private async Task ApplyOnStartupSetting()
    {
        ILocalSettingService localSetting = ServiceLocator.GetRequiredService<ILocalSettingService>();
        OnStartupSetting onStartupSetting = await localSetting.OnStartupSettingChanges.Take(1);

        MainWindow? window = null;

        BrowserWindowService browserWindowService = ServiceLocator.GetRequiredService<BrowserWindowService>();

        if (onStartupSetting is OnStartupSetting.RestoreOpenTabs or OnStartupSetting.RestoreAndOpenNewTab)
        {
            BrowserWindow? lastWindow = await browserWindowService.GetLastWindowAsync();
            if (lastWindow is not null)
            {
                window = ServiceLocator.GetRequiredService<WindowsManager>().CreateWindow(lastWindow.Id);
                AppWindow appWindow = window.AppWindow;

                appWindow.Move(new PointInt32(lastWindow.X, lastWindow.Y));
                appWindow.Resize(new SizeInt32(lastWindow.Width, lastWindow.Height));

                if (lastWindow.IsMaximized && appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            }
        }

        if (window is null)
        {
            int newWindowId = await browserWindowService.CreateWindowAsync();
            window = ServiceLocator.GetRequiredService<WindowsManager>().CreateWindow(newWindowId);
        }

        await window.ApplyOnStartupSettingAsync(onStartupSetting);
        window.Activate();
    }
}
