using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// VR safety assist: auto-brake when a vehicle is in the bike's lane, plus a hard stop if already overlapping.
    /// The bike is kinematic, so this is the only thing that prevents riding through a bus.
    /// </summary>
    public class SmartBicycleSafetyAssistant : MonoBehaviour
    {
        [Header("Auto-Braking Settings")]
        [SerializeField] private bool enableAutoBrake = true;
        [SerializeField, Tooltip("Distance at which auto-braking begins")]
        private float criticalBrakeDistance = 12f;
        [SerializeField, Tooltip("Half-width of the forward lane check (m). Passing traffic outside this is ignored.")]
        private float maxLateralOffset = 1.1f;
        [SerializeField] private LayerMask obstacleLayers = ~0;

        [Header("Lateral Nudge Settings")]
        [SerializeField] private bool enableLateralNudge = true;
        [SerializeField] private float criticalLateralDistance = 1.2f;
        [SerializeField] private float nudgeStrength = 1.2f;

        private BikeURP.BicyclePhysicsController _physicsController;
        private Rigidbody _rigidbody;
        private Collider _bikeCollider;
        private readonly Collider[] _overlapHits = new Collider[24];

        private void Awake()
        {
            _physicsController = GetComponent<BikeURP.BicyclePhysicsController>();
            _rigidbody = GetComponent<Rigidbody>();
            _bikeCollider = GetComponent<Collider>();
            maxLateralOffset = Mathf.Max(0.85f, maxLateralOffset);
            criticalBrakeDistance = Mathf.Max(8f, criticalBrakeDistance);
            EnsureNavMeshObstacle();
        }

        private void EnsureNavMeshObstacle()
        {
            var obstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = false;
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(0.9f, 1.6f, 1.8f);
            obstacle.center = new Vector3(0f, 0.8f, 0f);
        }

        private void FixedUpdate()
        {
            if (_physicsController == null) return;

            float brake = 0f;
            if (enableAutoBrake)
            {
                if (TryResolveOverlap())
                {
                    brake = 1f;
                    _physicsController.HaltForwardMotion();
                }
                else
                {
                    brake = Mathf.Max(ComputeForwardBrake(), ComputeApproachingVehicleBrake());
                }
            }

            _physicsController.SetSafetyBrake(brake);

            if (enableLateralNudge && brake < 0.99f)
            {
                CheckLateralProximityNudge();
            }
        }

        private bool TryResolveOverlap()
        {
            Vector3 center = transform.position + Vector3.up * 0.75f;
            Vector3 halfExtents = new Vector3(0.4f, 0.7f, 0.85f);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapHits, transform.rotation,
                obstacleLayers, QueryTriggerInteraction.Ignore);

            bool overlapping = false;
            for (int i = 0; i < count; i++)
            {
                Collider other = _overlapHits[i];
                if (other == null || other.transform.IsChildOf(transform) || !TrafficIdentity.IsVehicle(other)) continue;

                overlapping = true;
                SeparateFrom(other);
            }

            return overlapping;
        }

        private void SeparateFrom(Collider other)
        {
            Vector3 push;
            if (_bikeCollider != null &&
                Physics.ComputePenetration(_bikeCollider, transform.position, transform.rotation,
                    other, other.transform.position, other.transform.rotation, out Vector3 dir, out float dist)
                && dist > 0.001f)
            {
                push = dir * (dist + 0.08f);
            }
            else
            {
                Vector3 away = transform.position - other.ClosestPoint(transform.position);
                away.y = 0f;
                push = (away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward) * 0.2f;
            }

            push.y = 0f;
            Vector3 next = transform.position + push;
            if (_rigidbody != null) _rigidbody.MovePosition(next);
            else transform.position = next;
        }

        private float ComputeForwardBrake()
        {
            Vector3 origin = transform.position + Vector3.up * 0.7f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) return 0f;
            forward.Normalize();

            float halfWidth = Mathf.Max(0.85f, maxLateralOffset);
            Vector3 halfExtents = new Vector3(halfWidth, 0.8f, 0.4f);
            RaycastHit[] hits = Physics.BoxCastAll(origin, halfExtents, forward, transform.rotation,
                criticalBrakeDistance, obstacleLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                if (!TrafficIdentity.IsVehicle(hits[i].collider)) continue;
                nearest = Mathf.Min(nearest, hits[i].distance);
            }

            if (nearest >= criticalBrakeDistance) return 0f;

            float t = 1f - Mathf.Clamp01(nearest / criticalBrakeDistance);
            return Mathf.Clamp01(t * 1.5f);
        }

        private float ComputeApproachingVehicleBrake()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, 8f, _overlapHits, obstacleLayers,
                QueryTriggerInteraction.Ignore);

            float brake = 0f;
            for (int i = 0; i < count; i++)
            {
                Collider other = _overlapHits[i];
                if (other == null || other.transform.IsChildOf(transform) || !TrafficIdentity.IsVehicle(other)) continue;

                Vector3 toBike = transform.position - other.transform.position;
                toBike.y = 0f;
                float distance = toBike.magnitude;
                if (distance < 0.05f)
                {
                    brake = 1f;
                    continue;
                }

                Vector3 carForward = other.transform.forward;
                carForward.y = 0f;
                if (carForward.sqrMagnitude < 0.01f) continue;
                carForward.Normalize();

                float closing = Vector3.Dot(carForward, toBike / distance);
                float lateral = Mathf.Abs(Vector3.Dot(toBike, Vector3.Cross(Vector3.up, carForward)));
                if (closing < 0.45f || lateral > 1.8f) continue;

                float t = 1f - Mathf.Clamp01(distance / 8f);
                brake = Mathf.Max(brake, Mathf.Clamp01(t * 1.4f));
            }

            return brake;
        }

        private void CheckLateralProximityNudge()
        {
            Vector3 origin = transform.position + Vector3.up * 0.8f;

            if (Physics.Raycast(origin, -transform.right, out RaycastHit leftHit, criticalLateralDistance, obstacleLayers)
                && !leftHit.transform.IsChildOf(transform) && TrafficIdentity.IsVehicle(leftHit.collider))
            {
                ApplyNudge(transform.right, leftHit.distance);
            }

            if (Physics.Raycast(origin, transform.right, out RaycastHit rightHit, criticalLateralDistance, obstacleLayers)
                && !rightHit.transform.IsChildOf(transform) && TrafficIdentity.IsVehicle(rightHit.collider))
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

    }
}
