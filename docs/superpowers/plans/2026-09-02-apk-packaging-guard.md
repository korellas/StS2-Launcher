# APK Packaging Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent incomplete launcher APKs and produce a verified local replacement for the broken v0.4.1 build.

**Architecture:** A focused shell helper owns the finite list of required package assets and validates either an Android assets directory or a completed APK. The existing local build entry point invokes it before and after Gradle. Ignored binary inputs are restored only in the current workspace and remain outside Git.

**Tech Stack:** Bash, unzip/zip, Gradle Android build, Android SDK `apksigner` and `aapt`.

---

### Task 1: Package asset verifier

**Files:**
- Create: `scripts/verify-package-assets.sh`
- Create: `scripts/tests/verify-package-assets-test.sh`

- [ ] **Step 1: Write the failing verifier test**

Create fixtures for an incomplete asset directory, a complete asset directory, an incomplete APK ZIP, and a complete APK ZIP. Assert that incomplete fixtures fail with the missing entry in stderr and complete fixtures succeed.

- [ ] **Step 2: Run the test and verify it fails**

Run: `bash scripts/tests/verify-package-assets-test.sh`

Expected: non-zero because `scripts/verify-package-assets.sh` does not exist.

- [ ] **Step 3: Implement the minimal verifier**

The helper accepts `inputs <android-assets-directory>` or `apk <apk-path>`. It checks these stable sentinels:

```text
bootstrap.pck
dotnet_bcl/0Harmony.dll
dotnet_bcl/GodotSharp.dll
dotnet_bcl/System.Private.CoreLib.dll
dotnet_bcl/System.Runtime.dll
```

For APK mode it checks the corresponding `assets/` paths in `unzip -Z1` output. Any missing entry prints `ERROR: required package asset missing: <path>` and exits non-zero.

- [ ] **Step 4: Run the verifier test and verify it passes**

Run: `bash scripts/tests/verify-package-assets-test.sh`

Expected: `PASS: package asset verifier`.

### Task 2: Integrate the guard with the local build

**Files:**
- Modify: `scripts/build.sh`

- [ ] **Step 1: Add pre-build and post-build calls**

After launcher assemblies are copied, run:

```bash
bash "$ROOT/scripts/verify-package-assets.sh" inputs "$BUILD_DIR/assets"
```

After Gradle produces the expected APK, run:

```bash
bash "$ROOT/scripts/verify-package-assets.sh" apk "$APK_DIR/StS2Launcher-v$NEW_NAME.apk"
```

- [ ] **Step 2: Prove the current incomplete workspace is rejected**

Run the input verifier before restoring ignored binaries.

Expected: non-zero naming `bootstrap.pck` or a required runtime assembly.

### Task 3: Restore ignored local build inputs

**Files:**
- Restore ignored local files under: `android/assets/dotnet_bcl/`
- Restore ignored local file: `android/assets/bootstrap.pck`
- Reconnect other ignored local build inputs required by the documented pipeline.

- [ ] **Step 1: Restore from the known-good local v0.4.0 artifact set**

Copy the full managed runtime asset directory and bootstrap pack from the preserved local v0.4.0 workspace. Do not add these ignored binaries to Git.

- [ ] **Step 2: Run the input verifier**

Run: `bash scripts/verify-package-assets.sh inputs android/assets`

Expected: `Package asset inputs verified`.

### Task 4: Build and validate a local replacement APK

**Files:**
- Temporarily modify and restore: `android/gradle.properties`
- Produce ignored build output: `android/build/outputs/apk/mono/release/StS2Launcher-v0.4.2-local.apk`

- [ ] **Step 1: Assign a local-only upgrade version**

Temporarily set `export_version_name=0.4.2-local` and `export_version_code=40002` so Android treats the artifact as an upgrade from broken v0.4.1. Restore the tracked file after the APK is produced.

- [ ] **Step 2: Run the canonical local build**

Run: `bash scripts/build.sh --no-bump`

Expected: Gradle succeeds and the post-build package guard passes.

- [ ] **Step 3: Validate the completed APK**

Verify ZIP integrity, signature, package name, local version metadata, required packaged assets, and the managed runtime/bootstrap entry set against v0.4.0.

- [ ] **Step 4: Review and commit tracked changes**

Run verifier tests, `git diff --check`, inspect the complete diff and Git status, then commit only the guard, test, and plan. Do not push or publish a release.

### Task 5: Serve the verified APK locally

**Files:**
- No repository changes.

- [ ] **Step 1: Start a task-owned HTTP server**

Serve only the verified APK directory on an available LAN port bound to `0.0.0.0`.

- [ ] **Step 2: Verify the download response**

Confirm HTTP 200, content type, content length, and provide both `korellas-mac.local` and LAN-IP URLs.

