using System.Collections.Generic;
using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Spawns AI traffic vehicles along multiple paths.
    /// </summary>
    public class TrafficSpawner : MonoBehaviour
    {
        [Header("Traffic Settings")]
        [Tooltip("Paths to spawn vehicles on.")]
        [SerializeField] private List<WaypointPath> trafficPaths = new List<WaypointPath>();

        [Tooltip("Prefabs to spawn.")]
        [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();

        [Tooltip("Time in seconds between spawns.")]
        [SerializeField] private float spawnInterval = 5f;

        [Tooltip("Maximum number of vehicles active at once.")]
        [SerializeField] private int maxVehicles = 10;

        [Tooltip("Automatically start spawning on Start.")]
        [SerializeField] private bool autoStart = true;

        private List<GameObject> _activeVehicles = new List<GameObject>();
        private float _spawnTimer = 0f;
        private bool _isSpawning = false;

        private void Start()
        {
            if (autoStart)
            {
                StartSpawning();
            }
        }

        private void Update()
        {
            if (!_isSpawning || trafficPaths.Count == 0 || vehiclePrefabs.Count == 0) return;

            // Clean up nulls in case they were destroyed
            _activeVehicles.RemoveAll(v => v == null);

            if (_activeVehicles.Count >= maxVehicles) return;

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval)
            {
                _spawnTimer = 0f;
                SpawnVehicle();
            }
        }

        private void SpawnVehicle()
        {
            WaypointPath path = trafficPaths[Random.Range(0, trafficPaths.Count)];
            GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)];

            if (path == null || path.WaypointCount == 0) return;

            GameObject vehicle = Instantiate(prefab, path.GetWaypoint(0), Quaternion.identity);
            
            WaypointFollower follower = vehicle.GetComponent<WaypointFollower>();
            if (follower == null)
            {
                follower = vehicle.AddComponent<WaypointFollower>();
            }

            follower.Path = path;
            follower.OnPathComplete += () => HandlePathComplete(vehicle);

            _activeVehicles.Add(vehicle);
        }

        private void HandlePathComplete(GameObject vehicle)
        {
            if (_activeVehicles.Contains(vehicle))
            {
                _activeVehicles.Remove(vehicle);
            }
        }

        /// <summary>
        /// Starts the spawning process.
        /// </summary>
        public void StartSpawning()
        {
            _isSpawning = true;
            _spawnTimer = 0f;
        }

        /// <summary>
        /// Stops the spawning process.
        /// </summary>
        public void StopSpawning()
        {
            _isSpawning = false;
        }

        /// <summary>
        /// Destroys all currently spawned vehicles.
        /// </summary>
        public void DespawnAll()
        {
            foreach (var vehicle in _activeVehicles)
            {
                if (vehicle != null)
                {
                    Destroy(vehicle);
                }
            }
            _activeVehicles.Clear();
        }

        /// <summary>
        /// Changes the spawn interval.
        /// </summary>
        public void SetSpawnInterval(float interval)
        {
            spawnInterval = interval;
        }

        private void OnDestroy()
        {
            DespawnAll();
        }
    }
}
