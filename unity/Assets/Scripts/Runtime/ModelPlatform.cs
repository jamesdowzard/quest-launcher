using UnityEngine;

namespace QuestBase.Runtime
{
    /// <summary>
    /// Auto-centers and auto-scales any loaded model to a comfortable viewing size.
    /// Attach to an empty GameObject — loaded models become children of this transform.
    /// </summary>
    public class ModelPlatform : MonoBehaviour
    {
        [Tooltip("Target height of the model in metres after scaling")]
        [SerializeField] private float targetHeight = 1.5f;

        [Tooltip("Height of the platform surface above floor (metres)")]
        [SerializeField] private float platformHeight = 0.8f;

        [Tooltip("Maximum scale multiplier to prevent tiny models from becoming huge")]
        [SerializeField] private float maxScale = 50f;

        private GameObject _currentModel;
        private Vector3 _originalScale;

        /// <summary>
        /// Place a model on the platform. Auto-centers and scales it.
        /// Removes any previously placed model.
        /// </summary>
        public void PlaceModel(GameObject model)
        {
            if (_currentModel != null)
            {
                Destroy(_currentModel);
            }

            _currentModel = model;
            model.transform.SetParent(transform, false);

            // Calculate bounds of all renderers
            var bounds = CalculateBounds(model);
            if (bounds.size == Vector3.zero)
            {
                Debug.LogWarning("[ModelPlatform] Model has no renderers or zero-size bounds");
                return;
            }

            // Scale to target height
            float currentHeight = bounds.size.y;
            float scale = Mathf.Min(targetHeight / currentHeight, maxScale);
            model.transform.localScale = Vector3.one * scale;
            _originalScale = model.transform.localScale;

            // Recalculate bounds after scaling
            bounds = CalculateBounds(model);

            // Center horizontally, place bottom at platform height
            Vector3 offset = new Vector3(
                -bounds.center.x,
                platformHeight - bounds.min.y,
                -bounds.center.z
            );
            model.transform.localPosition = offset;

            Debug.Log($"[ModelPlatform] Placed model: scale={scale:F2}, bounds={bounds.size}, center={bounds.center}");
        }

        /// <summary>
        /// Reset model to its auto-scaled size and centered position.
        /// </summary>
        public void ResetTransform()
        {
            if (_currentModel != null)
            {
                _currentModel.transform.localScale = _originalScale;
                PlaceModel(_currentModel);
            }
        }

        public GameObject CurrentModel => _currentModel;

        public static Bounds CalculateBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds();

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }
    }
}
