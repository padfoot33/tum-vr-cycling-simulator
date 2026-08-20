using System.Collections.Generic;
using UnityEngine;
using CyclingExperiment.AI;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Implements the right-turn mixed traffic scenario.
    /// Spawns intersection vehicles that navigate across the junction and turn into cross streets.
    /// </summary>
    public class Scenario2_RightTurn : MonoBehaviour
    {
        [Header("Traffic Setup")]
        [SerializeField, Tooltip("Paths for intersection traffic")]
        private List<WaypointPath> intersectionTrafficPaths = new List<WaypointPath>();

        [SerializeField, Tooltip("Prefabs for vehicles")]
        private List<GameObject> vehiclePrefabs = new List<GameObject>();

        [SerializeField, Tooltip("Number of vehicles to spawn")]
        private int vehicleCount = 3;

        [SerializeField, Tooltip("Speed of intersection vehicles in m/s (10 m/s = 36 km/h)")]
        private float vehicleSpeed = 10f;

        [Header("Player Reference")]
        [SerializeField, Tooltip("Reference to the player's transform")]
        private Transform playerTransform;

        [Header("End Trigger (Optional)")]
        [SerializeField, Tooltip("Trigger zone to mark end of intersection")]
        private Collider intersectionEndTrigger;

        private List<GameObject> _spawnedVehicles = new List<GameObject>();
        private bool _isScenarioActive = false;
        private bool _hasClearedIntersection = false;

        private void Start()
        {
            AutoAssignReferencesIfNull();
        }

        private void AutoAssignReferencesIfNull()
        {
            if (playerTransform == null)
            {
                var refs = ExperimentSceneRefs.Instance;
                if (refs != null && refs.bicycleTransform != null)
                    playerTransform = refs.bicycleTransform;
            }

            if (intersectionTrafficPaths == null)
            {
                intersectionTrafficPaths = new List<WaypointPath>();
            }

#if UNITY_EDITOR
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0)
            {
                vehiclePrefabs = new List<GameObject>();
                var taxi = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TaxiModel/Prefabs/TaxiOpenSource.prefab");
                if (taxi != null) vehiclePrefabs.Add(taxi);
            }
#endif
        }

        /// <summary>
        /// Starts the right turn scenario.
        /// </summary>
        public void ActivateScenario()
        {
            if (ScenarioManager.Instance != null && ScenarioManager.Instance.CurrentCondition == ExperimentCondition.Baseline)
            {
                Debug.Log("[Scenario2_RightTurn] Skipping traffic spawn (Baseline condition).");
                return;
            }

            AutoAssignReferencesIfNull();

            if (intersectionTrafficPaths.Count == 0 || vehiclePrefabs.Count == 0)
            {
                Debug.LogWarning("[Scenario2_RightTurn] Missing traffic paths or vehicle prefabs.");
                return;
            }

            // If vehicles are already spawned, do not duplicate
            if (_spawnedVehicles.Count > 0)
            {
                return;
            }

            Debug.Log("[Scenario2_RightTurn] Activating Right Turn Mixed Traffic Scenario!");

            if (EventMarkerLogger.Instance != null)
            {
                EventMarkerLogger.Instance.LogEvent("RIGHT_TURN_START");
            }

            if (ScenarioManager.Instance != null)
            {
                ScenarioManager.Instance.StartScenario("RightTurn");
            }

            for (int i = 0; i < vehicleCount; i++)
            {
                WaypointPath path = intersectionTrafficPaths[i % intersectionTrafficPaths.Count];
                if (path == null || path.WaypointCount == 0) continue;

                GameObject prefab = vehiclePrefabs[i % vehiclePrefabs.Count];
                if (prefab == null) continue;

                Vector3 spawnPos = path.GetWaypoint(0);
                Quaternion spawnRot = Quaternion.identity;
                if (path.WaypointCount > 1)
                {
                    Vector3 dir = (path.GetWaypoint(1) - spawnPos).normalized;
                    dir.y = 0;
                    if (dir != Vector3.zero) spawnRot = Quaternion.LookRotation(dir);
                }

                GameObject vehicle = Instantiate(prefab, spawnPos, spawnRot);
                vehicle.name = $"Scenario2_Traffic_{i}";

                // Disable SUMO controllers so they don't throw NullReferenceExceptions in pure Unity mode
                var taxiCtrl = vehicle.GetComponent("TaxiController") as MonoBehaviour;
                if (taxiCtrl != null) taxiCtrl.enabled = false;

                var carCtrl = vehicle.GetComponent("CarController") as MonoBehaviour;
                if (carCtrl != null) carCtrl.enabled = false;

                var busCtrl = vehicle.GetComponent("BusController") as MonoBehaviour;
                if (busCtrl != null) busCtrl.enabled = false;

                var rb = vehicle.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                // Vehicles yield to one another, but (by design) not to the cyclist during
                // the stress condition.  This keeps the event controlled without traffic
                // rear-ending or overlapping itself.
                var ai = vehicle.GetComponent<SmartVehicleAI>() ?? vehicle.AddComponent<SmartVehicleAI>();
                ai.Path = path;
                ai.Speed = vehicleSpeed + Random.Range(-1.5f, 1.5f);
                ai.DestroyAtEnd = true;
                ai.IsExperimentStressVehicle = true;

                _spawnedVehicles.Add(vehicle);
            }

            _isScenarioActive = true;
            _hasClearedIntersection = false;
        }

        private void Update()
        {
            if (_isScenarioActive && !_hasClearedIntersection && playerTransform != null)
            {
                if (intersectionEndTrigger != null)
                {
                    if (intersectionEndTrigger.bounds.Contains(playerTransform.position))
                    {
                        CompleteIntersection();
                    }
                }
                else
                {
                    // Fallback: if player travels 45m past this trigger
                    if (Vector3.Distance(transform.position, playerTransform.position) > 45f)
                    {
                        CompleteIntersection();
                    }
                }
            }
        }

        /// <summary>
        /// Cleans up spawned vehicles.
        /// </summary>
        public void DeactivateScenario()
        {
            if (_isScenarioActive)
            {
                CompleteIntersection();
            }
            
            foreach (var vehicle in _spawnedVehicles)
            {
                if (vehicle != null)
                {
                    Destroy(vehicle);
                }
            }
            _spawnedVehicles.Clear();
            _isScenarioActive = false;
        }

        private void CompleteIntersection()
        {
            if (_hasClearedIntersection) return;

            _hasClearedIntersection = true;
            Debug.Log("[Scenario2_RightTurn] Player cleared intersection. Logging RIGHT_TURN_END.");

            if (EventMarkerLogger.Instance != null)
            {
                EventMarkerLogger.Instance.LogEvent("RIGHT_TURN_END");
            }

            if (ScenarioManager.Instance != null)
            {
                ScenarioManager.Instance.EndScenario("RightTurn");
            }
        }

        private void OnDestroy()
        {
            DeactivateScenario();
        }
    }
}
