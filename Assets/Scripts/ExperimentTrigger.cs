using UnityEngine;

public class ExperimentTrigger : MonoBehaviour
{
    public enum TriggerType
    {
        StartExperiment,
        EnterLowLOD,
        EnterHighLOD,
        Event1Start,
        Event1End,
        Event2Start,
        Event2End,
        FinishExperiment
    }

    [Header("Setup")]
    public TriggerType triggerType;
    public ExperimentManager experimentManager;   // drag your ExperimentManager here
    public string bikeTag = "Player";             // tag used by the bike root
    public bool triggerOnlyOnce = true;

    private bool _hasTriggered = false;

    private void Reset()
    {
        // Make trigger collider default when you add this component
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnlyOnce && _hasTriggered) return;
        if (!other.CompareTag(bikeTag)) return;
        if (experimentManager == null)
        {
            Debug.LogError($"[{name}] ExperimentManager not assigned!");
            return;
        }

        _hasTriggered = true;

        switch (triggerType)
        {
            case TriggerType.StartExperiment:
                experimentManager.StartExperiment();
                break;

            case TriggerType.EnterLowLOD:
                experimentManager.EnterLowLOD();
                break;

            case TriggerType.EnterHighLOD:
                experimentManager.EnterHighLOD();
                break;

            case TriggerType.Event1Start:
                experimentManager.MarkEvent("EVENT1_START");
                break;

            case TriggerType.Event1End:
                experimentManager.MarkEvent("EVENT1_END");
                break;

            case TriggerType.Event2Start:
                experimentManager.MarkEvent("EVENT2_START");
                break;

            case TriggerType.Event2End:
                experimentManager.MarkEvent("EVENT2_END");
                break;

            case TriggerType.FinishExperiment:
                experimentManager.FinishExperiment();
                break;
        }

        Debug.Log($"[{name}] Trigger fired: {triggerType}");
    }
}