using CyclingExperiment.Logging;
using UnityEngine;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Logs spatial comparison zones for Route 1 in both traffic
    /// and no-traffic conditions.
    /// </summary>
    [RequireComponent(typeof(ScenarioTrigger))]
    public class Route1SegmentTrigger : MonoBehaviour
    {
        [SerializeField] private string segmentId = "C2";
        [SerializeField] private string taskContext = "interaction";
        [SerializeField] private string zoneMarker = "BUS_ZONE";

        [SerializeField] private string exitSegmentId = "";
        [SerializeField] private string exitTaskContext = "";

        private ScenarioTrigger _trigger;

        public void Configure(
            string segment,
            string context,
            string marker,
            string nextSegment = "",
            string nextContext = "")
        {
            segmentId = segment;
            taskContext = context;
            zoneMarker = marker;
            exitSegmentId = nextSegment;
            exitTaskContext = nextContext;
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

            logger.SetSegment(segmentId, taskContext);
            logger.MarkEvent(zoneMarker + "_START");

            Debug.Log(
                $"[Route1SegmentTrigger] {zoneMarker}_START → {segmentId} ({taskContext})");
        }

        private void OnExited()
        {
            var logger = ExperimentRunLogger.Instance;
            if (logger == null)
                return;

            logger.MarkEvent(zoneMarker + "_END");

            if (!string.IsNullOrEmpty(exitSegmentId))
            {
                logger.SetSegment(exitSegmentId, exitTaskContext);

                Debug.Log(
                    $"[Route1SegmentTrigger] {zoneMarker}_END → {exitSegmentId} ({exitTaskContext})");
            }
            else
            {
                Debug.Log($"[Route1SegmentTrigger] {zoneMarker}_END");
            }
        }
    }
}