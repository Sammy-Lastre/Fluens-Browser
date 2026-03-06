using Fluens.AppCore.Helpers;
using Fluens.Data;
using Fluens.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Toimik.UrlNormalization;

namespace Fluens.AppCore.Services;

public class PlacesService(IDbContextFactory<BrowserDbContext> dbContextFactory, HttpUrlNormalizer httpUrlNormalizer)
{
    public async Task<int> GetOrCreatePlaceAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        string normalizedUrl = url != Constants.AboutBlankUri ? httpUrlNormalizer.Normalize(url.ToString()) : Constants.AboutBlankUri.ToString();

        //Get or create place
        Place place = await dbContext.Places.SingleOrDefaultAsync(e => e.NormalizedUrl == normalizedUrl, cancellationToken: cancellationToken)
            ?? (await dbContext.Places.AddAsync(new Place()
            {
                Url = url.ToString(),
                NormalizedUrl = normalizedUrl,
                Path = url.AbsolutePath,
                Hostname = url.Host,
                LastVisitDate = DateTime.UtcNow,
            }, cancellationToken)).Entity;

        await dbContext.SaveChangesAsync(cancellationToken);

        return place.Id;
    }

    public async Task UpdatePlaceAsync(int placeId, string? faviconUrl = null, string? title = null, CancellationToken cancellationToken = default)
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Places.Where(p => p.Id == placeId)
            .ExecuteUpdateAsync(u =>
            {
                if (faviconUrl is not null)
                {
                    u.SetProperty(p => p.FaviconUrl, faviconUrl);
                }
                if (title is not null)
                {
                    u.SetProperty(p => p.Title, title);
                }
            }, cancellationToken: cancellationToken);
    }
}
