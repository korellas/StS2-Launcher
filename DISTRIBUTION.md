# Distribution Rights and External Permission Boundaries

This repository can document its code and package third-party notices, but
source changes cannot grant rights controlled by Mega Crit, Valve, Esoteric
Software, Firelight Technologies, Google, or other rightsholders.

This document is a project risk boundary, not a legal opinion.

## Public APK status

A public APK should not be treated as cleared merely because it is free, asks
the user to own the Steam game, or downloads game files after installation.
Until the publisher can document the external permissions below, source-only
publication is the lower-risk distribution choice.

Existing remote releases are outside this local cleanup and are not validated
or withdrawn by it.

## Mega Crit and the game

Mega Crit's Content Policy permits non-plagiarized, non-discriminatory mods that
follow the relevant platform's terms and do not suggest official affiliation.
It generally prohibits monetizing mods other than accepting donations.

The policy does not expressly address repackaging the desktop game runtime as an
unofficial Android port. Whether this launcher qualifies as a permitted mod is
therefore unresolved without confirmation from Mega Crit or another defensible
legal basis.

Source: https://megacrit.com/content-policy/

## Valve, Steam access, and downloaded content

The Steam Subscriber Agreement licenses Steam content and services for
personal, non-commercial use subject to applicable subscription terms. It also
contains restrictions on modifying or creating derivative works from content,
protocol emulation, redirecting communications, and distributing content except
where Valve, the relevant rightsholder, or applicable law permits it.

SteamKit2's LGPL license covers that library's code; it does not authorize a
particular way of accessing Steam or distributing software that does so. A
publisher should obtain confirmation that this authentication and depot-access
workflow complies with the current Valve and game-specific terms.

Source: https://store.steampowered.com/subscriber_agreement/

## Spine Runtimes

The Spine Runtimes License Agreement requires a valid integration basis and
requires the runtime license and copyright notice to accompany redistribution.
Merely including `SPINE-RUNTIMES-LICENSE.txt` does not prove that the person who
integrated or modified the runtime, or a downstream publisher, has the required
license or authorization.

Source: https://esotericsoftware.com/spine-runtimes-license

## FMOD

FMOD's terms distinguish personal, educational, non-commercial, limited
commercial, and separately licensed uses. They require the applicable project
registration and an in-app credit; FMOD's attribution page also requires the
FMOD logo at startup unless Firelight grants a waiver. Each APK publisher must
confirm that its own use and distribution satisfy the current terms.

Sources:

- https://www.fmod.com/legal
- https://www.fmod.com/attribution

## Names, logo, and replacement artwork

The launcher is labeled unofficial and includes a non-affiliation statement.
The game name in the hand-drawn logo identifies compatibility, but using the
full name prominently still carries more trademark risk than a plain-text,
abbreviated compatibility reference. No source change can guarantee nominative
or fair-use treatment in every jurisdiction.

The maintainer supplied newly generated replacement artwork. Before public
redistribution, the publisher should retain the generator/service terms and
prompt or authorship record showing that the files may be redistributed and
were not generated from unlicensed official images.

## Open-source and SDK obligations

An APK publisher must distribute the notices under `android/assets/legal/`,
preserve LGPL replacement/source rights for SteamKit2, show required Google
translation attribution and disclaimer, and comply with the other component
licenses indexed by `THIRD_PARTY_LICENSES.md`.

These obligations are cumulative; satisfying one does not cure a missing game,
platform, Spine, FMOD, trademark, or artwork permission.
