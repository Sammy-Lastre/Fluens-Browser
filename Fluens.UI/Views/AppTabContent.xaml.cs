using CommunityToolkit.WinUI;
using DynamicData;
using Fluens.AppCore.Helpers;
using Fluens.AppCore.ViewModels;
using Fluens.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Windows.System;
using Windows.UI;

namespace Fluens.UI.Views;

public partial class ReactiveAppTab : ReactiveUserControl<AppTabViewModel>;
public sealed partial class AppTabContent : ReactiveAppTab, IDisposable
{
    private readonly CompositeDisposable Disposables = [];
    public AppTabContent()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Url)
                .Where(url => url == Constants.AboutBlankUri || url is null)
                .Subscribe(_ => SearchBar.DispatcherQueue.TryEnqueue(() => SearchBar.Focus(FocusState.Programmatic)))
                .DisposeWith(d);
        });

        this.WhenAnyValue(x => x.ViewModel)
            .WhereNotNull()
            .Subscribe(vm => vm.ObservableWebView = new ObservableWebView(WebView))
            .DisposeWith(Disposables);

        Observable.FromEventPattern<WebView2, CoreWebView2NavigationCompletedEventArgs>(WebView, nameof(WebView.NavigationCompleted))
                .Subscribe(ep => WebView.Focus(FocusState.Programmatic))
                .DisposeWith(Disposables);

        this.Bind(ViewModel, vm => vm.SearchBarText, v => v.SearchBar.Text).DisposeWith(Disposables);

        this.Bind(ViewModel, vm => vm.SettingsDialogIsOpen, v => v.SettingsPopup.IsOpen).DisposeWith(Disposables);
        this.OneWayBind(ViewModel, vm => vm.SettingsDialogIsOpen, v => v.ConfigBtn.IsChecked).DisposeWith(Disposables);
        this.OneWayBind(ViewModel, vm => vm.CanStop, v => v.StopBtn.Visibility).DisposeWith(Disposables);
        this.OneWayBind(ViewModel, vm => vm.CanRefresh, v => v.RefreshBtn.Visibility).DisposeWith(Disposables);

        this.BindCommand(ViewModel, vm => vm.GoBack, v => v.GoBackBtn).DisposeWith(Disposables);
        this.BindCommand(ViewModel, vm => vm.GoForward, v => v.GoForwardBtn).DisposeWith(Disposables);
        this.BindCommand(ViewModel, vm => vm.Refresh, v => v.RefreshBtn).DisposeWith(Disposables);
        this.BindCommand(ViewModel, vm => vm.Stop, v => v.StopBtn).DisposeWith(Disposables);
        this.BindCommand(ViewModel, vm => vm.ToggleSettingsDialogCommand, v => v.ConfigBtn).DisposeWith(Disposables);

        Observable.FromEventPattern(SearchBar, nameof(SearchBar.Loaded))
            .SelectMany(_ => Observable.FromEventPattern<KeyRoutedEventArgs>(SearchBar.FindDescendant<TextBox>()!, nameof(KeyDown)))
            .Subscribe(ep => DetectEnterKey(ep.EventArgs.Key))
            .DisposeWith(Disposables);

        Observable.FromEventPattern<RoutedEventArgs>(SearchBar, nameof(SearchBar.GotFocus))
                .Subscribe(_ => SearchBar.FindDescendant<TextBox>()!.SelectAll())
                .DisposeWith(Disposables);

        Observable.FromEventPattern<SizeChangedEventArgs>(WebView, nameof(WebView.SizeChanged))
            .Subscribe(ep =>
            {
                SettingsDialogContent.Width = WebView.ActualWidth;
                SettingsDialogContent.Height = WebView.ActualHeight;
            })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.SettingsView.ViewModel.HistoryPageViewModel)
            .WhereNotNull()
            .Select(vm => vm.Entries.Connect()
                .MergeMany(entry => entry.OpenUrl.Select(_ => entry.Url))
            )
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async url =>
            {
                await ViewModel!.ObservableWebView!.NavigateToUrlAsync(url);
                ViewModel!.SettingsDialogIsOpen = false;
            })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.ViewModel!.Url)
            .Select(url => url == Constants.AboutBlankUri)
            .Subscribe(isBlank =>
            {
                Color brush;

                if (isBlank)
                {
                    brush = (Color)Application.Current.Resources["SolidBackgroundFillColorTertiary"];
                }
                else
                {
                    brush = (Color)Application.Current.Resources["SystemChromeWhiteColor"];
                }
                WebView.DefaultBackgroundColor = brush;
            })
            .DisposeWith(Disposables);
    }

    private void DetectEnterKey(VirtualKey key)
    {
        if (key == VirtualKey.Enter)
        {
            SettingsPopup.IsOpen = false;
            ViewModel?.NavigateToInputCommand.Execute().Subscribe();
            WebView.Focus(FocusState.Programmatic);
        }
    }

    public void Dispose()
    {
        Disposables.Dispose();
        ViewModel?.Dispose();
    }
}