using System.Collections.Generic;
using UnityEngine;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Extra waypoint traffic around the Route 1 right-turn junction.
    /// </summary>
    public class IntersectionTrafficFlowManager : MonoBehaviour
    {
        [Header("Intersection Center")]
        [SerializeField] private Vector3 intersectionCenter = new Vector3(430f, 0f, 174f);

        [Header("Traffic Flow Settings")]
        [SerializeField] private bool autoStart = false;
        [SerializeField] private float spawnInterval = 6f;
        [SerializeField] private int maxVehiclesInScene = 6;
        [SerializeField] private float vehicleSpeed = 9.0f;
        [SerializeField] private float approachRadius = 55f;

        [Header("Vehicle Prefabs")]
        [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();

        [SerializeField] private GlobalCityTrafficManager cityTraffic;

        private readonly List<GameObject> _activeVehicles = new List<GameObject>();
        private readonly List<WaypointPath> _nearbyPaths = new List<WaypointPath>(8);
        private float _timer;
        private float _cullTimer;
        private bool _isTrafficActive;

        private void Start()
        {
            if (maxVehiclesInScene < 1) maxVehiclesInScene = 1;
            if (spawnInterval < 0.5f) spawnInterval = 0.5f;
            AutoFindPrefabsAndPaths();
            BindCityTraffic();
            if (autoStart)
            {
                StartTrafficFlow();
            }
        }

        public void AutoFindPrefabsAndPaths()
        {
#if UNITY_EDITOR
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0)
            {
                vehiclePrefabs = new List<GameObject>();
                var taxi = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TaxiModel/Prefabs/TaxiOpenSource.prefab");
                if (taxi != null) vehiclePrefabs.Add(taxi);
            }
#endif
            if (vehiclePrefabs != null)
            {
                vehiclePrefabs.RemoveAll(p => p != null && GlobalCityTrafficManager.IsExcludedAmbientPrefab(p.name));
            }

            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0)
            {
                BindCityTraffic();
                if (cityTraffic != null && cityTraffic.vehiclePrefabs != null && cityTraffic.vehiclePrefabs.Count > 0)
                {
                    vehiclePrefabs = new List<GameObject>(cityTraffic.vehiclePrefabs);
                }
            }
        }

        public void StartTrafficFlow()
        {
            BindCityTraffic();
            _isTrafficActive = true;
            _timer = 0f;
            Debug.Log("[IntersectionTrafficFlow] Waypoint intersection traffic STARTED.");

            RefreshNearbyPaths();
            int waves = Mathf.Max(1, _nearbyPaths.Count);
            for (int i = 0; i < waves; i++)
            {
                SpawnVehicleNearIntersection(i);
            }
        }

        public void StopTrafficFlow()
        {
            _isTrafficActive = false;
            ClearAllVehicles();
            Debug.Log("[IntersectionTrafficFlow] Waypoint intersection traffic STOPPED.");
        }

        private void Update()
        {
            if (!_isTrafficActive) return;

            _activeVehicles.RemoveAll(v => v == null);

            _cullTimer += Time.deltaTime;
            if (_cullTimer >= 0.25f)
            {
                _cullTimer = 0f;
                CullDistantVehicles();
            }

            _timer += Time.deltaTime;
            if (_timer >= spawnInterval)
            {
                _timer = 0f;
                if (_activeVehicles.Count < maxVehiclesInScene)
                {
                    SpawnVehicleNearIntersection(Random.Range(0, Mathf.Max(1, _nearbyPaths.Count)));
                }
            }
        }

        private void BindCityTraffic()
        {
            var refs = ExperimentRefs.Instance;
            if (cityTraffic == null && refs != null) cityTraffic = refs.cityTraffic;
        }

        private void RefreshNearbyPaths()
        {
            _nearbyPaths.Clear();
            BindCityTraffic();
            if (cityTraffic == null) return;

            IReadOnlyList<WaypointPath> paths = cityTraffic.TrafficPaths;
            if (paths == null) return;

            float radiusSq = approachRadius * approachRadius;
            for (int i = 0; i < paths.Count; i++)
            {
                WaypointPath path = paths[i];
                if (path == null || path.WaypointCount == 0) continue;
                path.SyncFromChildren();
                if (PathPassesNearIntersection(path, radiusSq))
                {
                    _nearbyPaths.Add(path);
                }
            }
        }

        private bool PathPassesNearIntersection(WaypointPath path, float radiusSq)
        {
            for (int i = 0; i < path.WaypointCount; i++)
            {
                Vector3 delta = path.GetWaypoint(i) - intersectionCenter;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSq) return true;
            }

            return false;
        }

        private void SpawnVehicleNearIntersection(int approachIndex)
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0) return;
            BindCityTraffic();
            if (_nearbyPaths.Count == 0) RefreshNearbyPaths();
            if (_nearbyPaths.Count == 0) return;

            GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)];
            if (prefab == null) return;

            WaypointPath path = _nearbyPaths[Mathf.Clamp(approachIndex, 0, _nearbyPaths.Count - 1)];
            if (path == null || path.WaypointCount == 0) return;
            path.SyncFromChildren();

            float t = path.isLoop ? Random.Range(0f, 1f) : Random.Range(0.05f, 0.35f);
            if (!path.TryGetPointAlongPath(t, out Vector3 position, out Vector3 forward, out int nextIndex))
                return;
            if (cityTraffic != null && !cityTraffic.IsInsideSpawnRing(position)) return;
            if (!IsApproachClear(position)) return;

            Quaternion rotation = forward.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(forward)
                : Quaternion.identity;

            GameObject vehicle = VehicleRuntimeFactory.SpawnAmbientOnWaypointPath(
                prefab,
                position,
                rotation,
                path,
                nextIndex,
                vehicleSpeed + Random.Range(-1.5f, 2.0f),
                $"TrafficFlow_Intersection_{_activeVehicles.Count}");

            if (vehicle != null) _activeVehicles.Add(vehicle);
        }

        private bool IsApproachClear(Vector3 position)
        {
            if (GlobalCityTrafficManager.Instance != null &&
                !GlobalCityTrafficManager.Instance.IsSpawnClear(position))
            {
                return false;
            }

            const float clearRadius = GlobalCityTrafficManager.SpawnClearRadius;
            for (int i = 0; i < _activeVehicles.Count; i++)
            {
                GameObject other = _activeVehicles[i];
                if (other == null) continue;
                Vector3 delta = other.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude < clearRadius * clearRadius) return false;
            }

            return true;
        }

        private void CullDistantVehicles()
        {
            BindCityTraffic();
            if (cityTraffic == null) return;

            for (int i = _activeVehicles.Count - 1; i >= 0; i--)
            {
                GameObject vehicle = _activeVehicles[i];
                if (vehicle == null)
                {
                    _activeVehicles.RemoveAt(i);
                    continue;
                }

                if (!cityTraffic.IsBeyondDespawnRadius(vehicle.transform.position)) continue;
                Destroy(vehicle);
                _activeVehicles.RemoveAt(i);
            }
        }

        public void ClearAllVehicles()
        {
            foreach (var v in _activeVehicles)
            {
                if (v != null) Destroy(v);
            }
            _activeVehicles.Clear();
        }

        private void OnDestroy()
        {
            ClearAllVehicles();
        }
    }
}
