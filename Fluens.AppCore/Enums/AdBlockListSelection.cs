namespace Fluens.AppCore.Enums;

[Flags]
public enum AdBlockListSelection
{
    None = 0,
    EasyList = 1 << 0,
    EasyPrivacy = 1 << 1,
    UBlockFilters = 1 << 2
}
