using System;
using UnityEngine;

namespace CyclingExperiment.Scenarios
{
    public enum ExperimentCondition
    {
        Baseline,
        Stress
    }

    /// <summary>
    /// Singleton managing experiment state.
    /// </summary>
    public class ScenarioManager : MonoBehaviour
    {
        public static ScenarioManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField, Tooltip("Current experiment condition")]
        private ExperimentCondition currentCondition = ExperimentCondition.Stress;

        [SerializeField, Tooltip("Reference to EventMarkerLogger")]
        private EventMarkerLogger eventMarkerLogger;

        public ExperimentCondition CurrentCondition => currentCondition;

        public bool IsScenarioActive { get; private set; }
        public string ActiveScenarioName { get; private set; }

        public event Action<string> OnScenarioStarted;
        public event Action<string> OnScenarioEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Starts a scenario and logs the event.
        /// </summary>
        public void StartScenario(string name)
        {
            if (IsScenarioActive)
            {
                Debug.LogWarning($"[ScenarioManager] Cannot start {name}, scenario {ActiveScenarioName} is already active.");
                return;
            }

            IsScenarioActive = true;
            ActiveScenarioName = name;
            
            if (eventMarkerLogger != null)
            {
                eventMarkerLogger.LogEvent($"SCENARIO_START_{name}");
            }
            else if (EventMarkerLogger.Instance != null)
            {
                EventMarkerLogger.Instance.LogEvent($"SCENARIO_START_{name}");
            }

            OnScenarioStarted?.Invoke(name);
        }

        /// <summary>
        /// Ends a scenario and logs the event.
        /// </summary>
        public void EndScenario(string name)
        {
            if (!IsScenarioActive || ActiveScenarioName != name)
            {
                Debug.LogWarning($"[ScenarioManager] Cannot end {name}, it is not the active scenario.");
                return;
            }

            if (eventMarkerLogger != null)
            {
                eventMarkerLogger.LogEvent($"SCENARIO_END_{name}");
            }
            else if (EventMarkerLogger.Instance != null)
            {
                EventMarkerLogger.Instance.LogEvent($"SCENARIO_END_{name}");
            }

            IsScenarioActive = false;
            ActiveScenarioName = null;

            OnScenarioEnded?.Invoke(name);
        }
    }
}
