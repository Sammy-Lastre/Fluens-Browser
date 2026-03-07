using Fluens.AppCore.Enums;

namespace Fluens.AppCore.Contracts;

public interface ILocalSettingService : IDisposable
{
    IObservable<OnStartupSetting> OnStartupSettingChanges { get; }
    IObservable<string> AccentColorChanges { get; }

    OnStartupSetting CurrentOnStartupSetting { get; }
    string CurrentAccentColor { get; }

    void SetStartupConfig(OnStartupSetting onStartupSetting);
    void SetAccentColor(string accentColor);
}