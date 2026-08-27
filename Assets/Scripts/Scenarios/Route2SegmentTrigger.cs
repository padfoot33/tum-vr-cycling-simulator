using CyclingExperiment.Logging;
using UnityEngine;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Sets the run-log segment and spatial comparison markers
    /// when the rider enters/exits a Route 2 trigger.
    /// These markers are logged in both traffic and no-traffic conditions
    /// so the same physical road sections can be compared.
    /// </summary>
    [RequireComponent(typeof(ScenarioTrigger))]
    public class Route2SegmentTrigger : MonoBehaviour
    {
        [SerializeField] private string segmentId = "C2";
        [SerializeField] private string taskContext = "interaction";

        private ScenarioTrigger _trigger;

        public void Configure(string id, string context)
        {
            if (!string.IsNullOrEmpty(id))
                segmentId = id;

            if (!string.IsNullOrEmpty(context))
                taskContext = context;
        }

        private void OnEnable()
        {
            _trigger = GetComponent<ScenarioTrigger>();

            if (_trigger != null)
            {
                _trigger.OnPlayerEntered.AddListener(OnEntered);
                _trigger.OnPlayerExited.AddListener(OnExited);
            }
        }

        private void OnDisable()
        {
            if (_trigger != null)
            {
                _trigger.OnPlayerEntered.RemoveListener(OnEntered);
                _trigger.OnPlayerExited.RemoveListener(OnExited);
            }
        }

        private void OnEntered()
        {
            var logger = ExperimentRunLogger.Instance;
            if (logger == null)
                return;

            // Spatial segment is identical for traffic and no-traffic runs.
            logger.SetSegment(segmentId, taskContext);

            // Comparable spatial marker.
            logger.MarkEvent($"ROUTE2_{segmentId}_ZONE_START");

            Debug.Log(
                $"[Route2SegmentTrigger] Segment {segmentId} ({taskContext}) START");
        }

        private void OnExited()
        {
            var logger = ExperimentRunLogger.Instance;
            if (logger == null)
                return;

            // Same location marker in both experimental conditions.
            logger.MarkEvent($"ROUTE2_{segmentId}_ZONE_END");

            Debug.Log(
                $"[Route2SegmentTrigger] Segment {segmentId} ({taskContext}) END");
        }
    }
}