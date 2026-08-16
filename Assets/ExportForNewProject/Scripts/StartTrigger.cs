using UnityEngine;

public class StartTrigger : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Rigidbody bikeRigidbody;
    public RunLogger runLogger;

    [Header("Marker")]
    public string startMarkerName = "START";

    private bool fired = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        if (bikeRigidbody == null || runLogger == null) return;
        if (other.attachedRigidbody != bikeRigidbody) return;

        fired = true;

        Debug.Log("[StartTrigger] START reached!");

        runLogger.StartLogging();
        runLogger.SetEvent("NONE");
        runLogger.LogMarker(startMarkerName);
    }
}