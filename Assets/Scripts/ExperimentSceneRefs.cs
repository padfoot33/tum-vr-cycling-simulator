using UnityEngine;
using UnityEngine.EventSystems;
using CyclingExperiment.AI;
using CyclingExperiment.Camera;
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
        public BikeURP.BicyclePhysicsController bicyclePhysics;

        [Header("Systems")]
        public Scenario1_CombinedController route1;
        public GlobalCityTrafficManager cityTraffic;
        public IntersectionTrafficFlowManager intersectionTraffic;
        public HUDController hud;
        public SmoothFollowBicycleCamera followCamera;
        public EventSystem eventSystem;

        [Header("Route 1")]
        public Transform busStopTrigger;
        public Transform route1CyclistSpawn;
        public GameObject cityTrafficPaths;
        public GameObject campusTrafficPaths;
        public TrafficDestinationSet trafficDestinations;
        public RoadNetwork campusRoadNetwork;

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

        private void BindMissingOnce()
        {
            if (bicycle == null) bicycle = GameObject.Find("bicyle_animated_human");
            if (bicycle != null)
            {
                if (bicycleTransform == null) bicycleTransform = bicycle.transform;
                if (bicyclePhysics == null) bicyclePhysics = bicycle.GetComponent<BikeURP.BicyclePhysicsController>();
            }

            if (route1 == null) route1 = Object.FindObjectOfType<Scenario1_CombinedController>();
            if (cityTraffic == null) cityTraffic = Object.FindObjectOfType<GlobalCityTrafficManager>();
            if (intersectionTraffic == null) intersectionTraffic = Object.FindObjectOfType<IntersectionTrafficFlowManager>();
            if (hud == null) hud = Object.FindObjectOfType<HUDController>();
            if (followCamera == null) followCamera = Object.FindObjectOfType<SmoothFollowBicycleCamera>();
            if (eventSystem == null) eventSystem = Object.FindObjectOfType<EventSystem>();
            if (busStopTrigger == null)
            {
                var trigger = GameObject.Find("Trigger_Scenario1_BusStop");
                if (trigger != null) busStopTrigger = trigger.transform;
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
        }

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
