using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BikeSensorToSumoTrigger : MonoBehaviour
{
    [Header("Trigger filter")]
    public string triggeringTag = "Ego";

    [Header("Debug")]
    public string stationNumber = "5";

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject go = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;

        if (!go.CompareTag(triggeringTag))
            return;

        Debug.Log($"[BikeSensorToSumoTrigger] ENTER -> station {stationNumber}");
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject go = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;

        if (!go.CompareTag(triggeringTag))
            return;

        Debug.Log($"[BikeSensorToSumoTrigger] EXIT -> station {stationNumber}");
    }
}