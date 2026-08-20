using UnityEngine;
using UnityEngine.EventSystems;
using CyclingExperiment.AI;
using CyclingExperiment.Camera;
using CyclingExperiment.Logging;
using CyclingExperiment.Scenarios;
using CyclingExperiment.UI;

namespace CyclingExperiment
{
    /// <summary>
    /// Scene-wired references. Other runtime scripts should use this instead of Find/GetComponent.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class ExperimentSceneRefs : MonoBehaviour
    {
        public static ExperimentSceneRefs Instance { get; private set; }

        [Header("Cyclist")]
        public GameObject bicycle;
        public Transform bicycleTransform;
        [Tooltip("Optional leftover from the keyboard bike. Prefer cyclistMotion.")]
        public BikeURP.BicyclePhysicsController bicyclePhysics;
        [SerializeField, Tooltip("SimBike or any ICyclistMotion adapter")]
        private MonoBehaviour cyclistMotion;

        public ICyclistMotion Cyclist => cyclistMotion as ICyclistMotion;

        [Header("Systems")]
        public Scenario1_CombinedController route1;
        public GlobalCityTrafficManager cityTraffic;
        public IntersectionTrafficFlowManager intersectionTraffic;
        public HUDController hud;
        public SmoothFollowBicycleCamera followCamera;
        public EventSystem eventSystem;

        [Header("Route 1")]
        public Transform busStopTrigger;
        public Transform rightTurnTrigger;
        public Transform rightTurnSign;
        public Transform route1CyclistSpawn;
        public GameObject cityTrafficPaths;
        public GameObject campusTrafficPaths;
        public TrafficDestinationSet trafficDestinations;
        public RoadNetwork campusRoadNetwork;
        public ReferencePathTracker route1PathTracker;

        [Header("Logging")]
        public ExperimentRunLogger runLogger;

        [Header("Play area")]
        public PlayAreaBounds route1PlayArea;
        public PlayAreaBounds route2PlayArea;
        public PlayAreaConstraint playAreaConstraint;

        [Header("Route 2")]
        public Transform route2CyclistSpawn;

        [Header("Locked participant run (set before Build)")]
        [Tooltip("When on, Play/Build starts only this route, hides M/T/1/2, and applies traffic. Leave off for editor testing.")]
        public bool lockParticipantRun;
        [Tooltip("1 = Route 1 (bus + right-turn), 2 = Route 2 (construction).")]
        public int lockedRouteIndex = 1;
        public bool lockedTrafficEnabled = true;

        public static ExperimentSceneRefs EnsureExists()
        {
            if (Instance != null) return Instance;

            var existing = UnityEngine.Object.FindObjectOfType<ExperimentSceneRefs>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("Experiment_Scene_Refs");
            return go.AddComponent<ExperimentSceneRefs>();
        }

        private void Awake()
        {
            if (lockParticipantRun)
                ExperimentBuildSession.Apply(lockedRouteIndex, lockedTrafficEnabled, true);

            Instance = this;
            BindMissingOnce();
        }

        public void SetLockedRun(bool lockRun, int routeIndex, bool trafficEnabled)
        {
            lockParticipantRun = lockRun;
            lockedRouteIndex = routeIndex < 2 ? 1 : 2;
            lockedTrafficEnabled = trafficEnabled;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetCyclist(GameObject bike, ICyclistMotion motion)
        {
            bicycle = bike;
            bicycleTransform = bike != null ? bike.transform : null;
            cyclistMotion = motion as MonoBehaviour;
            if (bike != null)
                bicyclePhysics = bike.GetComponent<BikeURP.BicyclePhysicsController>();
        }

        private void BindMissingOnce()
        {
            if (bicycle == null) bicycle = FindExperimentBicycle();
            if (bicycle != null && bicycle.GetComponent<SBPScripts.Simulator.BicycleSimulatorController>() == null)
            {
#if UNITY_EDITOR
                SpawnSimBikeInEditor();
#endif
            }
            if (bicycle == null)
            {
#if UNITY_EDITOR
                SpawnSimBikeInEditor();
#endif
            }
            if (bicycle != null)
            {
                if (bicycleTransform == null) bicycleTransform = bicycle.transform;
                if (bicyclePhysics == null) bicyclePhysics = bicycle.GetComponent<BikeURP.BicyclePhysicsController>();
                if (cyclistMotion == null)
                {
                    var simMotion = bicycle.GetComponent<SimBikeCyclistMotion>();
                    if (simMotion == null && bicycle.GetComponent<SBPScripts.Simulator.BicycleSimulatorController>() != null)
                        simMotion = bicycle.AddComponent<SimBikeCyclistMotion>();
                    cyclistMotion = simMotion;
                }

                if (bicycle.GetComponent<SBPScripts.Simulator.BicycleSimulatorController>() != null)
                {
                    var listeners = bicycle.GetComponentsInChildren<AudioListener>(true);
                    foreach (var listener in listeners)
                        listener.enabled = listener.gameObject.name == "Main Camera";
                }
            }

            if (route1 == null) route1 = Object.FindObjectOfType<Scenario1_CombinedController>();
            if (cityTraffic == null) cityTraffic = Object.FindObjectOfType<GlobalCityTrafficManager>();
            if (intersectionTraffic == null) intersectionTraffic = Object.FindObjectOfType<IntersectionTrafficFlowManager>();
            if (hud == null) hud = Object.FindObjectOfType<HUDController>();
            if (hud != null && cyclistMotion != null)
                hud.SetBicycleController(cyclistMotion);
            if (followCamera == null) followCamera = Object.FindObjectOfType<SmoothFollowBicycleCamera>();
            if (followCamera != null && bicycle != null &&
                bicycle.GetComponent<SBPScripts.Simulator.BicycleSimulatorController>() != null)
            {
                followCamera.gameObject.SetActive(false);
                followCamera = null;
            }
            if (eventSystem == null) eventSystem = Object.FindObjectOfType<EventSystem>();
            if (busStopTrigger == null)
            {
                var trigger = GameObject.Find("Trigger_Scenario1_BusStop");
                if (trigger != null) busStopTrigger = trigger.transform;
            }

            if (rightTurnTrigger == null)
            {
                var trigger = GameObject.Find("Trigger_Scenario1_RightTurn");
                if (trigger != null) rightTurnTrigger = trigger.transform;
            }

            if (route1CyclistSpawn == null)
            {
                var spawn = GameObject.Find("Cyclist_Spawn_Route1");
                if (spawn == null)
                {
                    spawn = new GameObject("Cyclist_Spawn_Route1");
                    spawn.AddComponent<CyclistSpawnMarker>();
                    spawn.transform.position = new Vector3(436.1f, 0.2f, -80f);
                    if (route1 != null) spawn.transform.SetParent(route1.transform);
                }
                route1CyclistSpawn = spawn.transform;
            }

            if (route2CyclistSpawn == null)
            {
                var spawn = GameObject.Find("Cyclist_Spawn_Route2");
                if (spawn == null)
                {
                    spawn = new GameObject("Cyclist_Spawn_Route2");
                    spawn.AddComponent<CyclistSpawnMarker>();
                    spawn.transform.position = Scenario3_ConstructionNarrowing.ApproachPosition;
                    spawn.transform.rotation = Quaternion.Euler(0f, Scenario3_ConstructionNarrowing.ApproachHeading, 0f);
                    var scenario2 = GameObject.Find("Scenario_2");
                    if (scenario2 != null) spawn.transform.SetParent(scenario2.transform);
                }
                route2CyclistSpawn = spawn.transform;
            }

            if (cityTrafficPaths == null) cityTrafficPaths = GameObject.Find("City_Traffic_Paths");
            if (cityTrafficPaths != null) cityTrafficPaths.SetActive(false);

            if (campusTrafficPaths == null) campusTrafficPaths = GameObject.Find(WaypointPath.CampusRootName);

            if (trafficDestinations == null)
            {
                var destObj = GameObject.Find("Traffic_Destinations");
                if (destObj != null) trafficDestinations = destObj.GetComponent<TrafficDestinationSet>();
            }

            if (campusRoadNetwork == null)
            {
                var networkObj = GameObject.Find(RoadNetwork.RootName);
                if (networkObj != null) campusRoadNetwork = networkObj.GetComponent<RoadNetwork>();
            }

            Scenario3_ConstructionNarrowing.DisableCampusRoadNavMesh();

            if (route1PlayArea == null)
                route1PlayArea = PlayAreaBounds.FindOrCreateRoute1(this);
            if (route2PlayArea == null)
                route2PlayArea = PlayAreaBounds.FindOrCreateRoute2(this);

            if (playAreaConstraint == null)
                playAreaConstraint = GetComponent<PlayAreaConstraint>();
            if (playAreaConstraint == null)
                playAreaConstraint = gameObject.AddComponent<PlayAreaConstraint>();
            playAreaConstraint.Bind(this);

            EnsureRightTurnSign();
            EnsureRoute1ReferencePath();
            EnsureRunLogger();
        }

        public void EnsureRunLogger()
        {
            if (runLogger == null)
                runLogger = GetComponent<ExperimentRunLogger>();
            if (runLogger == null)
                runLogger = gameObject.AddComponent<ExperimentRunLogger>();

            if (GetComponent<VehicleInteractionTracker>() == null)
                gameObject.AddComponent<VehicleInteractionTracker>();

            runLogger.BindRefs();
        }

        public void EnsureRightTurnSign()
        {
            if (rightTurnTrigger == null) return;
            if (rightTurnSign == null)
                rightTurnSign = Route1RightTurnSign.Ensure(rightTurnTrigger);
        }

        public void EnsureRoute1ReferencePath()
        {
            if (route1PathTracker != null) return;

            var existing = GameObject.Find("Route1_ReferencePath");
            GameObject root = existing;
            if (root == null)
            {
                root = new GameObject("Route1_ReferencePath");
                if (route1 != null) root.transform.SetParent(route1.transform, true);
            }

            route1PathTracker = root.GetComponent<ReferencePathTracker>() ?? root.AddComponent<ReferencePathTracker>();
            route1PathTracker.bikeTransform = bicycleTransform;
            route1PathTracker.autoCollectChildren = true;

            if (root.transform.childCount < 2)
            {
                ClearNamedPathChildren(root.transform);
                Vector3 spawn = route1CyclistSpawn != null ? route1CyclistSpawn.position : new Vector3(403.72f, 0.26f, -11.11f);
                Vector3 bus = busStopTrigger != null ? busStopTrigger.position : spawn;
                Vector3 bay = bus;
                var busPath = GameObject.Find("Bus_Overtake_Path");
                if (busPath != null)
                {
                    var path = busPath.GetComponent<WaypointPath>();
                    if (path != null && path.WaypointCount > 0)
                        bay = path.GetWaypoint(path.WaypointCount - 1);
                }

                Vector3 turn = rightTurnTrigger != null ? rightTurnTrigger.position : new Vector3(790f, 0.2f, -177.2f);
                CreatePathPoint(root.transform, "P_00", spawn);
                CreatePathPoint(root.transform, "P_01", bus);
                CreatePathPoint(root.transform, "P_02", bay);
                CreatePathPoint(root.transform, "P_03", turn);
            }
        }

        private static void ClearNamedPathChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null || !child.name.StartsWith("P_")) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private static void CreatePathPoint(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
        }

        private static GameObject FindExperimentBicycle()
        {
            var sim = GameObject.Find("SimBike");
            if (sim != null) return sim;
            return GameObject.Find("bicyle_animated_human");
        }

#if UNITY_EDITOR
        private void SpawnSimBikeInEditor()
        {
            const string path = "Assets/BicycleSimulatorModel/Prefabs/SimBike.prefab";
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            if (bicycle != null)
                bicycle.SetActive(false);

            var instance = Object.Instantiate(prefab);
            instance.name = "SimBike";
            instance.tag = "Player";
            bicycle = instance;
            bicycleTransform = instance.transform;
            bicyclePhysics = null;
            cyclistMotion = null;

            if (followCamera != null)
                followCamera.gameObject.SetActive(false);

            var listeners = instance.GetComponentsInChildren<AudioListener>(true);
            foreach (var listener in listeners)
                listener.enabled = listener.gameObject.name == "Main Camera";

            var cyclist = instance.transform.Find("Old Cycle Cyclist");
            var animator = cyclist != null ? cyclist.GetComponent<Animator>() : null;
            if (animator != null) animator.applyRootMotion = false;
        }
#endif

        public void ApplyPlayArea(int scenarioIndex)
        {
            if (route1PlayArea != null)
                route1PlayArea.gameObject.SetActive(scenarioIndex == 1);
            if (route2PlayArea != null)
                route2PlayArea.gameObject.SetActive(scenarioIndex == 2);

            if (playAreaConstraint == null) return;
            if (scenarioIndex == 1)
                playAreaConstraint.SetActiveArea(route1PlayArea);
            else if (scenarioIndex == 2)
                playAreaConstraint.SetActiveArea(route2PlayArea);
            else
                playAreaConstraint.SetActiveArea(null);
        }
    }
}
