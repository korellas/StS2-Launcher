# CLAUDE.md — StS2-Launcher (korellas fork)

Instructions for Claude Code when working in this repository.

## Releases

**MANDATORY**: all releases go through `scripts/release.sh`. Do **not** manually run gradle, tag, or `gh release create` separately. The script is the single source of truth for the pipeline.

### Why a local pipeline (no CI/CD)

- The runtime uses a **custom Godot fork** built by Ekyso that is not published. Stock Godot templates cannot build this project.
- **FMOD Studio SDK** (`android/assets/FMOD_LOGOS/`, vendored `.aar`) is licensed and must not be uploaded to a public CI runner.
- Result: GitHub Actions cannot reproduce the build. Releases must be cut from a developer machine that already has the custom Godot artifacts + FMOD SDK in place.

### Command

```bash
bash scripts/release.sh <version> [--debug] [--draft] [--prerelease] [--notes "text"]
```

Examples:
- `bash scripts/release.sh 0.4.0 --notes "Fix cloud sync freeze"` — production release.
- `bash scripts/release.sh 0.4.0-rc1 --prerelease --notes "testing"` — prerelease snapshot.

### What the script does

1. Bumps `android/gradle.properties`: `export_version_name` to `<version>`, `export_version_code` +1.
2. Runs `scripts/build.sh` (Release unless `--debug`) → produces `android/build/outputs/apk/mono/release/StS2Launcher-v<version>.apk`.
3. `git add android/gradle.properties && git commit -m "Release v<version>"`.
4. `git tag -a v<version>` + pushes branch and tag to `origin`.
5. `gh release create v<version> <apk> --title v<version> --notes-file <...>` — includes commit log since the previous tag.

### Pre-flight

- `git status` must be **clean** (the script exits otherwise — commit or stash first).
- `gh auth status` must succeed.
- `~/.gradle/gradle.properties` must define `release_keystore_password` and `release_keystore_alias`; `android/sts2.keystore` must exist.
- Java + Android SDK + NDK env: user's `~/.zprofile` sets `JAVA_HOME` to Android Studio's JBR. If a Claude Code shell cannot find `java`, export it inline for the run:
  ```bash
  export JAVA_HOME="/Applications/Android Studio.app/Contents/jbr/Contents/Home"
  export PATH="$JAVA_HOME/bin:$PATH"
  export ANDROID_HOME="$HOME/Library/Android/sdk"
  export PATH="$PATH:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$ANDROID_HOME/cmdline-tools/latest/bin"
  ```

### In-app update check

`src/STS2Mobile/Steam/AppUpdateChecker.cs` hits `https://api.github.com/repos/korellas/StS2-Launcher/releases?per_page=10` and picks the newest non-draft release. **Prereleases count** — this is intentional because the fork iterates rapidly and sideloaded builds benefit from being notified of snapshots. If you want a "stable channel" later, switch the endpoint to `/releases/latest` and accept that prereleases stop producing update prompts.

### Recovery from a failed release run

If `release.sh` fails mid-run:
- After the `sed` bump but before commit: run `git checkout -- android/gradle.properties` to revert the uncommitted version bump, then re-invoke the script with the same version.
- After commit but before tag push: manually `git push origin HEAD v<version>`; then `gh release create ...` (mirror the script's final section).
- After release create: you're done, just verify with `gh release view v<version>`.

## Installing a built APK to a device

Wireless `adb` stays paired across sessions as `adb-<SERIAL>._adb-tls-connect._tcp`:

```bash
export PATH="$PATH:$HOME/Library/Android/sdk/platform-tools"
adb devices
adb install -r android/build/outputs/apk/mono/release/StS2Launcher-v<version>.apk
```

`-r` (not uninstall/reinstall) preserves the Android Keystore credentials so the Steam login persists across updates — only possible because we sign with the stable local `android/sts2.keystore`.

## Codebase entrypoints

- `src/STS2Mobile/ModEntry.cs` — Harmony mod bootstrap; hooked by the custom `gd_mono.cpp` via `coreclr_create_delegate`.
- `src/STS2Mobile/Patches/LauncherPatches.cs` — intercepts `NGame.GameStartupWrapper` to show the launcher UI before the game starts; manages the cloud sync deferred queue.
- `src/STS2Mobile/Launcher/LauncherUI.cs` → `LauncherView.cs` + `LauncherController.cs` + `LauncherModel.cs` — the pre-game Steam login + game-file download UI (MVC split).
- `android/src/com/game/sts2launcher/GodotApp.java` — Android `GodotActivity` subclass; handles SplashScreen API and exposes `getVersionName` for `AppUpdateChecker`.

## Do's and don'ts

- **Do** bundle all release-worthy changes before invoking `release.sh` — the release commit should only bump the version. (The script enforces a clean tree.)
- **Don't** push tags that are not created by `release.sh`. The script's tag message and the commit graph depend on it running end-to-end.
- **Don't** commit anything under `android/build/`, `android/assets/dotnet_bcl/`, `android/local.properties`, `*.apk`, `*.so`, `*.jar` (except `android/gradle/wrapper/gradle-wrapper.jar`), `*.keystore`, or `vendor/`. `.gitignore` covers these — don't add exceptions lightly.
- **Don't** edit the hardcoded releases URL in `AppUpdateChecker.cs` to point at a different fork without first updating this file.
