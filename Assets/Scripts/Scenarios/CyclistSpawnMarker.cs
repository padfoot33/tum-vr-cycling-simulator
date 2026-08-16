using UnityEngine;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Scene marker for a cyclist spawn. Move this empty in the Scene view.
    /// </summary>
    public class CyclistSpawnMarker : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.45f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);
        }
    }
}
