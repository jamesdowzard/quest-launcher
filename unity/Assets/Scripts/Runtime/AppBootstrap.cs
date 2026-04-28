using UnityEngine;
using UnityEngine.XR.Management;

namespace QuestBase.Runtime
{
    /// <summary>
    /// Logs XR status at startup. XR initialization is handled automatically by
    /// Unity's XR Plugin Management via XRGeneralSettings (m_InitManagerOnStart).
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        void Start()
        {
            var settings = XRGeneralSettings.Instance;
            if (settings == null)
            {
                Debug.Log("[AppBootstrap] XRGeneralSettings.Instance is null");
                return;
            }

            var mgr = settings.Manager;
            if (mgr == null)
            {
                Debug.Log("[AppBootstrap] XRGeneralSettings.Manager is null");
                return;
            }

            Debug.Log($"[AppBootstrap] initComplete={mgr.isInitializationComplete} activeLoader={mgr.activeLoader?.name ?? "null"} loaderCount={mgr.activeLoaders?.Count ?? 0}");

            if (mgr.activeLoader != null)
            {
                Debug.Log($"[AppBootstrap] VR active via: {mgr.activeLoader.name}");
            }
        }
    }
}
