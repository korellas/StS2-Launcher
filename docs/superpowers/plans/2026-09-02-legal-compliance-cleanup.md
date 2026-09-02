# Legal Compliance Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace avoidable game-derived branding, add accurate in-app legal/privacy information and translation attribution, and package the notices required by the APK without changing the launcher's Steam or game runtime behavior.

**Architecture:** Keep the existing programmatic Godot UI and Android asset pipeline. Replace the distributable art with the maintainer-supplied originals, preserve the accepted hand-drawn logo with an alpha-only edge feather and adjacent non-affiliation text, read one canonical offline notice from Android assets through the existing Godot-to-Activity bridge, and record which translation provider produced each result so Google attribution appears only for ML Kit output. Full license texts remain ordinary files under `android/assets/legal/`, which makes the APK and release bundle consume the same canonical copies.

**Tech Stack:** C#/.NET 9, Godot 4.5.1, Java/Android, Gradle, Bash, PNG/ffmpeg, Markdown/plain-text notices.

---

## File map

- `art/app-icon.png`, `art/launcher-background.png`, `art/splash.png`, `art/launcher-logo.png`: independent editable art sources supplied by the maintainer.
- `art/README.md`: asset provenance, allowed use, and source-to-derived mapping.
- `android/assets/launcher_bg.png`, `android/res/drawable-nodpi/splash_art.png`, `android/res/mipmap-*/icon*.png`: runtime derivatives of the new sources.
- `src/STS2Mobile/Launcher/LauncherView.cs`: accepted logo, fallback text title, non-affiliation statement, and Legal & Privacy menu entry.
- `src/STS2Mobile/Launcher/Sections/LegalInfoView.cs`: scrollable offline notice body.
- `src/STS2Mobile/Launcher/LegalNotices.cs`: narrow bridge for reading `legal/NOTICE.txt` from the APK.
- `src/STS2Mobile/Launcher/Localization.cs`: bilingual UI labels and required translation wording.
- `src/STS2Mobile/Launcher/TranslationBridge.cs`: exposes the provider selected by Android.
- `src/STS2Mobile/Launcher/Sections/NewsArticleView.cs`: ML Kit trigger wording and result-adjacent Google badge.
- `src/STS2Mobile/Launcher/Components/LauncherTheme.cs`: loads the accepted background and hand-drawn logo.
- `android/src/com/game/sts2launcher/GodotApp.java`: reads bundled notices and identifies platform versus ML Kit translation results.
- `android/assets/google-translate-attribution.png`: unmodified official Google attribution badge.
- `android/assets/legal/`: canonical packaged license and notice texts.
- `README.md`, `THIRD_PARTY_LICENSES.md`, `PRIVACY.md`, `DISTRIBUTION.md`: source-license scope, data handling, component notices, and unresolved external permission boundaries.
- `scripts/release.sh`: attaches the canonical legal directory as a release archive alongside the APK.

### Task 1: Replace distributable branding assets

**Files:**
- Modify: `art/app-icon.png`
- Modify: `art/launcher-background.png`
- Modify: `art/splash.png`
- Modify: `art/launcher-logo.png`
- Delete: `art/reference/in-game-dialog.png`
- Delete: `art/reference/in-game-main-menu.png`
- Delete: `art/reference/in-game-settings.png`
- Modify: `art/README.md`
- Modify: `android/assets/launcher_bg.png`
- Modify: `android/assets/launcher_logo.png`
- Modify: `android/res/drawable-nodpi/splash_art.png`
- Modify: `android/res/mipmap-mdpi/icon.png`
- Modify: `android/res/mipmap-mdpi/icon_foreground.png`
- Modify: `android/res/mipmap-hdpi/icon.png`
- Modify: `android/res/mipmap-hdpi/icon_foreground.png`
- Modify: `android/res/mipmap-xhdpi/icon.png`
- Modify: `android/res/mipmap-xhdpi/icon_foreground.png`
- Modify: `android/res/mipmap-xxhdpi/icon.png`
- Modify: `android/res/mipmap-xxhdpi/icon_foreground.png`
- Modify: `android/res/mipmap-xxxhdpi/icon.png`
- Modify: `android/res/mipmap-xxxhdpi/icon_foreground.png`

- [ ] **Step 1: Preserve the accepted upload roles**

Use upload 1 only as the icon source and upload 2 as both the launcher background and splash source. Do not copy the earlier third upload into the repository. Use the later 1536x1024 transparent upload as the replacement logo, preserving its exact lettering and applying only a one-pixel-class alpha feather to its outer edge.

- [ ] **Step 2: Normalize the icon source without distorting it**

Run:

```bash
ffmpeg -y \
  -i /Users/korellas/.paseo/uploads/upload_ee64db5f-3881-439a-ae3b-da1dcaf2719a/file_00000000f31c82119c685ae8e21f78a6.png \
  -vf "scale=1278:1278:force_original_aspect_ratio=decrease:flags=lanczos,pad=1278:1278:(ow-iw)/2:(oh-ih)/2:color=0x00000000" \
  -frames:v 1 -pix_fmt rgba art/app-icon.png
```

Expected: `art/app-icon.png` is square, retains alpha, and does not crop the drawing.

- [ ] **Step 3: Install the independent landscape and logo sources**

Copy upload 2 byte-for-byte to `art/launcher-background.png`, `art/splash.png`, `android/assets/launcher_bg.png`, and `android/res/drawable-nodpi/splash_art.png`.

Create `art/launcher-logo.png` from the later upload with RGB unchanged and only its alpha channel blurred with `gblur=sigma=0.7`; copy that result byte-for-byte to `android/assets/launcher_logo.png`:

```bash
ffmpeg -y \
  -i /Users/korellas/.paseo/uploads/upload_307d24d7-6e77-45d1-a994-25d65caf7506/file_000000009a148207a4517984ba67c35f.png \
  -filter_complex "[0:v]format=rgba,split[color][alpha_source];[alpha_source]alphaextract,gblur=sigma=0.7[soft_alpha];[color][soft_alpha]alphamerge" \
  -frames:v 1 -pix_fmt rgba art/launcher-logo.png
```

- [ ] **Step 4: Regenerate legacy and adaptive icons**

Generate the established Android density sizes with aspect-preserving scale and transparent padding. Legacy content occupies 88% of its canvas; adaptive foreground content occupies 62%.

```text
density  legacy canvas/content  adaptive canvas/content
mdpi     48/42                  108/67
hdpi     72/63                  162/100
xhdpi    96/84                  216/134
xxhdpi   144/127                324/201
xxxhdpi  192/169                432/268
```

For each row, run the following form with the row's dimensions:

```bash
ffmpeg -y -i art/app-icon.png \
  -vf "scale=42:42:force_original_aspect_ratio=decrease:flags=lanczos,pad=48:48:(ow-iw)/2:(oh-ih)/2:color=0x00000000" \
  -frames:v 1 -pix_fmt rgba android/res/mipmap-mdpi/icon.png
ffmpeg -y -i art/app-icon.png \
  -vf "scale=67:67:force_original_aspect_ratio=decrease:flags=lanczos,pad=108:108:(ow-iw)/2:(oh-ih)/2:color=0x00000000" \
  -frames:v 1 -pix_fmt rgba android/res/mipmap-mdpi/icon_foreground.png
```

Expected: each output exactly matches its canvas dimensions and retains alpha.

- [ ] **Step 5: Remove legacy branding and screenshots**

Delete the exact screenshot files listed above. Confirm `git ls-files art/reference` returns no paths after staging. Confirm the two accepted logo paths remain.

- [ ] **Step 6: Replace the art documentation**

Document these verified facts only:

```markdown
# Art sources

The app icon, landscape artwork, and hand-drawn launcher logo were newly generated and supplied by the
repository maintainer on 2026-09-02 for this cleanup. The third uploaded image
was rejected in favor of the later hand-drawn replacement and is not distributed.

`app-icon.png` is the source for Android launcher icons.
`launcher-background.png` is copied to `android/assets/launcher_bg.png`.
`splash.png` is copied to `android/res/drawable-nodpi/splash_art.png`.
`launcher-logo.png` is copied to `android/assets/launcher_logo.png` after an
alpha-only edge feather; its game name is used only to identify compatibility.

Before public redistribution, the publisher must retain the generator/service
terms or other authorship record that establishes redistribution rights. These
files contain no license grant for Mega Crit, Valve, Google, FMOD, or other
third-party marks.
```

- [ ] **Step 7: Inspect the image delta**

Run `sips -g pixelWidth -g pixelHeight -g hasAlpha` on every source and derived output. Expected: the icon files have alpha, landscape outputs are `1448x1086`, and no removed path remains.

- [ ] **Step 8: Commit the asset boundary**

```bash
git add art android/assets/launcher_bg.png android/res/drawable-nodpi/splash_art.png android/res/mipmap-*
git commit -m "art: replace legacy game branding"
```

### Task 2: Pair the replacement logo with a neutral app identity

**Files:**
- Modify: `android/AndroidManifest.xml`
- Modify: `android/src/com/game/sts2launcher/GodotApp.java`
- Modify: `src/STS2Mobile/Launcher/Components/LauncherTheme.cs`
- Modify: `src/STS2Mobile/Launcher/LauncherView.cs`
- Modify: `src/STS2Mobile/Launcher/Localization.cs`

- [ ] **Step 1: Preserve only the accepted logo extraction and loader**

Keep the existing `extractAssetFile("launcher_logo.png", "launcher_logo.png")` call and `LauncherTheme.LoadLogo`; they now consume only the accepted replacement. Leave `LoadKeyArt` and its fallback unchanged.

- [ ] **Step 2: Add exact localized identity strings**

Use these messages:

```csharp
// Korean
["LAUNCHER_TITLE"] = "비공식 StS2 런처",
["UNOFFICIAL_NOTICE"] = "Mega Crit 또는 Valve와 제휴하거나 보증받은 앱이 아닙니다.",

// English
["LAUNCHER_TITLE"] = "Unofficial StS2 Launcher",
["UNOFFICIAL_NOTICE"] = "Not affiliated with or endorsed by Mega Crit or Valve.",
```

- [ ] **Step 3: Place the non-affiliation statement beside the logo**

Keep the `LauncherTheme.LoadLogo()` block and add the following plain label immediately after it. It must not call `ApplyGameFont` or `ApplyFont`:

```csharp
var unofficial = new Label
{
    Text = Localization.Tr("UNOFFICIAL_NOTICE"),
    HorizontalAlignment = HorizontalAlignment.Center,
    AutowrapMode = TextServer.AutowrapMode.WordSmart,
};
unofficial.AddThemeFontSizeOverride("font_size", (int)(12 * scale));
unofficial.AddThemeColorOverride("font_color", LauncherTheme.Dim);
menu.AddChild(unofficial);
menu.AddChild(new Control { CustomMinimumSize = new Vector2(0, (int)(20 * scale)) });
```

- [ ] **Step 4: Rename the Android-visible label**

Set both `<application android:label>` and `.GodotApp`'s `android:label` to `Unofficial StS2 Launcher`. Do not change the application ID.

- [ ] **Step 5: Compile-check the source boundary**

Run `dotnet build src/STS2Mobile/STS2Mobile.csproj --no-restore` after the required proprietary reference files are present. Expected: no C# compile errors. If those references are absent, record that constraint and run CSharpier plus direct symbol inspection instead.

- [ ] **Step 6: Commit the neutral identity**

```bash
git add android/AndroidManifest.xml android/src/com/game/sts2launcher/GodotApp.java src/STS2Mobile/Launcher
git commit -m "ui: identify launcher as unofficial"
```

### Task 3: Package canonical third-party notices and expose them offline

**Files:**
- Create: `android/assets/legal/NOTICE.txt`
- Create: `android/assets/legal/MIT.txt`
- Create: `android/assets/legal/APACHE-2.0.txt`
- Create: `android/assets/legal/LGPL-2.1.txt`
- Create: `android/assets/legal/BSD-3-CLAUSE.txt`
- Create: `android/assets/legal/SPINE-RUNTIMES-LICENSE.txt`
- Create: `android/assets/legal/GODOT-COPYRIGHT.txt`
- Create: `android/assets/legal/DOTNET-THIRD-PARTY-NOTICES.txt`
- Copy: `android/assets/legal/SYSTEM.IO.HASHING-THIRD-PARTY-NOTICES.txt`
- Copy: `android/assets/launcher_font-OFL.txt` to `android/assets/legal/OFL-1.1.txt`
- Create: `android/assets/legal/FMOD-NOTICE.txt`
- Create: `android/assets/legal/GOOGLE-ML-KIT-NOTICE.txt`
- Create: `src/STS2Mobile/Launcher/LegalNotices.cs`
- Create: `src/STS2Mobile/Launcher/Sections/LegalInfoView.cs`
- Modify: `android/src/com/game/sts2launcher/GodotApp.java`
- Modify: `src/STS2Mobile/Launcher/LauncherView.cs`
- Modify: `src/STS2Mobile/Launcher/Localization.cs`

- [ ] **Step 1: Add unmodified canonical license texts**

Use the license text carried by each dependency or its official upstream source. `SPINE-RUNTIMES-LICENSE.txt` must contain the current Spine Runtimes notice verbatim, including the redistribution and per-user-license language. `FMOD-NOTICE.txt` must include the required credit, the current EULA URL, and the Xiph BSD-3-Clause copyright/conditions for Ogg, Vorbis, Tremor, FLAC, and Opus. `GODOT-COPYRIGHT.txt` must come from the exact Godot engine version used by the build, and the .NET notice must match the runtime pack used by the APK.

Do not state that merely packaging these texts grants a Spine, FMOD, Steam, or game license.

- [ ] **Step 2: Write the offline notice summary**

`NOTICE.txt` must contain, in Korean and English:

```text
Unofficial StS2 Launcher
Not affiliated with or endorsed by Mega Crit or Valve. StS2 identifies compatibility only.

Steam account data
The launcher sends credentials and tokens directly to Steam through SteamKit2.
Refresh tokens are encrypted at rest with Android Keystore and stored on this device.
The publisher of this launcher does not operate an account-collection backend.

On-device translation
Google ML Kit may download language models. Text translated by ML Kit is processed on device.
Google's SDK may collect SDK diagnostics or usage information under Google's applicable terms.
Google-powered results are identified in the article view. The required Google translation disclaimer is in GOOGLE-ML-KIT-NOTICE.txt.

Licenses
Full third-party notices are packaged in this APK under assets/legal/ and distributed beside release APKs.
The repository MIT license covers only material its contributors have the right to license.

Distribution warning
These notices do not supply permission for an unofficial Android port, use of Steam access methods, or Spine runtime redistribution. A publisher must resolve those rights separately.
```

The Korean section must convey the same facts without adding a broader legal conclusion.

- [ ] **Step 3: Add an Android asset reader**

Add `readBundledLegalNotices` to `GodotApp` using `InputStream`, `ByteArrayOutputStream`, and UTF-8. Return an empty string and log a warning on failure; do not throw into Godot.

```java
public String readBundledLegalNotices() {
    try (InputStream in = getAssets().open("legal/NOTICE.txt");
            ByteArrayOutputStream out = new ByteArrayOutputStream()) {
        byte[] buffer = new byte[8192];
        int count;
        while ((count = in.read(buffer)) >= 0) {
            out.write(buffer, 0, count);
        }
        return new String(out.toByteArray(), StandardCharsets.UTF_8);
    } catch (IOException e) {
        Log.w(TAG, "Could not read bundled legal notices", e);
        return "";
    }
}
```

- [ ] **Step 4: Add the narrow C# bridge**

```csharp
public static class LegalNotices
{
    public static string Load()
    {
        try
        {
            var text = (string)LauncherModel.GetGodotApp()?.Call("readBundledLegalNotices");
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Legal] notice load failed: {ex.Message}");
        }

        return Localization.Tr("LEGAL_LOAD_FAILED");
    }
}
```

- [ ] **Step 5: Add the offline legal view**

`LegalInfoView` is a `VBoxContainer` containing a selectable, wrapping `RichTextLabel` with `Text = LegalNotices.Load()`, no network fetch, and the existing launcher font/colors. Add localized strings `MENU_LEGAL`, `LEGAL_TITLE`, and `LEGAL_LOAD_FAILED`.

- [ ] **Step 6: Wire the legal submenu**

Create a `SubmenuOverlay(Localization.Tr("LEGAL_TITLE"), scale, heightRatio: 0.78f)`, add `LegalInfoView`, add it to the parent, and place a `MENU_LEGAL` entry before Quit. Its only signal opens the local overlay.

- [ ] **Step 7: Verify the packaged legal tree**

Run:

```bash
find android/assets/legal -maxdepth 1 -type f -print | sort
rg -n "Mega Crit|Valve|SteamKit2|FMOD|Spine|ML Kit|Godot|Mono|Cinzel" android/assets/legal/NOTICE.txt THIRD_PARTY_LICENSES.md
```

Expected: every named packaged component has a canonical owner/license entry; no text says the APK is legally cleared.

- [ ] **Step 8: Commit notices and the offline view**

```bash
git add android/assets/legal android/src/com/game/sts2launcher/GodotApp.java src/STS2Mobile/Launcher
git commit -m "legal: bundle notices and offline disclosures"
```

### Task 4: Attribute only Google-powered translation results

**Files:**
- Create: `android/assets/google-translate-attribution.png`
- Modify: `android/src/com/game/sts2launcher/GodotApp.java`
- Modify: `src/STS2Mobile/Launcher/TranslationBridge.cs`
- Modify: `src/STS2Mobile/Launcher/Sections/NewsArticleView.cs`
- Modify: `src/STS2Mobile/Launcher/Localization.cs`

- [ ] **Step 1: Add the unmodified official badge**

Download Google's official attribution archive from `https://docs.cloud.google.com/static/translate/images/google-translate-attribution.zip` and copy `png/white-regular@3x.png` unchanged to `android/assets/google-translate-attribution.png`. Record its source URL in `GOOGLE-ML-KIT-NOTICE.txt`.

Add this exact startup extraction beside the existing launcher background/font extraction so Godot can load the badge from `OS.GetDataDir()`:

```java
extractAssetFile("google-translate-attribution.png", "google-translate-attribution.png");
```

- [ ] **Step 2: Track the result provider in Android**

Add `translationProvider`, clear it at the start of every request, set it to `platform` only on a successful platform result, and set it to `google_mlkit` only on a successful ML Kit result. Expose it through `getTranslationProvider()`.

```java
private volatile String translationProvider = "";

public String getTranslationProvider() {
    return translationProvider;
}
```

- [ ] **Step 3: Expose the provider through `TranslationBridge`**

```csharp
public bool ResultUsesGoogleMlKit()
{
    try
    {
        return (string)_godotApp?.Call("getTranslationProvider") == "google_mlkit";
    }
    catch
    {
        return false;
    }
}
```

- [ ] **Step 4: Add provider-aware article state**

Reset `_usedGoogleMlKit` when an article is shown and when translation starts. After each successful paragraph result, OR in `ResultUsesGoogleMlKit()`. Load `google-translate-attribution.png` from `OS.GetDataDir()` using the same PNG-buffer pattern as `LauncherView.LoadFmodLogo`. Show the official badge adjacent to the translated body only when `_showingTranslation && _usedGoogleMlKit`; hide it for the original and for platform-translated results. If the badge cannot be loaded, fail the ML Kit translation result closed by showing the original text and logging the missing required attribution instead of displaying an unattributed Google result.

The trigger string must disclose the possible provider before the request:

```csharp
["NEWS_TRANSLATE"] = "Translate (Google if needed)",
["NEWS_TRANSLATE_GOOGLE"] = "Translate with Google",
```

Use `NEWS_TRANSLATE_GOOGLE` when `translationCapabilities()` reports ML Kit as the selected route; otherwise use the generic disclosure. Do not label a platform-only result as Google-translated.

- [ ] **Step 5: Make the required disclaimer available**

Place Google's required all-caps translation warranty disclaimer verbatim in both `GOOGLE-ML-KIT-NOTICE.txt` and the Google section of `NOTICE.txt`, which is the local Legal & Privacy view's body. Do not paraphrase that required text in the UI.

- [ ] **Step 6: Compile and inspect both provider branches**

Search for every assignment to `translationState = "done"`; each must assign the provider first. Inspect `NewsArticleView` to confirm that original/platform output cannot make the Google badge visible.

- [ ] **Step 7: Commit translation attribution**

```bash
git add android/assets/google-translate-attribution.png android/assets/legal/GOOGLE-ML-KIT-NOTICE.txt android/src/com/game/sts2launcher/GodotApp.java src/STS2Mobile/Launcher
git commit -m "legal: attribute ML Kit translations"
```

### Task 5: Correct source-license, privacy, and distribution claims

**Files:**
- Modify: `README.md`
- Modify: `THIRD_PARTY_LICENSES.md`
- Create: `PRIVACY.md`
- Create: `DISTRIBUTION.md`
- Modify: `UPSTREAM-README.md`

- [ ] **Step 1: Narrow the README license claim**

Replace “MIT, same as upstream” with:

```markdown
## License and third-party rights

The MIT License applies only to launcher code and assets that contributors have
the right to license. It does not license Slay the Spire 2, Steam, FMOD, Spine,
Google ML Kit, or other third-party material. See
[THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md), [PRIVACY.md](PRIVACY.md), and
[DISTRIBUTION.md](DISTRIBUTION.md).
```

Place the non-affiliation statement near the first project description, not only in the license section.

- [ ] **Step 2: Make `THIRD_PARTY_LICENSES.md` the component index**

For each actually packaged component, identify the copyright owner, license/terms, upstream source, and matching file in `android/assets/legal/`. Include the SteamKit2 transitive assemblies copied by `scripts/build.sh`, AndroidX/Kotlin/ML Kit dependencies, Godot/.NET, FMOD codecs, Spine Runtimes, and Cinzel. Avoid claiming that a source URL substitutes for a redistribution right.

- [ ] **Step 3: Add the privacy disclosure**

`PRIVACY.md` must describe only observed flows: Steam credentials/tokens sent directly to Steam; refresh tokens encrypted with Android Keystore and stored locally; Steam news/update network calls; ML Kit model downloads and on-device text processing; potential Google SDK diagnostics/usage collection under Google's terms; and no launcher-operator analytics/backend introduced by this project. Link Valve and Google privacy terms without claiming control over their processing.

- [ ] **Step 4: Add the distribution boundary**

`DISTRIBUTION.md` must state that non-commercial distribution and absence of bundled game files do not independently clear an APK. List the unresolved permission boundaries: Mega Crit's mod permission is conditioned on platform terms and does not expressly authorize this Android port; Valve's subscriber terms restrict modifying, protocol emulation, and Steam content use except where permitted; Spine requires a valid integration/distribution basis; FMOD requires the applicable license/registration and startup attribution. Recommend source-only publication until the publisher documents those permissions.

- [ ] **Step 5: Correct the archived upstream README claim**

Preserve it as historical documentation but add a short banner saying its legal statements are upstream's snapshot and that current distribution guidance lives in `DISTRIBUTION.md`. Do not rewrite the archived body.

- [ ] **Step 6: Review claims against primary sources**

Verify the documents against Mega Crit's Content Policy, the current Steam Subscriber Agreement, the Spine Runtimes License Agreement, FMOD's legal/attribution pages, and Google's ML Kit/Cloud Translation attribution requirements. Delete or narrow any statement that goes beyond those sources.

- [ ] **Step 7: Commit documentation**

```bash
git add README.md UPSTREAM-README.md THIRD_PARTY_LICENSES.md PRIVACY.md DISTRIBUTION.md
git commit -m "docs: clarify source and APK distribution rights"
```

### Task 6: Attach the same notices to releases

**Files:**
- Modify: `scripts/release.sh`

- [ ] **Step 1: Create a deterministic notice archive after the APK build**

Add:

```bash
LEGAL_ARCHIVE="$APK_DIR/StS2Launcher-v$VERSION-legal-notices.zip"
(
    cd "$ROOT/android/assets"
    COPYFILE_DISABLE=1 zip -qr "$LEGAL_ARCHIVE" legal
)
```

The archive is generated from `android/assets/legal/`; do not maintain another hand-edited release copy.

- [ ] **Step 2: Upload the notice archive with the APK**

Change the release argument construction to:

```bash
GH_ARGS=(release create "$TAG" "$APK_PATH" "$LEGAL_ARCHIVE" --title "$TAG" --notes-file "$NOTES_FILE")
```

- [ ] **Step 3: Syntax-check without publishing**

Run `bash -n scripts/release.sh`. Expected: exit 0. Do not execute the release script, create a tag, push, or mutate GitHub.

- [ ] **Step 4: Commit release packaging**

```bash
git add scripts/release.sh
git commit -m "build: attach legal notices to releases"
```

### Task 7: Validate the complete local result

**Files:**
- Inspect: all task-owned changes

- [ ] **Step 1: Format C# and check shell syntax**

Run:

```bash
~/.dotnet/tools/csharpier format src/STS2Mobile
bash -n scripts/build.sh
bash -n scripts/release.sh
```

Expected: formatting completes and both syntax checks exit 0.

- [ ] **Step 2: Compile where proprietary references permit**

Run `dotnet publish -c Release src/STS2Mobile/STS2Mobile.csproj`. If `upstream/godot-export` is absent, report the missing proprietary input and do not add stubs or dependencies to bypass it.

- [ ] **Step 3: Build the APK without a version bump**

Run `bash scripts/build.sh --no-bump` only when the existing FMOD, Godot, Spine, Android SDK, and signing inputs are available. Do not change product code to hide an environment failure.

- [ ] **Step 4: Inspect APK contents if the build succeeds**

Run:

```bash
unzip -l android/build/outputs/apk/mono/release/StS2Launcher-v*.apk | rg 'assets/legal/|launcher_bg|launcher_logo|google-translate-attribution|splash_art|mipmap-.*/icon'
```

Expected: canonical legal files and accepted replacement assets, including `launcher_logo.png`, are present; reference screenshots are absent.

- [ ] **Step 5: Check the source tree for rejected material**

Run:

```bash
git ls-files | rg 'art/reference' && exit 1 || true
git diff --check
git status --short
```

Expected: the rejected screenshots are absent, the accepted replacement logo is present at its two documented paths, `git diff --check` is clean, and every remaining change belongs to this plan.

- [ ] **Step 6: Review acceptance criteria and external blockers**

Confirm the app is explicitly unofficial, the accepted alpha-feathered logo is present, only accepted art is distributed, legal/privacy text is accessible offline, Google attribution is provider-correct, and release notices share one canonical source. Record that Mega Crit/Valve/Spine permission and the maintainer's generator/service redistribution terms remain external evidence gates; do not label the APK “legally cleared.”
