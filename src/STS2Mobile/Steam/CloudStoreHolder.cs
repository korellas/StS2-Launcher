namespace STS2Mobile.Steam;

// Holds the live cloud store so SteamKit2CloudSaveStore does not have to hold
// itself. It used to expose `static SteamKit2CloudSaveStore Instance`, a static
// of its own type, which makes laying the class out require the class — and that
// class is the one failing vtable setup on the device while a probe sharing its
// interfaces loads cleanly. Keeping the reference outside removes the recursion.
internal static class CloudStoreHolder
{
    internal static SteamKit2CloudSaveStore Current { get; set; }
}
