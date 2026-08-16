using UnityEngine;

public class SimpleEventTrigger : MonoBehaviour
{
    public ClosePassSpawner spawner;
    public Rigidbody bikeRigidbody;
    public RunLogger runLogger;

    public bool triggerOnlyOnce = true;
    public string markerName = "EVENT01_LOW_START";
    private bool fired = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnlyOnce && fired) return;
        if (bikeRigidbody == null || spawner == null) return;
        if (other.attachedRigidbody != bikeRigidbody) return;

        fired = true;

        Debug.Log($"[EVENT] {markerName}");

        if (runLogger != null)
        {
            runLogger.SetEvent(markerName);
            runLogger.MarkEvent(markerName);
        }

        spawner.TriggerClosePass();
    }
}