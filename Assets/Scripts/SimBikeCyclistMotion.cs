using SBPScripts.Simulator;
using UnityEngine;

namespace CyclingExperiment
{
    /// <summary>
    /// Adapts SimBike rigidbody physics to <see cref="ICyclistMotion"/> for experiment systems.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class SimBikeCyclistMotion : MonoBehaviour, ICyclistMotion
    {
        [SerializeField] BicycleSimulatorController simulator;
        [SerializeField] SimBikeSpawnController spawnController;
        [SerializeField] Rigidbody rootBody;

        Rigidbody[] _bodies;

        public Transform Transform => transform;

        public float GetSpeedMps()
        {
            if (rootBody == null) return 0f;
            Vector3 v = rootBody.linearVelocity;
            v.y = 0f;
            return v.magnitude;
        }

        public float GetSpeedKph() => GetSpeedMps() * 3.6f;

        public float GetSteeringAngleDeg()
        {
            CacheRefs();
            if (simulator == null || simulator.cycleGeometry.handles == null)
                return float.NaN;

            float angle = simulator.cycleGeometry.handles.transform.localEulerAngles.y;
            if (angle > 180f)
                angle -= 360f;
            return angle;
        }

        public float MaxSpeedMps
        {
            get => simulator != null ? simulator.topSpeed : 0f;
            set
            {
                if (simulator != null)
                    simulator.topSpeed = Mathf.Max(0.1f, value);
            }
        }

        private void Awake()
        {
            CacheRefs();
        }

        private void CacheRefs()
        {
            if (simulator == null) simulator = GetComponent<BicycleSimulatorController>();
            if (spawnController == null) spawnController = GetComponent<SimBikeSpawnController>();
            if (rootBody == null) rootBody = GetComponent<Rigidbody>();
            if (_bodies == null || _bodies.Length == 0)
                _bodies = GetComponentsInChildren<Rigidbody>(true);
        }

        public void Teleport(Vector3 worldPosition, float yawDegrees)
        {
            CacheRefs();
            if (spawnController != null)
            {
                spawnController.TeleportTo(worldPosition, yawDegrees);
                return;
            }

            ApplyPoseImmediate(worldPosition, Quaternion.Euler(0f, yawDegrees, 0f));
        }

        public void SetWorldPositionKeepYaw(Vector3 worldPosition)
        {
            CacheRefs();
            if (_bodies == null || _bodies.Length == 0)
            {
                transform.position = worldPosition;
                return;
            }

            Vector3 delta = worldPosition - transform.position;
            foreach (var body in _bodies)
            {
                if (body == null) continue;
                body.position += delta;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
            simulator?.HaltIntegratedVelocity();
        }

        public void StopLongitudinalSpeed()
        {
            CacheRefs();
            if (_bodies == null) return;

            foreach (var body in _bodies)
            {
                if (body == null) continue;
                Vector3 v = body.linearVelocity;
                v.x = 0f;
                v.z = 0f;
                body.linearVelocity = v;
                body.angularVelocity = Vector3.zero;
            }

            simulator?.HaltIntegratedVelocity();
        }

        private void ApplyPoseImmediate(Vector3 position, Quaternion rotation)
        {
            foreach (var body in _bodies)
            {
                if (body == null) continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            simulator?.HaltIntegratedVelocity();
        }
    }
}
