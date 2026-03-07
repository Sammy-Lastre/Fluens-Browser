using Fluens.AppCore.Contracts;
using Fluens.AppCore.Enums;
using Fluens.AppCore.Helpers;
using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace Fluens.UI.Services;

public partial class LocalSettingService : ILocalSettingService
{
    public IObservable<OnStartupSetting> OnStartupSettingChanges => _onStartupSettingChanges.AsObservable();
    public IObservable<string> AccentColorChanges => _accentColorChanges.AsObservable();
    public IObservable<AdBlockSettings> AdBlockSettingsChanges => _adBlockSettingsChanges.AsObservable();

    public OnStartupSetting CurrentOnStartupSetting => _onStartupSettingChanges.Value;
    public string CurrentAccentColor => _accentColorChanges.Value;
    public AdBlockSettings CurrentAdBlockSettings => _adBlockSettingsChanges.Value;

    private const OnStartupSetting defaultOnStartupSetting = OnStartupSetting.OpenNewTab;
    private static readonly AdBlockSettings DefaultAdBlockSettings = new(
        IsEnabled: true,
        SelectedLists: AdBlockListSelection.EasyList | AdBlockListSelection.EasyPrivacy);

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

        _adBlockSettingsChanges = new(GetSavedAdBlockSettings() ?? DefaultAdBlockSettings);
        PersistAdBlockSettings(_adBlockSettingsChanges.Value);
    }

    private const string OnStartupSettingKey = "OnStartupSetting";
    private const string AdBlockEnabledKey = "AdBlockEnabled";
    private const string AdBlockSelectedListsKey = "AdBlockSelectedLists";
    private static readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
    private readonly BehaviorSubject<OnStartupSetting> _onStartupSettingChanges;
    private readonly BehaviorSubject<string> _accentColorChanges;
    private readonly BehaviorSubject<AdBlockSettings> _adBlockSettingsChanges;
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

    public void SetAdBlockEnabled(bool isEnabled)
    {
        AdBlockSettings next = _adBlockSettingsChanges.Value with { IsEnabled = isEnabled };
        PersistAdBlockSettings(next);
        _adBlockSettingsChanges.OnNext(next);
    }

    public void SetSelectedAdBlockLists(AdBlockListSelection selectedLists)
    {
        AdBlockListSelection normalizedSelection = selectedLists == AdBlockListSelection.None
            ? AdBlockListSelection.EasyList
            : selectedLists;

        AdBlockSettings next = _adBlockSettingsChanges.Value with { SelectedLists = normalizedSelection };
        PersistAdBlockSettings(next);
        _adBlockSettingsChanges.OnNext(next);
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

    private static AdBlockSettings? GetSavedAdBlockSettings()
    {
        bool? isEnabled = localSettings.Values.TryGetValue(AdBlockEnabledKey, out object? rawIsEnabled)
            && bool.TryParse(rawIsEnabled?.ToString(), out bool parsedIsEnabled)
            ? parsedIsEnabled
            : null;

        AdBlockListSelection? selectedLists = localSettings.Values.TryGetValue(AdBlockSelectedListsKey, out object? rawSelectedLists)
            && int.TryParse(rawSelectedLists?.ToString(), out int parsedSelectedLists)
            && parsedSelectedLists >= 0
            && (((AdBlockListSelection)parsedSelectedLists) & ~(AdBlockListSelection.EasyList | AdBlockListSelection.EasyPrivacy | AdBlockListSelection.UBlockFilters)) == 0
            ? (AdBlockListSelection)parsedSelectedLists
            : null;

        if (isEnabled is null && selectedLists is null)
        {
            return null;
        }

        return new AdBlockSettings(
            IsEnabled: isEnabled ?? DefaultAdBlockSettings.IsEnabled,
            SelectedLists: selectedLists is null or AdBlockListSelection.None
                ? DefaultAdBlockSettings.SelectedLists
                : selectedLists.Value);
    }

    private static void PersistAdBlockSettings(AdBlockSettings settings)
    {
        localSettings.Values[AdBlockEnabledKey] = settings.IsEnabled.ToString(CultureInfo.InvariantCulture);
        localSettings.Values[AdBlockSelectedListsKey] = ((int)settings.SelectedLists).ToString(CultureInfo.InvariantCulture);
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
        _adBlockSettingsChanges.OnCompleted();
        _adBlockSettingsChanges.Dispose();
    }
}
