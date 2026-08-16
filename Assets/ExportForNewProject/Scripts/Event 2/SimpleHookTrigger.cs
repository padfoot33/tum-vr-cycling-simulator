using UnityEngine;

public class SimpleHookTrigger : MonoBehaviour
{
    [Header("References")]
    public HookCarSpawner spawner;
    public Rigidbody bikeRigidbody;
    public RunLogger runLogger;

    [Header("Settings")]
    public bool triggerOnlyOnce = true;
    public string markerName = "EVENT02_HIGH_START";

    private bool hasTriggered = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        Rigidbody otherRb = other.attachedRigidbody;
        if (otherRb == null || otherRb != bikeRigidbody)
            return;

        hasTriggered = true;

        if (runLogger != null)
        {
            runLogger.SetEvent(markerName);
            runLogger.MarkEvent(markerName);
            Debug.Log("[SimpleHookTrigger] Marker: " + markerName);
        }
        else
        {
            Debug.Log("[SimpleHookTrigger] Triggered, but RunLogger not assigned. Marker: " + markerName);
        }

        if (spawner != null)
        {
            spawner.SpawnHookCar();
        }
    }
}