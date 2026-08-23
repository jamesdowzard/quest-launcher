# Quest Launcher

Custom Quest 3 launcher — a mixed-reality app shelf you pin to your room. Browse and launch every installed app (sideloaded + Quest store) from a spatial 3D shelf rendered in passthrough.

**Why:** Sideloaded apps are buried under Library → Unknown Sources. The native Universal Menu pin is shallow. This is a more interesting answer: an MR shelf with passthrough + spatial anchors that lives wherever you place it in your room.

## Status

Scaffolded 2026-04-28 from `unity-quest-base`. **No implementation yet** — design + impl plan committed, GH issues raised. See `docs/plans/`.

## Stack

| Item | Value |
|------|-------|
| Engine | Unity 6 (6000.0.72f1) |
| Platform | Android (Quest 3), ARM64, IL2CPP |
| App ID | `ai.jamesisan.questlauncher` |
| XR Stack | Meta XR SDK v85 + OpenXR v1.16 + XRI Toolkit 3.1 |
| Native | AndroidJavaClass JNI bridge → Kotlin `LauncherBridge` for `PackageManager` queries + intent launch |
| Anchors | Meta XR SDK Spatial Anchors (one anchor for shelf root) |

Inherited from `unity-quest-base`: CLI build pipeline (no Editor GUI), URP, hand tracking, GLTFast, Quest screenshot/logcat scripts, all 8 Unity-6-on-Quest gotchas pre-solved.

## Layout

```
quest-launcher/
├── unity/                          # Unity 6 project (template-inherited)
│   ├── Assets/
│   │   ├── Scripts/Runtime/        # ShelfManager, AppCard, LauncherBridge (C# JNI)
│   │   ├── Plugins/Android/        # LauncherBridge.kt — PackageManager + intent launch
│   │   └── Scenes/                 # Launcher.unity (replaces Main.unity)
│   ├── Packages/manifest.json      # +com.meta.xr.sdk.spatialanchors
│   └── ProjectSettings/
├── helper/                         # companion APK — AndroidManifest.xml, build.sh,
│                                   #   src/com/jhg/questhelper/HelperActivity.java
├── docs/
│   └── plans/                      # design + implementation plans
└── scripts/                        # build.sh, logcat.sh, screenshot.sh (template-inherited)
```

## Sister projects

- [`unity-quest-base`](https://github.com/jamesdowzard/unity-quest-base) — template this is forked from. Update template gotchas back upstream when discovered.
- [`tommi-xr`](https://github.com/jamesdowzard/tommi-xr) — Unity panel-rendering pattern. Lift `PanelManager` + interaction layer.
- [`quest`](https://github.com/jamesdowzard/quest) — Python ADB/sideload toolkit. `sideload.yaml` is the source-of-truth for "favourite sideloaded apps".
- [`vr-player`](https://github.com/jamesdowzard/vr-player) — Rust+OpenXR sister project. Reference for Quest manifest gotchas (hand-tracking required, cleartext HTTP, package name no-hyphen rule).

## Key constraints

- **Cannot replace Horizon Home.** Meta locks the system launcher. Goal is "one tap from Universal Menu, opens an MR shelf in your real room."
- **`QUERY_ALL_PACKAGES` mandatory** — without it, `PackageManager.getInstalledApplications()` returns only system apps + this app on Android 11+.
- **Hand tracking `required=true`** — bypasses Meta's "controller required" launch dialog (per `vr-player` gotcha #69).
- **Package name no hyphens** — Android rejects them. App ID is `ai.jamesisan.questlauncher` (no separator).
- **Spatial anchors persist via Meta XR SDK** — single anchor for shelf root, app cards positioned relative.

## Dev

Inherited from `unity-quest-base`. Full reference in template README. Quick path:

```bash
./scripts/build.sh                 # configure + build + deploy to Quest
./scripts/logcat.sh QuestLauncher  # filtered logs
./scripts/screenshot.sh             # in-Quest screenshot
```

## See also

- Design: [`docs/plans/2026-04-28-quest-launcher-design.md`](docs/plans/2026-04-28-quest-launcher-design.md)
- Impl plan: [`docs/plans/2026-04-28-quest-launcher-implementation.md`](docs/plans/2026-04-28-quest-launcher-implementation.md)
- Dossier: `~/code/personal/dossiers/quest-launcher/`

## Quest device control

Device state, headless verification and logs live in `~/code/personal/quest/`
(CLI `quest …`, MCP `mcp__quest__*`, skill `/quest`). Do not hand-roll `adb`
for these.

- **Never ask James to wear the headset.** `quest device force-worn` disarms the
  proximity sensor so XR apps render at full frame rate with it on a desk.
  Survives reboot.
- **`adb logcat -s` matches tags EXACTLY.** Rust `android_logger` tags by module
  path (`crate::mod::sub`), so exact-match filters hide almost everything and
  mimic a hung app. Use `quest device logcat --prefix <crate>`.
- **Only one immersive app holds the slot.** `am start` succeeding with no
  process forked means another VR app owns it — force-stop it first.
- **Screenshots are 8-bit** — fine for layout and gross brightness, useless for
  banding or bit-depth questions.
- `quest device extensions <pkg>` names the exact manifest string any gated
  OpenXR extension is missing.
