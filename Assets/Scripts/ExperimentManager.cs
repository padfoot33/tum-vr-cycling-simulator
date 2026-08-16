using UnityEngine;

public class ExperimentManager : MonoBehaviour
{
    public enum ExperimentState
    {
        Idle,
        Baseline_HIGH,
        Transition_To_LOW,
        LOW_Condition,
        Transition_To_HIGH,
        HIGH_Condition,
        Finished
    }

    [Header("State")]
    public ExperimentState currentState = ExperimentState.Idle;

    private ExperimentState _lastState;

    private void Start()
    {
        _lastState = currentState;
        Debug.Log("[ExperimentManager] Ready");
    }

    private void Update()
    {
        // This prints whenever the state changes (so you can SEE it working)
        if (currentState != _lastState)
        {
            Debug.Log($"[ExperimentManager] STATE: {_lastState} -> {currentState}");
            _lastState = currentState;
        }
    }

    public void StartExperiment()
    {
        currentState = ExperimentState.Baseline_HIGH;
        Debug.Log("[ExperimentManager] Experiment Started (Baseline HIGH)");
    }

    public void EnterLowLOD()
    {
        currentState = ExperimentState.LOW_Condition;
        Debug.Log("[ExperimentManager] Entered LOW LOD");
    }

    public void EnterHighLOD()
    {
        currentState = ExperimentState.HIGH_Condition;
        Debug.Log("[ExperimentManager] Entered HIGH LOD");
    }

    public void MarkEvent(string marker)
    {
        // Later we will write this into CSV too — for now console is enough.
        Debug.Log($"[ExperimentManager] EVENT MARKER: {marker} (State={currentState})");
    }

    public void FinishExperiment()
    {
        currentState = ExperimentState.Finished;
        Debug.Log("[ExperimentManager] Experiment Finished");
    }
}