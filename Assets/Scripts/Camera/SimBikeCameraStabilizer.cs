using UnityEngine;

namespace CyclingExperiment.Camera
{
    /// <summary>
    /// Keeps a SimBike camera rig level: follows the bike root position and yaw only,
    /// cancelling pitch/roll inherited from rigidbody physics. Attach to VR_Glasses
    /// (not Camera Offset) so XR Origin / TrackedPoseDriver can still manage the HMD.
    /// </summary>
    [DisallowMultipleComponent]
    public class SimBikeCameraStabilizer : MonoBehaviour
    {
        [SerializeField, Tooltip("Physics root to follow (SimBike). Defaults to root parent.")]
        Transform followRoot;

        [SerializeField, Tooltip("Smooth vertical bumps from the road (seconds). 0 = off.")]
        float verticalSmoothTime = 0.08f;

        [SerializeField, Tooltip("Local offset from follow root in yaw-only space (captured at Start if zero).")]
        Vector3 localPositionOffset = Vector3.zero;

        Vector3 _velocityY;
        float _smoothedY;
        bool _hasSmoothedY;
        Quaternion _localRotationOffset = Quaternion.identity;
        bool _capturedOffset;

        void Awake()
        {
            if (followRoot == null)
            {
                Transform t = transform.parent;
                while (t != null && t.parent != null)
                    t = t.parent;
                followRoot = t != null ? t : transform.root;
            }
        }

        void Start()
        {
            CaptureOffsetIfNeeded();
            if (followRoot != null)
            {
                _smoothedY = followRoot.position.y + localPositionOffset.y;
                _hasSmoothedY = true;
            }
        }

        void LateUpdate()
        {
            if (followRoot == null) return;
            CaptureOffsetIfNeeded();

            float yaw = followRoot.eulerAngles.y;
            Quaternion yawOnly = Quaternion.Euler(0f, yaw, 0f);

            Vector3 desired = followRoot.position + yawOnly * localPositionOffset;
            if (verticalSmoothTime > 0.0001f)
            {
                if (!_hasSmoothedY)
                {
                    _smoothedY = desired.y;
                    _hasSmoothedY = true;
                }

                _smoothedY = Mathf.SmoothDamp(
                    _smoothedY,
                    desired.y,
                    ref _velocityY.y,
                    verticalSmoothTime
                );
                desired.y = _smoothedY;
            }

            transform.SetPositionAndRotation(desired, yawOnly * _localRotationOffset);
        }

        void CaptureOffsetIfNeeded()
        {
            if (_capturedOffset || followRoot == null) return;

            if (localPositionOffset.sqrMagnitude < 1e-8f)
            {
                // Express current world offset in yaw-only local space of the bike.
                float yaw = followRoot.eulerAngles.y;
                Quaternion invYaw = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f));
                localPositionOffset = invYaw * (transform.position - followRoot.position);
            }

            // Level mount — XR TrackedPoseDriver on Main Camera still applies HMD rotation.
            _localRotationOffset = Quaternion.identity;
            _capturedOffset = true;
        }

        /// <summary>
        /// Editor/runtime helper: attach stabilizer under a camera subtree.
        /// </summary>
        public static SimBikeCameraStabilizer EnsureOn(Transform cameraAnchor, Transform bikeRoot)
        {
            if (cameraAnchor == null) return null;

            var existing = cameraAnchor.GetComponent<SimBikeCameraStabilizer>();
            if (existing == null)
                existing = cameraAnchor.gameObject.AddComponent<SimBikeCameraStabilizer>();

            existing.followRoot = bikeRoot != null ? bikeRoot : cameraAnchor.root;
            return existing;
        }
    }
}
