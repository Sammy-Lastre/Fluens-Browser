using System.Collections.Immutable;

namespace Fluens.UI.Services.AdBlocking;

internal sealed class AdBlockSnapshot(ImmutableArray<AdBlockRule> blockRules, ImmutableArray<AdBlockRule> exceptionRules)
{
    public static readonly AdBlockSnapshot Empty = new([], []);

    public int BlockRuleCount => blockRules.Length;
    public int ExceptionRuleCount => exceptionRules.Length;

    public bool ShouldBlock(Uri uri, AdBlockResourceType resourceType)
    {
        foreach (AdBlockRule exceptionRule in exceptionRules)
        {
            if (exceptionRule.Matches(uri, resourceType))
            {
                return false;
            }
        }

        foreach (AdBlockRule blockRule in blockRules)
        {
            if (blockRule.Matches(uri, resourceType))
            {
                return true;
            }
        }

        return false;
    }
}
