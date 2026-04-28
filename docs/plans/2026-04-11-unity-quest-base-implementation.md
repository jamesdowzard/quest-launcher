# unity-quest-base Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a GitHub template repo for CLI-driven Unity 6 Quest 3 apps with 3D model loading and mixed reality support.

**Architecture:** Minimal Unity 6 project with Meta XR SDK, OpenXR, GLTFast. All build/scene setup via Editor scripts in batch mode. Generic enough to fork for any Quest 3 app.

**Tech Stack:** Unity 6 (6000.0.72f1), C#, Meta XR SDK v85, OpenXR, GLTFast, Android ARM64/IL2CPP

---

### Task 1: Init repo and create .gitignore

**Files:**
- Create: `.gitignore`

**Step 1: Init git repo**

```bash
cd ~/code/personal/unity-quest-base
git init
gh-switch  # personal account
```

**Step 2: Create .gitignore**

```gitignore
# Unity
unity/Library/
unity/Temp/
unity/Logs/
unity/UserSettings/
unity/obj/
unity/.utmp/

# Build output
build/

# IDE
.idea/
.vs/
*.csproj
*.sln
*.user

# OS
.DS_Store
Thumbs.db
```

**Step 3: Commit**

```bash
git add .gitignore docs/
git commit -m "initial: project structure and design docs"
```

---

### Task 2: Create Unity package manifest

**Files:**
- Create: `unity/Packages/manifest.json`

**Step 1: Write trimmed manifest**

Stripped from tommi-xr — remove unused packages (ads, purchasing, multiplayer, 2d, tilemap, analytics, collab), add GLTFast. Keep: URP, XR stack, input system, hands, interaction toolkit, test framework, TextMeshPro (ugui).

```json
{
  "dependencies": {
    "com.unity.cloud.gltfast": "6.10.1",
    "com.unity.inputsystem": "1.19.0",
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.unity.render-pipelines.universal": "17.0.4",
    "com.unity.test-framework": "1.6.0",
    "com.unity.ugui": "2.0.0",
    "com.unity.xr.hands": "1.7.3",
    "com.unity.xr.interaction.toolkit": "3.1.1",
    "com.unity.xr.management": "4.5.4",
    "com.unity.xr.openxr": "1.16.1",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.animation": "1.0.0",
    "com.unity.modules.assetbundle": "1.0.0",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0",
    "com.unity.modules.vr": "1.0.0",
    "com.unity.modules.xr": "1.0.0"
  }
}
```

**Step 2: Commit**

```bash
git add unity/Packages/manifest.json
git commit -m "feat: add Unity package manifest with XR + GLTFast"
```

---

### Task 3: Copy and clean ProjectSettings from tommi-xr

**Files:**
- Create: `unity/ProjectSettings/` (copy from tommi-xr, modify)

**Step 1: Copy ProjectSettings directory**

```bash
cp -r ~/code/personal/tommi-xr/unity/ProjectSettings/ ~/code/personal/unity-quest-base/unity/ProjectSettings/
```

**Step 2: Update ProjectSettings.asset**

Change these fields:
- `companyName: DefaultCompany` (template — user sets their own)
- `productName: Quest App` (template)
- `bundleVersion: 0.1.0`
- Application identifier: `com.example.questapp` (template)

**Step 3: Commit**

```bash
git add unity/ProjectSettings/
git commit -m "feat: add ProjectSettings configured for Quest 3"
```

---

### Task 4: Create QuestBuildTools.cs (Editor script)

**Files:**
- Create: `unity/Assets/Scripts/Editor/QuestBuildTools.cs`

**Step 1: Write generic Editor script**

Extract from tommi-xr's TommiXRSceneSetup.cs:
- `ConfigureXRSettings()` — Android SDK 32, GameActivity, IL2CPP, OpenXR, Meta features. Change namespace to `QuestBase.Editor`, app ID to `com.example.questapp`.
- `ImportXRISamples()` — import Starter Assets + Hand Visualizer (same as tommi-xr)
- `SetupBaseScene()` — XR Origin from prefab, camera config, floor plane, basic lighting. NO panels/data/filters.
- `BuildAPK()` — build to `../build/quest-app.apk`
- Keep all helpers: `InstantiateXROrigin`, `CreateMaterial`, `CreateUnlitMaterial`, `SetSerializedField`, `FindChildRecursive`, `EnsureDirectory`, `HexColor`

**Step 2: Commit**

```bash
git add unity/Assets/Scripts/Editor/QuestBuildTools.cs
git commit -m "feat: add QuestBuildTools Editor script (CLI build pipeline)"
```

---

### Task 5: Create ModelLoader.cs (runtime glTF/GLB loading)

**Files:**
- Create: `unity/Assets/Scripts/Runtime/ModelLoader.cs`

**Step 1: Write GLTFast wrapper**

Simple async loader: `LoadModel(string path)` and `LoadModelFromUrl(string url)`. Returns the instantiated GameObject. Handles error logging.

**Step 2: Commit**

```bash
git add unity/Assets/Scripts/Runtime/ModelLoader.cs
git commit -m "feat: add ModelLoader for runtime glTF/GLB import"
```

---

### Task 6: Create AppBootstrap.cs (XR session + passthrough)

**Files:**
- Create: `unity/Assets/Scripts/Runtime/AppBootstrap.cs`

**Step 1: Write bootstrap MonoBehaviour**

Attached to a GameObject in the scene. On Start: logs XR status. Includes `TogglePassthrough()` method bound to B button. Minimal — apps extend this.

**Step 2: Commit**

```bash
git add unity/Assets/Scripts/Runtime/AppBootstrap.cs
git commit -m "feat: add AppBootstrap with passthrough toggle"
```

---

### Task 7: Create AndroidManifest.xml

**Files:**
- Create: `unity/Assets/Plugins/Android/AndroidManifest.xml`

**Step 1: Write manifest**

GameActivity entry point, VR intent category, Meta Quest device filter. Same pattern as tommi-xr gotcha #3.

**Step 2: Commit**

```bash
git add unity/Assets/Plugins/Android/AndroidManifest.xml
git commit -m "feat: add AndroidManifest for Quest 3 GameActivity"
```

---

### Task 8: Create CLAUDE.md

**Files:**
- Create: `CLAUDE.md`

**Step 1: Write CLAUDE.md**

Port the full gotchas section from tommi-xr. Update namespaces (`QuestBase.Editor`), method names (`SetupBaseScene`), app ID (`com.example.questapp`). Add GLTFast model loading section. Remove all Tommi-specific content (panels, data flow, station codes, interaction bindings).

**Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: add CLAUDE.md with Quest 3 build gotchas"
```

---

### Task 9: Create GitHub repo as template

**Step 1: Create remote**

```bash
gh repo create unity-quest-base --public --source . --push
```

**Step 2: Mark as template repo**

```bash
gh repo edit jamesdowzard/unity-quest-base --template
```

**Step 3: Verify**

```bash
gh repo view jamesdowzard/unity-quest-base --json isTemplate
```
