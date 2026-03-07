using Fluens.AppCore.Contracts;
using Fluens.AppCore.Enums;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace Fluens.UI.Services;

public partial class LocalSettingService : ILocalSettingService
{
    public IObservable<OnStartupSetting> OnStartupSettingChanges => _onStartupSettingChanges.AsObservable();
    public IObservable<string> AccentColorChanges => _accentColorChanges.AsObservable();

    public OnStartupSetting CurrentOnStartupSetting => _onStartupSettingChanges.Value;
    public string CurrentAccentColor => _accentColorChanges.Value;

    private const OnStartupSetting defaultOnStartupSetting = OnStartupSetting.OpenNewTab;

    public LocalSettingService()
    {
        if (GetStartupConfig() is OnStartupSetting savedSetting)
        {
            _onStartupSettingChanges = new(savedSetting);
        }
        else
        {
            _onStartupSettingChanges = new(defaultOnStartupSetting);
            SetStartupConfig(defaultOnStartupSetting);
        }

        _uiSettings = new UISettings();
        _accentColorChanges = new(GetSystemAccentColorHex(_uiSettings));
        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
    }

    private const string OnStartupSettingKey = "OnStartupSetting";
    private static readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
    private readonly BehaviorSubject<OnStartupSetting> _onStartupSettingChanges;
    private readonly BehaviorSubject<string> _accentColorChanges;
    private readonly UISettings _uiSettings;

    private OnStartupSetting? GetStartupConfig()
    {
        if (localSettings.Values.TryGetValue(OnStartupSettingKey, out object? rawSetting))
        {
            if (Enum.TryParse(rawSetting.ToString(), out OnStartupSetting parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    public void SetStartupConfig(OnStartupSetting onStartupSetting)
    {
        localSettings.Values[OnStartupSettingKey] = onStartupSetting.ToString();
        _onStartupSettingChanges.OnNext(GetStartupConfig()!.Value);
    }

    public void SetAccentColor(string accentColor)
    {
        if (string.IsNullOrWhiteSpace(accentColor))
        {
            throw new ArgumentException("Accent color cannot be null or whitespace.", nameof(accentColor));
        }

        if (string.Equals(_accentColorChanges.Value, accentColor, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _accentColorChanges.OnNext(accentColor);
    }

    private void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        SetAccentColor(GetSystemAccentColorHex(sender));
    }

    private static string GetSystemAccentColorHex(UISettings settings)
    {
        Windows.UI.Color accentColor = settings.GetColorValue(UIColorType.Accent);
        return $"#{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool dispose)
    {
        _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
        _onStartupSettingChanges.OnCompleted();
        _onStartupSettingChanges.Dispose();
        _accentColorChanges.OnCompleted();
        _accentColorChanges.Dispose();
    }
}
