using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Ambient car. Route is calculated along the current road corridor, not every frame.
    /// </summary>
    public class NavMeshVehicleAI : MonoBehaviour
    {
        public const float Route2CenterX = 723f;
        public const float Route2HalfWidth = 18f;
        public const float Route2MinZ = 60f;
        public const float Route2MaxZ = 200f;

        [SerializeField] private float cruiseSpeed = 9.5f;
        [SerializeField] private float rightLaneOffset = 1.6f;
        [SerializeField] private float followCheckInterval = 0.25f;
        [SerializeField] private float forwardLookaheadDistance = 12f;
        [SerializeField] private float stoppingBuffer = 3.5f;
        [SerializeField] private float forwardDetectionRadius = 0.65f;
        [SerializeField] private float groundBias = 0.16f;

        private enum RecoverPhase
        {
            None,
            Reverse,
            Sidestep
        }

        private NavMeshAgent _agent;
        private NavMeshPath _cachedPath;
        private float _currentTargetSpeed;
        private float _followTimer;
        private float _repathCooldown;
        private bool _isExperimentStressVehicle;
        private Transform _currentDestination;
        private Vector3? _queuedDest;
        private float _stoppedTime;
        private RecoverPhase _recoverPhase;
        private float _recoverTime;

        public float CruiseSpeed
        {
            get => cruiseSpeed;
            set => cruiseSpeed = value;
        }

        public bool IsExperimentStressVehicle
        {
            get => _isExperimentStressVehicle;
            set => _isExperimentStressVehicle = value;
        }

        public static bool IsRoute2Corridor(Vector3 position)
        {
            return Mathf.Abs(position.x - Route2CenterX) < Route2HalfWidth
                   && position.z > Route2MinZ
                   && position.z < Route2MaxZ;
        }

        public void BindAgent(NavMeshAgent agent)
        {
            _agent = agent;
            if (_agent == null) return;

            _agent.speed = cruiseSpeed;
            _agent.acceleration = 8f;
            _agent.angularSpeed = 90f;
            _agent.radius = 1.1f;
            _agent.height = 1.0f;
            _agent.baseOffset = Mathf.Max(0f, _agent.baseOffset - groundBias);
            _agent.autoBraking = true;
            _agent.updatePosition = true;
            _agent.updateRotation = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            _agent.avoidancePriority = 40;
            _cachedPath = new NavMeshPath();
            _currentTargetSpeed = cruiseSpeed;
        }

        public void AssignRoadCorridorRoute()
        {
            EnsureOnMesh();
            if (_agent == null || !_agent.isOnNavMesh) return;

            if (TrafficDestinationSet.Instance != null &&
                TrafficDestinationSet.Instance.TryPickNext(transform.position, transform.forward, _currentDestination, out Transform next)
                && !IsSouthboundOnRoute2(transform.position, next.position))
            {
                _currentDestination = next;
                Vector3 dest = next.position;
                if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                    dest = hit.position;
                AssignSpawnRoute(dest);
                return;
            }

            AssignSpawnRoute(PickRoadCorridorDestination());
        }

        public void AssignSpawnRoute(Vector3 destination)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            if (IsSouthboundOnRoute2(transform.position, destination))
            {
                destination = PickRoadCorridorDestination();
            }

            Vector3 from = transform.position;
            Vector3 toDest = destination - from;
            toDest.y = 0f;
            Vector3 forward = FlatForward();
            Vector3 approach = toDest.sqrMagnitude > 0.01f ? toDest.normalized : forward;
            destination = OffsetToRightLane(destination, approach);

            float turnAngle = toDest.sqrMagnitude > 0.01f ? Vector3.Angle(forward, toDest) : 0f;
            if (turnAngle > 35f && _queuedDest == null)
            {
                Vector3 via = from + forward * 12f + Vector3.Cross(Vector3.up, forward) * rightLaneOffset;
                if (NavMesh.SamplePosition(via, out NavMeshHit viaHit, 8f, NavMesh.AllAreas)
                    && !IsSouthboundOnRoute2(from, viaHit.position))
                {
                    _queuedDest = destination;
                    destination = viaHit.position;
                }
            }

            if (_cachedPath == null) _cachedPath = new NavMeshPath();

            if (_agent.CalculatePath(destination, _cachedPath) &&
                _cachedPath.status != NavMeshPathStatus.PathInvalid)
            {
                _agent.SetPath(_cachedPath);
                return;
            }

            _agent.SetDestination(destination);
        }

        public static Quaternion HeadingAlongRoad(Vector3 position)
        {
            return Quaternion.LookRotation(PickLongestRoadHeading(position));
        }

        public static Vector3 PickLongestRoadHeading(Vector3 position)
        {
            if (IsRoute2Corridor(position)) return Vector3.forward;

            Vector3[] candidates = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            float best = 0f;
            Vector3 bestDir = Vector3.forward;

            foreach (var dir in candidates)
            {
                float walkable = MeasureWalkable(position, dir, 90f);
                if (walkable > best)
                {
                    best = walkable;
                    bestDir = dir;
                }
            }

            return bestDir;
        }

        public static float MeasureWalkable(Vector3 origin, Vector3 direction, float maxDistance)
        {
            if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 8f, NavMesh.AllAreas)) return 0f;

            Vector3 flat = direction;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return 0f;
            Vector3 end = start.position + flat.normalized * maxDistance;

            if (NavMesh.Raycast(start.position, end, out NavMeshHit blocked, NavMesh.AllAreas))
            {
                return Vector3.Distance(start.position, blocked.position);
            }

            return maxDistance;
        }

        private void Update()
        {
            if (_agent == null) return;
            if (!_agent.isOnNavMesh)
            {
                EnsureOnMesh();
                return;
            }

            if (_recoverPhase != RecoverPhase.None)
            {
                TickRecover();
                return;
            }

            _followTimer += Time.deltaTime;
            if (_followTimer >= 0.08f)
            {
                _followTimer = 0f;
                _currentTargetSpeed = CheckForwardVehicleSpeed();
                _agent.speed = _currentTargetSpeed;
                bool mustStop = _currentTargetSpeed < 0.05f;
                if (_agent.isStopped != mustStop) _agent.isStopped = mustStop;
                if (mustStop)
                {
                    _agent.velocity = Vector3.zero;
                    _stoppedTime += 0.08f;
                    if (!_isExperimentStressVehicle && _stoppedTime >= 1.5f)
                    {
                        BeginRecover();
                    }
                }
                else
                {
                    _stoppedTime = 0f;
                }
            }

            if (_repathCooldown > 0f) _repathCooldown -= Time.deltaTime;

            bool needsPath = !_agent.pathPending && (!_agent.hasPath || _agent.remainingDistance <= 2f);
            if (needsPath && _repathCooldown <= 0f)
            {
                _repathCooldown = 1.5f;
                if (_queuedDest.HasValue)
                {
                    Vector3 queued = _queuedDest.Value;
                    _queuedDest = null;
                    AssignSpawnRoute(queued);
                }
                else
                {
                    AssignRoadCorridorRoute();
                }
            }
        }

        private void BeginRecover()
        {
            _recoverPhase = RecoverPhase.Reverse;
            _recoverTime = 0f;
            _stoppedTime = 0f;
            if (_agent != null)
            {
                _agent.isStopped = false;
                _agent.updateRotation = false;
                _agent.velocity = Vector3.zero;
            }
        }

        private void TickRecover()
        {
            _recoverTime += Time.deltaTime;

            if (_recoverPhase == RecoverPhase.Reverse)
            {
                Vector3 back = transform.position - FlatForward() * (4.2f * Time.deltaTime);
                if (NavMesh.SamplePosition(back, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                }

                if (_recoverTime >= 0.85f)
                {
                    _recoverPhase = RecoverPhase.Sidestep;
                    _recoverTime = 0f;
                    TrySidestepAndResume();
                }

                return;
            }

            FinishRecover();
        }

        private void TrySidestepAndResume()
        {
            Vector3 right = Vector3.Cross(Vector3.up, FlatForward());
            for (float lateral = 2.5f; lateral <= 5.2f; lateral += 1.3f)
            {
                Vector3 guess = transform.position + right * lateral + FlatForward() * 2.5f;
                if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 3.5f, NavMesh.AllAreas)) continue;
                if (IsSouthboundOnRoute2(transform.position, hit.position)) continue;

                _queuedDest = null;
                FinishRecover();
                AssignSpawnRoute(hit.position);
                _repathCooldown = 0.35f;
                return;
            }

            _currentDestination = null;
            _queuedDest = null;
            FinishRecover();
            AssignRoadCorridorRoute();
            _repathCooldown = 0.35f;
        }

        private void FinishRecover()
        {
            _recoverPhase = RecoverPhase.None;
            _recoverTime = 0f;
            if (_agent != null)
            {
                _agent.updateRotation = true;
                _agent.isStopped = false;
            }
        }

        private void EnsureOnMesh()
        {
            if (_agent == null) return;
            if (_agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 16f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
        }

        private Vector3 PickRoadCorridorDestination()
        {
            Vector3 origin = transform.position;
            Vector3 forward = FlatForward();
            if (IsRoute2Corridor(origin)) forward = Vector3.forward;

            float walkable = MeasureWalkable(origin, forward, 90f);
            if (walkable < 10f && !IsRoute2Corridor(origin))
            {
                forward = PickLongestRoadHeading(origin);
                walkable = MeasureWalkable(origin, forward, 90f);
            }

            float along = Mathf.Clamp(walkable * 0.85f, 12f, 90f);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 guess = origin + forward * along + right * rightLaneOffset;

            if (NavMesh.SamplePosition(guess, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                return hit.position;
            if (NavMesh.SamplePosition(origin + forward * along, out hit, 16f, NavMesh.AllAreas))
                return hit.position;
            return origin + forward * along;
        }

        private Vector3 OffsetToRightLane(Vector3 destination, Vector3 approach)
        {
            approach.y = 0f;
            if (approach.sqrMagnitude < 0.01f) return destination;
            Vector3 right = Vector3.Cross(Vector3.up, approach.normalized);
            Vector3 guess = destination + right * rightLaneOffset;
            if (NavMesh.SamplePosition(guess, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                return hit.position;
            return destination;
        }

        private static bool IsSouthboundOnRoute2(Vector3 from, Vector3 to)
        {
            if (!IsRoute2Corridor(from) || !IsRoute2Corridor(to)) return false;
            return to.z < from.z - 2f;
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) return Vector3.forward;
            return forward.normalized;
        }

        private float CheckForwardVehicleSpeed()
        {
            float speed = cruiseSpeed;
            Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 0.4f;
            Vector3 forward = FlatForward();

            if (!_isExperimentStressVehicle)
            {
                Transform bike = TrafficIdentity.Cyclist;
                if (bike != null)
                {
                    speed = Mathf.Min(speed, TrafficIdentity.SpeedForPointAhead(
                        transform.position, forward, bike.position, 1.7f,
                        forwardLookaheadDistance + 2f, stoppingBuffer, cruiseSpeed));
                }
            }

            Vector3 halfExtents = new Vector3(1.15f, 0.7f, 0.5f);
            RaycastHit[] hits = Physics.BoxCastAll(origin, halfExtents, forward, transform.rotation,
                forwardLookaheadDistance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || hits[i].transform.IsChildOf(transform)) continue;

                bool cyclist = TrafficIdentity.IsCyclist(col);
                if (cyclist && _isExperimentStressVehicle) continue;
                if (!cyclist && !TrafficIdentity.IsVehicle(col)) continue;

                if (hits[i].distance <= stoppingBuffer) return 0f;
                float available = Mathf.Max(0.01f, forwardLookaheadDistance - stoppingBuffer);
                speed = Mathf.Min(speed, cruiseSpeed * Mathf.Clamp01((hits[i].distance - stoppingBuffer) / available));
            }

            return speed;
        }
    }
}
