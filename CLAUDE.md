# unity-quest-base — Unity 6 Quest 3 Template

GitHub template repo for CLI-driven native Quest 3 apps. Fork this for any new Unity Quest project.

## Quick Reference

| Item | Value |
|------|-------|
| Unity | 6000.0.72f1 (`/Applications/Unity/Hub/Editor/6000.0.72f1/`) |
| Unity CLI | `/Applications/Unity/Hub/Editor/6000.0.72f1/Unity.app/Contents/MacOS/Unity` |
| Platform | Android (Quest 3), ARM64, IL2CPP |
| Min SDK | API 32 |
| App ID | `com.example.questapp` (change per app) |
| XR Stack | Meta XR SDK v85 + OpenXR v1.16 + XRI Toolkit 3.1 |
| Model loading | GLTFast (runtime glTF/GLB import) |

## Development — CLI Only

**NEVER open Unity Editor GUI.** Everything is done via batch mode + Editor scripts.

### First-time setup

```bash
export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
export ANDROID_HOME=~/Library/Android/sdk

# Ensure Android SDK licenses are in Unity's bundled SDK
cp -r ~/Library/Android/sdk/licenses /Applications/Unity/Hub/Editor/6000.0.72f1/PlaybackEngines/AndroidPlayer/SDK/licenses
```

### Configure XR Settings

```bash
/Applications/Unity/Hub/Editor/6000.0.72f1/Unity.app/Contents/MacOS/Unity \
  -projectPath "$(pwd)/unity" -batchmode -quit \
  -executeMethod QuestBase.Editor.QuestBuildTools.ConfigureXRSettings \
  -logFile -
```

### Setup Base Scene

```bash
/Applications/Unity/Hub/Editor/6000.0.72f1/Unity.app/Contents/MacOS/Unity \
  -projectPath "$(pwd)/unity" -batchmode -quit \
  -executeMethod QuestBase.Editor.QuestBuildTools.SetupBaseScene \
  -logFile -
```

### Build APK

```bash
/Applications/Unity/Hub/Editor/6000.0.72f1/Unity.app/Contents/MacOS/Unity \
  -projectPath "$(pwd)/unity" -batchmode -quit \
  -executeMethod QuestBase.Editor.QuestBuildTools.BuildAPK \
  -logFile -
```

### Sideload to Quest

```bash
adb install -r build/quest-app.apk
```

### Full Build Sequence (one command)

```bash
./scripts/build.sh
```

Or with flags:
```bash
./scripts/build.sh --skip-configure   # skip XR config (already done)
./scripts/build.sh --skip-scene       # skip scene setup (already done)
```

### Setup Model Viewer Scene

```bash
/Applications/Unity/Hub/Editor/6000.0.72f1/Unity.app/Contents/MacOS/Unity \
  -projectPath "$(pwd)/unity" -batchmode -quit \
  -executeMethod QuestBase.Editor.QuestBuildTools.SetupModelViewerScene \
  -logFile -
```

## Validation & Debug Loop

Fast feedback without building a full APK:

### Compile Check (~30s)

```bash
$UNITY -projectPath "$(pwd)/unity" -batchmode -quit \
  -executeMethod QuestBase.Editor.QuestBuildTools.CompileCheck -logFile -
```

### Validate a 3D Model (instant, no build)

```bash
./scripts/validate-model.sh /path/to/model.glb
```

Checks: file exists, format, GLB header magic, size warnings for Quest 3 memory limits.

### Validate Scene (missing references)

```bash
$UNITY -projectPath "$(pwd)/unity" -batchmode -quit \
  -executeMethod QuestBase.Editor.QuestBuildTools.ValidateScene -logFile -
```

### Debug a Running Quest App

```bash
./scripts/logcat.sh              # stream filtered logs
./scripts/logcat.sh MyTag        # add custom tag filter
./scripts/screenshot.sh          # capture screenshot
./scripts/screenshot.sh test-1   # named screenshot
```

Screenshots saved to `screenshots/` directory.

### Recommended Workflow

```
1. Write code      → CompileCheck        (30s feedback)
2. Add a model     → validate-model.sh   (instant, no build)
3. Check scene     → ValidateScene       (catches missing refs)
4. Ready to test   → build.sh            (configure + build + deploy)
5. Debugging       → logcat.sh + screenshot.sh
```

## Architecture

```
unity-quest-base/
├── unity/
│   ├── Assets/
│   │   ├── Scenes/                     # Main.unity + ModelViewer.unity
│   │   ├── Scripts/
│   │   │   ├── Runtime/
│   │   │   │   ├── ModelLoader.cs      # GLTFast wrapper for runtime glTF/GLB import
│   │   │   │   ├── ModelPlatform.cs    # Auto-center/scale models on pedestal
│   │   │   │   ├── ModelInfoHUD.cs     # World-space stats (verts, tris, bounds)
│   │   │   │   ├── OrbitController.cs  # Thumbstick orbit/pan/zoom
│   │   │   │   └── AppBootstrap.cs     # XR session init, passthrough toggle
│   │   │   └── Editor/
│   │   │       └── QuestBuildTools.cs  # Build + validation + scene setup
│   │   ├── Materials/               # Created by SetupBaseScene
│   │   ├── Models/                  # Drop .fbx here — auto-loaded into viewer
│   │   ├── Plugins/Android/
│   │   │   └── AndroidManifest.xml
│   │   └── XR/Settings/
│   ├── Packages/manifest.json
│   └── ProjectSettings/
├── scripts/
│   ├── build.sh                    # Full build + deploy pipeline
│   ├── logcat.sh                   # Filtered Quest log streaming
│   ├── screenshot.sh               # Quest screenshot capture
│   └── validate-model.sh           # Model validation without building
├── screenshots/                    # Captured Quest screenshots (gitignored)
├── build/                          # APK output (gitignored)
├── docs/plans/
├── .gitignore
└── CLAUDE.md
```

## Model Viewer

The template includes a ready-made model viewer scene with:

- **ModelPlatform** — auto-centers and auto-scales any loaded model to comfortable viewing size on a pedestal
- **OrbitController** — thumbstick orbit/pan/zoom around the model
- **ModelInfoHUD** — world-space text showing vertex count, triangles, materials, bounds
- **Three-point lighting** — key, fill, and rim lights tuned for model inspection

### VR Controls

| Input | Action |
|-------|--------|
| Left stick | Pan (horizontal + vertical) |
| Right stick | Orbit (yaw + pitch) |
| A button | Reset view |
| B button | Toggle passthrough |

### Desktop Fallback

| Key | Action |
|-----|--------|
| WASD | Pan |
| QE | Orbit |
| RF | Zoom in/out |
| Space | Reset view |

### Two Ways to Load Models

**Option A — Bundled FBX (editor-time import):** Drop a `.fbx` file into `unity/Assets/Models/`. `SetupModelViewerScene` picks up the first alphabetical FBX, instantiates it on the ModelPlatform, and the HUD shows its stats. Unity imports the FBX at edit time — proper material conversion, no runtime cost, best performance.

- Use for: known project models shipped with the APK
- Supports: FBX, OBJ, DAE, and anything else Unity imports natively
- Zero runtime loading cost

**Option B — Runtime glTF loading (`ModelLoader`):** Use for arbitrary models loaded at runtime (user-selected, downloaded from a server, streamed from Quest storage).

```csharp
var loader = GetComponent<ModelLoader>();
var platform = GetComponent<ModelPlatform>();
var hud = FindObjectOfType<ModelInfoHUD>();

var model = await loader.LoadFromFile("/path/to/model.glb");
platform.PlaceModel(model);
hud.UpdateDisplay(model);
```

- Use for: user-selected or downloaded-at-runtime models
- Supports: glTF 2.0, GLB only
- Async, non-blocking load

For IFC/NWD/RVT, convert to FBX or glTF first. Autodesk Navisworks and Revit can export FBX natively.

## XR Stack

| Package | Version | Purpose |
|---------|---------|---------|
| `com.unity.xr.interaction.toolkit` | 3.1.1 | XR Origin, controllers, hands |
| `com.unity.xr.openxr` | 1.16.1 | OpenXR runtime |
| `com.unity.xr.management` | 4.5.4 | XR plugin management |
| `com.unity.xr.hands` | 1.7.3 | Hand tracking |
| `com.unity.cloud.gltfast` | 6.10.1 | Runtime glTF/GLB import |
| `com.unity.render-pipelines.universal` | 17.0.4 | URP rendering |

**Note:** Meta XR SDK (`com.meta.xr.sdk.core`) is configured via OpenXR features, not as a direct package dependency. The `com.unity.xr.openxr` package includes `MetaQuestFeature`.

## Unity 6 + Quest 3 — Gotchas

These were discovered during implementation. **Read before modifying XR config.**

### 1. OVRManager serialization bug (Meta XR SDK v68)
**Problem:** `com.meta.xr.sdk.core` v68 has an `OVRManager` class with `#if UNITY_EDITOR` fields that cause serialization mismatch with IL2CPP builds.
**Fix:** Use v74+. v85 is current.

### 2. OculusQuestFeature is deprecated — MUST be disabled
**Problem:** `OculusQuestFeature` has a validation rule that blocks the build if enabled.
**Fix:** Enable `MetaQuestFeature`, disable `OculusQuestFeature`. Both exist as sub-assets in `OpenXRPackageSettings.asset`.

### 3. GameActivity required on Unity 6+
**Problem:** Meta SDK validation requires `PlayerSettings.Android.applicationEntry == AndroidApplicationEntry.GameActivity` AND the AndroidManifest must contain `com.unity3d.player.UnityPlayerGameActivity`.
**Fix:** Set applicationEntry in ConfigureXRSettings AND use the custom AndroidManifest in `Assets/Plugins/Android/`.

### 4. OpenXR validation reads EditorUserBuildSettings.selectedBuildTargetGroup
**Problem:** In batch mode, `selectedBuildTargetGroup` defaults to Standalone. OpenXR validation finds no Android features.
**Fix:** Call `EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android)` before building.

### 5. OpenXR features are sub-assets, not standalone assets
**Problem:** `AssetDatabase.FindAssets("t:OpenXRFeature")` won't find them.
**Fix:** Use `AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXRPackageSettings.asset")` then iterate via `SerializedObject`.

### 6. Android SDK licenses must be in Unity's bundled SDK
**Problem:** Gradle build fails with "licences have not been accepted" even after `sdkmanager --licenses`.
**Fix:** `cp -r ~/Library/Android/sdk/licenses /Applications/Unity/Hub/Editor/6000.0.72f1/PlaybackEngines/AndroidPlayer/SDK/licenses`

### 7. Two-step build for clean configuration
**Problem:** If ConfigureXRSettings and BuildAPK run in the same Unity session, OpenXR validation may read stale state.
**Fix:** Run ConfigureXRSettings in one batch invocation, then BuildAPK in a second.

### 8. Leftover Meta SDK assets after package removal
**Problem:** Removing `com.meta.xr.sdk.core` leaves behind `OculusRuntimeSettings.asset` etc.
**Fix:** Delete any assets referencing removed packages from `Assets/Resources/` and `Assets/Oculus/`.

## Build Requirements

- Java 21: `export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home`
- Android SDK: `export ANDROID_HOME=~/Library/Android/sdk`
- Android SDK Platform 32: `sdkmanager "platforms;android-32"`
- SDK licenses in Unity's SDK (see gotcha #6)
- Quest connected: `adb devices -l` (verify model=Quest_3 before sideloading)

## Creating a New App From This Template

```bash
gh repo create my-quest-app --template jamesdowzard/unity-quest-base --clone
cd my-quest-app

# Update app identity
# In unity/ProjectSettings/ProjectSettings.asset:
#   - companyName
#   - productName
#   - applicationIdentifier (Android)
# In unity/Assets/Plugins/Android/AndroidManifest.xml:
#   - android:label
```
