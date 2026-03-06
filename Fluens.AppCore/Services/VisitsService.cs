using Fluens.AppCore.Helpers;
using Fluens.AppCore.ViewModels.Settings.History;
using Fluens.Data;
using Fluens.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace Fluens.AppCore.Services;

public class VisitsService(IDbContextFactory<BrowserDbContext> dbContextFactory)
{
    public async Task AddEntryAsync(int placeId, CancellationToken cancellationToken = default)
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Visits.AddAsync(new() { PlaceId = placeId }, cancellationToken);

        await dbContext.Places.Where(p => p.Id == placeId)
            .ExecuteUpdateAsync(u =>
            {
                u.SetProperty(p => p.VisitCount, p => p.VisitCount + 1);
                u.SetProperty(p => p.LastVisitDate, DateTime.UtcNow);
            }, cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<HistoryPage> GetEntriesAsync(DateTime? lastDate = null, int? lastId = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Fetch the items and determine if there are more
        IQueryable<int> latestIdsQuery = dbContext.Visits
            .GroupBy(v => v.PlaceId)
            .Select(g => g.OrderByDescending(v => v.VisitDate)
                          .ThenByDescending(v => v.Id)
                          .Select(v => v.Id)
                          .FirstOrDefault());

        Visit[] visits = await dbContext.Visits
            .Where(v => latestIdsQuery.Contains(v.Id))
            .Include(v => v.Place)
            .Where(e =>
                lastDate == null ||
                e.VisitDate < lastDate.Value ||
                (e.VisitDate == lastDate.Value && e.Id < lastId))
            .OrderByDescending(v => v.VisitDate)
            .ThenByDescending(v => v.Id)
            .Take(limit + 1)
            .ToArrayAsync(cancellationToken);

        // Extract the cursor and ID for the next page
        bool hasMore = visits.Length > limit;
        int? nextLastId = hasMore ? visits[^1].Id : null;
        DateTime? nextLastDate = hasMore ? visits[^1].VisitDate : null;

        IEnumerable<HistoryEntryViewModel> items = visits.Select(v => new HistoryEntryViewModel()
        {
            Id = v.Id,
            Url = new Uri(v.Place.Url),
            FaviconUrl = v.Place.FaviconUrl,
            DocumentTitle = v.Place.Title,
            LastVisitedOn = v.VisitDate.ToLocalTime(),
            Host = v.Place.Hostname,
            PlaceId = v.PlaceId,
        });

        return new HistoryPage()
        {
            Items = new ReadOnlyCollection<HistoryEntryViewModel>([.. hasMore ? items.SkipLast(1) : items]),
            NextLastDate = nextLastDate,
            NextLastId = nextLastId
        };
    }

    internal async Task DeleteEntriesAsync(int[] placeIds, CancellationToken cancellationToken = default)
    {
        if (placeIds.Length == 0)
        {
            return;
        }

        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Visits.Where(e => placeIds.Contains(e.PlaceId)).ExecuteDeleteAsync(cancellationToken);
    }

    internal async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Visits.ExecuteDeleteAsync(cancellationToken);
    }
}
