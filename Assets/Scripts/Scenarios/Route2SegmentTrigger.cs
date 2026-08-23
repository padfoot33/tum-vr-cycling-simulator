using CyclingExperiment.Logging;
using UnityEngine;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Sets the run-log segment when the rider enters a Route 2 scenario trigger.
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
                _trigger.OnPlayerEntered.AddListener(OnEntered);
        }

        private void OnDisable()
        {
            if (_trigger != null)
                _trigger.OnPlayerEntered.RemoveListener(OnEntered);
        }

        private void OnEntered()
        {
            var logger = ExperimentRunLogger.Instance;
            if (logger == null)
                return;

            logger.SetSegment(segmentId, taskContext);
            Debug.Log($"[Route2SegmentTrigger] Segment {segmentId} ({taskContext})");
        }
    }
}
