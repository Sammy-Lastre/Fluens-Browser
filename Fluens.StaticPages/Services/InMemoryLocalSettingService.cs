using Fluens.AppCore.Contracts;
using Fluens.AppCore.Enums;
using System.Reactive.Subjects;

namespace Fluens.StaticPages.Services;

internal sealed class InMemoryLocalSettingService : ILocalSettingService
{
    private readonly BehaviorSubject<OnStartupSetting> OnStartupSettingSubject = new(OnStartupSetting.OpenNewTab);
    private readonly BehaviorSubject<string> AccentColorSubject = new("#0F6CBD");
    private bool IsDisposed { get; set; }

    public IObservable<OnStartupSetting> OnStartupSettingChanges => OnStartupSettingSubject;
    public IObservable<string> AccentColorChanges => AccentColorSubject;

    public OnStartupSetting CurrentOnStartupSetting => OnStartupSettingSubject.Value;
    public string CurrentAccentColor => AccentColorSubject.Value;

    public void SetStartupConfig(OnStartupSetting onStartupSetting)
    {
        if (!IsDisposed)
        {
            OnStartupSettingSubject.OnNext(onStartupSetting);
        }
        else
        {
            throw new ObjectDisposedException(nameof(InMemoryLocalSettingService));
        }
    }

    public void SetAccentColor(string accentColor)
    {
        if (!IsDisposed)
        {
            if (string.IsNullOrWhiteSpace(accentColor))
            {
                throw new ArgumentException("Accent color cannot be null or whitespace.", nameof(accentColor));
            }

            if (string.Equals(AccentColorSubject.Value, accentColor, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AccentColorSubject.OnNext(accentColor);
        }
        else
        {
            throw new ObjectDisposedException(nameof(InMemoryLocalSettingService));
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        OnStartupSettingSubject.OnCompleted();
        OnStartupSettingSubject.Dispose();
        AccentColorSubject.OnCompleted();
        AccentColorSubject.Dispose();
    }
}
