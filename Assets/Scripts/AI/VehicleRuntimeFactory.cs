using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Shared spawn helper: disable SUMO controllers, freeze physics, attach waypoint AI.
    /// </summary>
    public static class VehicleRuntimeFactory
    {
        public struct SpawnSettings
        {
            public WaypointPath Path;
            public float Speed;
            public bool DestroyAtEnd;
            public bool IsExperimentStressVehicle;
            public bool StopSmoothlyAtPathEnd;
            public bool PreserveSpawnPosition;
            public int StartWaypointIndex;
            public string Name;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, SpawnSettings settings)
        {
            return SpawnOnWaypointPath(prefab, position, rotation, settings);
        }

        /// <summary>
        /// Scripted scenario vehicle: follow a fixed waypoint path.
        /// </summary>
        public static GameObject SpawnOnWaypointPath(GameObject prefab, Vector3 position, Quaternion rotation, SpawnSettings settings)
        {
            if (prefab == null) return null;

            GameObject vehicle = Object.Instantiate(prefab, position, rotation);
            if (!string.IsNullOrEmpty(settings.Name))
            {
                vehicle.name = settings.Name;
            }

            Prepare(vehicle);
            DisableAmbientDrivers(vehicle);

            var follower = GetOrAdd<WaypointFollower>(vehicle);
            follower.enabled = true;
            follower.Path = settings.Path;
            follower.Speed = settings.Speed;
            follower.DestroyAtEnd = settings.DestroyAtEnd;
            follower.PreserveSpawnPosition = settings.PreserveSpawnPosition;

            return vehicle;
        }

        /// <summary>
        /// Ambient campus car: follow an authored WaypointPath with gap and cyclist yield.
        /// Uses GlobalCityTrafficManager's pool when present.
        /// </summary>
        public static GameObject SpawnAmbientOnWaypointPath(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            WaypointPath path,
            int startWaypointIndex,
            float speed,
            string name)
        {
            if (prefab == null || path == null) return null;

            GameObject vehicle = null;
            var pool = GlobalCityTrafficManager.Instance != null
                ? GlobalCityTrafficManager.Instance.VehiclePool
                : null;
            if (pool != null)
            {
                vehicle = pool.Rent(prefab, position, rotation);
            }

            if (vehicle == null)
            {
                vehicle = Object.Instantiate(prefab, position, rotation);
                Prepare(vehicle, disableRigidbody: true);
            }
            else
            {
                vehicle.transform.SetPositionAndRotation(position, rotation);
            }

            ConfigureAmbientOnWaypointPath(vehicle, path, startWaypointIndex, speed, name);
            return vehicle;
        }

        public static void ConfigureAmbientOnWaypointPath(
            GameObject vehicle,
            WaypointPath path,
            int startWaypointIndex,
            float speed,
            string name)
        {
            if (vehicle == null || path == null) return;

            if (!string.IsNullOrEmpty(name)) vehicle.name = name;

            DisableAmbientDrivers(vehicle);

            var agent = vehicle.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            var follower = vehicle.GetComponent<WaypointFollower>();
            if (follower != null) follower.enabled = false;

            var graph = vehicle.GetComponent<GraphVehicleAI>();
            if (graph != null) graph.enabled = false;

            var ai = GetOrAdd<SmartVehicleAI>(vehicle);
            ai.Path = path;
            ai.Speed = speed;
            ai.DestroyAtEnd = true;
            ai.IsExperimentStressVehicle = false;
            ai.PreserveSpawnPosition = true;
            ai.StartWaypointIndex = Mathf.Clamp(startWaypointIndex, 0, Mathf.Max(0, path.WaypointCount - 1));
            ai.enabled = true;
            if (!vehicle.activeSelf) vehicle.SetActive(true);
            ai.ResetTrip();
        }

        public static GameObject SpawnOnGraph(GameObject prefab, RoadNetwork network, RoadEdge edge, float distanceAlong, float speed, string name)
        {
            if (prefab == null || network == null || edge == null) return null;

            if (!edge.Sample(distanceAlong, out Vector3 position, out Vector3 forward))
                return null;

            Quaternion rotation = forward.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(forward)
                : Quaternion.identity;

            GameObject vehicle = Object.Instantiate(prefab, position, rotation);
            if (!string.IsNullOrEmpty(name)) vehicle.name = name;

            Prepare(vehicle, disableRigidbody: true);
            DisableAmbientDrivers(vehicle);

            var agent = vehicle.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            var ai = GetOrAdd<GraphVehicleAI>(vehicle);
            ai.enabled = true;
            ai.CruiseSpeed = speed;
            ai.IsExperimentStressVehicle = false;
            ai.Bind(network, edge, distanceAlong, speed);
            return vehicle;
        }

        public static void Prepare(GameObject vehicle, bool disableRigidbody = false)
        {
            if (vehicle == null) return;

            DisableNamed(vehicle, "TaxiController");
            DisableNamed(vehicle, "CarController");
            DisableNamed(vehicle, "BusController");

            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (disableRigidbody)
                {
                    Object.Destroy(rb);
                }
                else
                {
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.detectCollisions = true;
                }
            }

            var follower = vehicle.GetComponent<WaypointFollower>();
            if (follower != null) follower.enabled = false;

            var physicsBus = vehicle.GetComponent<PhysicsBusController>();
            if (physicsBus != null) physicsBus.enabled = false;
        }

        private static void DisableAmbientDrivers(GameObject vehicle)
        {
            var smart = vehicle.GetComponent<SmartVehicleAI>();
            if (smart != null) smart.enabled = false;

            var graph = vehicle.GetComponent<GraphVehicleAI>();
            if (graph != null) graph.enabled = false;

            var agent = vehicle.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }

        private static void DisableNamed(GameObject vehicle, string typeName)
        {
            var behaviour = vehicle.GetComponent(typeName) as MonoBehaviour;
            if (behaviour != null) behaviour.enabled = false;
        }
    }

    /// <summary>
    /// Ambient car that stays on a RoadEdge and chooses an outgoing edge at each node.
    /// Lives in this file so Unity always compiles it with VehicleRuntimeFactory.
    /// </summary>
    public class GraphVehicleAI : MonoBehaviour
    {
        [SerializeField] private float cruiseSpeed = 9.5f;
        [SerializeField] private float rotationSpeed = 6f;
        [SerializeField] private float forwardLookaheadDistance = 12f;
        [SerializeField] private float stoppingBuffer = 3.5f;
        [SerializeField] private float holdGap = 11f;

        private RoadNetwork _network;
        private RoadEdge _edge;
        private RoadEdge _plannedNext;
        private float _distanceAlong;
        private float _currentSpeed;
        private float _followTimer;
        private float _cachedFollowSpeed;
        private float _pivotAboveVisualBottom;
        private bool _isExperimentStressVehicle;

        private static readonly List<GraphVehicleAI> Active = new List<GraphVehicleAI>(48);

        public RoadEdge CurrentEdge => _edge;
        public float DistanceAlong => _distanceAlong;

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

        public void Bind(RoadNetwork network, RoadEdge edge, float distanceAlong, float speed)
        {
            _network = network;
            _edge = edge;
            _plannedNext = null;
            _distanceAlong = Mathf.Max(0f, distanceAlong);
            cruiseSpeed = speed;
            _currentSpeed = speed;
            _cachedFollowSpeed = speed;
            CacheVisualBottomOffset();
            SnapToPath(true);
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Start()
        {
            CacheVisualBottomOffset();
            if (_edge == null && _network != null)
            {
                _network.TryFindNearestEdge(transform.position, out _edge, out _distanceAlong);
            }

            SnapToPath(true);
        }

        private void Update()
        {
            if (_edge == null || _edge.Length < 0.5f)
            {
                if (!TryRecoverEdge())
                {
                    Destroy(gameObject);
                }

                return;
            }

            _followTimer += Time.deltaTime;
            if (_followTimer >= 0.08f)
            {
                _followTimer = 0f;
                _cachedFollowSpeed = CheckForwardVehicleSpeed();
            }

            float target = _cachedFollowSpeed * GetCornerSlowdown();
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, 12f * Time.deltaTime);
            _distanceAlong += _currentSpeed * Time.deltaTime;

            int hops = 0;
            while (_edge != null && _distanceAlong >= _edge.Length && hops < 4)
            {
                float overflow = _distanceAlong - _edge.Length;
                if (!AdvanceToNextEdge())
                {
                    Destroy(gameObject);
                    return;
                }

                _distanceAlong = overflow;
                hops++;
            }

            SnapToPath(false);
        }

        private bool AdvanceToNextEdge()
        {
            RoadEdge next = PlannedNext();
            _plannedNext = null;
            if (next == null) return false;
            _edge = next;
            return true;
        }

        private RoadEdge PlannedNext()
        {
            if (_network == null) _network = RoadNetwork.Instance;
            if (_edge != null && _plannedNext != null && _plannedNext.from == _edge.to)
                return _plannedNext;
            _plannedNext = _network != null ? _network.PickNext(_edge) : null;
            return _plannedNext;
        }

        private bool TryRecoverEdge()
        {
            if (_network == null) _network = RoadNetwork.Instance;
            if (_network == null) return false;
            return _network.TryFindNearestEdge(transform.position, out _edge, out _distanceAlong);
        }

        private void SnapToPath(bool instantRotation)
        {
            if (_edge == null) return;
            if (!_edge.Sample(_distanceAlong, out Vector3 pos, out Vector3 forward)) return;

            pos = SnapPositionToGround(pos);
            transform.position = pos;
            if (forward.sqrMagnitude < 0.01f) return;

            Quaternion target = Quaternion.LookRotation(forward);
            transform.rotation = instantRotation
                ? target
                : Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        private Vector3 SnapPositionToGround(Vector3 position)
        {
            float visualBottom = transform.position.y - _pivotAboveVisualBottom;
            Vector3 probe = new Vector3(position.x, visualBottom + 0.2f, position.z);
            if (TryGroundHit(probe + Vector3.up * 6f, 20f, out RaycastHit hit))
            {
                return new Vector3(position.x, hit.point.y + 0.04f + _pivotAboveVisualBottom, position.z);
            }

            return new Vector3(position.x, position.y, position.z);
        }

        private bool TryGroundHit(Vector3 origin, float distance, out RaycastHit best)
        {
            best = default;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform != null && hits[i].transform.IsChildOf(transform)) continue;
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

        private float CheckForwardVehicleSpeed()
        {
            float speed = cruiseSpeed;
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

            if (TryFindLaneLeader(out GraphVehicleAI leader, out float leaderAhead))
            {
                if (leaderAhead <= holdGap) return 0f;
                float available = Mathf.Max(0.01f, forwardLookaheadDistance - holdGap);
                speed = Mathf.Min(speed, cruiseSpeed * Mathf.Clamp01((leaderAhead - holdGap) / available));
            }

            Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 0.4f;
            RaycastHit[] hits = Physics.BoxCastAll(origin, new Vector3(1.1f, 0.7f, 0.5f), forward, transform.rotation,
                forwardLookaheadDistance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || hits[i].transform.IsChildOf(transform)) continue;

                bool cyclist = TrafficIdentity.IsCyclist(col);
                if (cyclist && _isExperimentStressVehicle) continue;
                if (!cyclist && !TrafficIdentity.IsVehicle(col)) continue;
                if (!cyclist && !IsSameDirection(col.transform.root, forward)) continue;

                if (hits[i].distance <= stoppingBuffer) return 0f;
                float available = Mathf.Max(0.01f, forwardLookaheadDistance - stoppingBuffer);
                speed = Mathf.Min(speed, cruiseSpeed * Mathf.Clamp01((hits[i].distance - stoppingBuffer) / available));
            }

            return speed;
        }

        private bool TryFindLaneLeader(out GraphVehicleAI leader, out float aheadDistance)
        {
            leader = null;
            aheadDistance = float.PositiveInfinity;
            if (_edge == null) return false;

            for (int i = 0; i < Active.Count; i++)
            {
                GraphVehicleAI other = Active[i];
                if (other == null || other == this || other._edge == null) continue;

                float ahead;
                if (other._edge == _edge)
                {
                    ahead = other._distanceAlong - _distanceAlong;
                }
                else if (other._edge.from == _edge.to)
                {
                    ahead = (_edge.Length - _distanceAlong) + other._distanceAlong;
                }
                else
                {
                    continue;
                }

                if (ahead < 1.5f || ahead > 28f) continue;
                if (ahead < aheadDistance)
                {
                    aheadDistance = ahead;
                    leader = other;
                }
            }

            return leader != null;
        }

        private float GetCornerSlowdown()
        {
            if (_network == null || _edge == null) return 1f;
            float remaining = _edge.Length - _distanceAlong;
            if (remaining > 18f) return 1f;

            RoadEdge next = PlannedNext();
            if (next == null || next.Polyline == null || next.Polyline.Length < 2) return 1f;
            if (!_edge.Sample(_distanceAlong, out _, out Vector3 currentFwd)) return 1f;
            if (!next.Sample(Mathf.Min(4f, next.Length * 0.2f), out _, out Vector3 nextFwd)) return 1f;

            float angle = Vector3.Angle(currentFwd, nextFwd);
            if (angle < 20f) return 1f;
            return Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(20f, 80f, angle));
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) return Vector3.forward;
            return forward.normalized;
        }

        private static bool IsSameDirection(Transform other, Vector3 forward)
        {
            if (other == null) return false;
            Vector3 otherForward = other.forward;
            otherForward.y = 0f;
            if (otherForward.sqrMagnitude < 0.01f) return true;
            return Vector3.Dot(otherForward.normalized, forward) >= 0.15f;
        }
    }
}
