using Microsoft.Web.WebView2.Core;

namespace Fluens.UI.Services.AdBlocking;

internal interface IAdBlockService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    bool ShouldBlock(Uri requestUri, CoreWebView2WebResourceContext resourceContext);
}
