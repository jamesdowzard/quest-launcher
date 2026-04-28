using UnityEngine;

namespace QuestBase.Runtime
{
    /// <summary>
    /// Thumbstick-based orbit/pan/zoom around a target point.
    /// Designed for VR controllers but works with keyboard fallback.
    ///
    /// VR controls:
    ///   Left stick  = pan (horizontal + vertical)
    ///   Right stick  = orbit (horizontal = yaw, vertical = pitch)
    ///   Right grip   = zoom (hold + right stick Y)
    ///   A button     = reset view
    ///
    /// Desktop fallback:
    ///   WASD = pan, QE = orbit, RF = zoom, Space = reset
    /// </summary>
    public class OrbitController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform to orbit around. Usually the ModelPlatform.")]
        [SerializeField] private Transform target;

        [Header("Orbit")]
        [SerializeField] private float orbitSpeed = 90f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 2f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float minDistance = 0.5f;
        [SerializeField] private float maxDistance = 20f;

        [Header("Initial View")]
        [SerializeField] private float initialDistance = 3f;
        [SerializeField] private float initialYaw = 0f;
        [SerializeField] private float initialPitch = 20f;

        private float _yaw;
        private float _pitch;
        private float _distance;
        private Vector3 _panOffset;

        void Start()
        {
            ResetView();
        }

        void Update()
        {
            if (target == null) return;

            // Read input (desktop fallback — VR input wired via XRI action maps)
            float orbitH = 0f, orbitV = 0f;
            float panH = 0f, panV = 0f;
            float zoom = 0f;

            // Desktop controls
            if (Input.GetKey(KeyCode.Q)) orbitH -= 1f;
            if (Input.GetKey(KeyCode.E)) orbitH += 1f;
            if (Input.GetKey(KeyCode.W)) panV += 1f;
            if (Input.GetKey(KeyCode.S)) panV -= 1f;
            if (Input.GetKey(KeyCode.A)) panH -= 1f;
            if (Input.GetKey(KeyCode.D)) panH += 1f;
            if (Input.GetKey(KeyCode.R)) zoom += 1f;
            if (Input.GetKey(KeyCode.F)) zoom -= 1f;
            if (Input.GetKeyDown(KeyCode.Space)) ResetView();

            // Apply orbit
            _yaw += orbitH * orbitSpeed * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch - orbitV * orbitSpeed * Time.deltaTime, minPitch, maxPitch);

            // Apply zoom
            _distance = Mathf.Clamp(_distance - zoom * zoomSpeed * Time.deltaTime, minDistance, maxDistance);

            // Apply pan (in camera-local space)
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            _panOffset += (right * panH + up * panV) * panSpeed * Time.deltaTime;

            // Calculate position from spherical coordinates
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 position = target.position + _panOffset + rotation * new Vector3(0f, 0f, -_distance);

            transform.position = position;
            transform.LookAt(target.position + _panOffset);
        }

        /// <summary>
        /// Apply VR thumbstick input. Call from an XRI input action binding.
        /// </summary>
        public void ApplyOrbitInput(Vector2 stick)
        {
            _yaw += stick.x * orbitSpeed * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch - stick.y * orbitSpeed * Time.deltaTime, minPitch, maxPitch);
        }

        /// <summary>
        /// Apply VR pan input. Call from an XRI input action binding.
        /// </summary>
        public void ApplyPanInput(Vector2 stick)
        {
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            _panOffset += (right * stick.x + up * stick.y) * panSpeed * Time.deltaTime;
        }

        /// <summary>
        /// Apply VR zoom input. Call from grip + stick Y.
        /// </summary>
        public void ApplyZoom(float amount)
        {
            _distance = Mathf.Clamp(_distance - amount * zoomSpeed * Time.deltaTime, minDistance, maxDistance);
        }

        public void ResetView()
        {
            _yaw = initialYaw;
            _pitch = initialPitch;
            _distance = initialDistance;
            _panOffset = Vector3.zero;
        }

        public void SetTarget(Transform t)
        {
            target = t;
            ResetView();
        }
    }
}
