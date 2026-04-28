# Quest Launcher — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task.

**Goal:** Ship an MVP MR launcher: discovers all installed Quest apps, renders them on a curved spatial shelf in passthrough, launches via trigger pull. Single anchor, controller input, icon-only cards.

**Architecture:** Unity 6 + Meta XR SDK v85, native Kotlin JNI bridge for `PackageManager` queries + intent launching. See [`2026-04-28-quest-launcher-design.md`](./2026-04-28-quest-launcher-design.md) for full design.

**Tech stack:** Unity 6 LTS (6000.0.72f1), Meta XR SDK v85, OpenXR v1.16, XRI Toolkit 3.1, Kotlin (no Gradle module — single .kt in `Assets/Plugins/Android/`), C# 9.

**Phases:**

1. App discovery (Kotlin bridge + C# wrapper)
2. Flat shelf MVP (no anchor — fixed-position curved arc)
3. Spatial anchor + first-launch placement flow
4. Categories + sideload.yaml integration
5. Recents + favourites + filter bar
6. Polish (hover states, audio cues, re-pin gesture)
7. Universal Menu pinning + sideload.yaml entry for self-install

Each phase = one GH issue = one feature branch = one PR. Phases are deliberately small — the unknowns are in early phases (JNI bridge, anchor lifecycle), so de-risk those first.

---

## Phase 1: App discovery (Kotlin bridge + C# wrapper)

**Goal:** From inside Unity, get a JSON list of every installed app. Verified by logging the count and sample to logcat.

**Files:**

- Create: `unity/Assets/Plugins/Android/LauncherBridge.kt`
- Create: `unity/Assets/Scripts/Runtime/LauncherBridge.cs`
- Create: `unity/Assets/Scripts/Runtime/AppInfo.cs`
- Modify: `unity/Assets/Scripts/Runtime/AppBootstrap.cs` (add startup discovery + log)

**Step 1: `LauncherBridge.kt`**

```kotlin
package ai.jamesisan.questlauncher

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.drawable.BitmapDrawable
import org.json.JSONArray
import org.json.JSONObject
import java.io.ByteArrayOutputStream

object LauncherBridge {

    @JvmStatic
    fun listApps(context: Context): String {
        val pm = context.packageManager

        val launcherIntent = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_LAUNCHER)
        val vrIntent = Intent(Intent.ACTION_MAIN).addCategory("com.oculus.intent.category.VR")

        val launcherApps = pm.queryIntentActivities(launcherIntent, 0)
        val vrPkgs = pm.queryIntentActivities(vrIntent, 0).map { it.activityInfo.packageName }.toSet()

        val arr = JSONArray()
        for (info in launcherApps) {
            val pkg = info.activityInfo.packageName
            val appInfo = info.activityInfo.applicationInfo
            val obj = JSONObject().apply {
                put("package", pkg)
                put("label", info.loadLabel(pm).toString())
                put("isVR", vrPkgs.contains(pkg))
                put("isSystem", (appInfo.flags and android.content.pm.ApplicationInfo.FLAG_SYSTEM) != 0)
            }
            arr.put(obj)
        }
        return arr.toString()
    }

    @JvmStatic
    fun getIcon(context: Context, packageName: String): ByteArray? {
        val pm = context.packageManager
        return try {
            val drawable = pm.getApplicationIcon(packageName)
            val bitmap = if (drawable is BitmapDrawable) {
                drawable.bitmap
            } else {
                val w = drawable.intrinsicWidth.coerceAtLeast(128)
                val h = drawable.intrinsicHeight.coerceAtLeast(128)
                Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888).also { bmp ->
                    val canvas = Canvas(bmp)
                    drawable.setBounds(0, 0, w, h)
                    drawable.draw(canvas)
                }
            }
            val out = ByteArrayOutputStream()
            bitmap.compress(Bitmap.CompressFormat.PNG, 100, out)
            out.toByteArray()
        } catch (e: PackageManager.NameNotFoundException) {
            null
        }
    }

    @JvmStatic
    fun launch(context: Context, packageName: String): Boolean {
        val pm = context.packageManager
        val intent = pm.getLaunchIntentForPackage(packageName) ?: return false
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        context.startActivity(intent)
        return true
    }
}
```

**Step 2: `AppInfo.cs`**

```csharp
using System;

[Serializable]
public class AppInfo {
    public string package;
    public string label;
    public bool isVR;
    public bool isSystem;
}

[Serializable]
public class AppInfoList {
    public AppInfo[] apps;
}
```

**Step 3: `LauncherBridge.cs`** — C# wrapper using `AndroidJavaClass`

```csharp
using UnityEngine;
using System.Collections.Generic;

public static class LauncherBridge {

    private const string KOTLIN_CLASS = "ai.jamesisan.questlauncher.LauncherBridge";

    private static AndroidJavaObject GetContext() {
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    }

    public static List<AppInfo> ListApps() {
#if !UNITY_ANDROID || UNITY_EDITOR
        return new List<AppInfo>();
#else
        using var bridge = new AndroidJavaClass(KOTLIN_CLASS);
        var ctx = GetContext();
        var json = bridge.CallStatic<string>("listApps", ctx);
        var wrapped = "{\"apps\":" + json + "}";
        return new List<AppInfo>(JsonUtility.FromJson<AppInfoList>(wrapped).apps);
#endif
    }

    public static Texture2D GetIcon(string packageName) {
#if !UNITY_ANDROID || UNITY_EDITOR
        return null;
#else
        using var bridge = new AndroidJavaClass(KOTLIN_CLASS);
        var ctx = GetContext();
        var bytes = bridge.CallStatic<byte[]>("getIcon", ctx, packageName);
        if (bytes == null) return null;
        var tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        return tex;
#endif
    }

    public static bool Launch(string packageName) {
#if !UNITY_ANDROID || UNITY_EDITOR
        Debug.Log($"[Launch] {packageName} (no-op in editor)");
        return false;
#else
        using var bridge = new AndroidJavaClass(KOTLIN_CLASS);
        var ctx = GetContext();
        return bridge.CallStatic<bool>("launch", ctx, packageName);
#endif
    }
}
```

**Step 4: Smoke test in `AppBootstrap.cs`** — log count + sample on Start.

```csharp
void Start() {
    var apps = LauncherBridge.ListApps();
    Debug.Log($"[QuestLauncher] Discovered {apps.Count} apps");
    foreach (var a in apps.GetRange(0, System.Math.Min(5, apps.Count))) {
        Debug.Log($"  {a.label} ({a.package}) VR={a.isVR}");
    }
}
```

**Acceptance:** `./scripts/build.sh` deploys; `./scripts/logcat.sh QuestLauncher` shows ≥10 apps including Findroid (`dev.jdtech.jellyfin`), Tailscale (`com.tailscale.ipn`), and `org.jellyfin.androidtv`. VR apps flagged correctly.

---

## Phase 2: Flat shelf MVP (no anchor)

**Goal:** Render every discovered app as an icon card in a curved arc 1.2m in front of the player. Trigger pull on a card launches the app. No spatial anchor yet — fixed position relative to camera at scene start.

**Files:**

- Create: `unity/Assets/Scripts/Runtime/AppCard.cs`
- Create: `unity/Assets/Scripts/Runtime/ShelfManager.cs`
- Create: `unity/Assets/Scripts/Runtime/AppRepository.cs`
- Create: `unity/Assets/Prefabs/AppCard.prefab` (programmatic — Editor script in QuestBuildTools)
- Modify: `unity/Assets/Scripts/Editor/QuestBuildTools.cs` — add `SetupLauncherScene` method
- Modify: `unity/Assets/Scripts/Runtime/AppBootstrap.cs` — instantiate `ShelfManager`

**Key bits:**

- Curved arc: `angle = (i - count/2) * 15°`; position = `anchor.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * 1.2f`; rotation faces anchor
- Card prefab: 0.2m × 0.2m Quad with icon material + TextMeshPro label child
- `XRSimpleInteractable` on each card; subscribe to `selectEntered` → `LauncherBridge.Launch(card.PackageName)`
- Icon load is async-ish: `GetIcon` is JNI-blocking but only happens once per card on instantiation; if it stutters, move to a coroutine yielding one card per frame

**Acceptance:** Don headset, build+deploy; see ~20 cards in a curved arc with correct icons and labels; pull trigger on Findroid card → Findroid launches.

---

## Phase 3: Spatial anchor + first-launch placement

**Goal:** First launch shows a placement reticle ("point at where you want the shelf"); trigger pull places a `OVRSpatialAnchor`; saved UUID persists. Subsequent launches re-bind to the saved anchor and the shelf appears at the same physical spot.

**Files:**

- Modify: `unity/Assets/Scripts/Runtime/ShelfManager.cs` — anchor lifecycle
- Create: `unity/Assets/Scripts/Runtime/PlacementController.cs` — first-launch reticle
- Modify: `unity/Packages/manifest.json` — add `com.meta.xr.sdk.spatialanchors` if not in v85 core
- Modify: `unity/Assets/Plugins/Android/AndroidManifest.xml` — add `<uses-permission android:name="com.oculus.permission.USE_ANCHOR_API" />` if required by SDK

**Anchor lifecycle:**

```csharp
// On first launch:
var anchor = gameObject.AddComponent<OVRSpatialAnchor>();
yield return new WaitUntil(() => anchor.Created);
anchor.SaveAsync(new OVRSpaceSaveOptions { Storage = OVRSpace.StorageLocation.Local });
PlayerPrefs.SetString("shelf_anchor_uuid", anchor.Uuid.ToString());

// On subsequent launches:
var uuid = new Guid(PlayerPrefs.GetString("shelf_anchor_uuid"));
var loadOptions = new OVRSpatialAnchor.LoadOptions {
    Uuids = new[] { uuid },
    StorageLocation = OVRSpace.StorageLocation.Local,
};
OVRSpatialAnchor.LoadUnboundAnchorsAsync(loadOptions, anchors => {
    foreach (var a in anchors) a.BindTo(shelfRoot.AddComponent<OVRSpatialAnchor>());
});
```

**Acceptance:** First launch: see reticle, place shelf at chosen spot, kill app, relaunch → shelf appears at same physical location. Walk around it; cards stay world-locked.

---

## Phase 4: Categories + sideload.yaml integration

**Goal:** `quest/sideload.yaml` is bundled into Streaming Assets; categories enrich card metadata; cards display category badges.

**Files:**

- Create: `unity/Assets/Scripts/Runtime/SideloadManifestLoader.cs`
- Create: `unity/Assets/Scripts/Runtime/AppCategory.cs`
- Modify: `unity/Assets/Scripts/Runtime/AppRepository.cs` — cross-reference apps with manifest
- Modify: `unity/Assets/Scripts/Runtime/AppCard.cs` — render category badge
- Create: `shared/manifests/favourites.yaml` (symlink or copy)
- Modify: `scripts/build.sh` — copy `shared/manifests/favourites.yaml` → `unity/Assets/StreamingAssets/` before build

**YAML parsing:** Bring in `YamlDotNet` package (Unity-compatible) OR shell-convert to JSON at build time and use `JsonUtility`. Recommendation: build-time JSON conversion. Avoids runtime dependency, faster cold start.

**Acceptance:** Cards for Tailscale/Termux/Wolvic show category badges (favourite/utility/browser); category not in manifest = "Other" badge; logcat confirms manifest parsed.

---

## Phase 5: Recents + favourites + filter bar

**Goal:** Floating tab bar above shelf: All / VR / Sideloaded / Recent / Favourites. Tap a tab → shelf rebuilds with filtered list. Recents persist via `PlayerPrefs` (last 8 launches, MRU order).

**Files:**

- Create: `unity/Assets/Scripts/Runtime/CategoryFilter.cs`
- Create: `unity/Assets/Prefabs/FilterTab.prefab` (programmatic)
- Modify: `unity/Assets/Scripts/Runtime/AppRepository.cs` — `SetActiveFilter(Filter)` method, recents persistence
- Modify: `unity/Assets/Scripts/Runtime/LauncherBridge.cs` — `Launch()` records to recents

**Acceptance:** Tap "Recent" → shelf shows only last-launched apps in MRU order; launch a new app, return, recents updated.

---

## Phase 6: Polish

**Goal:** Hover scale + glow, audio cue on launch, re-pin gesture (A+B held 2s → re-enter placement mode), card sorting (alphabetical default, recents = MRU).

**Files:**

- Modify: `unity/Assets/Scripts/Runtime/AppCard.cs` — hover state animation
- Create: `unity/Assets/Audio/launch.wav` (placeholder synth or freesound)
- Modify: `unity/Assets/Scripts/Runtime/PlacementController.cs` — re-pin entry gesture
- Modify: `unity/Assets/Scripts/Runtime/AppRepository.cs` — sort modes

**Acceptance:** Subjective — feels good in headset. Specifically: hover affordance is unambiguous, launch has tactile audio feedback, re-pin works without quit-restart.

---

## Phase 7: Universal Menu integration

**Goal:** Pin Quest Launcher to the Universal Menu so it's one tap from anywhere. Add quest-launcher to `quest/sideload.yaml` for first-class self-install.

**Files:**

- Modify (in `~/code/personal/quest/`): `sideload.yaml` — add quest-launcher entry pointing at GH releases
- Create: `.github/workflows/release.yml` — auto-build APK on tag, attach to release
- Document in `CLAUDE.md` how to pin to Universal Menu

**Acceptance:** `quest sync` (Python toolkit) installs the latest release; pinned to Universal Menu with one tap from "All apps."

---

## Acceptance — overall MVP shipped

- Built APK installs on Quest 3 cleanly
- ≥10 apps discovered including all sideloaded
- Spatial anchor persists across at least 5 cold launches
- Trigger-pull launch works for at least: Findroid, Wolvic, Tailscale, official Jellyfin Android TV, one Quest store app
- One tap from Universal Menu opens the shelf in <2 seconds
- No crashes in 30 minutes of use
- Logcat clean (no errors, no warnings tagged QuestLauncher)

## Estimated effort

| Phase | Time (evenings, 2-3h each) |
|-------|----------------------------|
| 1 — App discovery | 1 |
| 2 — Flat shelf MVP | 2 |
| 3 — Spatial anchor | 1-2 (anchor API has learning curve) |
| 4 — Categories | 1 |
| 5 — Recents + filter bar | 1 |
| 6 — Polish | 1-2 |
| 7 — Universal Menu | 0.5 |
| **Total** | **~8 evenings (16-24h)** |

## Conventions

- One phase = one issue = one feature branch (`/f:new`) = one PR
- All PRs go through `/f:ship` (auto-merge, cleanup)
- After each phase: build + deploy + manual verification in headset before opening PR
- Logcat tag: `QuestLauncher` (single tag for all our code so `logcat.sh QuestLauncher` is the one-stop debug stream)
- C# namespace: `ai.jamesisan.questlauncher` matching app ID
- Kotlin package: same
