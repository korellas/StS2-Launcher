# Privacy Notice

This notice describes the data flows implemented by this source tree. It does
not describe independent processing performed by Steam, Google, Microsoft, a
browser, GitHub, or a distributor of a modified build.

## Steam account and game data

Interactive login sends the Steam account name, password, and any Steam Guard
response directly to Steam through SteamKit2. The password is used for that
login and is not written to the launcher's credential file.

After a successful login, the launcher stores the account name, Steam refresh
token, and Steam Guard data in `steam_credentials.enc` in app-private storage.
The file is encrypted with AES-GCM using a key held by Android Keystore. An
encrypted ownership marker is also stored locally. Clearing the app's data or
uninstalling it removes the app-private files; there is no project-operated
account-recovery service.

The launcher communicates with Steam to authenticate, verify ownership,
download depots, retrieve public news, and synchronize Steam Cloud saves.
Steam's processing is governed by Valve's agreements and privacy policy:
https://store.steampowered.com/privacy_agreement/

## Google ML Kit translation

Article text translated by Google ML Kit is processed on the device. Google
states that ML Kit does not send translation input or output to its servers.
The SDK may contact Google to download models, fixes, and hardware compatibility
information.

Google also states that ML Kit sends performance and utilization metrics. Its
Android disclosure guidance lists device and app information, installation or
device identifiers where applicable, performance metrics, API configuration,
input/output sizes, feature versions, event and error information, and the
configured source and destination languages for Translation. Google says this
data is encrypted in transit and used for diagnostics and usage analytics.

Sources:

- https://developers.google.com/ml-kit/terms
- https://developers.google.com/ml-kit/android-data-disclosure
- https://policies.google.com/privacy

## News pages and web translation

Opening an original announcement launches the URL in a Custom Tab or an in-app
WebView. The browser/WebView and destination site receive ordinary web request
data under their own privacy terms. If the WebView's Korean site-translation
toggle is used, the original page URL is sent to Microsoft's site-translation
service so it can fetch and translate that page.

## Updates and local networking

The update checker requests release metadata and APK files from GitHub. GitHub
receives ordinary HTTPS request data under its privacy statement:
https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement

LAN multiplayer discovery sends and receives broadcast packets on the local
network. The launcher also requests broad shared-storage access on supported
Android versions for local backups and the mods directory. Files selected by
those features remain on the device unless another explicit sync or sharing
operation sends them elsewhere.

## Project-operated collection

This project adds no operator analytics SDK, advertising SDK, account database,
or remote legal-text service. That statement does not cover third-party SDK
metrics described above or a distributor's modifications and infrastructure.
