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
        public TrafficDestinationSet trafficDestinations;

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
            Instance = this;
            BindMissingOnce();
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

            if (cityTrafficPaths == null) cityTrafficPaths = GameObject.Find("City_Traffic_Paths");
            if (cityTrafficPaths != null) cityTrafficPaths.SetActive(false);

            if (trafficDestinations == null)
            {
                var destObj = GameObject.Find("Traffic_Destinations");
                if (destObj != null) trafficDestinations = destObj.GetComponent<TrafficDestinationSet>();
            }
        }
    }
}
