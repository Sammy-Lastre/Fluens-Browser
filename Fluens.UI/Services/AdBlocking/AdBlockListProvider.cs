using Fluens.AppCore.Enums;
using Fluens.AppCore.Helpers;
using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace Fluens.UI.Services.AdBlocking;

internal sealed class AdBlockListProvider(IHttpClientFactory httpClientFactory, ILogger<AdBlockListProvider> logger)
{
    private static readonly Action<ILogger, string, Uri, Exception?> LogListRefreshFailedDelegate =
        LoggerMessage.Define<string, Uri>(
            LogLevel.Warning,
            new EventId(1010, nameof(LogListRefreshFailed)),
            "Unable to refresh adblock list {ListName} from {ListUrl}.");

    private const string CacheFolderName = "adblock-lists";
    private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromHours(24);

    public async Task<string> GetSelectedRulesAsync(AdBlockListSelection selectedLists, CancellationToken cancellationToken)
    {
        IReadOnlyList<AdBlockListDefinition> selectedDefinitions = AdBlockListCatalog.GetSelected(selectedLists);
        List<string> contents = [];

        foreach (AdBlockListDefinition definition in selectedDefinitions)
        {
            string content = await GetListContentAsync(definition, cancellationToken);
            if (!string.IsNullOrWhiteSpace(content))
            {
                contents.Add(content);
            }
        }

        return string.Join(Environment.NewLine, contents);
    }

    private async Task<string> GetListContentAsync(AdBlockListDefinition definition, CancellationToken cancellationToken)
    {
        string cacheFolder = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, CacheFolderName);
        Directory.CreateDirectory(cacheFolder);

        string cacheFilePath = Path.Combine(cacheFolder, $"{definition.Id}.txt");
        string etagFilePath = Path.Combine(cacheFolder, $"{definition.Id}.etag");

        string? cachedContent = await TryReadFileAsync(cacheFilePath, cancellationToken);
        bool shouldRefresh = cachedContent is null
            || DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(cacheFilePath) > CacheRefreshInterval;

        if (!shouldRefresh)
        {
            return cachedContent!;
        }

        string? etag = await TryReadFileAsync(etagFilePath, cancellationToken);

        try
        {
            HttpClient httpClient = httpClientFactory.CreateClient("AdBlockLists");
            using HttpRequestMessage request = new(HttpMethod.Get, definition.SourceUrl);

            if (!string.IsNullOrWhiteSpace(etag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && cachedContent is not null)
            {
                return cachedContent;
            }

            response.EnsureSuccessStatusCode();
            string downloadedContent = await response.Content.ReadAsStringAsync(cancellationToken);

            await File.WriteAllTextAsync(cacheFilePath, downloadedContent, cancellationToken);

            string? responseEtag = response.Headers.ETag?.Tag;
            if (!string.IsNullOrWhiteSpace(responseEtag))
            {
                await File.WriteAllTextAsync(etagFilePath, responseEtag, cancellationToken);
            }

            return downloadedContent;
        }
        catch (HttpRequestException ex)
        {
            LogListRefreshFailed(logger, definition.DisplayName, definition.SourceUrl, ex);

            if (cachedContent is not null)
            {
                return cachedContent;
            }

            return string.Empty;
        }
    }

    private static async Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private static void LogListRefreshFailed(ILogger logger, string listName, Uri listUrl, Exception exception)
    {
        LogListRefreshFailedDelegate(logger, listName, listUrl, exception);
    }
}
