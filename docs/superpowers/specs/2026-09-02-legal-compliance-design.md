# Legal Compliance Cleanup Design

## Goal

Reduce avoidable copyright, trademark, privacy, and license-distribution risk in
the source tree and Android package without removing the launcher's existing
Steam, game-download, cloud-save, audio, or rendering functionality.

The implementation is based on the public `v0.4.0` release. It does not by
itself authorize future public APK distribution.

## Scope

### Independent branding

The launcher will stop distributing artwork that copies, incorporates, or is
presented as a close imitation of Slay the Spire 2 artwork or branding.

The following existing sources and their processed copies will be replaced or
removed:

- `art/app-icon.png`: replace with an independently created square app icon.
- `art/launcher-background.png`: replace with independently created landscape
  artwork.
- `art/splash.png`: replace with independently created landscape artwork or a
  neutral composition derived from the new app icon.
- `art/launcher-logo.png`: replace with the maintainer-supplied hand-drawn logo.
  Its original color and lettering will be preserved while only the outer alpha
  edge is feathered by about one pixel. Because it uses the game's name, a plain
  unofficial/non-affiliation statement will remain immediately adjacent in the
  launcher UI.
- `art/reference/`: remove the game screenshots without replacement.
- Processed Android and launcher assets derived from the files above will be
  regenerated from the new sources.

The replacement assets must not contain the official game logo, official
artwork, characters, cards, relics, UI sprites, distinctive typography, or a
close imitation of Mega Crit's visual style. They must not have been generated
from official images used as image references. The asset creator must have the
right to redistribute the result and will provide the generation or authorship
provenance that supports that right.

Preferred source specifications:

- App icon: `1024x1024` PNG, square, with important content inside the central
  safe area and no text.
- Launcher background: `1920x1080` PNG, landscape, with a quiet central/lower
  region so controls remain readable.
- Launcher logo: transparent landscape PNG, independent artwork rather than an
  official logo copy, with no use outside compatibility identification.
- Splash artwork: `1920x1080` PNG, landscape, with the mark centered and no
  embedded product name. It may be derived from the app icon.

The Android-facing title will say `Unofficial StS2 Launcher`. The hand-drawn
launcher logo will be accompanied by a plain-language statement that the
project is not affiliated with or endorsed by Mega Crit or Valve. The game name
is used only to identify compatibility.

Runtime use of resources from game files downloaded by an authenticated owner
is not changed in this cleanup. Those resources are not distributed in the
source repository or APK. Replacing that runtime styling would be a separate
product redesign with wider regression risk.

### License scope and notices

The repository will make clear that its MIT license covers only code and assets
whose contributors have the right to license. It will not claim to relicense
Slay the Spire 2, Steam, FMOD, Spine, Google ML Kit, or other third-party
materials.

Canonical third-party notices will identify each distributed component, its
copyright owner, applicable license or terms, source location, and the
redistribution obligations relevant to the APK. Full required license texts
will live under `android/assets/legal/` so they are included with every APK.
The release workflow will attach the same legal documentation alongside the
APK without maintaining a second hand-edited copy.

The notices will cover the components actually packaged by the release build,
including:

- the upstream launcher and other MIT-licensed components;
- SteamKit2 under LGPL-2.1 and its source/replacement rights;
- Apache-licensed Android and serialization components;
- Godot and the .NET/Mono runtime notices;
- FMOD distribution conditions, attribution, and bundled codec notices;
- the Spine Runtimes license and the requirement for a valid integration or
  third-party distribution right;
- the Cinzel font's SIL Open Font License; and
- Google ML Kit terms, attribution, and privacy disclosures.

The Gradle exclusions for dependency-provided `META-INF` files may remain only
because the consolidated canonical notices will be packaged explicitly. They
must not be described as removing the underlying obligations.

### User-visible legal and privacy information

The launcher will expose locally packaged legal notices without requiring a
network connection. The legal surface will include:

- the unofficial/non-affiliation statement;
- links or local text for third-party licenses;
- notice that Steam credentials and tokens are used to communicate directly
  with Steam and stored locally using the Android Keystore design already in
  the application;
- notice that ML Kit may download models and send SDK performance or usage
  metrics to Google as described by Google's terms; and
- a `Translated by Google` attribution when an ML Kit translation is shown.

No new analytics, operator backend, account collection, or remote legal-text
service will be introduced.

### Distribution documentation

Distribution documentation will distinguish source-code rights from APK
distribution rights. It will state that a public APK must not be represented as
cleared merely because it contains no game files or is non-commercial.

The documentation will identify the external permissions that source changes
cannot supply:

- permission or another defensible basis for an unofficial Android port under
  Mega Crit's policies;
- a Steam access method consistent with Valve's applicable terms; and
- a valid Spine integration/distribution license or written authorization.

FMOD non-commercial distribution requirements remain independently binding.
The release script will package notices but will not use a self-attestation flag
or a heuristic file check as a substitute for real permission.

## Non-goals

- Removing SteamKit2, FMOD, Spine, cloud saves, depot downloads, or other core
  runtime functionality.
- Claiming that documentation cures missing permissions from a rightsholder.
- Changing the Android application ID, which would disrupt upgrades and local
  credential storage.
- Deleting or modifying existing public releases or remote repository state.
- Creating a legal-compliance scanner, prose linter, or permission registry.
- Redesigning every launcher control that can optionally use resources from a
  locally downloaded game pack.

## Implementation boundaries

Work will be performed directly on the local `legal-compliance` branch created
from `v0.4.0`. No push, release mutation, or remote takedown is authorized.

Repository changes will be limited to:

- branding assets and their existing processing/loading paths;
- license, privacy, README, and distribution documentation;
- packaging the canonical legal files into the APK and release assets;
- the smallest launcher UI changes needed for the unofficial statement, local
  legal access, and ML Kit attribution; and
- focused tests or inspectable validation for those observable behaviors.

## Error handling

Missing optional artwork will fall back to the launcher's neutral color
background and text title. Failure to open the local legal view will be logged
and will not block Steam login or game launch. ML Kit attribution will be tied
to the translation path that produced the displayed text rather than shown for
platform translations.

## Validation

Behavioral changes will follow test-first development where the existing code
can be exercised without introducing a disproportionate Godot/Android test
harness. Documentation and image/resource replacements are directly
inspectable and use focused build and package validation instead.

Validation will include:

- focused tests for legal-view and ML Kit attribution state where practical;
- C# formatting and compilation;
- Android build validation using the repository's supported build path when
  the required local proprietary dependencies are available;
- APK entry inspection confirming canonical legal texts are packaged;
- source and package inspection confirming removed screenshots and legacy
  branding assets are absent; and
- `git diff --check`, complete diff review, status, and untracked-file review.

## Acceptance criteria

- No official game screenshot remains in the tracked tree.
- No legacy launcher logo or key art remains in the source or APK; the accepted
  maintainer-supplied replacements are used instead.
- The source-to-derived mapping for the supplied replacement assets is
  documented, and public redistribution remains gated on retaining applicable
  generator/service or authorship evidence.
- The launcher clearly identifies itself as unofficial and non-affiliated.
- Required third-party notices are present in the source distribution and APK.
- ML Kit translation output has the required attribution and privacy disclosure.
- Distribution documentation accurately states unresolved external permission
  boundaries.
- Existing launcher functionality remains intact.
