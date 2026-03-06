using Fluens.Data;
using Fluens.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluens.AppCore.Services;

public class BrowserWindowService(IDbContextFactory<BrowserDbContext> dbContextFactory)
{
    public async Task<BrowserWindow?> GetLastWindowAsync()
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        BrowserWindow? lastWindow = await dbContext.BrowserWindows
            .Where(t => t.ClosedOn != null)
            .OrderByDescending(t => t.ClosedOn)
            .FirstOrDefaultAsync();

        return lastWindow;
    }
    public async Task<int> CreateWindowAsync()
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        BrowserWindow entity = new() { };

        await dbContext.BrowserWindows.AddAsync(entity);

        await dbContext.SaveChangesAsync();

        return entity.Id;
    }

    public async Task SaveWindowStateAsync(int id, int x, int y, int width, int height, bool isMaximized)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        await dbContext.BrowserWindows.Where(w => w.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls => setPropertyCalls.SetProperty(w => w.X, x)
                .SetProperty(w => w.Y, y)
                .SetProperty(w => w.Width, width)
                .SetProperty(w => w.Height, height)
                .SetProperty(w => w.IsMaximized, isMaximized)
                .SetProperty(w => w.ClosedOn, DateTime.UtcNow));
    }

    public async Task ClearWindowsAsync()
    {
        await using BrowserDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        await dbContext.BrowserWindows.ExecuteDeleteAsync();
    }
}
