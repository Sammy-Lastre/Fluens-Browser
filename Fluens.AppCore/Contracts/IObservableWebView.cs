using Fluens.AppCore.Helpers;

namespace Fluens.AppCore.Contracts;

public interface IObservableWebView : IDisposable
{
    IObservable<string> DocumentTitle { get; }
    IObservable<string> FaviconUrl { get; }
    IObservable<bool> IsNavigating { get; }
    IObservable<Uri> Url { get; }
    IObservable<NewTabRequest> OpenNewTab { get; }
    IObservable<ShortcutMessage> KeyboardShortcuts { get; }
    void GoBack();
    void GoForward();
    void StopNavigation();
    void Refresh();
    Task NavigateToUrlAsync(Uri url);
    Task ActivateAsync();

    Uri? Source { get; }
}