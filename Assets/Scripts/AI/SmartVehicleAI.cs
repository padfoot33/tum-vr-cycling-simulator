using System;
using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Smart Autonomous Vehicle AI with vehicle-to-vehicle collision avoidance.
    /// Follows a WaypointPath, detects vehicles ahead to avoid rear-end collisions,
    /// and supports stress-vehicle bypass mode for experiment scenarios.
    /// </summary>
    public class SmartVehicleAI : MonoBehaviour
    {
        [Header("Path & Speed")]
        [SerializeField] private WaypointPath path;
        [SerializeField] private float speed = 10f;
        [SerializeField] private float rotationSpeed = 6f;
        [SerializeField] private float waypointThreshold = 4.5f;
        [SerializeField] private bool destroyAtEnd = true;

        [Header("Collision Avoidance")]
        [SerializeField, Tooltip("Detects other cars ahead and stops/slows down")]
        private bool enableVehicleToVehicleAvoidance = true;

        [SerializeField, Tooltip("Lookahead raycast distance to detect cars ahead")]
        private float forwardLookaheadDistance = 12.0f;

        [SerializeField, Tooltip("Minimum stopping distance behind a leading car")]
        private float stoppingBuffer = 3.5f;

        [SerializeField, Tooltip("Radius used when looking for the vehicle ahead")]
        private float forwardDetectionRadius = 0.65f;

        [SerializeField, Tooltip("Deceleration used when a vehicle is configured to stop at its final waypoint")]
        private float endStopDeceleration = 6f;

        [Header("Experiment Configuration")]
        [SerializeField, Tooltip("If true, this vehicle is a stress-inducer and will not stop for the cyclist")]
        private bool isExperimentStressVehicle = false;

        private int _currentWaypointIndex = 0;
        private bool _isMoving = true;
        private bool _isAtEnd = false;
        private float _currentSpeed = 0f;
        private float _followTimer;
        private float _cachedFollowSpeed;

        private bool _stopSmoothlyAtPathEnd;
        private bool _preserveSpawnPosition;
        private int _startWaypointIndex;
        private float _pivotAboveVisualBottom;

        public bool PreserveSpawnPosition
        {
            get => _preserveSpawnPosition;
            set => _preserveSpawnPosition = value;
        }

        public int StartWaypointIndex
        {
            get => _startWaypointIndex;
            set => _startWaypointIndex = value;
        }

        public WaypointPath Path
        {
            get => path;
            set => path = value;
        }

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        public bool DestroyAtEnd
        {
            get => destroyAtEnd;
            set => destroyAtEnd = value;
        }

        public bool IsExperimentStressVehicle
        {
            get => isExperimentStressVehicle;
            set => isExperimentStressVehicle = value;
        }

        /// <summary>Whether this vehicle has completed a non-looping path.</summary>
        public bool IsAtEnd => _isAtEnd;

        /// <summary>
        /// When enabled, reduce speed before the final waypoint instead of stopping abruptly.
        /// Intended for buses that remain parked at a stop.
        /// </summary>
        public bool StopSmoothlyAtPathEnd
        {
            get => _stopSmoothlyAtPathEnd;
            set => _stopSmoothlyAtPathEnd = value;
        }

        public event Action OnPathComplete;

        private void Start()
        {
            _currentSpeed = speed;
            _cachedFollowSpeed = speed;
            CacheVisualBottomOffset();

            if (path == null || path.WaypointCount == 0) return;

            if (_preserveSpawnPosition)
            {
                _currentWaypointIndex = Mathf.Clamp(_startWaypointIndex, 0, path.WaypointCount - 1);
                Vector3 dir = (path.GetWaypoint(_currentWaypointIndex) - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude < 0.01f && _currentWaypointIndex + 1 < path.WaypointCount)
                {
                    dir = path.GetWaypoint(_currentWaypointIndex + 1) - path.GetWaypoint(_currentWaypointIndex);
                    dir.y = 0;
                }
                if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
            else
            {
                transform.position = path.GetWaypoint(0);
                _currentWaypointIndex = 0;
                if (path.WaypointCount > 1)
                {
                    Vector3 dir = (path.GetWaypoint(1) - transform.position).normalized;
                    dir.y = 0;
                    if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
                }
            }

            SnapWheelsToGround();
        }

        private void Update()
        {
            if (!_isMoving || _isAtEnd || path == null || path.WaypointCount == 0) return;

            float targetSpeed = speed;
            if (enableVehicleToVehicleAvoidance)
            {
                _followTimer += Time.deltaTime;
                if (_followTimer >= 0.2f)
                {
                    _followTimer = 0f;
                    _cachedFollowSpeed = CheckForwardVehicleSpeed();
                }
                targetSpeed = _cachedFollowSpeed;
            }

            targetSpeed *= GetCornerSlowdownFactor();

            if (_stopSmoothlyAtPathEnd && !path.isLoop)
            {
                float remainingDistance = GetRemainingPathDistance();
                float stoppingSpeed = Mathf.Sqrt(2f * endStopDeceleration * Mathf.Max(0f, remainingDistance));
                targetSpeed = Mathf.Min(targetSpeed, stoppingSpeed);
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, 12f * Time.deltaTime);

            Vector3 targetPosition = path.GetWaypoint(_currentWaypointIndex);
            Vector3 direction = (targetPosition - transform.position);
            direction.y = 0;
            float distance = direction.magnitude;

            if (_currentSpeed > 0.05f && distance > 0.05f)
            {
                Vector3 moveDir = direction.normalized;
                transform.position += moveDir * (_currentSpeed * Time.deltaTime);
                SnapWheelsToGround();

                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            if (distance <= waypointThreshold)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= path.WaypointCount)
                {
                    if (path.isLoop)
                    {
                        _currentWaypointIndex = 0;
                    }
                    else
                    {
                        _isAtEnd = true;
                        _isMoving = false;
                        OnPathComplete?.Invoke();

                        if (destroyAtEnd)
                        {
                            Destroy(gameObject);
                        }
                    }
                }
            }
        }

        public static Vector3 SnapPositionToGround(Vector3 position, Transform ignoreRoot = null)
        {
            if (TryGroundHit(position + Vector3.up * 8f, 24f, ignoreRoot, out RaycastHit hit))
            {
                return new Vector3(position.x, hit.point.y + 0.05f, position.z);
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 8f, NavMesh.AllAreas))
            {
                return new Vector3(position.x, navHit.position.y + 0.05f, position.z);
            }

            return position;
        }

        private static bool TryGroundHit(Vector3 origin, float distance, Transform ignoreRoot, out RaycastHit best)
        {
            best = default;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (ignoreRoot != null && hits[i].transform != null && hits[i].transform.IsChildOf(ignoreRoot))
                {
                    continue;
                }

                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    best = hits[i];
                    found = true;
                }
            }

            return found;
        }

        private void CacheVisualBottomOffset()
        {
            float minY = float.MaxValue;
            var renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) minY = Mathf.Min(minY, renderers[i].bounds.min.y);
            }

            _pivotAboveVisualBottom = minY < 999f
                ? Mathf.Max(0f, transform.position.y - minY)
                : 0.05f;
        }

        private void SnapWheelsToGround()
        {
            float visualBottom = transform.position.y - _pivotAboveVisualBottom;
            Vector3 probe = new Vector3(transform.position.x, visualBottom + 0.2f, transform.position.z);

            if (!TryGroundHit(probe + Vector3.up * 6f, 20f, transform, out RaycastHit hit))
            {
                transform.position = SnapPositionToGround(transform.position, transform);
                return;
            }

            float targetY = hit.point.y + 0.04f + _pivotAboveVisualBottom;
            Vector3 pos = transform.position;
            pos.y = targetY;
            transform.position = pos;
        }

        private float CheckForwardVehicleSpeed()
        {
            Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 1.5f;
            Vector3 fwd = transform.forward;
            RaycastHit[] hits = Physics.SphereCastAll(origin, forwardDetectionRadius, fwd, forwardLookaheadDistance, ~0,
                QueryTriggerInteraction.Ignore);
            float nearestVehicleDistance = float.PositiveInfinity;

            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;

                bool isCyclist = hit.collider.CompareTag("Player") || hit.collider.name.Contains("bicyle");

                if (isCyclist)
                {
                    if (isExperimentStressVehicle) continue;
                    nearestVehicleDistance = Mathf.Min(nearestVehicleDistance, hit.distance);
                    continue;
                }

                if (IsVehicleCollider(hit.collider))
                {
                    nearestVehicleDistance = Mathf.Min(nearestVehicleDistance, hit.distance);
                }
            }

            if (nearestVehicleDistance < stoppingBuffer) return 0f;
            if (nearestVehicleDistance < forwardLookaheadDistance)
            {
                float availableDistance = Mathf.Max(0.01f, forwardLookaheadDistance - stoppingBuffer);
                float factor = (nearestVehicleDistance - stoppingBuffer) / availableDistance;
                return speed * Mathf.Clamp01(factor);
            }

            return speed;
        }

        private float GetCornerSlowdownFactor()
        {
            if (path == null || _currentWaypointIndex >= path.WaypointCount) return 1f;

            Vector3 toCurrent = path.GetWaypoint(_currentWaypointIndex) - transform.position;
            toCurrent.y = 0f;
            if (toCurrent.sqrMagnitude < 0.01f) return 1f;

            int nextIndex = _currentWaypointIndex + 1;
            if (nextIndex >= path.WaypointCount)
            {
                if (!path.isLoop) return 1f;
                nextIndex = 0;
            }

            Vector3 toNext = path.GetWaypoint(nextIndex) - path.GetWaypoint(_currentWaypointIndex);
            toNext.y = 0f;
            if (toNext.sqrMagnitude < 0.01f) return 1f;

            float angle = Vector3.Angle(toCurrent, toNext);
            if (angle < 20f) return 1f;
            return Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(20f, 80f, angle));
        }

        private float GetRemainingPathDistance()
        {
            if (path == null || _currentWaypointIndex >= path.WaypointCount) return 0f;

            float distance = Vector3.Distance(transform.position, path.GetWaypoint(_currentWaypointIndex));
            for (int i = _currentWaypointIndex; i < path.WaypointCount - 1; i++)
            {
                distance += Vector3.Distance(path.GetWaypoint(i), path.GetWaypoint(i + 1));
            }

            return distance;
        }

        private static bool IsVehicleCollider(Collider collider)
        {
            if (collider == null) return false;

            Transform root = collider.transform.root;
            string name = root.name;
            return name.StartsWith("CityTraffic_") ||
                   name.StartsWith("TrafficFlow_") ||
                   name.StartsWith("Scenario1_") ||
                   name.IndexOf("car", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("bus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("taxi", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
