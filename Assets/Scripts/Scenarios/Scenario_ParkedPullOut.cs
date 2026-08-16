using UnityEngine;
using CyclingExperiment.AI;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Implements the optional Parked Vehicle Pull-Out scenario.
    /// A parked vehicle starts indicating and pulls out into the traffic/bike lane as the cyclist approaches.
    /// </summary>
    public class Scenario_ParkedPullOut : MonoBehaviour
    {
        [Header("Vehicle Setup")]
        [SerializeField, Tooltip("The parked vehicle GameObject or prefab")]
        private GameObject parkedVehicle;

        [SerializeField, Tooltip("Path the vehicle takes when pulling out")]
        private WaypointPath pullOutPath;

        [SerializeField, Tooltip("Pull-out movement speed in m/s")]
        private float pullOutSpeed = 5f;

        [Header("Player Reference")]
        [SerializeField] private Transform playerTransform;

        private bool _hasPulledOut = false;

        private void Start()
        {
            if (playerTransform == null)
            {
                var bike = GameObject.Find("bicyle_animated_human");
                if (bike != null) playerTransform = bike.transform;
            }
        }

        public void ActivateScenario()
        {
            if (_hasPulledOut) return;

            if (ScenarioManager.Instance != null && ScenarioManager.Instance.CurrentCondition == ExperimentCondition.Baseline)
            {
                Debug.Log("[Scenario_ParkedPullOut] Skipping pull out (Baseline condition).");
                return;
            }

            if (parkedVehicle == null || pullOutPath == null)
            {
                Debug.LogWarning("[Scenario_ParkedPullOut] Missing parked vehicle or pull-out path.");
                return;
            }

            Debug.Log("[Scenario_ParkedPullOut] Parked vehicle pulling out!");
            _hasPulledOut = true;

            if (EventMarkerLogger.Instance != null)
            {
                EventMarkerLogger.Instance.LogEvent("PARKED_PULLOUT_START");
            }

            var taxiCtrl = parkedVehicle.GetComponent("TaxiController") as MonoBehaviour;
            if (taxiCtrl != null) taxiCtrl.enabled = false;
            var carCtrl = parkedVehicle.GetComponent("CarController") as MonoBehaviour;
            if (carCtrl != null) carCtrl.enabled = false;

            var rb = parkedVehicle.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            var follower = parkedVehicle.GetComponent<WaypointFollower>() ?? parkedVehicle.AddComponent<WaypointFollower>();
            follower.Path = pullOutPath;
            follower.Speed = pullOutSpeed;
            follower.DestroyAtEnd = false;
        }
    }
}
