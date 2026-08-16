using UnityEngine;

public class LODMarkerTrigger : MonoBehaviour
{
    public string markerName = "LOD_LOW_ENTER";
    public Rigidbody bikeRigidbody;
    public RunLogger runLogger;

    public bool triggerOnlyOnce = true;
    private bool fired = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnlyOnce && fired) return;
        if (bikeRigidbody == null) return;

        if (other.attachedRigidbody != bikeRigidbody) return;

        fired = true;

        Debug.Log($"[LODMarkerTrigger] {markerName}");

        // Update CSV state
        if (runLogger != null)
        {
            if (markerName.Contains("ENTER"))
                runLogger.SetLOD("LOW");
            else if (markerName.Contains("EXIT"))
                runLogger.SetLOD("HIGH");

            runLogger.MarkEvent(markerName); // also log marker in event column (one row)
        }
    }
}