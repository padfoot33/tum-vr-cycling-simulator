using CyclingExperiment.AI;
using UnityEngine;

namespace CyclingExperiment.Logging
{
    /// <summary>
    /// Registers a spawned or pooled vehicle with <see cref="VehicleInteractionTracker"/>.
    /// Added at spawn time — no scene search.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrackedTrafficVehicle : MonoBehaviour
    {
        WaypointFollower _follower;
        SmartVehicleAI _smart;
        GraphVehicleAI _graph;

        private void Awake()
        {
            _follower = GetComponent<WaypointFollower>();
            _smart = GetComponent<SmartVehicleAI>();
            _graph = GetComponent<GraphVehicleAI>();
        }

        private void OnEnable()
        {
            if (_follower == null) _follower = GetComponent<WaypointFollower>();
            if (_smart == null) _smart = GetComponent<SmartVehicleAI>();
            if (_graph == null) _graph = GetComponent<GraphVehicleAI>();
            VehicleInteractionTracker.Register(this);
        }

        private void OnDisable()
        {
            VehicleInteractionTracker.Unregister(this);
        }

        public float SpeedKph
        {
            get
            {
                if (_follower != null && _follower.enabled)
                    return _follower.IsAtEnd ? 0f : _follower.Speed * 3.6f;
                if (_smart != null && _smart.enabled)
                    return _smart.CurrentSpeed * 3.6f;
                if (_graph != null && _graph.enabled)
                    return _graph.CurrentSpeed * 3.6f;
                return 0f;
            }
        }
    }
}
