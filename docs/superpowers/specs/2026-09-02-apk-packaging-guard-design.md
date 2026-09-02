# APK Packaging Guard Design

## Problem

The v0.4.1 APK was produced without `assets/bootstrap.pck` and required .NET runtime assemblies such as `System.Private.CoreLib.dll`. Android can open the native activity, but managed startup cannot complete, producing a black screen followed by process termination.

The omission was not a licensing requirement. These build inputs are intentionally ignored by Git and must already exist in the local build workspace. The build script copied the launcher-specific assemblies but did not verify the remaining local inputs, and Gradle successfully packaged the incomplete asset directory.

## Scope

- Restore the ignored runtime assets and bootstrap pack in the current workspace from the known-good local v0.4.0 inputs.
- Add a local preflight to `scripts/build.sh` that stops before Gradle when required build inputs are absent.
- Add a post-build check that stops when the resulting APK does not contain the required entries.
- Build and validate an installable local APK.

No GitHub Actions workflow, remote push, tag, GitHub release, or in-app installer redesign is included.

## Design

`scripts/build.sh` remains the canonical build entry point. Before building the APK, it checks stable runtime sentinels owned by the local artifact set: the bootstrap pack, the managed runtime core, Harmony, and GodotSharp. A missing sentinel produces a direct error naming the missing path.

After Gradle finishes, the same script reads the APK file table and verifies the corresponding packaged paths. This catches both missing local inputs and Gradle packaging regressions. The check uses tools already required or normally present in the Android build environment and adds no dependency or CI service.

The current workspace's ignored assets are restored from the known-good v0.4.0 local artifact set. They remain untracked and are not added to the repository.

## Error Handling

The build exits non-zero at the first missing required input or packaged entry. A failed check prevents release tooling from committing, tagging, pushing, or uploading an incomplete APK.

## Validation

- Demonstrate that the guard rejects a missing required input.
- Run the normal local build with restored inputs.
- Verify ZIP integrity, APK signature, package/version metadata, and required packaged entries.
- Compare the runtime/bootstrap entry set against the known-good v0.4.0 APK and account for any difference.

