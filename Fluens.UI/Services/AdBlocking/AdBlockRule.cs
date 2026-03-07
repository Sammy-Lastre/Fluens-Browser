namespace Fluens.UI.Services.AdBlocking;

internal sealed class AdBlockRule
{
    public required bool IsException { get; init; }
    public required AdBlockResourceType ResourceTypes { get; init; }
    public required AdBlockRuleMatchMode MatchMode { get; init; }
    public string? Host { get; init; }
    public string? Pattern { get; init; }
    public bool IsDomainAnchored { get; init; }
    public bool IsAnchoredAtStart { get; init; }
    public bool IsAnchoredAtEnd { get; init; }

    public bool Matches(Uri uri, AdBlockResourceType resourceType)
    {
        if (ResourceTypes != AdBlockResourceType.Any && !ResourceTypes.HasFlag(resourceType))
        {
            return false;
        }

        return MatchMode switch
        {
            AdBlockRuleMatchMode.Host when Host is not null => IsHostMatch(uri.Host, Host),
            AdBlockRuleMatchMode.Contains when Pattern is not null => uri.AbsoluteUri.Contains(Pattern, StringComparison.OrdinalIgnoreCase),
            AdBlockRuleMatchMode.Prefix when Pattern is not null => uri.AbsoluteUri.StartsWith(Pattern, StringComparison.OrdinalIgnoreCase),
            AdBlockRuleMatchMode.Suffix when Pattern is not null => uri.AbsoluteUri.EndsWith(Pattern, StringComparison.OrdinalIgnoreCase),
            AdBlockRuleMatchMode.Regex when Pattern is not null => IsPatternMatch(uri.AbsoluteUri, Pattern),
            _ => false
        };
    }

    private bool IsPatternMatch(string input, string pattern)
    {
        if (IsDomainAnchored)
        {
            return IsDomainAnchoredMatch(input, pattern);
        }

        if (IsAnchoredAtStart)
        {
            return TryMatchAt(input, 0, pattern, out int endIndex)
                && (!IsAnchoredAtEnd || endIndex == input.Length);
        }

        for (int i = 0; i <= input.Length; i++)
        {
            if (!TryMatchAt(input, i, pattern, out int endIndex))
            {
                continue;
            }

            if (IsAnchoredAtEnd && endIndex != input.Length)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsDomainAnchoredMatch(string input, string pattern)
    {
        int schemeSeparator = input.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator < 0)
        {
            return false;
        }

        int hostStart = schemeSeparator + 3;
        int hostEnd = input.IndexOfAny(['/', '?', '#'], hostStart);
        if (hostEnd < 0)
        {
            hostEnd = input.Length;
        }

        for (int start = hostStart; start < hostEnd; start++)
        {
            if (start > hostStart && input[start - 1] != '.')
            {
                continue;
            }

            if (!TryMatchAt(input, start, pattern, out int endIndex))
            {
                continue;
            }

            if (IsAnchoredAtEnd && endIndex != input.Length)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryMatchAt(string input, int startIndex, string pattern, out int endIndex)
    {
        int inputIndex = startIndex;
        int patternIndex = 0;
        int starPatternIndex = -1;
        int starInputIndex = -1;

        while (inputIndex < input.Length)
        {
            if (patternIndex == pattern.Length)
            {
                endIndex = inputIndex;
                return true;
            }

            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starPatternIndex = patternIndex++;
                starInputIndex = inputIndex;
                continue;
            }

            if (patternIndex < pattern.Length && IsPatternTokenMatch(pattern[patternIndex], input, inputIndex))
            {
                if (pattern[patternIndex] == '^')
                {
                    inputIndex++;
                    patternIndex++;
                    continue;
                }

                inputIndex++;
                patternIndex++;
                continue;
            }

            if (starPatternIndex >= 0)
            {
                patternIndex = starPatternIndex + 1;
                inputIndex = ++starInputIndex;
                continue;
            }

            endIndex = startIndex;
            return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '^')
        {
            patternIndex++;
        }

        endIndex = inputIndex;
        return patternIndex == pattern.Length;
    }

    private static bool IsPatternTokenMatch(char token, string input, int inputIndex)
    {
        if (token == '^')
        {
            return inputIndex >= input.Length || IsSeparator(input[inputIndex]);
        }

        return char.ToUpperInvariant(token) == char.ToUpperInvariant(input[inputIndex]);
    }

    private static bool IsSeparator(char value)
    {
        return !char.IsLetterOrDigit(value)
            && value != '_'
            && value != '-'
            && value != '.'
            && value != '%';
    }

    private static bool IsHostMatch(string requestHost, string expectedHost)
    {
        if (requestHost.Equals(expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return requestHost.EndsWith($".{expectedHost}", StringComparison.OrdinalIgnoreCase);
    }
}
