# Art sources

Working files for the launcher's artwork. Nothing here is read at runtime — each
file is processed into the location the app actually loads from, so these stay as
the editable originals.

| File | Consumed as | Processing |
|---|---|---|
| `launcher-logo.png` | `android/assets/launcher_logo.png` | Transparent margin trimmed. Edges are feathered by about a pixel so the black outline blends into the artwork behind it rather than ending abruptly. |
| `launcher-background.png` | `android/assets/launcher_bg.png` | Copied as-is. |
| `splash.png` | `android/res/drawable-nodpi/splash_art.png` | Copied as-is; drawn centred over `@color/splash_background`. |
| `app-icon.png` | `android/res/mipmap-*/icon.png`, `icon_foreground.png` | Scaled per density: 88% coverage for the legacy icon, 62% for the adaptive foreground so masking cannot clip the card. |

`reference/` holds screenshots of the game's own menus, kept as the target the
launcher's styling is matched against.

Assets are extracted from the APK on first run, and re-extracted whenever the
version code changes — see `GodotApp.extractAssetFile`.
