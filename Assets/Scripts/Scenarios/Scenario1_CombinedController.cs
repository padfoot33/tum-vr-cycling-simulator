using UnityEngine;
using CyclingExperiment.AI;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Master State Machine Controller for Combined Scenario 1 (Route 1).
    /// Continuous flow:
    /// Stage 1: Bus Overtake on left & Bus Stand Parking
    /// Stage 2: Recovery Corridor past parked bus along red-orange bike path
    /// Stage 3: Aggressive Car Overtake during Right-Turn junction
    /// Stage 4: Route 1 Complete
    /// </summary>
    public class Scenario1_CombinedController : MonoBehaviour
    {
        [Header("Stage 1: Bus Overtake Setup")]
        [SerializeField, Tooltip("Path the bus follows to overtake and pull into the bus bay")]
        private WaypointPath busOvertakePath;

        [SerializeField, Tooltip("Bus prefab to spawn")]
        private GameObject busPrefab;

        [SerializeField, Range(4f, 12f), Tooltip("Overtaking bus speed in m/s (12 ≈ 43 km/h). Tune on Scenario_1.")]
        private float busSpeed = 12f;

        [Header("Stage 3: Right Turn Overtaking Car Setup")]
        [SerializeField, Tooltip("Path the aggressive car follows during the right turn")]
        private WaypointPath rightTurnCarPath;

        [SerializeField, Tooltip("Overtaking car prefab")]
        private GameObject overtakingCarPrefab;

        [SerializeField, Tooltip("Speed of overtaking car in m/s (11 ≈ 40 km/h)")]
        private float overtakingCarSpeed = 11f;

        [Header("Timing / Distances")]
        [SerializeField, Tooltip("How far ahead the bus must be (m) to count as having overtaken")]
        private float busPassedAheadDistance = 4f;
        [SerializeField, Tooltip("How far ahead the cyclist must be (m) to end the bus stage")]
        private float cyclistClearedBusDistance = 4f;
        [SerializeField, Tooltip("How far ahead the right-turn car must be (m) to end that stage")]
        private float carPassedAheadDistance = 3f;

        [Header("Station Ambient")]
        [SerializeField, Range(0f, 1f)] private float stationAmbientVolume = 0.22f;
        [SerializeField] private float stationAmbientMinDistance = 6f;
        [SerializeField] private float stationAmbientMaxDistance = 36f;

        [Header("Player Reference")]
        [SerializeField] private Transform playerTransform;

        [Header("Scene refs")]
        [SerializeField] private ExperimentRefs sceneRefs;
        [SerializeField] private IntersectionTrafficFlowManager intersectionTraffic;

        [Header("Bus Audio")]
        [SerializeField] private AudioClip busEngineLoop;
        [SerializeField] private AudioClip busBrake;
        [SerializeField] private AudioClip busIdleStop;
        [SerializeField] private AudioClip busDeparture;
        [SerializeField] private AudioClip busStationAmbient;

        // Internal state
        private GameObject _spawnedBus;
        private WaypointFollower _spawnedBusFollower;
        private GameObject _spawnedOvertakingCar;
        private bool _busOvertakeTriggered = false;
        private bool _rightTurnTriggered = false;
        private bool _busPassedPlayer = false;
        private bool _busClearedByCyclist = false;
        private bool _carPassedPlayer = false;

        private void Start()
        {
            AutoAssignReferences();
            ClampScenarioSpeeds();
            StartStationAmbient();
        }

        private void ClampScenarioSpeeds()
        {
            busSpeed = Mathf.Clamp(busSpeed, 4f, 12f);
            if (overtakingCarSpeed > 14f) overtakingCarSpeed = 11f;
        }

        public void AutoAssignReferences()
        {
            if (sceneRefs == null) sceneRefs = ExperimentRefs.EnsureExists();
            if (playerTransform == null && sceneRefs != null) playerTransform = sceneRefs.bicycleTransform;
            if (intersectionTraffic == null && sceneRefs != null) intersectionTraffic = sceneRefs.intersectionTraffic;

#if UNITY_EDITOR
            if (busOvertakePath == null)
            {
                var p = GameObject.Find("Bus_Overtake_Path");
                if (p != null) busOvertakePath = p.GetComponent<WaypointPath>();
            }
            if (rightTurnCarPath == null)
            {
                var p = GameObject.Find("RightTurn_Overtaking_Car_Path");
                if (p != null) rightTurnCarPath = p.GetComponent<WaypointPath>();
            }
#endif

#if UNITY_EDITOR
            var bogdan = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BusModel/Prefabs/BogdanA092.prefab");
            if (bogdan != null) busPrefab = bogdan;
            else if (busPrefab == null)
            {
                busPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BusModel/Prefabs/BusOpenSource.prefab");
            }

            if (overtakingCarPrefab == null)
            {
                overtakingCarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sumonity-PassengerCars/prefabs/sedanCar.prefab")
                                  ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TaxiModel/Prefabs/TaxiOpenSource.prefab");
            }

            if (busEngineLoop == null) busEngineLoop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bus/bus_engine_loop.mp3");
            if (busBrake == null) busBrake = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bus/bus_brake.mp3");
            if (busIdleStop == null) busIdleStop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bus/bus_idle_stop.mp3");
            if (busDeparture == null) busDeparture = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bus/bus_departure.wav");
            if (busStationAmbient == null) busStationAmbient = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bus/bus_station_ambient.wav");
#endif
        }

        /// <summary>
        /// Trigger 1: Starts Stage 1 Bus Overtake
        /// </summary>
        public void TriggerBusOvertake()
        {
            if (_busOvertakeTriggered) return;

            AutoAssignReferences();

            if (ScenarioManager.Instance != null && ScenarioManager.Instance.CurrentCondition == ExperimentCondition.Baseline)
            {
                Debug.Log("[Scenario1] Baseline condition: Skipping bus overtake.");
                return;
            }

            if (busOvertakePath == null || busPrefab == null)
            {
                Debug.LogWarning("[Scenario1] Missing bus path or bus prefab.");
                return;
            }

            ClampScenarioSpeeds();
            _busOvertakeTriggered = true;
            Debug.Log("[Scenario1] Stage 1: Bus Overtake Triggered!");

            if (EventMarkerLogger.Instance != null) EventMarkerLogger.Instance.LogEvent("BUS_EVENT_START");
            if (ScenarioManager.Instance != null) ScenarioManager.Instance.StartScenario("Route1_BusOvertake");

            Vector3 spawnPos = busOvertakePath.GetWaypoint(0);
            Quaternion spawnRot = Quaternion.identity;
            if (busOvertakePath.WaypointCount > 1)
            {
                Vector3 dir = busOvertakePath.GetWaypoint(1) - spawnPos;
                dir.y = 0;
                if (dir != Vector3.zero) spawnRot = Quaternion.LookRotation(dir.normalized);
            }

            _spawnedBus = VehicleRuntimeFactory.SpawnOnWaypointPath(busPrefab, spawnPos, spawnRot, new VehicleRuntimeFactory.SpawnSettings
            {
                Path = busOvertakePath,
                Speed = busSpeed,
                DestroyAtEnd = false,
                PreserveSpawnPosition = true,
                Name = "Scenario1_Bus_Spawned"
            });
            _spawnedBusFollower = _spawnedBus != null ? _spawnedBus.GetComponent<WaypointFollower>() : null;
            if (_spawnedBus == null)
            {
                Debug.LogError("[Scenario1] Bus prefab instantiated as null. Check Assets/BusModel/Prefabs/BogdanA092.prefab.");
                return;
            }

            Debug.Log($"[Scenario1] Spawned {_spawnedBus.name} from {busPrefab.name} at {spawnPos}");
            BindBusAudio();
        }

        /// <summary>
        /// Trigger 2: Starts Stage 3 Right Turn Overtaking Car
        /// </summary>
        public void TriggerRightTurnCar()
        {
            if (_rightTurnTriggered) return;

            AutoAssignReferences();

            if (ScenarioManager.Instance != null && ScenarioManager.Instance.CurrentCondition == ExperimentCondition.Baseline)
            {
                Debug.Log("[Scenario1] Baseline condition: Skipping right-turn car overtake.");
                return;
            }

            if (rightTurnCarPath == null || overtakingCarPrefab == null)
            {
                Debug.LogWarning("[Scenario1] Missing right turn car path or prefab.");
                return;
            }

            ClampScenarioSpeeds();

            // The route trigger is deliberately after the bus bay.  Completing this stage
            // here is a safe fallback if the cyclist cleared the bus between Update frames.
            CompleteBusStageIfCleared(true);
            if (ScenarioManager.Instance != null && ScenarioManager.Instance.IsScenarioActive)
            {
                Debug.LogWarning("[Scenario1] Right-turn event ignored because Route 1 bus stage is still active.");
                return;
            }

            _rightTurnTriggered = true;
            Debug.Log("[Scenario1] Stage 3: Right Turn Car Overtake Triggered!");

            if (EventMarkerLogger.Instance != null) EventMarkerLogger.Instance.LogEvent("RIGHT_TURN_START");
            if (ScenarioManager.Instance != null) ScenarioManager.Instance.StartScenario("Route1_RightTurn");

            Vector3 spawnPos = rightTurnCarPath.GetWaypoint(0);
            Quaternion spawnRot = Quaternion.identity;
            if (rightTurnCarPath.WaypointCount > 1)
            {
                Vector3 dir = (rightTurnCarPath.GetWaypoint(1) - spawnPos).normalized;
                dir.y = 0;
                if (dir != Vector3.zero) spawnRot = Quaternion.LookRotation(dir);
            }

            _spawnedOvertakingCar = VehicleRuntimeFactory.SpawnOnWaypointPath(overtakingCarPrefab, spawnPos, spawnRot, new VehicleRuntimeFactory.SpawnSettings
            {
                Path = rightTurnCarPath,
                Speed = overtakingCarSpeed,
                DestroyAtEnd = true,
                PreserveSpawnPosition = true,
                Name = "Scenario1_RightTurn_OvertakingCar"
            });

            bool ambientOn = sceneRefs == null || sceneRefs.cityTraffic == null || sceneRefs.cityTraffic.IsTrafficEnabled;
            if (!ambientOn) return;

            if (intersectionTraffic == null && sceneRefs != null)
            {
                intersectionTraffic = sceneRefs.intersectionTraffic;
            }
            if (intersectionTraffic == null)
            {
                var host = new GameObject("Intersection_Traffic_System");
                intersectionTraffic = host.AddComponent<IntersectionTrafficFlowManager>();
                if (sceneRefs != null) sceneRefs.intersectionTraffic = intersectionTraffic;
            }
            intersectionTraffic.AutoFindPrefabsAndPaths();
            intersectionTraffic.StartTrafficFlow();
        }

        private void Update()
        {
            // Monitor bus pass
            if (_busOvertakeTriggered && !_busPassedPlayer && _spawnedBus != null && playerTransform != null)
            {
                Vector3 toBus = _spawnedBus.transform.position - playerTransform.position;
                if (Vector3.Dot(toBus, playerTransform.forward) > busPassedAheadDistance)
                {
                    _busPassedPlayer = true;
                    Debug.Log("[Scenario1] Bus overtook cyclist.");
                    if (EventMarkerLogger.Instance != null) EventMarkerLogger.Instance.LogEvent("BUS_OVERTAKE_COMPLETE");
                }
            }

            CompleteBusStageIfCleared(false);

            // Monitor right turn car pass
            if (_rightTurnTriggered && !_carPassedPlayer && _spawnedOvertakingCar != null && playerTransform != null)
            {
                Vector3 toCar = _spawnedOvertakingCar.transform.position - playerTransform.position;
                if (Vector3.Dot(toCar, playerTransform.forward) > carPassedAheadDistance)
                {
                    _carPassedPlayer = true;
                    Debug.Log("[Scenario1] Overtaking car executed right turn. Logging RIGHT_TURN_END.");
                    if (EventMarkerLogger.Instance != null) EventMarkerLogger.Instance.LogEvent("RIGHT_TURN_END");

                    if (ScenarioManager.Instance != null) ScenarioManager.Instance.EndScenario("Route1_RightTurn");
                }
            }
        }

        public void ResetScenario()
        {
            _busOvertakeTriggered = false;
            _rightTurnTriggered = false;
            _busPassedPlayer = false;
            _busClearedByCyclist = false;
            _carPassedPlayer = false;

            if (_spawnedBus != null) Destroy(_spawnedBus);
            if (_spawnedOvertakingCar != null) Destroy(_spawnedOvertakingCar);
            _spawnedBus = null;
            _spawnedBusFollower = null;
            _spawnedOvertakingCar = null;
        }

        private void OnDestroy()
        {
            ResetScenario();
        }

        private void BindBusAudio()
        {
            var busAudio = _spawnedBus.GetComponent<ScenarioBusAudio>();
            if (busAudio == null) busAudio = _spawnedBus.AddComponent<ScenarioBusAudio>();
            busAudio.Bind(_spawnedBusFollower, busEngineLoop, busBrake, busIdleStop, busDeparture);
        }

        private void StartStationAmbient()
        {
            Transform trigger = sceneRefs != null ? sceneRefs.busStopTrigger : null;
            if (trigger == null || busStationAmbient == null) return;

            var source = trigger.GetComponent<AudioSource>();
            if (source == null) source = trigger.gameObject.AddComponent<AudioSource>();
            source.clip = busStationAmbient;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.volume = stationAmbientVolume;
            source.minDistance = stationAmbientMinDistance;
            source.maxDistance = stationAmbientMaxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            if (!source.isPlaying) source.Play();
        }

        private void CompleteBusStageIfCleared(bool allowRouteTriggerFallback)
        {
            if (!_busOvertakeTriggered || _busClearedByCyclist || _spawnedBus == null || playerTransform == null) return;

            bool busIsParked = _spawnedBusFollower != null && _spawnedBusFollower.IsAtEnd;
            Vector3 cyclistFromBus = playerTransform.position - _spawnedBus.transform.position;
            bool cyclistIsAhead = Vector3.Dot(cyclistFromBus, playerTransform.forward) > cyclistClearedBusDistance;

            // Normal path: wait for the bus to reach its stop and for the cyclist to pass it.
            // The route trigger fallback handles a legitimate pass that occurs in the same
            // frame as the next event trigger, without ever ending at the instant of overtaking.
            if (!cyclistIsAhead || (!busIsParked && !allowRouteTriggerFallback)) return;

            _busClearedByCyclist = true;
            Debug.Log("[Scenario1] Cyclist cleared parked bus. Logging BUS_EVENT_END.");
            if (EventMarkerLogger.Instance != null) EventMarkerLogger.Instance.LogEvent("BUS_EVENT_END");
            if (ScenarioManager.Instance != null) ScenarioManager.Instance.EndScenario("Route1_BusOvertake");
        }
    }
}
