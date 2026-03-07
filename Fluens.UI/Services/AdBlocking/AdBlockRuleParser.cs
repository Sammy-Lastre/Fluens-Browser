namespace Fluens.UI.Services.AdBlocking;

internal static class AdBlockRuleParser
{
    public static IReadOnlyList<AdBlockRule> Parse(string rawRules)
    {
        if (string.IsNullOrWhiteSpace(rawRules))
        {
            return [];
        }

        List<AdBlockRule> parsedRules = [];
        ReadOnlySpan<char> input = rawRules.AsSpan();
        int lineStart = 0;

        for (int i = 0; i <= input.Length; i++)
        {
            if (i < input.Length && input[i] != '\n')
            {
                continue;
            }

            ReadOnlySpan<char> line = TrimWhitespace(input[lineStart..i]);
            lineStart = i + 1;

            if (ShouldSkipLine(line))
            {
                continue;
            }

            bool isException = line.StartsWith("@@", StringComparison.Ordinal);
            ReadOnlySpan<char> effectiveRule = isException ? line[2..] : line;

            int optionsSeparatorIndex = effectiveRule.IndexOf('$');
            ReadOnlySpan<char> pattern = optionsSeparatorIndex >= 0
                ? effectiveRule[..optionsSeparatorIndex]
                : effectiveRule;
            ReadOnlySpan<char> options = optionsSeparatorIndex >= 0
                ? effectiveRule[(optionsSeparatorIndex + 1)..]
                : [];

            pattern = TrimWhitespace(pattern);

            if (pattern.IsEmpty)
            {
                continue;
            }

            if (!TryParseResourceTypes(options, out AdBlockResourceType resourceTypes))
            {
                continue;
            }

            if (TryCreateHostRule(pattern, isException, resourceTypes, out AdBlockRule? hostRule))
            {
                if (hostRule is not null)
                {
                    parsedRules.Add(hostRule);
                }

                continue;
            }

            AdBlockRule? rule = CreateUrlRule(pattern, isException, resourceTypes);
            if (rule is not null)
            {
                parsedRules.Add(rule);
            }
        }

        return parsedRules;
    }

    private static bool ShouldSkipLine(ReadOnlySpan<char> line)
    {
        if (line.IsEmpty)
        {
            return true;
        }

        if (line.StartsWith('!') || line.StartsWith('['))
        {
            return true;
        }

        return line.IndexOf("##", StringComparison.Ordinal) >= 0
            || line.IndexOf("#@#", StringComparison.Ordinal) >= 0
            || line.IndexOf("#$#", StringComparison.Ordinal) >= 0
            || line.IndexOf("#?#", StringComparison.Ordinal) >= 0;
    }

    private static bool TryCreateHostRule(ReadOnlySpan<char> pattern, bool isException, AdBlockResourceType resourceTypes, out AdBlockRule? rule)
    {
        rule = null;

        if (!pattern.StartsWith("||", StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> remaining = pattern[2..];

        if (!remaining.EndsWith('^'))
        {
            return false;
        }

        ReadOnlySpan<char> host = remaining[..^1];

        if (!IsValidHost(host))
        {
            return false;
        }

        rule = new AdBlockRule
        {
            IsException = isException,
            ResourceTypes = resourceTypes,
            MatchMode = AdBlockRuleMatchMode.Host,
            Host = host.ToString()
        };

        return true;
    }

    private static AdBlockRule? CreateUrlRule(ReadOnlySpan<char> pattern, bool isException, AdBlockResourceType resourceTypes)
    {
        bool startsWithAnchor = pattern.StartsWith('|');
        bool endsWithAnchor = pattern.EndsWith('|');

        ReadOnlySpan<char> normalized = TrimAnchors(pattern);

        if (normalized.IsEmpty)
        {
            return null;
        }

        if (startsWithAnchor && endsWithAnchor)
        {
            return new AdBlockRule
            {
                IsException = isException,
                ResourceTypes = resourceTypes,
                MatchMode = AdBlockRuleMatchMode.Regex,
                IsAnchoredAtStart = true,
                IsAnchoredAtEnd = true,
                Pattern = normalized.ToString()
            };
        }

        bool isDomainAnchored = pattern.StartsWith("||", StringComparison.Ordinal);

        if (isDomainAnchored
            || normalized.IndexOf('*') >= 0
            || normalized.IndexOf('^') >= 0)
        {
            return new AdBlockRule
            {
                IsException = isException,
                ResourceTypes = resourceTypes,
                MatchMode = AdBlockRuleMatchMode.Regex,
                IsDomainAnchored = isDomainAnchored,
                IsAnchoredAtStart = !isDomainAnchored && startsWithAnchor,
                IsAnchoredAtEnd = endsWithAnchor,
                Pattern = isDomainAnchored
                    ? pattern[2..].ToString()
                    : normalized.ToString()
            };
        }

        return new AdBlockRule
        {
            IsException = isException,
            ResourceTypes = resourceTypes,
            MatchMode = startsWithAnchor
                ? AdBlockRuleMatchMode.Prefix
                : endsWithAnchor
                    ? AdBlockRuleMatchMode.Suffix
                    : AdBlockRuleMatchMode.Contains,
            Pattern = normalized.ToString()
        };
    }

    private static bool TryParseResourceTypes(ReadOnlySpan<char> options, out AdBlockResourceType resourceTypes)
    {
        options = TrimWhitespace(options);

        if (options.IsEmpty)
        {
            resourceTypes = AdBlockResourceType.Any;
            return true;
        }

        AdBlockResourceType types = AdBlockResourceType.None;
        bool hasPositiveResourceType = false;
        int tokenStart = 0;

        for (int i = 0; i <= options.Length; i++)
        {
            if (i < options.Length && options[i] != ',')
            {
                continue;
            }

            ReadOnlySpan<char> token = TrimWhitespace(options[tokenStart..i]);
            tokenStart = i + 1;

            if (token.IsEmpty)
            {
                continue;
            }

            bool isNegated = token.StartsWith('~');
            ReadOnlySpan<char> normalizedToken = isNegated ? token[1..] : token;
            AdBlockResourceType mappedType = MapResourceType(normalizedToken);

            if (mappedType == AdBlockResourceType.None)
            {
                resourceTypes = AdBlockResourceType.None;
                return false;
            }

            if (isNegated)
            {
                resourceTypes = AdBlockResourceType.None;
                return false;
            }

            types |= mappedType;
            hasPositiveResourceType = true;
        }

        if (!hasPositiveResourceType)
        {
            resourceTypes = AdBlockResourceType.None;
            return false;
        }

        resourceTypes = types;
        return true;
    }

    private static AdBlockResourceType MapResourceType(ReadOnlySpan<char> token)
    {
        if (token.Equals("script", StringComparison.Ordinal))
        {
            return AdBlockResourceType.Script;
        }

        if (token.Equals("image", StringComparison.Ordinal))
        {
            return AdBlockResourceType.Image;
        }

        if (token.Equals("stylesheet", StringComparison.Ordinal))
        {
            return AdBlockResourceType.StyleSheet;
        }

        if (token.Equals("xmlhttprequest", StringComparison.Ordinal)
            || token.Equals("xhr", StringComparison.Ordinal))
        {
            return AdBlockResourceType.XmlHttpRequest;
        }

        if (token.Equals("media", StringComparison.Ordinal))
        {
            return AdBlockResourceType.Media;
        }

        if (token.Equals("font", StringComparison.Ordinal))
        {
            return AdBlockResourceType.Font;
        }

        if (token.Equals("websocket", StringComparison.Ordinal))
        {
            return AdBlockResourceType.WebSocket;
        }

        if (token.Equals("fetch", StringComparison.Ordinal))
        {
            return AdBlockResourceType.Fetch;
        }

        if (token.Equals("document", StringComparison.Ordinal)
            || token.Equals("subdocument", StringComparison.Ordinal))
        {
            return AdBlockResourceType.Document;
        }

        if (token.Equals("other", StringComparison.Ordinal))
        {
            return AdBlockResourceType.Other;
        }

        return AdBlockResourceType.None;
    }

    private static bool IsValidHost(ReadOnlySpan<char> host)
    {
        if (host.IsEmpty)
        {
            return false;
        }

        foreach (char c in host)
        {
            if ((c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9')
                || c == '.'
                || c == '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static ReadOnlySpan<char> TrimAnchors(ReadOnlySpan<char> value)
    {
        int start = 0;
        int end = value.Length - 1;

        while (start <= end && value[start] == '|')
        {
            start++;
        }

        while (end >= start && value[end] == '|')
        {
            end--;
        }

        return start > end ? [] : value[start..(end + 1)];
    }

    private static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> value)
    {
        int start = 0;
        int end = value.Length - 1;

        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return start > end ? [] : value[start..(end + 1)];
    }
}
