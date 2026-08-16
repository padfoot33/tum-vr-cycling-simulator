using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// VR safety assist: auto-brake when a vehicle is ahead, plus a small lateral nudge.
    /// </summary>
    public class SmartBicycleSafetyAssistant : MonoBehaviour
    {
        [Header("Auto-Braking Settings")]
        [SerializeField] private bool enableAutoBrake = true;
        [SerializeField, Tooltip("Distance at which auto-braking begins")]
        private float criticalBrakeDistance = 12f;
        [SerializeField] private float forwardCastRadius = 0.35f;
        [SerializeField, Tooltip("Ignore vehicles farther left/right than this (m). Stops a passing bus from braking the bike.")]
        private float maxLateralOffset = 0.9f;
        [SerializeField] private float checkInterval = 0.15f;
        [SerializeField] private LayerMask obstacleLayers = ~0;

        [Header("Lateral Nudge Settings")]
        [SerializeField] private bool enableLateralNudge = true;
        [SerializeField] private float criticalLateralDistance = 1.2f;
        [SerializeField] private float nudgeStrength = 1.2f;

        private BikeURP.BicyclePhysicsController _physicsController;
        private Rigidbody _rigidbody;
        private float _checkTimer;
        private float _cachedSafetyBrake;

        private void Awake()
        {
            _physicsController = GetComponent<BikeURP.BicyclePhysicsController>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (_physicsController == null) return;

            _checkTimer += Time.fixedDeltaTime;
            if (_checkTimer >= checkInterval)
            {
                _checkTimer = 0f;
                _cachedSafetyBrake = enableAutoBrake ? ComputeForwardBrake() : 0f;
            }

            _physicsController.SetSafetyBrake(_cachedSafetyBrake);

            if (enableLateralNudge)
            {
                CheckLateralProximityNudge();
            }
        }

        private float ComputeForwardBrake()
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) return 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            RaycastHit[] hits = Physics.SphereCastAll(origin, Mathf.Max(0.15f, forwardCastRadius), forward,
                criticalBrakeDistance, obstacleLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                if (!IsVehicleCollider(hits[i].collider)) continue;

                Vector3 toHit = hits[i].point - origin;
                toHit.y = 0f;
                float ahead = Vector3.Dot(toHit, forward);
                float lateral = Mathf.Abs(Vector3.Dot(toHit, right));
                if (ahead < 1.2f || lateral > maxLateralOffset) continue;

                nearest = Mathf.Min(nearest, hits[i].distance);
            }

            if (nearest >= criticalBrakeDistance) return 0f;

            float t = 1f - Mathf.Clamp01(nearest / criticalBrakeDistance);
            return Mathf.Clamp01(t * 1.35f);
        }

        private void CheckLateralProximityNudge()
        {
            Vector3 origin = transform.position + Vector3.up * 0.8f;

            if (Physics.Raycast(origin, -transform.right, out RaycastHit leftHit, criticalLateralDistance, obstacleLayers)
                && !leftHit.transform.IsChildOf(transform) && IsVehicleCollider(leftHit.collider))
            {
                ApplyNudge(transform.right, leftHit.distance);
            }

            if (Physics.Raycast(origin, transform.right, out RaycastHit rightHit, criticalLateralDistance, obstacleLayers)
                && !rightHit.transform.IsChildOf(transform) && IsVehicleCollider(rightHit.collider))
            {
                ApplyNudge(-transform.right, rightHit.distance);
            }
        }

        private void ApplyNudge(Vector3 direction, float distance)
        {
            float intensity = 1f - (distance / criticalLateralDistance);
            Vector3 displacement = direction * (nudgeStrength * intensity * Time.fixedDeltaTime);

            if (_rigidbody != null)
            {
                _rigidbody.MovePosition(_rigidbody.position + displacement);
            }
            else
            {
                transform.position += displacement;
            }
        }

        private static bool IsVehicleCollider(Collider collider)
        {
            if (collider == null) return false;

            string name = collider.transform.root.name;
            return name.StartsWith("CityTraffic_") ||
                   name.StartsWith("TrafficFlow_") ||
                   name.StartsWith("Scenario1_") ||
                   name.IndexOf("car", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("bus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("taxi", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
