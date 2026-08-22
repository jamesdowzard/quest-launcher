using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager.UI;
using UnityEditor.Compilation;
using UnityEngine.XR.Management;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QuestBase.Editor
{
    public static class QuestBuildTools
    {
        private const string MaterialsPath = "Assets/Materials";
        private const string ScenesPath = "Assets/Scenes";
        private const string PrefabsPath = "Assets/Prefabs";
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ModelViewerScenePath = "Assets/Scenes/ModelViewer.unity";
        private const string LauncherScenePath = "Assets/Scenes/Launcher.unity";
        private const string AppCardPrefabPath = "Assets/Prefabs/AppCard.prefab";
        internal const string TMPSentinelPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Quest/Import XRI Samples", false, 0)]
        public static void ImportXRISamples()
        {
            var samples = Sample.FindByPackage("com.unity.xr.interaction.toolkit", null).ToList();
            Debug.Log($"[QuestBase] Found {samples.Count} XRI samples");

            foreach (var sample in samples)
            {
                Debug.Log($"[QuestBase]   - {sample.displayName} (imported: {sample.isImported})");
                if (sample.displayName == "Starter Assets" && !sample.isImported)
                {
                    bool ok = sample.Import(Sample.ImportOptions.OverridePreviousImports);
                    Debug.Log($"[QuestBase] Imported Starter Assets: {ok}");
                }
                if (sample.displayName == "XR Device Simulator" && !sample.isImported)
                {
                    bool ok = sample.Import(Sample.ImportOptions.OverridePreviousImports);
                    Debug.Log($"[QuestBase] Imported XR Device Simulator: {ok}");
                }
            }

            var handsSamples = Sample.FindByPackage("com.unity.xr.hands", null).ToList();
            foreach (var sample in handsSamples)
            {
                if (sample.displayName == "HandVisualizer" && !sample.isImported)
                {
                    sample.Import(Sample.ImportOptions.OverridePreviousImports);
                    Debug.Log($"[QuestBase] Imported HandVisualizer");
                }
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Quest/Configure XR Settings", false, 1)]
        public static void ConfigureXRSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32;

            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            EnsureXRPreloadedAssets();

            AssetDatabase.SaveAssets();
            Debug.Log("[QuestBase] XR settings configured for Quest 3 (Android, ARM64, IL2CPP, GameActivity)");
        }

        // Unity's XRGeneralBuildProcessor injects the XRGeneralSettings for the
        // ACTIVE build target — and only that one — into PlayerSettings.preloadedAssets
        // (see XRGeneralBuildProcessor.cs:158, SettingsForBuildTarget(targetGroup)).
        // On this repo it either doesn't run or only picks up the top-level container,
        // so the loader ScriptableObject gets stripped from the player. We replicate
        // the preload here.
        //
        // The target filter is load-bearing, not tidiness. XRGeneralSettings.Awake()
        // does `s_RuntimeSettingsInstance = this` unconditionally, with no build-target
        // check, so every preloaded XRGeneralSettings overwrites the last one and the
        // winner is whichever Unity happens to Awake last. Assets/XR/XRGeneralSettings.asset
        // holds four — Android and Standalone carry the OpenXR loader, iPhone and Lumin
        // have `m_Loaders: []` — so preloading all four is a coin flip between a working
        // XR stack and `activeLoader=null loaderCount=0` at runtime. Note that
        // XRManagerSettings.activeLoaders IS m_Loaders (XRManagerSettings.cs:125), the
        // serialized configuration, so an empty count means the wrong settings object
        // won, NOT that a loader failed to start.
        public static void EnsureXRPreloadedAssets()
        {
            const string GeneralSettingsPath = "Assets/XR/XRGeneralSettings.asset";
            const BuildTargetGroup TargetGroup = BuildTargetGroup.Android;

            var allInAsset = File.Exists(GeneralSettingsPath)
                ? AssetDatabase.LoadAllAssetsAtPath(GeneralSettingsPath)
                : new UnityEngine.Object[0];

            if (allInAsset.Length == 0)
            {
                Debug.LogWarning($"[QuestBase] Missing {GeneralSettingsPath} — XR preload skipped");
                return;
            }

            var perTarget = allInAsset
                .OfType<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>()
                .FirstOrDefault();

            var settings = perTarget != null ? perTarget.SettingsForBuildTarget(TargetGroup) : null;
            if (settings == null)
            {
                Debug.LogError($"[QuestBase] No XRGeneralSettings for {TargetGroup} in {GeneralSettingsPath} — "
                             + "open Project Settings > XR Plug-in Management and enable OpenXR for Android");
                return;
            }

            var manager = settings.Manager;
            if (manager == null)
            {
                Debug.LogError($"[QuestBase] {settings.name} has no XRManagerSettings — XR cannot initialise at runtime");
                return;
            }

            var loaders = (manager.activeLoaders ?? new List<XRLoader>())
                .Where(l => l != null)
                .Cast<UnityEngine.Object>()
                .ToList();

            if (loaders.Count == 0)
            {
                Debug.LogError($"[QuestBase] {manager.name} has no loaders — enable the OpenXR Loader for {TargetGroup}");
                return;
            }

            // Anything of these types that isn't our chosen pair is another build
            // target's settings and must be evicted, not merely skipped — a previous
            // run of this method (or Unity's own) may already have preloaded them.
            bool IsForeignXRSettings(UnityEngine.Object o)
            {
                if (o is XRGeneralSettings gs) return gs != settings;
                if (o is XRManagerSettings ms) return ms != manager;
                // Editor-only container; its runtime stub has a different serialization
                // layout and emits "Read X bytes but expected Y bytes" on the player.
                return o is UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget;
            }

            var current = (PlayerSettings.GetPreloadedAssets() ?? new UnityEngine.Object[0])
                .Where(o => o != null)
                .ToList();
            int before = current.Count;

            var evicted = current.Where(IsForeignXRSettings).Select(o => $"{o.name}:{o.GetType().Name}").ToList();
            current.RemoveAll(o => IsForeignXRSettings(o));

            var added = new List<string>();
            void Add(UnityEngine.Object obj)
            {
                if (obj == null || current.Contains(obj)) return;
                current.Add(obj);
                added.Add($"{obj.name}:{obj.GetType().Name}");
            }

            Add(settings);
            Add(manager);
            foreach (var l in loaders) Add(l);

            PlayerSettings.SetPreloadedAssets(current.ToArray());

            Debug.Log($"[QuestBase] XR preload for {TargetGroup}: using '{settings.name}' -> '{manager.name}' "
                    + $"with {loaders.Count} loader(s): {string.Join(", ", loaders.Select(l => l.name))}");
            Debug.Log($"[QuestBase] XR preloaded assets: {before} -> {current.Count}");
            if (added.Count > 0)
                Debug.Log($"[QuestBase] Added to preload: {string.Join(", ", added)}");
            if (evicted.Count > 0)
                Debug.Log($"[QuestBase] Evicted foreign-target XR settings from preload: {string.Join(", ", evicted)}");

            int generalCount = current.Count(o => o is XRGeneralSettings);
            if (generalCount != 1)
                Debug.LogError($"[QuestBase] {generalCount} XRGeneralSettings in preload — must be exactly 1, "
                             + "or Awake() ordering decides which one wins at runtime");
        }

        [MenuItem("Quest/Setup Base Scene", false, 5)]
        public static void SetupBaseScene()
        {
            EnsureDirectory(MaterialsPath);
            EnsureDirectory(ScenesPath);

            ImportXRISamples();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Materials
            CreateUnlitMaterial("RuntimeUnlit", Color.white);
            CreateUnlitMaterial("RuntimeLine", new Color(0.27f, 0.53f, 1f, 0.6f));
            CreateMaterial("Floor", HexColor("2a2a3a"));

            // XR Origin
            var xrOrigin = InstantiateXROrigin();

            // Camera
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = HexColor("0d0d14");
                mainCam.nearClipPlane = 0.01f;
                mainCam.farClipPlane = 1000f;
            }

            // Lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = HexColor("1a1a3a") * 0.4f;

            var light = new GameObject("MainLight");
            var lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.color = HexColor("6688cc");
            lightComp.intensity = 1.0f;
            lightComp.range = 30f;
            light.transform.position = new Vector3(0f, 4f, 0f);

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/Floor.mat");
            if (floorMat != null) floor.GetComponent<Renderer>().material = floorMat;

            // Bootstrap
            var bootstrapGO = new GameObject("AppBootstrap");
            bootstrapGO.AddComponent<QuestBase.Runtime.AppBootstrap>();

            // Save scene
            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = EditorBuildSettings.scenes;
            bool alreadyAdded = buildScenes.Any(s => s.path == ScenePath);
            if (!alreadyAdded)
            {
                var newScenes = new EditorBuildSettingsScene[buildScenes.Length + 1];
                buildScenes.CopyTo(newScenes, 0);
                newScenes[buildScenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = newScenes;
            }

            Debug.Log($"[QuestBase] Base scene saved to {ScenePath}");
        }

        [MenuItem("Quest/Build APK", false, 10)]
        public static void BuildAPK()
        {
            ConfigureXRSettings();

            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "build");
            Directory.CreateDirectory(buildDir);
            string apkPath = Path.Combine(buildDir, "quest-app.apk");

            // Include whichever scene(s) exist. Apps may have just Main, just
            // ModelViewer, or both — skip any that weren't created.
            var scenes = new[] { LauncherScenePath, ScenePath, ModelViewerScenePath }
                .Where(s => File.Exists(s))
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[QuestBase] No scenes to build. Run SetupBaseScene, SetupModelViewerScene, or SetupLauncherScene first.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[QuestBase] Including scenes: {string.Join(", ", scenes)}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[QuestBase] Building APK to {apkPath}...");
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[QuestBase] Build succeeded! APK: {apkPath} ({report.summary.totalSize / 1024 / 1024}MB)");
            }
            else
            {
                Debug.LogError($"[QuestBase] Build FAILED: {report.summary.result}");
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Warning)
                            Debug.LogError($"[QuestBase] {msg.content}");
                EditorApplication.Exit(1);
            }
        }

        // --- Model Viewer Scene ---

        [MenuItem("Quest/Setup Model Viewer Scene", false, 6)]
        public static void SetupModelViewerScene()
        {
            EnsureDirectory(MaterialsPath);
            EnsureDirectory(ScenesPath);

            ImportXRISamples();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Materials
            CreateUnlitMaterial("RuntimeUnlit", Color.white);
            CreateMaterial("Floor", HexColor("2a2a3a"));
            CreateMaterial("Platform", HexColor("3a3a4a"));

            // XR Origin
            var xrOrigin = InstantiateXROrigin();

            // Camera — far clip extended for large models
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = HexColor("0d0d14");
                mainCam.nearClipPlane = 0.01f;
                mainCam.farClipPlane = 1000f;
            }

            // Three-point lighting for model inspection
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = HexColor("2a2a3a") * 0.5f;

            CreateLight("KeyLight", LightType.Directional, HexColor("ffe8d0"), 1.2f,
                new Vector3(50f, -30f, 0f), new Vector3(0f, 5f, -3f));
            CreateLight("FillLight", LightType.Directional, HexColor("d0e0ff"), 0.5f,
                new Vector3(30f, 150f, 0f), new Vector3(0f, 3f, 3f));
            CreateLight("RimLight", LightType.Directional, HexColor("ffffff"), 0.3f,
                new Vector3(10f, -160f, 0f), new Vector3(0f, 4f, 2f));

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/Floor.mat");
            if (floorMat != null) floor.GetComponent<Renderer>().material = floorMat;

            // Platform pedestal (visual only)
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.position = new Vector3(0f, 0.4f, -2f);
            pedestal.transform.localScale = new Vector3(1.5f, 0.4f, 1.5f);
            var pedestalMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/Platform.mat");
            if (pedestalMat != null) pedestal.GetComponent<Renderer>().material = pedestalMat;

            // Model Platform (script)
            var platformGO = new GameObject("ModelPlatform");
            platformGO.transform.position = new Vector3(0f, 0f, -2f);
            var platform = platformGO.AddComponent<QuestBase.Runtime.ModelPlatform>();
            platformGO.AddComponent<QuestBase.Runtime.ModelLoader>();

            // Auto-load the first bundled FBX from Assets/Models if one exists
            var bundledModel = LoadBundledFBX();
            if (bundledModel != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(bundledModel);
                platform.PlaceModel(instance);
                Debug.Log($"[QuestBase] Placed bundled FBX: {bundledModel.name}");
            }

            // Model Info HUD
            var hudGO = new GameObject("ModelInfoHUD");
            hudGO.transform.position = new Vector3(-1.5f, 1.8f, -2f);
            hudGO.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            var hud = hudGO.AddComponent<QuestBase.Runtime.ModelInfoHUD>();
            SetSerializedField(hud, "platform", platform);

            // Orbit Controller (on a camera rig)
            var orbitGO = new GameObject("OrbitRig");
            var orbit = orbitGO.AddComponent<QuestBase.Runtime.OrbitController>();

            // Bootstrap
            var bootstrapGO = new GameObject("AppBootstrap");
            bootstrapGO.AddComponent<QuestBase.Runtime.AppBootstrap>();

            // Save scene
            EditorSceneManager.SaveScene(scene, ModelViewerScenePath);

            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == ModelViewerScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ModelViewerScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            Debug.Log($"[QuestBase] Model Viewer scene saved to {ModelViewerScenePath}");
        }

        // --- Launcher Scene (quest-launcher Phase 2) ---

        // Standalone entry point for scripts/build.sh. AssetDatabase.ImportPackage is
        // asynchronous even under -batchmode, so the reliable way to guarantee the
        // essentials are on disk before a build is to import them in a Unity process
        // that then exits — not mid-build.
        [MenuItem("Quest/Ensure TMP Essentials", false, 6)]
        public static void EnsureTMPEssentials()
        {
            EnsureTMPEssentialResources();
            AssetDatabase.SaveAssets();
            Debug.Log(File.Exists(TMPSentinelPath)
                ? $"[QuestBase] TMP essentials present at {TMPSentinelPath}"
                : $"[QuestBase] TMP essentials STILL MISSING at {TMPSentinelPath}");
        }

        [MenuItem("Quest/Setup Launcher Scene", false, 7)]
        public static void SetupLauncherScene()
        {
            EnsureDirectory(MaterialsPath);
            EnsureDirectory(ScenesPath);
            EnsureDirectory(PrefabsPath);

            ImportXRISamples();
            EnsureTMPEssentialResources();

            // Material must exist before EnsureAppCardPrefab so the prefab binds
            // to it instead of falling through to the Quad's default URP/Lit.
            CreateUnlitMaterial("RuntimeUnlit", Color.white);

            var cardPrefab = EnsureAppCardPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            InstantiateXROrigin();

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                // Passthrough: clear to transparent so MR background bleeds through.
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                mainCam.nearClipPlane = 0.01f;
                mainCam.farClipPlane = 100f;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = HexColor("404040");

            // Shelf anchor at typical eye height; Phase 3 will replace with OVRSpatialAnchor.
            var shelfRoot = new GameObject("ShelfRoot");
            shelfRoot.transform.position = new Vector3(0f, 1.4f, 0f);
            var shelf = shelfRoot.AddComponent<ShelfManager>();
            SetSerializedField(shelf, "cardPrefab", cardPrefab);

            var bootstrapGO = new GameObject("AppBootstrap");
            bootstrapGO.AddComponent<QuestBase.Runtime.AppBootstrap>();

            EditorSceneManager.SaveScene(scene, LauncherScenePath);

            // Make Launcher the only scene in the build — Phase 1's smoke test runs
            // off whichever scene loads, but the launcher is the product entry point.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LauncherScenePath, true)
            };

            Debug.Log($"[QuestBase] Launcher scene saved to {LauncherScenePath}");
        }

        // TMP_Text in batch mode needs the Essential Resources (font asset, default
        // shaders) imported once. The first-run dialog never fires under -batchmode,
        // so labels render blank without this. We import the unitypackage shipped
        // with the ugui package — runs once, no-op on subsequent builds.
        public static void EnsureTMPEssentialResources()
        {
            if (File.Exists(TMPSentinelPath)) return;

            var pkgRoot = "Library/PackageCache";
            if (!Directory.Exists(pkgRoot))
            {
                Debug.LogWarning("[QuestBase] TMP Essentials: PackageCache missing; skipping import");
                return;
            }
            var packages = Directory.GetFiles(pkgRoot, "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
            if (packages.Length == 0)
            {
                Debug.LogWarning("[QuestBase] TMP Essentials: package not found in PackageCache; labels may render blank");
                return;
            }
            Debug.Log($"[QuestBase] Importing TMP Essential Resources from {packages[0]}");
            AssetDatabase.ImportPackage(packages[0], false);
            AssetDatabase.Refresh();
        }

        private static GameObject EnsureAppCardPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(AppCardPrefabPath);
            if (existing != null) return existing;

            // Card root: Quad with collider + XRSimpleInteractable, used as the icon surface.
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = "AppCard";
            root.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

            // Replace default URP/Lit with the runtime unlit material so the icon texture
            // shows without lighting tricks.
            var iconMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/RuntimeUnlit.mat");
            if (iconMat != null)
            {
                root.GetComponent<MeshRenderer>().sharedMaterial = iconMat;
            }

            // Quad's BoxCollider sits at z=0 with very thin depth — XRI ray casts may miss
            // edge-on. Beef up to a 0.22 × 0.22 × 0.04 box for forgiving selection.
            Object.DestroyImmediate(root.GetComponent<MeshCollider>());
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.1f, 1.1f, 0.2f);

            root.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var card = root.AddComponent<AppCard>();
            SetSerializedField(card, "iconRenderer", root.GetComponent<MeshRenderer>());

            // Label: TMP child below the icon.
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(root.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, -0.65f, 0f);
            labelGO.transform.localScale = Vector3.one * 0.05f;

            var tmp = labelGO.AddComponent<TMPro.TextMeshPro>();
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 4f;
            tmp.color = Color.white;
            tmp.text = "App";
            // Bound TMP to a sane rect so wrap behaves predictably.
            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(6f, 1.2f);

            SetSerializedField(card, "labelText", tmp);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, AppCardPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[QuestBase] AppCard prefab saved to {AppCardPrefabPath}");
            return prefab;
        }

        private const string ModelsPath = "Assets/Models";

        /// <summary>
        /// Find the first .fbx file in Assets/Models (alphabetical) and return it as a prefab.
        /// Returns null if no FBX is found.
        /// </summary>
        private static GameObject LoadBundledFBX()
        {
            if (!AssetDatabase.IsValidFolder(ModelsPath))
            {
                Debug.Log("[QuestBase] No Assets/Models folder — skipping bundled model");
                return null;
            }

            var guids = AssetDatabase.FindAssets("t:Model", new[] { ModelsPath });
            var fbxPaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .ToList();

            if (fbxPaths.Count == 0)
            {
                Debug.Log("[QuestBase] No .fbx files in Assets/Models — skipping bundled model");
                return null;
            }

            Debug.Log($"[QuestBase] Found {fbxPaths.Count} FBX file(s), using: {fbxPaths[0]}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(fbxPaths[0]);
        }

        private static void SetSerializedField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[QuestBase] Field '{fieldName}' not found on {target.GetType().Name}");
            }
        }

        private static void CreateLight(string name, LightType type, Color color, float intensity,
            Vector3 eulerAngles, Vector3 position)
        {
            var go = new GameObject(name);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(eulerAngles);
        }

        // --- Validation & Debug ---

        [MenuItem("Quest/Compile Check", false, 20)]
        public static void CompileCheck()
        {
            Debug.Log("[QuestBase] Starting compile check...");

            var messages = new List<CompilerMessage>();
            bool compiled = false;

            CompilationPipeline.compilationFinished += (obj) => { compiled = true; };
            CompilationPipeline.assemblyCompilationFinished += (path, msgs) =>
            {
                messages.AddRange(msgs);
            };

            CompilationPipeline.RequestScriptCompilation(
                RequestScriptCompilationOptions.CleanBuildCache);

            // In batch mode, compilation is synchronous after RequestScriptCompilation
            int errors = messages.Count(m => m.type == CompilerMessageType.Error);
            int warnings = messages.Count(m => m.type == CompilerMessageType.Warning);

            if (errors > 0)
            {
                foreach (var msg in messages.Where(m => m.type == CompilerMessageType.Error))
                    Debug.LogError($"[QuestBase] COMPILE ERROR: {msg.message}");
                Debug.LogError($"[QuestBase] Compile check FAILED: {errors} errors, {warnings} warnings");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log($"[QuestBase] Compile check PASSED: 0 errors, {warnings} warnings");
            }
        }

        [MenuItem("Quest/Validate Model", false, 21)]
        public static void ValidateModel()
        {
            // Usage: pass model path via -modelPath argument
            string modelPath = GetCommandLineArg("-modelPath");
            if (string.IsNullOrEmpty(modelPath))
            {
                Debug.LogError("[QuestBase] ValidateModel requires -modelPath argument");
                Debug.LogError("[QuestBase] Usage: -executeMethod QuestBase.Editor.QuestBuildTools.ValidateModel -modelPath /path/to/model.glb");
                EditorApplication.Exit(1);
                return;
            }

            if (!File.Exists(modelPath))
            {
                Debug.LogError($"[QuestBase] Model file not found: {modelPath}");
                EditorApplication.Exit(1);
                return;
            }

            var fileInfo = new FileInfo(modelPath);
            Debug.Log($"[QuestBase] Validating model: {modelPath}");
            Debug.Log($"[QuestBase]   File size: {fileInfo.Length / 1024f / 1024f:F1} MB");
            Debug.Log($"[QuestBase]   Extension: {fileInfo.Extension}");

            string ext = fileInfo.Extension.ToLower();
            if (ext != ".glb" && ext != ".gltf")
            {
                Debug.LogWarning($"[QuestBase]   Format {ext} is not glTF/GLB. ModelLoader only supports .glb and .gltf.");
                Debug.LogWarning("[QuestBase]   Convert to glTF first, or add a format-specific loader.");
            }

            // Try to read glTF header for basic validation
            if (ext == ".glb")
            {
                using (var stream = File.OpenRead(modelPath))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length >= 12)
                    {
                        uint magic = reader.ReadUInt32();
                        uint version = reader.ReadUInt32();
                        uint length = reader.ReadUInt32();

                        if (magic == 0x46546C67) // "glTF"
                        {
                            Debug.Log($"[QuestBase]   GLB valid: version={version}, declared length={length / 1024f / 1024f:F1} MB");
                        }
                        else
                        {
                            Debug.LogError($"[QuestBase]   Invalid GLB magic: 0x{magic:X8} (expected 0x46546C67)");
                            EditorApplication.Exit(1);
                            return;
                        }
                    }
                }
            }

            // Size warnings for Quest 3
            if (fileInfo.Length > 500 * 1024 * 1024)
                Debug.LogWarning("[QuestBase]   WARNING: File > 500MB — may cause out-of-memory on Quest 3");
            else if (fileInfo.Length > 100 * 1024 * 1024)
                Debug.LogWarning("[QuestBase]   CAUTION: File > 100MB — loading may be slow on Quest 3");
            else
                Debug.Log("[QuestBase]   Size OK for Quest 3");

            Debug.Log("[QuestBase] Validation complete");
        }

        [MenuItem("Quest/Validate Scene", false, 22)]
        public static void ValidateScene()
        {
            string scenePath = ScenePath;
            if (!File.Exists(scenePath))
                scenePath = ModelViewerScenePath;
            if (!File.Exists(scenePath))
            {
                Debug.LogError("[QuestBase] No scene found. Run SetupBaseScene or SetupModelViewerScene first.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[QuestBase] Validating scene: {scenePath}");

            var scene = EditorSceneManager.OpenScene(scenePath);
            var rootObjects = scene.GetRootGameObjects();
            int issues = 0;

            foreach (var root in rootObjects)
            {
                var components = root.GetComponentsInChildren<Component>(true);
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        Debug.LogWarning($"[QuestBase]   Missing script on: {GetFullPath(root.transform)}");
                        issues++;
                        continue;
                    }

                    // Check for missing references in serialized fields
                    var so = new SerializedObject(comp);
                    var prop = so.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                            prop.objectReferenceValue == null &&
                            prop.objectReferenceInstanceIDValue != 0)
                        {
                            Debug.LogWarning($"[QuestBase]   Missing reference: {comp.GetType().Name}.{prop.name} on {comp.gameObject.name}");
                            issues++;
                        }
                    }
                }
            }

            if (issues > 0)
            {
                Debug.LogWarning($"[QuestBase] Scene validation: {issues} issues found");
            }
            else
            {
                Debug.Log("[QuestBase] Scene validation: PASSED (no missing references)");
            }
        }

        [MenuItem("Quest/Build And Deploy", false, 15)]
        public static void BuildAndDeploy()
        {
            Debug.Log("[QuestBase] === BUILD AND DEPLOY ===");

            // Step 1: Compile check
            Debug.Log("[QuestBase] Step 1/4: Compile check...");
            // Compilation happens implicitly when Unity opens

            // Step 2: Configure
            Debug.Log("[QuestBase] Step 2/4: Configure XR settings...");
            ConfigureXRSettings();

            // Step 3: Build
            Debug.Log("[QuestBase] Step 3/4: Building APK...");
            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "build");
            Directory.CreateDirectory(buildDir);
            string apkPath = Path.Combine(buildDir, "quest-app.apk");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath, ModelViewerScenePath }
                    .Where(s => File.Exists(s)).ToArray(),
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[QuestBase] Build FAILED: {report.summary.result}");
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error)
                            Debug.LogError($"[QuestBase] {msg.content}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[QuestBase] Step 4/4: Build succeeded ({report.summary.totalSize / 1024 / 1024}MB)");
            Debug.Log($"[QuestBase] APK: {apkPath}");
            Debug.Log("[QuestBase] Run: adb install -r build/quest-app.apk");
        }

        private static string GetCommandLineArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }

        private static string GetFullPath(Transform t)
        {
            if (t.parent == null) return t.name;
            return GetFullPath(t.parent) + "/" + t.name;
        }

        // --- Helpers ---

        private static GameObject InstantiateXROrigin()
        {
            string[] guids = AssetDatabase.FindAssets("XR Origin t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Starter Assets") && path.Contains("XR Origin (XR Rig)"))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        Debug.Log($"[QuestBase] Instantiated XR Origin from {path}");
                        return instance;
                    }
                }
            }

            Debug.LogError("[QuestBase] XR Origin (XR Rig) prefab not found! " +
                "Run 'Quest/Import XRI Samples' first.");
            return new GameObject("XR Origin (placeholder)");
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[QuestBase] URP/Lit shader not found!");
                return null;
            }
            var mat = new Material(shader);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material CreateUnlitMaterial(string name, Color color)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[QuestBase] URP/Unlit shader not found!");
                return null;
            }
            var mat = new Material(shader);
            mat.color = color;

            if (color.a < 1f)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }

        private static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color color))
                return color;
            return Color.white;
        }
    }

    // TMP_Text resolves its default font asset through TMP_Settings, which only
    // exists once the Essential Resources are imported. Without them every
    // TextMeshPro Awake() throws a NullReferenceException and cards render label-less.
    //
    // EnsureTMPEssentialResources is called from SetupLauncherScene, so building via
    // BuildAPK directly (or scripts/build.sh --skip-scene) silently skipped it and
    // produced an APK whose labels always throw. An hour of build time to find out at
    // runtime is the wrong trade: fail here instead, in seconds.
    public class TMPEssentialsBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platformGroup != BuildTargetGroup.Android)
                return;

            QuestBuildTools.EnsureTMPEssentialResources();

            if (!File.Exists(QuestBuildTools.TMPSentinelPath))
            {
                throw new BuildFailedException(
                    $"[QuestBase] TMP Essential Resources missing ({QuestBuildTools.TMPSentinelPath}). "
                    + "Every TextMeshPro component will throw at runtime and all labels will be blank. "
                    + "Run `Quest > Ensure TMP Essentials` (or -executeMethod "
                    + "QuestBase.Editor.QuestBuildTools.EnsureTMPEssentials) in its own Unity invocation, "
                    + "then rebuild. ImportPackage is async, so it cannot be relied on mid-build.");
            }
        }
    }

    public class XRPreloadBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platformGroup != BuildTargetGroup.Android)
                return;
            QuestBuildTools.EnsureXRPreloadedAssets();
        }
    }
}
