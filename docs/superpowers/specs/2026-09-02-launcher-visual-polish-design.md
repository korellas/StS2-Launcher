# Launcher Visual Polish Design

## Goal

Improve the launcher hierarchy for v0.4.3 by making the replacement logo readable over the key art, making legal notices read like ordinary content instead of an editable field, and applying one coherent typography path across user-facing launcher controls.

## Logo

The existing transparent logo remains the sole edit target. Preserve its lettering, colors, composition, transparency, and the existing edge fade. Add a warm white or cream sticker-like outline whose visible thickness is approximately three to four pixels at the launcher's rendered logo size. Add only a restrained dark outer shadow so the light outline remains defined over the background's bright flame.

The edit must not redraw, respell, recolor, crop, or add text to the logo. The approved output replaces both canonical copies of the launcher logo so art source and packaged asset remain identical.

## Legal Notices

Keep legal notices inside the shared submenu and its normal scrolling container. Remove text selection, the RichTextLabel's default field-like style box, and focus-like interaction. The legal body remains readable, selectable by neither mouse nor touch, and scrolls through the parent submenu without an inner border or filled rectangle.

## Typography

`LauncherTheme` remains the single font owner. User-facing labels, buttons, line edits, and rich text use the game's regular or bold font when available and the existing packaged launcher font as fallback. Korean continues to use the game's Korean font.

Add one RichTextLabel-specific theme helper alongside `ApplyGameFont`, then use it for legal notices and the news article body. Apply the existing game-font path to raw launcher labels and line edits that currently inherit Godot defaults. Preserve font-size hierarchy by role rather than making every string the same size. The diagnostic console remains an intentional compact utility exception.

## Scope

- Edit the launcher logo bitmap and keep transparent output.
- Remove legal-body selection and inner chrome.
- Normalize user-facing launcher font application through `LauncherTheme`.
- Build and validate a v0.4.3 candidate.

No layout redesign, translation behavior change, console restyle, dependency, CI workflow, in-app updater redesign, push, tag, or GitHub release is included before a separately verified publication step.

## Validation

- Inspect the edited logo at its rendered scale over the launcher background and verify the exact lettering and alpha are preserved.
- Verify the canonical logo copies are byte-identical.
- Build the launcher with the repository's packaging guards.
- Inspect the complete diff and run existing shell regression checks, formatting checks, APK asset validation, ZIP integrity validation, signature verification, and package metadata inspection.
- Perform an on-device launch check if ADB is available; otherwise report that omission before publication.

