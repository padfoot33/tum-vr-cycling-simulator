using System.Collections.Generic;
using UnityEngine;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Spawns spaced campus traffic onto authored WaypointPath routes.
    /// </summary>
    public class GlobalCityTrafficManager : MonoBehaviour
    {
        public static GlobalCityTrafficManager Instance { get; private set; }

        public const float SpawnClearRadius = 14f;

        [Header("Traffic Master Toggle")]
        [SerializeField] private bool isTrafficEnabled = true;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnInterval = 6f;
        [SerializeField] private int maxVehicles = 36;
        [SerializeField] private float defaultTrafficSpeed = 9.5f;

        [Header("Proximity spawn")]
        [SerializeField, Tooltip("Do not spawn closer than this (metres, horizontal).")]
        private float spawnMinDistance = 10f;
        [SerializeField, Tooltip("Do not spawn farther than this (metres, horizontal).")]
        private float spawnMaxDistance = 200f;
        [SerializeField, Tooltip("Destroy cars this many metres beyond max so they do not flicker at the outer edge.")]
        private float despawnPadding = 20f;
        [SerializeField] private bool drawSpawnRadiusGizmo = true;

        [Header("Vehicle Prefab Pool")]
        [SerializeField] public List<GameObject> vehiclePrefabs = new List<GameObject>();

        [Header("Campus traffic paths")]
        [SerializeField] public List<WaypointPath> trafficPaths = new List<WaypointPath>();
        [SerializeField] private GameObject campusTrafficPathsRoot;
        [SerializeField] private GameObject cityTrafficPathsRoot;

        private readonly List<GameObject> _spawnedVehicles = new List<GameObject>();
        private float _spawnTimer;
        private float _cullTimer;

        public bool IsTrafficEnabled => isTrafficEnabled;
        public IReadOnlyList<GameObject> ActiveVehicles => _spawnedVehicles;
        public IReadOnlyList<WaypointPath> TrafficPaths
        {
            get
            {
                RefreshPathList();
                return trafficPaths;
            }
        }

        private void Awake()
        {
            Instance = this;
            if (maxVehicles < 1) maxVehicles = 1;
            if (maxVehicles == 8) maxVehicles = 36;
            if (spawnInterval < 0.5f) spawnInterval = 0.5f;
            LoadAllVehiclePrefabsIfEmpty();
            SanitizePrefabPool();
            HideLegacyWaypointCityPaths();
            BindPaths();
            ClampProximity();
        }

        private void OnValidate()
        {
            ClampProximity();
        }

        private void ClampProximity()
        {
            if (spawnMinDistance < 0f) spawnMinDistance = 0f;
            if (spawnMaxDistance <= spawnMinDistance) spawnMaxDistance = spawnMinDistance + 1f;
            if (despawnPadding < 0f) despawnPadding = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearAllTraffic();
        }

        private void BindPaths()
        {
            var refs = ExperimentRefs.Instance;
            if (campusTrafficPathsRoot == null && refs != null) campusTrafficPathsRoot = refs.campusTrafficPaths;
            if (cityTrafficPathsRoot == null && refs != null) cityTrafficPathsRoot = refs.cityTrafficPaths;
            RefreshPathList();
        }

        public void RefreshPathList()
        {
            if (trafficPaths == null) trafficPaths = new List<WaypointPath>();
            trafficPaths.RemoveAll(p => p == null
                                        || WaypointPath.IsReservedScenarioPath(p)
                                        || !p.gameObject.activeInHierarchy);

            if (campusTrafficPathsRoot == null)
            {
                var refs = ExperimentRefs.Instance;
                if (refs != null) campusTrafficPathsRoot = refs.campusTrafficPaths;
            }

            if (campusTrafficPathsRoot == null) return;

            var found = campusTrafficPathsRoot.GetComponentsInChildren<WaypointPath>(true);
            for (int i = 0; i < found.Length; i++)
            {
                WaypointPath path = found[i];
                if (path == null || WaypointPath.IsReservedScenarioPath(path)) continue;
                if (!path.gameObject.activeInHierarchy) continue;
                if (!trafficPaths.Contains(path)) trafficPaths.Add(path);
            }
        }

        private void HideLegacyWaypointCityPaths()
        {
            if (cityTrafficPathsRoot != null && cityTrafficPathsRoot.name != WaypointPath.CampusRootName)
            {
                cityTrafficPathsRoot.SetActive(false);
            }
        }

        private void Start()
        {
            LoadAllVehiclePrefabsIfEmpty();
            BindPaths();

            if (isTrafficEnabled)
            {
                SpawnInitialVehicles();
            }
        }

        public void LoadAllVehiclePrefabsIfEmpty()
        {
#if UNITY_EDITOR
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0)
            {
                vehiclePrefabs = new List<GameObject>();
                string[] carNames = { "sedanCar", "suvCar", "hatchbackCar", "wagonCar", "coupeCar", "multivanCar" };
                foreach (var name in carNames)
                {
                    var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Sumonity-PassengerCars/prefabs/{name}.prefab");
                    if (p != null) vehiclePrefabs.Add(p);
                }

                var taxi = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TaxiModel/Prefabs/TaxiOpenSource.prefab");
                if (taxi != null) vehiclePrefabs.Add(taxi);
            }
#endif
            SanitizePrefabPool();
        }

        private void SanitizePrefabPool()
        {
            if (vehiclePrefabs == null) return;
            vehiclePrefabs.RemoveAll(p => p != null && IsExcludedAmbientPrefab(p.name));
        }

        public static bool IsExcludedAmbientPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            return prefabName.IndexOf("offroad", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || prefabName.IndexOf("bus", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void RegisterPath(WaypointPath path)
        {
            if (path == null || WaypointPath.IsReservedScenarioPath(path)) return;
            if (trafficPaths == null) trafficPaths = new List<WaypointPath>();
            if (!trafficPaths.Contains(path)) trafficPaths.Add(path);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleTraffic();
            }

            if (!isTrafficEnabled) return;

            _spawnedVehicles.RemoveAll(v => v == null);

            _cullTimer += Time.deltaTime;
            if (_cullTimer >= 0.25f)
            {
                _cullTimer = 0f;
                CullDistantVehicles();
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval)
            {
                _spawnTimer = 0f;
                if (_spawnedVehicles.Count < maxVehicles)
                    SpawnVehicleOnPath();
            }
        }

        private void SpawnInitialVehicles()
        {
            BindPaths();
            if (trafficPaths == null || trafficPaths.Count == 0)
            {
                Debug.LogWarning("[GlobalCityTraffic] No campus traffic paths. Use Cycling Experiment > Create Campus Traffic Path.");
                return;
            }

            SpawnVehicleOnPath();
            SpawnVehicleOnPath();
            SpawnVehicleOnPath();
        }

        public void SpawnVehicleOnGraph()
        {
            SpawnVehicleOnPath();
        }

        public void SpawnVehicleOnPath()
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0) return;
            if (_spawnedVehicles.Count >= maxVehicles) return;
            BindPaths();
            if (trafficPaths == null || trafficPaths.Count == 0) return;

            GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)];
            if (prefab == null) return;

            if (!TryPickSpawn(out WaypointPath path, out Vector3 position, out Quaternion rotation, out int nextIndex))
                return;

            GameObject vehicle = VehicleRuntimeFactory.SpawnAmbientOnWaypointPath(
                prefab,
                position,
                rotation,
                path,
                nextIndex,
                defaultTrafficSpeed + Random.Range(-1.5f, 2.0f),
                $"CityTraffic_{prefab.name}_{_spawnedVehicles.Count}");

            if (vehicle != null) _spawnedVehicles.Add(vehicle);
        }

        private bool TryPickSpawn(out WaypointPath path, out Vector3 position, out Quaternion rotation, out int nextIndex)
        {
            path = null;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            nextIndex = 0;

            int pathCount = trafficPaths.Count;
            int pathOffset = Random.Range(0, pathCount);
            for (int p = 0; p < pathCount; p++)
            {
                WaypointPath candidate = trafficPaths[(p + pathOffset) % pathCount];
                if (candidate == null || candidate.WaypointCount == 0) continue;
                candidate.SyncFromChildren();
                if (TryPickPointOnPath(candidate, out position, out Vector3 forward, out nextIndex))
                {
                    path = candidate;
                    rotation = forward.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(forward)
                        : Quaternion.identity;
                    return true;
                }
            }

            return false;
        }

        private bool TryPickPointOnPath(WaypointPath path, out Vector3 position, out Vector3 forward, out int nextIndex)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            nextIndex = 0;

            float tMax = path.isLoop ? 1f : 0.65f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float t = Random.Range(0f, tMax);
                if (!path.TryGetPointAlongPath(t, out position, out forward, out nextIndex)) continue;
                if (IsInsideSpawnRing(position) && IsSpawnClear(position)) return true;
            }

            for (float t = 0f; t <= tMax; t += 0.05f)
            {
                if (!path.TryGetPointAlongPath(t, out position, out forward, out nextIndex)) continue;
                if (IsInsideSpawnRing(position) && IsSpawnClear(position)) return true;
            }

            return false;
        }

        public bool IsInsideSpawnRing(Vector3 position)
        {
            if (!TryGetCyclistPosition(out Vector3 origin)) return false;
            float distance = HorizontalDistance(origin, position);
            return distance >= spawnMinDistance && distance <= spawnMaxDistance;
        }

        public bool IsBeyondDespawnRadius(Vector3 position)
        {
            if (!TryGetCyclistPosition(out Vector3 origin)) return false;
            return HorizontalDistance(origin, position) > spawnMaxDistance + despawnPadding;
        }

        private void CullDistantVehicles()
        {
            for (int i = _spawnedVehicles.Count - 1; i >= 0; i--)
            {
                GameObject vehicle = _spawnedVehicles[i];
                if (vehicle == null)
                {
                    _spawnedVehicles.RemoveAt(i);
                    continue;
                }

                if (!IsBeyondDespawnRadius(vehicle.transform.position)) continue;
                Destroy(vehicle);
                _spawnedVehicles.RemoveAt(i);
            }
        }

        private static bool TryGetCyclistPosition(out Vector3 position)
        {
            Transform bike = TrafficIdentity.Cyclist;
            if (bike == null)
            {
                position = Vector3.zero;
                return false;
            }

            position = bike.position;
            return true;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public bool IsSpawnClear(Vector3 position, GameObject ignore = null)
        {
            for (int i = 0; i < _spawnedVehicles.Count; i++)
            {
                GameObject other = _spawnedVehicles[i];
                if (other == null || other == ignore) continue;
                Vector3 delta = other.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude < SpawnClearRadius * SpawnClearRadius) return false;
            }

            return true;
        }

        private bool IsSpawnClear(Vector3 position)
        {
            return IsSpawnClear(position, null);
        }

        public void ToggleTraffic()
        {
            SetTrafficEnabled(!isTrafficEnabled);
        }

        public void SetTrafficEnabled(bool enabled)
        {
            isTrafficEnabled = enabled;
            if (!isTrafficEnabled)
            {
                ClearAllTraffic();
                Debug.Log("[GlobalCityTraffic] City traffic DISABLED.");
            }
            else
            {
                SpawnInitialVehicles();
                Debug.Log("[GlobalCityTraffic] City traffic ENABLED.");
            }
        }

        public void ClearAllTraffic()
        {
            foreach (var v in _spawnedVehicles)
            {
                if (v != null) Destroy(v);
            }
            _spawnedVehicles.Clear();
        }

        private void OnDrawGizmos()
        {
            if (!drawSpawnRadiusGizmo) return;

            Vector3 origin = transform.position;
            if (TryGetCyclistPosition(out Vector3 bikePos)) origin = bikePos;

            DrawRadiusCircle(origin, spawnMinDistance, new Color(1f, 0.45f, 0.15f, 0.9f));
            DrawRadiusCircle(origin, spawnMaxDistance, new Color(0.25f, 0.85f, 1f, 0.9f));
        }

        private static void DrawRadiusCircle(Vector3 center, float radius, Color color)
        {
            if (radius <= 0.01f) return;

            Gizmos.color = color;
            const int segments = 64;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
