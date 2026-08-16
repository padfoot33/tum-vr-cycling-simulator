using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Rigidbody bikeRigidbody;
    public RunLogger runLogger;

    [Header("Marker")]
    public string finishMarkerName = "FINISH";

    private bool fired = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        if (bikeRigidbody == null) return;
        if (other.attachedRigidbody != bikeRigidbody) return;

        fired = true;

        Debug.Log("[FinishTrigger] FINISH reached!");

        if (runLogger != null)
        {
            runLogger.SetEvent("FINISH");
            runLogger.MarkEventAndStop(finishMarkerName);
        }
        else
        {
            Debug.LogWarning("[FinishTrigger] RunLogger not assigned, can't stop logging.");
        }
    }
}