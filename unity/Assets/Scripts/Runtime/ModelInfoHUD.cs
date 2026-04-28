using UnityEngine;

namespace QuestBase.Runtime
{
    /// <summary>
    /// World-space text panel showing model statistics.
    /// Uses Unity's built-in TextMesh (no TMP dependency in base template).
    /// Position it relative to the ModelPlatform.
    /// </summary>
    public class ModelInfoHUD : MonoBehaviour
    {
        [SerializeField] private ModelPlatform platform;

        private TextMesh _textMesh;

        void Awake()
        {
            _textMesh = GetComponent<TextMesh>();
            if (_textMesh == null)
            {
                _textMesh = gameObject.AddComponent<TextMesh>();
                _textMesh.fontSize = 32;
                _textMesh.characterSize = 0.02f;
                _textMesh.anchor = TextAnchor.UpperLeft;
                _textMesh.color = new Color(0.7f, 0.8f, 1f);
            }

            UpdateDisplay(null);
        }

        void Start()
        {
            if (platform != null && platform.CurrentModel != null)
            {
                UpdateDisplay(platform.CurrentModel);
            }
        }

        /// <summary>
        /// Update the HUD with stats from a loaded model.
        /// </summary>
        public void UpdateDisplay(GameObject model)
        {
            if (model == null)
            {
                _textMesh.text = "No model loaded";
                return;
            }

            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            var meshFilters = model.GetComponentsInChildren<MeshFilter>();

            int vertexCount = 0;
            int triCount = 0;
            int materialCount = 0;

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    vertexCount += mf.sharedMesh.vertexCount;
                    triCount += mf.sharedMesh.triangles.Length / 3;
                }
            }

            var materials = new System.Collections.Generic.HashSet<Material>();
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null) materials.Add(m);
                }
            }
            materialCount = materials.Count;

            var bounds = ModelPlatform.CalculateBounds(model);

            _textMesh.text =
                $"Vertices: {vertexCount:N0}\n" +
                $"Triangles: {triCount:N0}\n" +
                $"Materials: {materialCount}\n" +
                $"Meshes: {meshFilters.Length}\n" +
                $"Bounds: {bounds.size.x:F2} x {bounds.size.y:F2} x {bounds.size.z:F2}m\n" +
                $"Children: {model.transform.GetComponentsInChildren<Transform>().Length}";
        }
    }
}
