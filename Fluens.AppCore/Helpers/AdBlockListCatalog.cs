using Fluens.AppCore.Enums;

namespace Fluens.AppCore.Helpers;

public static class AdBlockListCatalog
{
    private static readonly IReadOnlyList<AdBlockListDefinition> Definitions =
    [
        new(
            Id: AdBlockListSelection.EasyList,
            DisplayName: "EasyList",
            Description: "General ad and sponsored content blocking rules.",
            SourceUrl: new Uri("https://easylist.to/easylist/easylist.txt")),
        new(
            Id: AdBlockListSelection.EasyPrivacy,
            DisplayName: "EasyPrivacy",
            Description: "Privacy and tracker blocking rules.",
            SourceUrl: new Uri("https://easylist.to/easylist/easyprivacy.txt")),
        new(
            Id: AdBlockListSelection.UBlockFilters,
            DisplayName: "uBlock filters",
            Description: "uBlock Origin core network filtering rules.",
            SourceUrl: new Uri("https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/filters.txt"))
    ];

    public static IReadOnlyList<AdBlockListDefinition> GetDefinitions() => Definitions;

    public static IReadOnlyList<AdBlockListDefinition> GetSelected(AdBlockListSelection selectedLists)
    {
        if (selectedLists == AdBlockListSelection.None)
        {
            return [Definitions[0]];
        }

        return [.. Definitions.Where(definition => selectedLists.HasFlag(definition.Id))];
    }
}

public sealed record AdBlockListDefinition(
    AdBlockListSelection Id,
    string DisplayName,
    string Description,
    Uri SourceUrl);
