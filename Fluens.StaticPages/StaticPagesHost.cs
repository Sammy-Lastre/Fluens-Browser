using Fluens.AppCore.Contracts;
using Microsoft.FluentUI.AspNetCore.Components;
using System.Net;
using System.Net.Sockets;

namespace Fluens.StaticPages;

public sealed class StaticPagesHost
{
    private readonly Lock StartupGate = new();
    private readonly ILocalSettingService LocalSettingService;
    private Task? StartupTask { get; set; }

    private WebApplication? App { get; set; }
    private Uri? BaseAddress { get; set; }

    public StaticPagesHost(ILocalSettingService localSettingService)
    {
        ArgumentNullException.ThrowIfNull(localSettingService);
        LocalSettingService = localSettingService;
    }

    public async Task<Uri> GetSettingsUriAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);

        return BaseAddress!;
    }

    public bool IsHostedSettingsUri(Uri? uri)
    {
        if (uri is null)
            return false;

        if (BaseAddress is null)
        {
            return false;
        }

        return uri.Scheme.Equals(BaseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            && uri.Host.Equals(BaseAddress.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == BaseAddress.Port
            && uri.AbsolutePath.Equals("/", StringComparison.Ordinal);
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (App is not null)
        {
            return;
        }

        Task startupTask;

        lock (StartupGate)
        {
            StartupTask ??= StartAsync();
            startupTask = StartupTask;
        }

        await startupTask.WaitAsync(cancellationToken);
    }

    private async Task StartAsync()
    {
        if (App is not null)
        {
            return;
        }

        int port = GetOpenPort();
        string baseUrl = $"http://127.0.0.1:{port}/";

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.ApplicationName = typeof(StaticPagesHost).Assembly.GetName().Name!;
        builder.WebHost.UseUrls(baseUrl);
        builder.WebHost.UseStaticWebAssets();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddFluentUIComponents();
        builder.Services.AddSingleton(LocalSettingService);

        WebApplication app = builder.Build();

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        await app.StartAsync();

        lock (StartupGate)
        {
            if (App is null)
            {
                App = app;
                BaseAddress = new Uri(baseUrl);
            }
        }
    }

    private static int GetOpenPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
