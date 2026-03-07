using Fluens.AppCore.Enums;
using Fluens.AppCore.Helpers;

namespace Fluens.AppCore.Contracts;

public interface ILocalSettingService : IDisposable
{
    IObservable<OnStartupSetting> OnStartupSettingChanges { get; }
    IObservable<string> AccentColorChanges { get; }
    IObservable<AdBlockSettings> AdBlockSettingsChanges { get; }

    OnStartupSetting CurrentOnStartupSetting { get; }
    string CurrentAccentColor { get; }
    AdBlockSettings CurrentAdBlockSettings { get; }

    void SetStartupConfig(OnStartupSetting onStartupSetting);
    void SetAccentColor(string accentColor);
    void SetAdBlockEnabled(bool isEnabled);
    void SetSelectedAdBlockLists(AdBlockListSelection selectedLists);
}