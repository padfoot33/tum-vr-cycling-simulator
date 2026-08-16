using UnityEngine;
using CyclingExperiment.AI;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Implements the bus overtaking scenario using clean WaypointFollower navigation.
    /// Spawns the bus behind the cyclist at WP_0, overtakes briskly at high speed,
    /// turns right into the bus stop bay, and parks permanently at the bus stand (no vanishing).
    /// </summary>
    public class Scenario1_BusOvertake : MonoBehaviour
    {
        [Header("Bus Setup")]
        [SerializeField, Tooltip("The path the bus will follow")]
        private WaypointPath busOvertakePath;

        [SerializeField, Tooltip("The bus prefab to spawn")]
        private GameObject busPrefab;

        [SerializeField, Tooltip("Speed of the bus in m/s (16 m/s = ~58 km/h)")]
        private float busSpeed = 16.0f;

        [SerializeField, Tooltip("Keep the bus parked at the bus stand (true = does not disappear)")]
        private bool keepParkedAtStop = true;

        [Header("Player Reference")]
        [SerializeField, Tooltip("Reference to the player's transform")]
        private Transform playerTransform;

        private GameObject _spawnedBus;
        private WaypointFollower _follower;
        private bool _isScenarioActive = false;
        private bool _hasPassedPlayer = false;

        private void Start()
        {
            AutoAssignReferencesIfNull();
        }

        private void AutoAssignReferencesIfNull()
        {
            if (playerTransform == null)
            {
                var bike = GameObject.Find("bicyle_animated_human");
                if (bike != null) playerTransform = bike.transform;
            }

            if (busOvertakePath == null)
            {
                var pathObj = GameObject.Find("Bus_Overtake_Path");
                if (pathObj != null) busOvertakePath = pathObj.GetComponent<WaypointPath>();
            }

#if UNITY_EDITOR
            if (busPrefab == null)
            {
                busPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BusModel/Prefabs/BogdanA092.prefab")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BusModel/Prefabs/BusOpenSource.prefab");
            }
#endif
        }

        /// <summary>
        /// Starts the bus overtaking scenario.
        /// </summary>
        public void ActivateScenario()
        {
            // MainScene uses Scenario1_CombinedController as the authoritative Route 1 flow.
            // Keep this legacy component as a standalone fallback, but never let the same
            // trigger create a second bus when the combined controller is present.
            if (FindFirstObjectByType<Scenario1_CombinedController>() != null)
            {
                Debug.Log("[Scenario1_BusOvertake] Combined Scenario 1 is active; legacy bus spawn skipped.");
                return;
            }

            if (ScenarioManager.Instance != null && ScenarioManager.Instance.CurrentCondition == ExperimentCondition.Baseline)
            {
                Debug.Log("[Scenario1_BusOvertake] Skipping bus spawn (Baseline condition).");
                return;
            }

            AutoAssignReferencesIfNull();

            if (busPrefab == null)
            {
                Debug.LogError("[Scenario1_BusOvertake] Bus prefab is missing!");
                return;
            }

            if (busOvertakePath == null || busOvertakePath.WaypointCount == 0)
            {
                Debug.LogError("[Scenario1_BusOvertake] Bus overtake path is missing or empty!");
                return;
            }

            if (_spawnedBus != null)
            {
                return;
            }

            Debug.Log("[Scenario1_BusOvertake] Activating Bus Overtake Scenario!");

            if (EventMarkerLogger.Instance != null)
            {
                EventMarkerLogger.Instance.LogEvent("BUS_START");
            }

            if (ScenarioManager.Instance != null)
            {
                ScenarioManager.Instance.StartScenario("BusOvertake");
            }

            // Spawn bus at first waypoint (WP_0)
            Vector3 spawnPosition = busOvertakePath.GetWaypoint(0);
            Quaternion spawnRotation = Quaternion.identity;
            if (busOvertakePath.WaypointCount > 1)
            {
                Vector3 dir = (busOvertakePath.GetWaypoint(1) - spawnPosition).normalized;
                dir.y = 0;
                if (dir != Vector3.zero) spawnRotation = Quaternion.LookRotation(dir);
            }

            _spawnedBus = Instantiate(busPrefab, spawnPosition, spawnRotation);
            _spawnedBus.name = "Scenario_Bus_Spawned";

            // Disable conflicting SUMO scripts on the bus
            var sumoBusController = _spawnedBus.GetComponent("BusController") as MonoBehaviour;
            if (sumoBusController != null) sumoBusController.enabled = false;

            var rb = _spawnedBus.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Remove PhysicsBusController if attached
            var phys = _spawnedBus.GetComponent<PhysicsBusController>();
            if (phys != null) Destroy(phys);

            // Configure WaypointFollower
            _follower = _spawnedBus.GetComponent<WaypointFollower>() ?? _spawnedBus.AddComponent<WaypointFollower>();
            _follower.Path = busOvertakePath;
            _follower.Speed = busSpeed;
            _follower.DestroyAtEnd = !keepParkedAtStop; // If keepParkedAtStop is true, it will NOT destroy!

            _isScenarioActive = true;
            _hasPassedPlayer = false;
        }

        private void Update()
        {
            if (_isScenarioActive && _spawnedBus != null && !_hasPassedPlayer && playerTransform != null)
            {
                Vector3 toBus = _spawnedBus.transform.position - playerTransform.position;
                if (Vector3.Dot(toBus, playerTransform.forward) > 4f)
                {
                    _hasPassedPlayer = true;
                    Debug.Log("[Scenario1_BusOvertake] Bus passed cyclist. Logging BUS_END.");
                    if (EventMarkerLogger.Instance != null)
                    {
                        EventMarkerLogger.Instance.LogEvent("BUS_END");
                    }

                    if (ScenarioManager.Instance != null)
                    {
                        ScenarioManager.Instance.EndScenario("BusOvertake");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (!keepParkedAtStop && _spawnedBus != null)
            {
                Destroy(_spawnedBus);
            }
        }
    }
}
