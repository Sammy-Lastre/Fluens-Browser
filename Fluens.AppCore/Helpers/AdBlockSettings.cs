using Fluens.AppCore.Enums;

namespace Fluens.AppCore.Helpers;

public sealed record AdBlockSettings(bool IsEnabled, AdBlockListSelection SelectedLists);
