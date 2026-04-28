# Quest Launcher — Design

Custom Quest 3 launcher: a mixed-reality app shelf you pin to your room. Browse and launch every installed app (sideloaded + Quest store) from a 3D shelf rendered in passthrough.

## Why this exists

Quest's native UX for sideloaded apps is poor: they're buried under **Library → Unknown Sources**. The native "pin to Universal Menu" works but is shallow (~6 slots, no spatial context, no metadata). Existing third-party launchers (Launchpad, Quest Games Optimizer Launcher) are flat 2D grids that ignore Quest's actual capabilities.

This launcher leans into Quest 3's MR + spatial features:

- **Lives in your real room** via passthrough + a single spatial anchor
- **One tap from Universal Menu** → opens the shelf wherever you placed it
- **All apps in one view** — sideloaded + store + system, no Unknown Sources hiding
- **Categorised** via the existing `quest/sideload.yaml` manifest
- **Remembered** — recents and favourites persist between sessions

It cannot replace Horizon Home (Meta locks the system launcher). The bar it has to clear is "noticeably better than the Universal Menu pin shortcut."

## Scope (v1)

- MR passthrough with a single user-placed spatial anchor (the "shelf root")
- Curved-arc shelf of app cards rendered relative to anchor
- App discovery via `PackageManager.getInstalledApplications()` through a Kotlin JNI bridge
- Card content: app icon (from `PackageManager.getApplicationIcon()`), label, category badge
- Launch via `Intent` (the standard `getLaunchIntentForPackage` path, plus `com.oculus.intent.category.VR` fallback for VR apps)
- Categories sourced from `quest/sideload.yaml` (favourites/curated) + auto-detected (VR vs 2D vs system)
- Controller pointer + trigger for selection (hand-tracking deferred)
- Recents (last-launched, persisted via `PlayerPrefs`)
- "Re-pin shelf" gesture to reposition anchor

## Out of scope (v1)

- Replacing Horizon Home (impossible — Meta locks it)
- Hand-tracking input (controllers only for v1)
- Live app preview thumbnails (icon-only for v1; live previews via `ImageReader` deferred — vr-player has the pattern if we want it later)
- Voice control (deferred)
- Animated companion ("option 3" from brainstorm — separate project if it happens)
- Cloud sync of layout
- Multi-user
- Quest 2 support (Quest 3 only — passthrough quality + spatial anchor reliability are v3-grade)

## Architecture

```
quest-launcher/
├── unity/                                # Unity 6 + Meta XR SDK
│   ├── Assets/
│   │   ├── Scripts/Runtime/
│   │   │   ├── LauncherBridge.cs         # C# wrapper around Kotlin JNI
│   │   │   ├── AppRepository.cs          # discovery + categorisation + recents
│   │   │   ├── ShelfManager.cs           # spatial anchor + card layout
│   │   │   ├── AppCard.cs                # one card; renders icon + label + handles trigger
│   │   │   ├── CategoryFilter.cs         # tab-style filter (All / VR / Sideloaded / Recent)
│   │   │   ├── SideloadManifestLoader.cs # parses quest/sideload.yaml
│   │   │   └── AppBootstrap.cs           # XR session, passthrough toggle (template-inherited)
│   │   ├── Scripts/Editor/
│   │   │   └── QuestBuildTools.cs        # template-inherited; +SetupLauncherScene
│   │   ├── Scenes/
│   │   │   └── Launcher.unity            # replaces Main.unity
│   │   ├── Plugins/Android/
│   │   │   ├── AndroidManifest.xml       # +QUERY_ALL_PACKAGES, +<queries>
│   │   │   └── LauncherBridge.kt         # PackageManager + intent launch
│   │   └── XR/Settings/
│   ├── Packages/manifest.json            # +com.meta.xr.sdk.spatialanchors (if not in v85 core)
│   └── ProjectSettings/
├── shared/
│   └── manifests/
│       └── favourites.yaml               # symlink → ~/code/personal/quest/sideload.yaml
└── docs/plans/
    ├── 2026-04-28-quest-launcher-design.md
    └── 2026-04-28-quest-launcher-implementation.md
```

## Core components

### 1. LauncherBridge (Kotlin + C#)

Native Kotlin module exposes three calls:

```kotlin
// LauncherBridge.kt — Assets/Plugins/Android/
object LauncherBridge {
    fun listApps(context: Context): String  // JSON: [{package, label, isVR, isSystem}, ...]
    fun getIcon(context: Context, packageName: String): ByteArray?  // PNG bytes
    fun launch(context: Context, packageName: String): Boolean
}
```

C# wrapper uses `AndroidJavaClass`/`AndroidJavaObject`:

```csharp
// LauncherBridge.cs
public static List<AppInfo> ListApps() { /* AndroidJavaClass call → parse JSON */ }
public static Texture2D GetIcon(string pkg) { /* PNG bytes → Texture2D */ }
public static void Launch(string pkg) { /* fire-and-forget */ }
```

Why JSON across the JNI boundary instead of structured types: simplest serialisation, AndroidJavaClass marshalling for `List<CustomType>` is painful, and the call is once-per-launch not once-per-frame.

### 2. AppRepository (C#)

Single source of truth for app data. On startup:

1. `LauncherBridge.ListApps()` → all installed
2. Filter system apps unless toggled
3. Cross-reference with `SideloadManifestLoader` to assign curated category + priority
4. Auto-detect VR apps (presence of `com.oculus.intent.category.VR` intent filter — surfaced from Kotlin)
5. Load recents from `PlayerPrefs` (last 8 launched)

Provides filtered views: All, VR, Sideloaded, Recent, Favourites (priority ≤ 5 in sideload.yaml).

### 3. ShelfManager (C#)

Owns the spatial anchor and card layout.

- **First-launch flow:** user points controller, presses trigger → place anchor at hit point
- **Subsequent launches:** load saved anchor UUID, re-anchor to physical world
- **Layout:** curved arc, ~1.2m radius from anchor, eye-height. Card spacing = ~15° per card. Wraps if >24 cards (carousel scroll via thumbstick)
- **Re-pin gesture:** A+B held for 2s → enter re-place mode

Spatial anchor persistence uses Meta XR SDK's `OVRSpatialAnchor` (saves UUID to local storage, re-binds on next session).

### 4. AppCard (C#)

One MonoBehaviour per card. Renders:

- App icon as a Quad with the icon Texture2D as material
- Label as TextMeshPro below
- Category badge (small coloured dot) top-right
- Hover state (slight scale + glow) when controller pointer hits
- Trigger pull → `LauncherBridge.Launch(packageName)`

Uses XRI Toolkit's `XRSimpleInteractable` for hit testing — already plumbed in template.

### 5. CategoryFilter (C#)

Floating tab bar above the shelf: All / VR / Sideloaded / Recent / Favourites. Tap (trigger) to filter. Updates `AppRepository`'s active view, `ShelfManager` rebuilds cards.

### 6. SideloadManifestLoader (C#)

Reads `quest/sideload.yaml` (bundled into Streaming Assets at build time, or fetched from a known URL on the Pi). Used purely for metadata enrichment — not for installation.

Schema (matches existing `quest/sideload.yaml`):

```yaml
apps:
  - name: Tailscale
    package: com.tailscale.ipn
    priority: 1
    note: "..."
```

`priority ≤ 5` becomes the Favourites view. The `note` becomes the card's hover tooltip.

## Data flow

```
Quest installed apps
    -> LauncherBridge.kt (PackageManager query)
    -> JNI bridge (JSON)
    -> AppRepository (C#) ──┐
                            ├──> active view (filtered list)
quest/sideload.yaml         │
    -> SideloadManifestLoader  
    -> category + priority enrichment ──┘

active view
    -> ShelfManager (positions cards relative to anchor)
    -> AppCard prefabs (icon Quad + label + interactable)
    -> XRI Toolkit pointer hit
    -> trigger pull
    -> LauncherBridge.Launch(packageName)
    -> PackageManager.getLaunchIntentForPackage()
    -> Quest navigates to launched app (our Activity goes to background)
```

## Key decisions

- **Unity 6, not Rust.** vr-player's Rust+OpenXR stack is justified there (10-bit stereo). For a launcher it's pure overhead — Meta XR SDK in Unity has spatial anchors + passthrough + interaction toolkit ready to wire up.
- **Curved arc, not flat grid.** Uses Quest's spatial advantage. Flat grid is what Launchpad already does.
- **Single anchor for shelf root.** Per-card anchors would let users place each app independently but it's ~10x the complexity for marginal gain. Defer to v2 if requested.
- **YAML manifest reuse, not new schema.** `quest/sideload.yaml` already lists curated sideloaded apps with priority — repurposing it means the Python toolkit and the launcher share a source of truth.
- **JSON across JNI.** Simple. The discovery call happens once per launch, not per frame.
- **Icon-only for v1.** Live preview thumbnails are nice but require ImageReader plumbing per app (vr-player has the pattern but it's a week of work). Defer.
- **`QUERY_ALL_PACKAGES` permission.** Required since Android 11. Will trigger a Play Store review flag if we ever publish (not relevant for sideload, but document it).
- **Hand-tracking declared `required=true`.** Bypasses Meta's "controller required" launch dialog. Doesn't mean we use hand input — controllers still work fine with this set.

## Risks

| Risk | Mitigation |
|------|------------|
| Meta XR SDK Spatial Anchors API churn | Pin SDK version (v85). Document upgrade path in CLAUDE.md. |
| `PackageManager` returns only system apps without `QUERY_ALL_PACKAGES` | Permission already in manifest + `<queries>` fallback. |
| Quest store apps don't expose proper launcher intent | Fallback to `com.oculus.intent.category.VR`. Test on at least 5 store apps. |
| Spatial anchor drift / loss on Quest restart | Save UUID + last-known fallback position. Re-pin flow is one gesture. |
| Performance with 100+ apps | Pool AppCard prefabs + virtualise off-screen cards (carousel only renders visible arc). |
| Icon Drawable → Bitmap → Texture2D conversion JNI overhead | Cache textures in memory after first decode. |

## Future (v2+, deferred)

- Live preview thumbnails (vr-player ImageReader pattern)
- Per-app spatial anchors ("Jellyfin pinned to TV stand, Browser to desk")
- Voice control via Wit.ai / Quest voice SDK
- Companion creature (option 3 from brainstorm)
- Usage analytics ("you haven't opened X in 30 days")
- Pi-hosted manifest sync (Pi serves `favourites.yaml` so updating it on Mac instantly reflects in headset)
- Multi-shelf (different rooms have different shelves)

## Related

- [`unity-quest-base`](https://github.com/jamesdowzard/unity-quest-base) — template
- [`tommi-xr`](https://github.com/jamesdowzard/tommi-xr) — panel-rendering pattern to lift
- [`quest`](https://github.com/jamesdowzard/quest) — Python toolkit; `sideload.yaml` is the favourites source
- [`vr-player`](https://github.com/jamesdowzard/vr-player) — Quest manifest gotchas, JNI bridge patterns, ImageReader for future live previews
- Dossier: `~/code/personal/dossiers/quest-launcher/`
