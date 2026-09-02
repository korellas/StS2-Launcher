# Art sources

The app icon, landscape artwork, and hand-drawn launcher logo were newly
generated and supplied by the repository maintainer on 2026-09-02 for this
cleanup. An earlier logo candidate was rejected and is not distributed.

| File | Consumed as | Processing |
|---|---|---|
| `app-icon.png` | `android/res/mipmap-*/icon.png`, `icon_foreground.png` | Padded to a square without distortion, then scaled with transparent margins for legacy and adaptive icons. |
| `launcher-background.png` | `android/assets/launcher_bg.png` | Copied as-is. |
| `splash.png` | `android/res/drawable-nodpi/splash_art.png` | Copied as-is. |
| `launcher-logo.png` | `android/assets/launcher_logo.png` | RGB is preserved; only the outer alpha edge is feathered by about one pixel. |

The game name in the launcher logo identifies compatibility. It does not imply
affiliation with or endorsement by Mega Crit or Valve.

Before public redistribution, the publisher should retain the generator or
service terms, prompt/history, or another authorship record establishing the
right to redistribute these files. Nothing in this directory grants rights to
third-party names, marks, software, or game content.
