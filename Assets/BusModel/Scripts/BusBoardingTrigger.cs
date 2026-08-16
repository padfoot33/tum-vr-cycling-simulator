using UnityEngine;

/// <summary>
/// Attach to a trigger collider on the bus. When a pedestrian enters,
/// it disables their CharacterController, Animator, and renderers so
/// they no longer walk through the bus in Unity. SUMO linkage remains
/// intact (we don't remove or destroy the GameObject).
/// </summary>
public class BusBoardingTrigger : MonoBehaviour
{
    [Tooltip("Only react to objects with this tag (pedestrian prefabs use Person).")]
    public string pedestrianTag = "Person";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(pedestrianTag))
            return;

        TogglePedestrian(other.gameObject, false);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(pedestrianTag))
            return;

        // Optional: re-enable when leaving trigger (e.g., after alighting)
        TogglePedestrian(other.gameObject, true);
    }

    private void TogglePedestrian(GameObject root, bool enable)
    {
        foreach (var cc in root.GetComponentsInChildren<CharacterController>(true))
            cc.enabled = enable;

        foreach (var anim in root.GetComponentsInChildren<Animator>(true))
            anim.enabled = enable;

        foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            rend.enabled = enable;
    }
}
