using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Extra NavMesh traffic around the Route 1 right-turn junction.
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

        [Header("Vehicle Prefabs")]
        [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();

        [SerializeField] private GlobalCityTrafficManager cityTraffic;

        private readonly List<GameObject> _activeVehicles = new List<GameObject>();
        private float _timer;
        private bool _isTrafficActive;

        private static readonly Vector3[] ApproachOffsets =
        {
            new Vector3(-40f, 0f, 0f),
            new Vector3(40f, 0f, 0f),
            new Vector3(0f, 0f, -40f),
            new Vector3(0f, 0f, 40f)
        };

        private void Start()
        {
            if (maxVehiclesInScene > 6) maxVehiclesInScene = 6;
            if (spawnInterval < 5f) spawnInterval = 6f;
            AutoFindPrefabsAndPaths();
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
                var bus = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BusModel/Prefabs/BogdanA092.prefab")
                       ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BusModel/Prefabs/BusOpenSource.prefab");
                if (taxi != null) vehiclePrefabs.Add(taxi);
                if (bus != null) vehiclePrefabs.Add(bus);
            }
#endif
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0)
            {
                if (cityTraffic == null && ExperimentRefs.Instance != null)
                {
                    cityTraffic = ExperimentRefs.Instance.cityTraffic;
                }
                if (cityTraffic != null && cityTraffic.vehiclePrefabs != null && cityTraffic.vehiclePrefabs.Count > 0)
                {
                    vehiclePrefabs = new List<GameObject>(cityTraffic.vehiclePrefabs);
                }
            }
        }

        public void StartTrafficFlow()
        {
            _isTrafficActive = true;
            _timer = 0f;
            Debug.Log("[IntersectionTrafficFlow] NavMesh intersection traffic STARTED.");

            for (int i = 0; i < ApproachOffsets.Length; i++)
            {
                SpawnVehicleNearIntersection(i);
            }
        }

        public void StopTrafficFlow()
        {
            _isTrafficActive = false;
            ClearAllVehicles();
            Debug.Log("[IntersectionTrafficFlow] NavMesh intersection traffic STOPPED.");
        }

        private void Update()
        {
            if (!_isTrafficActive) return;

            _activeVehicles.RemoveAll(v => v == null);

            _timer += Time.deltaTime;
            if (_timer >= spawnInterval)
            {
                _timer = 0f;
                if (_activeVehicles.Count < maxVehiclesInScene)
                {
                    SpawnVehicleNearIntersection(Random.Range(0, ApproachOffsets.Length));
                }
            }
        }

        private void SpawnVehicleNearIntersection(int approachIndex)
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0) return;

            GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)];
            if (prefab == null) return;

            Vector3 guess = intersectionCenter + ApproachOffsets[approachIndex];
            if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 18f, NavMesh.AllAreas)) return;

            Vector3 toward = intersectionCenter - hit.position;
            toward.y = 0f;
            Quaternion rot = toward.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toward.normalized)
                : Quaternion.identity;

            GameObject vehicle = VehicleRuntimeFactory.SpawnOnNavMesh(
                prefab,
                hit.position,
                rot,
                vehicleSpeed + Random.Range(-1.5f, 2.0f),
                $"TrafficFlow_Intersection_{_activeVehicles.Count}");

            if (vehicle != null) _activeVehicles.Add(vehicle);
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
