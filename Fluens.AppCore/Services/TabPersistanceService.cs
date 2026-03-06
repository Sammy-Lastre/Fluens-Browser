using Fluens.Data;
using Fluens.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluens.AppCore.Services;

public class TabPersistencyService(IDbContextFactory<BrowserDbContext> dbContextFactory)
{
    public async Task<Tab[]> RecoverTabsAsync()
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Tab[] openTabs = await dbContext.Tabs
            .Include(t => t.Place)
            .Where(t => t.ClosedOn == null)
            .OrderBy(t => t.Index)
            .ToArrayAsync();

        return openTabs;
    }

    public Tab[] GetOpenTabs()
    {
        using BrowserDbContext dbContext = dbContextFactory.CreateDbContext();

        Tab[] openTabs = [.. dbContext.Tabs
            .Where(t => t.ClosedOn == null)
            .OrderBy(t => t.Index)];

        return openTabs;
    }

    public async Task<int> CreateTabAsync(int windowId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowId, 1);

        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Tab entity = new() { BrowserWindowId = windowId };

        dbContext.Tabs.Add(entity);

        await dbContext.SaveChangesAsync();

        return entity.Id;
    }

    public async Task UpdateTabInfoAsync(int id, int? index = null, int? placeId = null, bool? isSelected = null, int? windowId = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);

        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Tabs
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(u =>
            {
                if (index is not null)
                {
                    u.SetProperty(t => t.Index, index);
                }
                if (placeId is not null)
                {
                    u.SetProperty(t => t.PlaceId, placeId);
                }
                if (isSelected is not null)
                {
                    u.SetProperty(t => t.IsSelected, isSelected);
                }
                if (windowId is not null)
                {
                    u.SetProperty(t => t.BrowserWindowId, windowId);
                }
            }, cancellationToken: cancellationToken);
    }

    public async Task DeleteTabAsync(int id)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);

        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        await dbContext.Tabs.Where(t => t.Id == id).ExecuteDeleteAsync();
    }

    public async Task ClearTabsAsync()
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        await dbContext.Tabs.ExecuteDeleteAsync();
    }

    public async Task CloseTabAsync(int id)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);

        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        await dbContext.Tabs.Where(t => t.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls => setPropertyCalls.SetProperty(t => t.ClosedOn, DateTime.UtcNow));
    }

    public async Task<Tab?> GetClosedTabAsync()
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Tab? tab = await dbContext.Tabs
            .Include(t => t.Place)
            .Where(t => t.ClosedOn != null)
            .OrderByDescending(t => t.ClosedOn)
            .FirstOrDefaultAsync();

        if (tab != null)
        {
            await dbContext.Tabs.Where(t => t.Id == tab.Id)
                .ExecuteUpdateAsync(setPropertyCalls => setPropertyCalls.SetProperty(t => t.ClosedOn, (DateTime?)null));
        }

        return tab;
    }
}
