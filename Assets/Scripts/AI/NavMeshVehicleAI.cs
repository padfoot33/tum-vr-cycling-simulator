using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Ambient car. Route is calculated along the current road corridor, not every frame.
    /// </summary>
    public class NavMeshVehicleAI : MonoBehaviour
    {
        [SerializeField] private float cruiseSpeed = 9.5f;
        [SerializeField] private float rightLaneOffset = 1.6f;
        [SerializeField] private float followCheckInterval = 0.25f;
        [SerializeField] private float forwardLookaheadDistance = 12f;
        [SerializeField] private float stoppingBuffer = 3.5f;
        [SerializeField] private float forwardDetectionRadius = 0.65f;
        [SerializeField] private float groundBias = 0.16f;

        private NavMeshAgent _agent;
        private NavMeshPath _cachedPath;
        private float _currentTargetSpeed;
        private float _followTimer;
        private float _repathCooldown;
        private bool _isExperimentStressVehicle;
        private Transform _currentDestination;

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
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            _cachedPath = new NavMeshPath();
            _currentTargetSpeed = cruiseSpeed;
        }

        public void AssignRoadCorridorRoute()
        {
            EnsureOnMesh();
            if (_agent == null || !_agent.isOnNavMesh) return;

            if (TrafficDestinationSet.Instance != null &&
                TrafficDestinationSet.Instance.TryPickNext(transform.position, transform.forward, _currentDestination, out Transform next))
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

            _followTimer += Time.deltaTime;
            if (_followTimer >= followCheckInterval)
            {
                _followTimer = 0f;
                _currentTargetSpeed = CheckForwardVehicleSpeed();
                _agent.speed = _currentTargetSpeed;
            }

            if (_repathCooldown > 0f) _repathCooldown -= Time.deltaTime;

            bool needsPath = !_agent.pathPending && (!_agent.hasPath || _agent.remainingDistance <= 2f);
            if (needsPath && _repathCooldown <= 0f)
            {
                _repathCooldown = 1.5f;
                AssignRoadCorridorRoute();
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
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            float walkable = MeasureWalkable(origin, forward, 90f);
            if (walkable < 10f)
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

        private float CheckForwardVehicleSpeed()
        {
            Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 1.5f;
            if (!Physics.SphereCast(origin, forwardDetectionRadius, transform.forward, out RaycastHit hit,
                    forwardLookaheadDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return cruiseSpeed;
            }

            if (hit.transform.IsChildOf(transform)) return cruiseSpeed;

            bool isCyclist = hit.collider.CompareTag("Player") || hit.collider.name.Contains("bicyle");
            if (isCyclist && _isExperimentStressVehicle) return cruiseSpeed;

            bool isTraffic = hit.collider.CompareTag("Vehicle") ||
                             hit.transform.root.name.StartsWith("CityTraffic_") ||
                             hit.transform.root.name.StartsWith("TrafficFlow_");
            if (!isCyclist && !isTraffic) return cruiseSpeed;

            if (hit.distance < stoppingBuffer) return 0f;
            float available = Mathf.Max(0.01f, forwardLookaheadDistance - stoppingBuffer);
            return cruiseSpeed * Mathf.Clamp01((hit.distance - stoppingBuffer) / available);
        }
    }
}
