namespace Fluens.UI.Services.AdBlocking;

[Flags]
internal enum AdBlockResourceType
{
    None = 0,
    Script = 1 << 0,
    Image = 1 << 1,
    StyleSheet = 1 << 2,
    XmlHttpRequest = 1 << 3,
    Media = 1 << 4,
    Font = 1 << 5,
    WebSocket = 1 << 6,
    Fetch = 1 << 7,
    Document = 1 << 8,
    Other = 1 << 9,
    Any = Script | Image | StyleSheet | XmlHttpRequest | Media | Font | WebSocket | Fetch | Document | Other
}
