# Third-Party Licenses and Notices

This file indexes components used or packaged by the Android release build.
The canonical full texts distributed in the APK live in
`android/assets/legal/`. Dependency versions remain owned by
`src/STS2Mobile/STS2Mobile.csproj`, `android/config.gradle`, and the build's
resolved dependency graph.

Packaging a notice is not a substitute for obtaining a license or other
permission when one is required.

## Launcher code and MIT components

- **Upstream launcher and project contributions** — Copyright Eky and the
  applicable contributors; MIT. See `LICENSE` and
  `android/assets/legal/MIT.txt`.
- **Godot Engine** — Copyright Godot Engine contributors and its named
  third-party contributors; MIT and the component licenses collected in
  `android/assets/legal/GODOT-COPYRIGHT.txt`. Source:
  https://github.com/godotengine/godot
- **Harmony (0Harmony)** — Copyright Andreas Pardeike and contributors; MIT.
  Source: https://github.com/pardeike/Harmony
- **.NET / Mono** — Copyright Microsoft Corporation, .NET Foundation, and
  named contributors; MIT and collected third-party licenses. See
  `android/assets/legal/MIT.txt` and
  `android/assets/legal/DOTNET-THIRD-PARTY-NOTICES.txt`. Source:
  https://github.com/dotnet/runtime
- **System.IO.Hashing** — Copyright Microsoft Corporation and named
  contributors; MIT and collected third-party licenses. See
  `android/assets/legal/MIT.txt` and
  `android/assets/legal/SYSTEM.IO.HASHING-THIRD-PARTY-NOTICES.txt`. Source:
  https://github.com/dotnet/dotnet
- **ZstdSharp.Port** — Copyright Oleg Stepanischev and contributors; MIT.
  Source: https://github.com/oleg-st/ZstdSharp

## LGPL component

- **SteamKit2** — Copyright Ryan Stecker, the SteamRE Team, and contributors.
  The package metadata declares LGPL-2.1-only, while its packaged copyright
  notice says LGPL 2.1 or any later version. It is packaged as a separate
  managed assembly. See `android/assets/legal/STEAMKIT2-NOTICE.txt` and
  `android/assets/legal/LGPL-2.1.txt`. Corresponding source is identified by the
  package repository and commit recorded in the notice. Source:
  https://github.com/SteamRE/SteamKit

SteamKit2's license does not grant permission to use Valve's services, content,
protocols, or marks. Those remain subject to Valve's applicable agreements.

## Apache-licensed components

- **protobuf-net and protobuf-net.Core** — Copyright Marc Gravell and
  contributors; Apache-2.0. Source:
  https://github.com/protobuf-net/protobuf-net
- **AndroidX libraries** — Copyright The Android Open Source Project;
  Apache-2.0. Source: https://github.com/androidx/androidx
- **Kotlin Android tooling/runtime components** — Copyright JetBrains and
  contributors; Apache-2.0. Source: https://github.com/JetBrains/kotlin

The full Apache License is in `android/assets/legal/APACHE-2.0.txt`. Individual
dependency notices remain applicable where supplied by their owners.

## Google ML Kit and attribution asset

The Android build uses Google ML Kit on-device Translation under Google's
applicable API terms and usage guidelines. It is not licensed by this project's
MIT License. Google requires translation attribution and an in-app disclaimer.
The launcher packages an unmodified official Google attribution badge and shows
it only beside ML Kit results.

See `android/assets/legal/GOOGLE-ML-KIT-NOTICE.txt` and
https://developers.google.com/ml-kit/language/translation/translation-terms.

## FMOD and bundled codecs

FMOD runtime libraries and the FMOD logo may be included by a release build
from separately supplied SDK files. They remain proprietary to Firelight
Technologies Pty Ltd and require the applicable FMOD license, registration, and
startup attribution. See `android/assets/legal/FMOD-NOTICE.txt` and
https://www.fmod.com/legal.

FMOD includes Ogg, Vorbis, Tremor, FLAC, and Opus codec components under the
Xiph BSD 3-Clause license. Their copyright notices are in `FMOD-NOTICE.txt` and
the full license is in `android/assets/legal/BSD-3-CLAUSE.txt`.

## Spine Runtimes

The custom runtime used by a complete Android build may include Spine Runtimes.
Spine is not covered by the project's MIT License. The Spine Runtimes agreement
requires a valid integration basis and inclusion of its license and copyright
notice. See `android/assets/legal/SPINE-RUNTIMES-LICENSE.txt` and
https://esotericsoftware.com/spine-runtimes-license.

Including the notice does not establish that a particular distributor holds the
required Spine rights.

## Cinzel font

`android/assets/launcher_font.ttf` is Cinzel. Copyright 2020 The Cinzel Project
Authors; SIL Open Font License 1.1. See
`android/assets/legal/OFL-1.1.txt`. Source:
https://github.com/NDISCOVER/Cinzel

## Game and platform materials

Slay the Spire 2 game files are not included in this repository or launcher
APK. Files downloaded by an authenticated owner remain the property of their
rightsholders and subject to their applicable terms. The game name and other
third-party marks are not licensed by this project's MIT License.
