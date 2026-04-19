#!/usr/bin/env bash
#
# Local release pipeline:
#   1. Bumps gradle.properties to the given version.
#   2. Runs scripts/build.sh (Release mode by default, or --debug).
#   3. Commits the version bump, tags, and pushes both to origin.
#   4. Creates a GitHub release via `gh` and uploads the APK.
#
# Usage:
#   bash scripts/release.sh 0.4.0
#   bash scripts/release.sh 0.4.0-rc1 --debug --notes "Testing new overlay"
#
# Prereqs:
#   - gh CLI authenticated (`gh auth status`)
#   - Repo's `origin` points at the fork that should host the release
#   - android/sts2.keystore present and ~/.gradle/gradle.properties has
#     release_keystore_password / release_keystore_alias set

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GRADLE_PROPS="$ROOT/android/gradle.properties"
APK_DIR="$ROOT/android/build/outputs/apk/mono/release"

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <version> [--debug] [--notes \"<text>\"] [--draft] [--prerelease]" >&2
    echo "Example: $0 0.4.0" >&2
    exit 2
fi

VERSION="$1"
shift

BUILD_FLAGS=()
EXTRA_NOTES=""
DRAFT=false
PRERELEASE=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --debug)       BUILD_FLAGS+=(--debug) ;;
        --notes)       EXTRA_NOTES="$2"; shift ;;
        --draft)       DRAFT=true ;;
        --prerelease)  PRERELEASE=true ;;
        *)
            echo "Unknown flag: $1" >&2
            exit 2
            ;;
    esac
    shift
done

# Sanity checks
command -v gh >/dev/null || { echo "gh CLI not found"; exit 1; }
gh auth status >/dev/null 2>&1 || { echo "gh not authenticated. Run: gh auth login"; exit 1; }

if [[ -n "$(git -C "$ROOT" status --porcelain)" ]]; then
    echo "ERROR: working tree not clean. Commit or stash before releasing." >&2
    git -C "$ROOT" status --short >&2
    exit 1
fi

# Derive next version code by bumping the current one by one — release tags
# should strictly increase version_code so Android will accept the update.
CURRENT_CODE=$(grep '^export_version_code=' "$GRADLE_PROPS" | cut -d= -f2)
NEW_CODE=$((CURRENT_CODE + 1))

echo "→ Releasing v$VERSION (version_code $CURRENT_CODE -> $NEW_CODE)"

# Update gradle.properties.
sed -i.bak "s/^export_version_name=.*/export_version_name=$VERSION/" "$GRADLE_PROPS"
sed -i.bak "s/^export_version_code=.*/export_version_code=$NEW_CODE/" "$GRADLE_PROPS"
rm -f "$GRADLE_PROPS.bak"

# Build APK (passes through --debug if given).
echo "→ Building APK..."
bash "$ROOT/scripts/build.sh" --no-bump ${BUILD_FLAGS[@]+"${BUILD_FLAGS[@]}"}

APK_PATH="$APK_DIR/StS2Launcher-v$VERSION.apk"
if [[ ! -f "$APK_PATH" ]]; then
    echo "ERROR: expected APK not found at $APK_PATH" >&2
    ls -la "$APK_DIR" >&2 || true
    exit 1
fi
APK_SIZE=$(ls -lh "$APK_PATH" | awk '{print $5}')
echo "✓ APK built: $APK_PATH ($APK_SIZE)"

# Commit + tag + push
TAG="v$VERSION"
echo "→ Committing version bump + tagging $TAG"
git -C "$ROOT" add "$GRADLE_PROPS"
git -C "$ROOT" commit -m "Release $TAG"
git -C "$ROOT" tag -a "$TAG" -m "$TAG"
git -C "$ROOT" push origin HEAD
git -C "$ROOT" push origin "$TAG"

# Build release notes from the commit log since the last tag (auto-generated).
PREV_TAG=$(git -C "$ROOT" describe --tags --abbrev=0 "$TAG^" 2>/dev/null || true)
if [[ -n "$PREV_TAG" ]]; then
    AUTO_NOTES=$(git -C "$ROOT" log --pretty=format:"- %s (%h)" "$PREV_TAG..$TAG" -- . ':!android/gradle.properties')
else
    AUTO_NOTES=$(git -C "$ROOT" log --pretty=format:"- %s (%h)" -- . ':!android/gradle.properties' | head -30)
fi

NOTES_FILE=$(mktemp)
{
    if [[ -n "$EXTRA_NOTES" ]]; then
        printf "%s\n\n" "$EXTRA_NOTES"
    fi
    printf "## Changes since %s\n\n%s\n" "${PREV_TAG:-the beginning}" "${AUTO_NOTES:-(none)}"
} > "$NOTES_FILE"

# Create GitHub release and upload APK.
GH_ARGS=(release create "$TAG" "$APK_PATH" --title "$TAG" --notes-file "$NOTES_FILE")
if [[ "$DRAFT" == true ]]; then GH_ARGS+=(--draft); fi
if [[ "$PRERELEASE" == true ]]; then GH_ARGS+=(--prerelease); fi

echo "→ Creating GitHub release..."
gh "${GH_ARGS[@]}"

rm -f "$NOTES_FILE"

echo ""
echo "✓ Released $TAG — $APK_SIZE APK uploaded"
gh release view "$TAG" --json url --jq '.url'
