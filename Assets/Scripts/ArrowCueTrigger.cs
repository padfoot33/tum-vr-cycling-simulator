using UnityEngine;

public class ArrowCueTrigger : MonoBehaviour
{
    [Header("Arrow Object")]
    public GameObject targetArrow;

    [Header("Trigger Action")]
    public bool showArrow = true;

    [Header("Bike Reference")]
    public Rigidbody bikeRigidbody;

    private void Start()
    {
        if (targetArrow != null && showArrow)
        {
            targetArrow.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (bikeRigidbody == null || targetArrow == null) return;

        Rigidbody otherRb = other.attachedRigidbody;
        if (otherRb != bikeRigidbody) return;

        targetArrow.SetActive(showArrow);
        Debug.Log($"{gameObject.name} triggered -> Arrow {(showArrow ? "shown" : "hidden")}: {targetArrow.name}");
    }
}