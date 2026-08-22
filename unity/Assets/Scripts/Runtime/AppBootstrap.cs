using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace QuestBase.Runtime
{
    /// <summary>
    /// Logs XR status at startup. XR initialization is handled automatically by
    /// Unity's XR Plugin Management via XRGeneralSettings (m_InitManagerOnStart).
    ///
    /// The single-shot version of this logged `loaderCount=0` and nothing else, which
    /// was not enough to act on. Two things matter and neither was being reported:
    ///
    ///  - WHICH XRGeneralSettings won. Awake() assigns s_RuntimeSettingsInstance with
    ///    no build-target check, so with more than one preloaded, the last to Awake
    ///    wins. Logging settings.name tells you instantly whether Android's settings
    ///    are live or another target's empty ones are.
    ///  - That loaderCount is CONFIGURATION, not liveness. XRManagerSettings.activeLoaders
    ///    is a direct expression-body over m_Loaders, so 0 means nothing was serialized
    ///    into the player — it never means "a loader tried and failed".
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        // Long enough to cover any plausible async init, short enough to still be
        // on screen while you are watching logcat.
        const float PollSeconds = 10f;
        const float PollInterval = 0.5f;

        void Start()
        {
            var settings = XRGeneralSettings.Instance;
            if (settings == null)
            {
                Debug.LogWarning("[AppBootstrap] XRGeneralSettings.Instance is null — no XRGeneralSettings "
                               + "was preloaded into the player, so nothing ever called Awake()");
                LogDiscoveredApps();
                return;
            }

            var mgr = settings.Manager;
            if (mgr == null)
            {
                Debug.LogWarning($"[AppBootstrap] '{settings.name}' has a null Manager — "
                               + "the XRManagerSettings sub-asset was not preloaded");
                LogDiscoveredApps();
                return;
            }

            // The load-bearing line. Anything other than the Android pair here means
            // a foreign build target's settings won the Awake race.
            Debug.Log($"[AppBootstrap] resolved settings='{settings.name}' manager='{mgr.name}' "
                    + $"InitManagerOnStart={settings.InitManagerOnStart}");
            Debug.Log($"[AppBootstrap] {Describe(mgr)}");

            if (!settings.name.StartsWith("Android"))
            {
                Debug.LogError($"[AppBootstrap] WRONG BUILD TARGET: '{settings.name}' won the preload Awake race. "
                             + "Only the active target's XRGeneralSettings may be in PlayerSettings.preloadedAssets.");
            }

            StartCoroutine(WatchXRInit(mgr));
            LogDiscoveredApps();
        }

        static string Describe(XRManagerSettings mgr)
        {
            var loaders = mgr.activeLoaders;
            string names = (loaders == null || loaders.Count == 0)
                ? "-"
                : string.Join(",", loaders.Select(l => l == null ? "NULL-REF" : l.name));
            return $"initComplete={mgr.isInitializationComplete} "
                 + $"activeLoader={(mgr.activeLoader != null ? mgr.activeLoader.name : "null")} "
                 + $"loaderCount={loaders?.Count ?? 0} loaders=[{names}] "
                 + $"xrEnabled={XRSettings.enabled} device='{XRSettings.loadedDeviceName}' "
                 + $"deviceActive={XRSettings.isDeviceActive}";
        }

        IEnumerator WatchXRInit(XRManagerSettings mgr)
        {
            float elapsed = 0f;
            while (elapsed < PollSeconds)
            {
                if (mgr.isInitializationComplete && mgr.activeLoader != null)
                {
                    Debug.Log($"[AppBootstrap] XR ready after {elapsed:F1}s — {Describe(mgr)}");
                    yield break;
                }

                yield return new WaitForSeconds(PollInterval);
                elapsed += PollInterval;
                Debug.Log($"[AppBootstrap] t={elapsed:F1}s {Describe(mgr)}");
            }

            Debug.LogWarning($"[AppBootstrap] XR never initialised within {PollSeconds}s — {Describe(mgr)}");

            if (mgr.activeLoaders == null || mgr.activeLoaders.Count == 0)
            {
                // Nothing to initialise: this is a build-pipeline fault, not a runtime one.
                Debug.LogError("[AppBootstrap] loader list is empty — the OpenXR loader was never serialized "
                             + "into this settings object. Fix the preload, not the runtime.");
                yield break;
            }

            // Loaders ARE configured but automatic startup did not run them. Automatic
            // init fires at AfterAssembliesLoaded, long before this Start(), so a manual
            // attempt now cannot be racing it — if this works, automatic startup is the
            // fault; if it fails, the loader itself is, and the reason will be on the
            // Unity/OpenXR logcat tags rather than ours.
            Debug.Log("[AppBootstrap] loaders configured but idle — attempting manual InitializeLoaderSync()...");
            mgr.InitializeLoaderSync();
            Debug.Log($"[AppBootstrap] after manual init — {Describe(mgr)}");

            if (mgr.activeLoader != null)
            {
                mgr.StartSubsystems();
                Debug.Log($"[AppBootstrap] manual init WORKED, subsystems started — {Describe(mgr)}");
            }
            else
            {
                Debug.LogError("[AppBootstrap] manual init also failed — capture UNFILTERED logcat; "
                             + "the OpenXR error will be on the Unity tag, not the QuestLauncher prefix");
            }
        }

        void LogDiscoveredApps()
        {
            var apps = LauncherBridge.ListApps();
            Debug.Log($"[QuestLauncher] Discovered {apps.Count} apps");
            int sample = System.Math.Min(5, apps.Count);
            for (int i = 0; i < sample; i++)
            {
                var a = apps[i];
                Debug.Log($"[QuestLauncher]   {a.label} ({a.package}) VR={a.isVR}");
            }
        }
    }
}
