# Unofficial StS2 Launcher

This is an unofficial community Android launcher derived from
[Ekyso/StS2-Launcher](https://github.com/Ekyso/StS2-Launcher). It is not
affiliated with or endorsed by Mega Crit or Valve. “StS2” and the game name are
used only to identify compatibility.

The launcher authenticates an owning Steam account, downloads that account's
game files from Steam, and applies mobile runtime adaptations. Game files are
not included in this source repository or the launcher APK.

## Distribution status

The absence of game files and the project's non-commercial status do not by
themselves clear public APK distribution. Do not treat an existing release or
buildable source tree as evidence that the required permissions have been
obtained. See [DISTRIBUTION.md](DISTRIBUTION.md) before publishing an APK.

No existing remote release is changed by the legal-compliance work in this
branch.

## Building

The build requires locally supplied game files and separately licensed Godot,
FMOD, and Spine-related inputs. The original technical instructions are
preserved in [UPSTREAM-README.md](UPSTREAM-README.md). The release build packages
the notices in `android/assets/legal/` into the APK.

## Account and translation data

Steam passwords are used for the interactive Steam login and are not saved.
The account name, refresh token, and Steam Guard data are encrypted with Android
Keystore and stored on the device. Google ML Kit translation runs on device but
the SDK sends diagnostics and usage metrics described by Google. Details are in
[PRIVACY.md](PRIVACY.md).

## Credits

The launcher is derived from work by [Ekyso](https://github.com/Ekyso) and other
contributors. Component-level notices are listed in
[THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md).

## License and third-party rights

The [MIT License](LICENSE) applies only to launcher code and assets that
contributors have the right to license. It does not license Slay the Spire 2,
Steam, FMOD, Spine, Google ML Kit, or other third-party material. See
[THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md),
[PRIVACY.md](PRIVACY.md), and [DISTRIBUTION.md](DISTRIBUTION.md).
