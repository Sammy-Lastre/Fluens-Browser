namespace Fluens.AppCore.Helpers;

public static class Constants
{
    public static readonly Uri AboutBlankUri = new("about:blank");
    public static readonly Uri SettingsUri = new("fluens://settings");
    public static readonly Uri[] SpecialUrls = [AboutBlankUri, SettingsUri];
    public const int HistoryPaginationSize = 100;
    public const string LoadingFaviconUri = "LoadingIcon";
    public const string NewTabTitle = "New Tab";
    public const string SettingsTitle = "Settings";
}
