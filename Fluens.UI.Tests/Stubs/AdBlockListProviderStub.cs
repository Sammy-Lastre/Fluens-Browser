using Fluens.AppCore.Enums;

namespace Fluens.UI.Services.AdBlocking;

internal sealed class AdBlockListProvider
{
    public Task<string> GetSelectedRulesAsync(AdBlockListSelection selectedLists, CancellationToken cancellationToken)
    {
        return Task.FromResult(string.Empty);
    }
}
