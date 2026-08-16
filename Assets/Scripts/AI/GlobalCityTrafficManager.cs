using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Spawns ambient campus traffic onto the baked road NavMesh (not the old yellow waypoint lines).
    /// </summary>
    public class GlobalCityTrafficManager : MonoBehaviour
    {
        [Header("Traffic Master Toggle")]
        [SerializeField] private bool isTrafficEnabled = true;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnInterval = 6f;
        [SerializeField] private int maxVehicles = 8;
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
            new Vector3(723f, 1f, 128f),
            new Vector3(436f, 1f, 80f),
            new Vector3(580f, 1f, 170f),
            new Vector3(300f, 1f, 172f),
            new Vector3(910f, 1f, -200f),
            new Vector3(520f, 1f, 380f)
        };

        private readonly List<GameObject> _spawnedVehicles = new List<GameObject>();
        private float _spawnTimer;

        public bool IsTrafficEnabled => isTrafficEnabled;

        private void Awake()
        {
            if (maxVehicles > 8) maxVehicles = 8;
            if (spawnInterval < 5f) spawnInterval = 6f;
            LoadAllVehiclePrefabsIfEmpty();
            RemoveOffroadPrefabs();
            HideLegacyWaypointCityPaths();
            if (destinations == null) destinations = TrafficDestinationSet.Instance;
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

                var bus = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BusModel/Prefabs/BogdanA092.prefab")
                       ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BusModel/Prefabs/BusOpenSource.prefab");
                if (bus != null) vehiclePrefabs.Add(bus);
            }
#endif
        }

        private void RemoveOffroadPrefabs()
        {
            if (vehiclePrefabs == null) return;
            vehiclePrefabs.RemoveAll(p => p != null &&
                p.name.IndexOf("offroad", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void RegisterPath(WaypointPath path)
        {
            // Kept so older scene wiring does not break. Ambient cars no longer follow these paths.
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
                    SpawnVehicleOnNavMesh();
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

            int initial = Mathf.Min(4, maxVehicles);
            for (int i = 0; i < initial; i++)
            {
                SpawnVehicleOnNavMesh();
            }
        }

        public void SpawnVehicleOnNavMesh()
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0) return;

            GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)];
            if (prefab == null) return;

            if (!TryFindSpawnPose(out Vector3 pos, out Quaternion rot))
            {
                return;
            }

            GameObject vehicle = VehicleRuntimeFactory.SpawnOnNavMesh(
                prefab,
                pos,
                rot,
                defaultTrafficSpeed + Random.Range(-1.5f, 2.0f),
                $"CityTraffic_{prefab.name}_{_spawnedVehicles.Count}");

            if (vehicle != null) _spawnedVehicles.Add(vehicle);
        }

        private static bool TryFindSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            for (int i = 0; i < 12; i++)
            {
                Vector3 seed = SeedPoints[Random.Range(0, SeedPoints.Length)];
                seed += new Vector3(Random.Range(-18f, 18f), 0f, Random.Range(-18f, 18f));

                if (!NavMesh.SamplePosition(seed, out NavMeshHit hit, 20f, NavMesh.AllAreas)) continue;

                rotation = NavMeshVehicleAI.HeadingAlongRoad(hit.position);
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

        private void OnDestroy()
        {
            ClearAllTraffic();
        }
    }
}
