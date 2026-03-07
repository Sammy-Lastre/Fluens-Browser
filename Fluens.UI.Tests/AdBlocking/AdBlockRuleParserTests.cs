using Fluens.UI.Services.AdBlocking;
using Xunit;

namespace Fluens.UI.Tests.AdBlocking;

public class AdBlockRuleParserTests
{
    [Fact]
    public void WhenInputContainsOnlyCommentsAndCosmeticRulesThenNoRulesAreReturned()
    {
        string input = """
! title
[Adblock Plus 2.0]
example.com##.ad-banner
example.com#@#.ad-banner
example.com#$#body { color: red; }
example.com#?#div:has(> .ad)
""";

        IReadOnlyList<AdBlockRule> rules = AdBlockRuleParser.Parse(input);

        Assert.Empty(rules);
    }

    [Fact]
    public void WhenRuleUsesHostSyntaxThenHostRuleIsCreated()
    {
        IReadOnlyList<AdBlockRule> rules = AdBlockRuleParser.Parse("||ads.example.com^");

        AdBlockRule rule = Assert.Single(rules);
        Assert.Equal(AdBlockRuleMatchMode.Host, rule.MatchMode);
        Assert.Equal("ads.example.com", rule.Host);
        Assert.Equal(AdBlockResourceType.Any, rule.ResourceTypes);
    }

    [Fact]
    public void WhenRuleIsExceptionThenIsExceptionFlagIsTrue()
    {
        IReadOnlyList<AdBlockRule> rules = AdBlockRuleParser.Parse("@@||cdn.example.com^");

        AdBlockRule rule = Assert.Single(rules);
        Assert.True(rule.IsException);
        Assert.Equal(AdBlockRuleMatchMode.Host, rule.MatchMode);
    }

    [Fact]
    public void WhenRuleHasResourceOptionsThenResourceTypesAreMapped()
    {
        IReadOnlyList<AdBlockRule> rules = AdBlockRuleParser.Parse("||tracker.example.com^$script,image");

        AdBlockRule rule = Assert.Single(rules);
        Assert.True(rule.ResourceTypes.HasFlag(AdBlockResourceType.Script));
        Assert.True(rule.ResourceTypes.HasFlag(AdBlockResourceType.Image));
        Assert.False(rule.ResourceTypes.HasFlag(AdBlockResourceType.StyleSheet));
    }

    [Fact]
    public void WhenRuleHasOnlyNegatedResourceOptionsThenRuleIsSkipped()
    {
        IReadOnlyList<AdBlockRule> rules = AdBlockRuleParser.Parse("||tracker.example.com^$~script,~image");

        Assert.Empty(rules);
    }

    [Fact]
    public void WhenRuleHasUnsupportedOptionsThenRuleIsSkipped()
    {
        IReadOnlyList<AdBlockRule> rules = AdBlockRuleParser.Parse("@@||tracker.example.com^$domain=example.com");

        Assert.Empty(rules);
    }
}
