using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BikeSensor : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private string egoTag = "Ego";

    [Header("Debug")]
    [SerializeField] private string stationNumber = "";
    [SerializeField] private string bikeTriggerValue = "";

    public bool isTriggered = false;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(stationNumber))
        {
            string parentName = transform.parent != null ? transform.parent.name : "";

            if (!string.IsNullOrEmpty(parentName))
            {
                int lastUnderscore = parentName.LastIndexOf('_');
                if (lastUnderscore >= 0 && lastUnderscore < parentName.Length - 1)
                    stationNumber = parentName.Substring(lastUnderscore + 1);
                else
                    stationNumber = parentName;
            }
        }

        bikeTriggerValue = string.IsNullOrEmpty(stationNumber) ? "" : $"busStation_{stationNumber}";
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject rootGo = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;

        if (!rootGo.CompareTag(egoTag))
            return;

        isTriggered = true;
        Debug.Log($"[BikeSensor] ENTER -> station='{stationNumber}', trigger='{bikeTriggerValue}'");
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject rootGo = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;

        if (!rootGo.CompareTag(egoTag))
            return;

        isTriggered = false;
        Debug.Log($"[BikeSensor] EXIT -> station='{stationNumber}'");
    }
}