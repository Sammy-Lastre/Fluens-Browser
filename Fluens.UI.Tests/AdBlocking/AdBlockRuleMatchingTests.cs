using Fluens.UI.Services.AdBlocking;
using Xunit;

namespace Fluens.UI.Tests.AdBlocking;

public class AdBlockRuleMatchingTests
{
    [Fact]
    public void WhenHostRuleMatchesExactHostThenRequestIsBlocked()
    {
        AdBlockRule rule = ParseSingle("||ads.example.com^");

        bool isMatch = rule.Matches(new Uri("https://ads.example.com/banner.js"), AdBlockResourceType.Script);

        Assert.True(isMatch);
    }

    [Fact]
    public void WhenHostRuleMatchesSubdomainThenRequestIsBlocked()
    {
        AdBlockRule rule = ParseSingle("||ads.example.com^");

        bool isMatch = rule.Matches(new Uri("https://static.ads.example.com/banner.js"), AdBlockResourceType.Script);

        Assert.True(isMatch);
    }

    [Fact]
    public void WhenHostRuleDoesNotMatchDomainThenRequestIsAllowed()
    {
        AdBlockRule rule = ParseSingle("||ads.example.com^");

        bool isMatch = rule.Matches(new Uri("https://notads.example.net/banner.js"), AdBlockResourceType.Script);

        Assert.False(isMatch);
    }

    [Fact]
    public void WhenRuleRequiresSpecificResourceTypeThenDifferentResourceTypeDoesNotMatch()
    {
        AdBlockRule rule = ParseSingle("||ads.example.com^$image");

        bool isMatch = rule.Matches(new Uri("https://ads.example.com/banner.js"), AdBlockResourceType.Script);

        Assert.False(isMatch);
    }

    [Fact]
    public void WhenPatternUsesSeparatorTokenThenOnlySeparatorBoundaryMatches()
    {
        AdBlockRule rule = ParseSingle("banner^");

        bool separatorMatch = rule.Matches(new Uri("https://example.com/path/banner/file.js"), AdBlockResourceType.Script);
        bool nonSeparatorMatch = rule.Matches(new Uri("https://example.com/path/bannerx/file.js"), AdBlockResourceType.Script);

        Assert.True(separatorMatch);
        Assert.False(nonSeparatorMatch);
    }

    [Fact]
    public void WhenPatternUsesStartAndEndAnchorsThenOnlyExactUrlMatches()
    {
        AdBlockRule rule = ParseSingle("|https://example.com/ads.js|");

        bool exactMatch = rule.Matches(new Uri("https://example.com/ads.js"), AdBlockResourceType.Script);
        bool nonExactMatch = rule.Matches(new Uri("https://example.com/ads.js?v=1"), AdBlockResourceType.Script);

        Assert.True(exactMatch);
        Assert.False(nonExactMatch);
    }

    [Fact]
    public void WhenDomainAnchoredPatternContainsPathThenSubdomainPathMatchSucceeds()
    {
        AdBlockRule rule = ParseSingle("||example.com/ads/*");

        bool isMatch = rule.Matches(new Uri("https://cdn.example.com/ads/banner.js"), AdBlockResourceType.Script);

        Assert.True(isMatch);
    }

    private static AdBlockRule ParseSingle(string rule)
    {
        IReadOnlyList<AdBlockRule> rules = AdBlockRuleParser.Parse(rule);
        return Assert.Single(rules);
    }
}
