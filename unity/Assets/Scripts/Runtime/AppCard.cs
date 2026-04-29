using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class AppCard : MonoBehaviour {

    public string PackageName { get; private set; }
    public string AppLabel { get; private set; }

    [SerializeField] private MeshRenderer iconRenderer;
    [SerializeField] private TMP_Text labelText;

    private XRSimpleInteractable _interactable;

    void Awake() {
        _interactable = GetComponent<XRSimpleInteractable>();
        _interactable.selectEntered.AddListener(_ => OnSelected());
    }

    public void Bind(AppInfo info) {
        PackageName = info.package;
        AppLabel = info.label;
        gameObject.name = $"AppCard_{info.package}";

        if (labelText != null) {
            labelText.text = info.label;
        }

        if (iconRenderer != null) {
            var icon = LauncherBridge.GetIcon(info.package);
            if (icon != null) {
                var mat = new Material(iconRenderer.sharedMaterial);
                mat.mainTexture = icon;
                iconRenderer.material = mat;
            }
        }
    }

    private void OnSelected() {
        if (string.IsNullOrEmpty(PackageName)) {
            Debug.LogWarning("[QuestLauncher] AppCard selected without bound package");
            return;
        }
        Debug.Log($"[QuestLauncher] Launching {AppLabel} ({PackageName})");
        LauncherBridge.Launch(PackageName);
    }
}
