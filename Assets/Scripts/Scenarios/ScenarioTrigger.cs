using UnityEngine;
using UnityEngine.Events;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Trigger zone that fires when the player bicycle enters it.
    /// Robustly checks tags on root, attached rigidbody, child colliders, or name.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ScenarioTrigger : MonoBehaviour
    {
        [Header("Trigger Configuration")]
        [SerializeField, Tooltip("Identifier for this scenario")]
        private string scenarioId = "Scenario1";

        [SerializeField, Tooltip("Tag required on the triggering object")]
        private string triggerTag = "Player";

        [SerializeField, Tooltip("If true, this trigger will only fire once")]
        private bool oneShot = true;

        [SerializeField, Tooltip("Color for the gizmo drawing")]
        private Color gizmoColor = Color.green;

        [Header("Events")]
        public UnityEvent OnPlayerEntered = new UnityEvent();
        public UnityEvent OnPlayerExited = new UnityEvent();

        private bool _hasTriggered = false;
        private Collider _collider;

        public void ResetTrigger()
        {
            _hasTriggered = false;
        }

        public void Configure(string id, Color color)
        {
            if (!string.IsNullOrEmpty(id))
                scenarioId = id;
            gizmoColor = color;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
        }

        private bool IsPlayer(Collider other)
        {
            if (other.CompareTag(triggerTag)) return true;
            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(triggerTag)) return true;
            if (other.transform.root != null && other.transform.root.CompareTag(triggerTag)) return true;

            var refs = ExperimentSceneRefs.Instance;
            if (refs != null && refs.bicycleTransform != null)
            {
                Transform bike = refs.bicycleTransform;
                if (other.transform == bike || other.transform.IsChildOf(bike))
                    return true;
            }

            if (other.name.Contains("bicyle") || other.name.Contains("SimBike") ||
                (other.transform.root != null && (other.transform.root.name.Contains("bicyle") || other.transform.root.name.Contains("SimBike"))))
                return true;
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ExperimentBuildSession.IsTestRun) return;
            if (oneShot && _hasTriggered) return;

            if (IsPlayer(other))
            {
                _hasTriggered = true;
                Debug.Log($"[ScenarioTrigger] Player entered trigger: {gameObject.name} (Scenario: {scenarioId})");

                if (EventMarkerLogger.Instance != null)
                {
                    EventMarkerLogger.Instance.LogEvent($"{scenarioId}_TRIGGER_ENTER");
                }

                OnPlayerEntered?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (ExperimentBuildSession.IsTestRun) return;

            if (IsPlayer(other))
            {
                Debug.Log($"[ScenarioTrigger] Player exited trigger: {gameObject.name} (Scenario: {scenarioId})");

                if (EventMarkerLogger.Instance != null)
                {
                    EventMarkerLogger.Instance.LogEvent($"{scenarioId}_TRIGGER_EXIT");
                }

                OnPlayerExited?.Invoke();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                if (col is BoxCollider box)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                    Gizmos.matrix = Matrix4x4.identity;
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
                }
                else
                {
                    Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
                }
            }
        }
    }
}
