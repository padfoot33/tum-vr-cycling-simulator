using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Spawns spaced campus traffic onto the baked road NavMesh.
    /// </summary>
    public class GlobalCityTrafficManager : MonoBehaviour
    {
        public static GlobalCityTrafficManager Instance { get; private set; }

        public const float SpawnClearRadius = 14f;
        public const float Route2SlotSpacing = 16f;
        public const float Route2SlotStartZ = 65f;
        public const float Route2SlotEndZ = 190f;
        public const float Route2RightLaneX = 724.6f;

        [Header("Traffic Master Toggle")]
        [SerializeField] private bool isTrafficEnabled = true;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnInterval = 6f;
        [SerializeField] private int maxVehicles = 36;
        [SerializeField] private float defaultTrafficSpeed = 9.5f;

        [Header("Vehicle Prefab Pool")]
        [SerializeField] public List<GameObject> vehiclePrefabs = new List<GameObject>();

        [Header("Legacy waypoint list (unused for ambient traffic)")]
        [SerializeField] public List<WaypointPath> trafficPaths = new List<WaypointPath>();

        [SerializeField] private GameObject cityTrafficPathsRoot;
        [SerializeField] private TrafficDestinationSet destinations;

        private static readonly Vector3[] SeedPoints =
        {
            new Vector3(436f, 1f, -40f),
            new Vector3(430f, 1f, 174f),
            new Vector3(580f, 1f, 80f),
            new Vector3(436f, 1f, 80f),
            new Vector3(580f, 1f, 170f),
            new Vector3(300f, 1f, 172f),
            new Vector3(910f, 1f, -200f),
            new Vector3(520f, 1f, 380f)
        };

        private readonly List<GameObject> _spawnedVehicles = new List<GameObject>();
        private float _spawnTimer;
        private bool _preferRoute2Next = true;

        public bool IsTrafficEnabled => isTrafficEnabled;
        public IReadOnlyList<GameObject> ActiveVehicles => _spawnedVehicles;

        private void Awake()
        {
            Instance = this;
            if (maxVehicles < 1) maxVehicles = 1;
            if (maxVehicles == 8) maxVehicles = 36;
            if (spawnInterval < 0.5f) spawnInterval = 0.5f;
            LoadAllVehiclePrefabsIfEmpty();
            SanitizePrefabPool();
            HideLegacyWaypointCityPaths();
            if (destinations == null) destinations = TrafficDestinationSet.Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearAllTraffic();
        }

        private void HideLegacyWaypointCityPaths()
        {
            if (cityTrafficPathsRoot != null) cityTrafficPathsRoot.SetActive(false);
        }

        private void Start()
        {
            LoadAllVehiclePrefabsIfEmpty();

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
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleTraffic();
            }

            if (!isTrafficEnabled) return;

            _spawnedVehicles.RemoveAll(v => v == null);

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval)
            {
                _spawnTimer = 0f;
                if (_spawnedVehicles.Count < maxVehicles)
                {
                    SpawnVehicleOnNavMesh(_preferRoute2Next);
                    _preferRoute2Next = !_preferRoute2Next;
                }
            }
        }

        private void SpawnInitialVehicles()
        {
            if (!NavMesh.SamplePosition(SeedPoints[0], out _, 25f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[GlobalCityTraffic] No road NavMesh found. Use Cycling Experiment > Bake Road NavMesh.");
                return;
            }

            SpawnVehicleOnNavMesh(preferRoute2: true);
            SpawnVehicleOnNavMesh(preferRoute2: false);
            SpawnVehicleOnNavMesh(preferRoute2: true);
        }

        public void SpawnVehicleOnNavMesh()
        {
            SpawnVehicleOnNavMesh(preferRoute2: _preferRoute2Next);
        }

        public void SpawnVehicleOnNavMesh(bool preferRoute2)
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0) return;
            if (_spawnedVehicles.Count >= maxVehicles) return;

            GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)];
            if (prefab == null) return;

            if (!TryFindSpawnPose(preferRoute2, out Vector3 pos, out Quaternion rot))
            {
                if (preferRoute2 && !TryFindSpawnPose(false, out pos, out rot)) return;
                if (!preferRoute2) return;
            }

            GameObject vehicle = VehicleRuntimeFactory.SpawnOnNavMesh(
                prefab,
                pos,
                rot,
                defaultTrafficSpeed + Random.Range(-1.5f, 2.0f),
                $"CityTraffic_{prefab.name}_{_spawnedVehicles.Count}");

            if (vehicle != null) _spawnedVehicles.Add(vehicle);
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

        private bool TryFindSpawnPose(bool preferRoute2, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (preferRoute2 && TryFindSouthmostRoute2Slot(out position))
            {
                rotation = Quaternion.LookRotation(Vector3.forward);
                return true;
            }

            for (int i = 0; i < 16; i++)
            {
                Vector3 seed = SeedPoints[Random.Range(0, SeedPoints.Length)];
                seed += new Vector3(Random.Range(-18f, 18f), 0f, Random.Range(-18f, 18f));
                if (NavMeshVehicleAI.IsRoute2Corridor(seed)) continue;
                if (!NavMesh.SamplePosition(seed, out NavMeshHit hit, 20f, NavMesh.AllAreas)) continue;
                if (NavMeshVehicleAI.IsRoute2Corridor(hit.position)) continue;
                if (!IsSpawnClear(hit.position)) continue;

                rotation = NavMeshVehicleAI.HeadingAlongRoad(hit.position);
                position = hit.position;
                return true;
            }

            return false;
        }

        private bool TryFindSouthmostRoute2Slot(out Vector3 position)
        {
            position = Vector3.zero;
            for (float z = Route2SlotStartZ; z <= Route2SlotEndZ; z += Route2SlotSpacing)
            {
                Vector3 guess = new Vector3(Route2RightLaneX, 1f, z);
                if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 8f, NavMesh.AllAreas)) continue;
                if (!NavMeshVehicleAI.IsRoute2Corridor(hit.position)) continue;
                if (!IsSpawnClear(hit.position)) continue;

                position = hit.position;
                return true;
            }

            return false;
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
    }
}
