# unity-quest-base — Design

GitHub template repo for CLI-driven Unity 6 Quest 3 apps with 3D model loading and mixed reality.

## Purpose

Reusable starting point for native Quest 3 apps. Each new app forks this template via `gh repo create --template`. Contains all XR configuration, build tooling, and hard-won gotchas — no app-specific code.

## Scope

**In scope:**
- Unity 6 project pre-configured for Quest 3 (Meta XR SDK v85, OpenXR, IL2CPP, ARM64)
- Editor script: `ConfigureXRSettings`, `SetupBaseScene`, `BuildAPK` (extracted from tommi-xr)
- Passthrough/MR enabled by default via MetaQuestFeature
- Minimal starter scene: XR Origin, passthrough camera, hand tracking, floor plane
- Runtime glTF/GLB model loading via GLTFast (Unity's built-in package)
- AndroidManifest with GameActivity + VR intents
- CLAUDE.md with all 8 Unity 6 + Meta XR gotchas and CLI build commands
- `.gitignore` excluding Library/, Temp/, Logs/, build/

**Out of scope:**
- App-specific UI, panels, data sources
- Any Tommi/SMWLW integration
- IFC/NWD loading (format-specific, add per-app)

## Architecture

```
unity-quest-base/
├── unity/
│   ├── Assets/
│   │   ├── Scenes/               # Empty — SetupBaseScene creates one
│   │   ├── Scripts/
│   │   │   ├── Runtime/
│   │   │   │   ├── ModelLoader.cs    # GLTFast wrapper for runtime glTF/GLB import
│   │   │   │   └── AppBootstrap.cs   # XR session init, passthrough toggle
│   │   │   └── Editor/
│   │   │       └── QuestBuildTools.cs # ConfigureXRSettings, SetupBaseScene, BuildAPK
│   │   ├── Plugins/Android/
│   │   │   └── AndroidManifest.xml
│   │   └── XR/Settings/
│   ├── Packages/manifest.json
│   └── ProjectSettings/
├── build/                        # APK output (gitignored)
├── docs/plans/
├── .gitignore
└── CLAUDE.md
```

## Key decisions

1. **Template repo, not submodule or UPM package** — matches existing workflow, each app owns its copy, no sync overhead
2. **GLTFast for model loading** — built into Unity 6, no third-party dependency, handles glTF/GLB which is the standard interchange format
3. **No IFC/NWD loader in base** — those are heavy dependencies, add per-app when needed
4. **Passthrough on by default** — MR is a primary use case, easier to disable than enable
5. **CLI-only** — all configuration via Editor scripts in batch mode, never open Unity GUI

## XR stack

| Package | Version | Purpose |
|---------|---------|---------|
| `com.meta.xr.sdk.core` | 85.0.0 | Hand tracking, passthrough, foveation |
| `com.unity.xr.meta-openxr` | 2.5.0 | Meta Quest OpenXR provider |
| `com.unity.xr.openxr` | 1.15.1 | OpenXR runtime |
| `com.unity.xr.management` | 4.5.0 | XR plugin management |
| `com.unity.cloud.gltfast` | latest | Runtime glTF/GLB import |

## Usage

```bash
# Create new app from template
gh repo create my-quest-app --template jamesdowzard/unity-quest-base --clone
cd my-quest-app

# Build and deploy
export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
export ANDROID_HOME=~/Library/Android/sdk
UNITY=/Applications/Unity/Hub/Editor/6000.0.72f1/Unity.app/Contents/MacOS/Unity
$UNITY -projectPath "$(pwd)/unity" -batchmode -quit -executeMethod QuestBase.Editor.QuestBuildTools.ConfigureXRSettings -logFile -
$UNITY -projectPath "$(pwd)/unity" -batchmode -quit -executeMethod QuestBase.Editor.QuestBuildTools.SetupBaseScene -logFile -
$UNITY -projectPath "$(pwd)/unity" -batchmode -quit -executeMethod QuestBase.Editor.QuestBuildTools.BuildAPK -logFile -
adb install -r build/quest-app.apk
```
