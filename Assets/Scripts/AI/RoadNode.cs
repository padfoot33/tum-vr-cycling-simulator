using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Junction or mid-block vertex on <see cref="RoadNetwork"/>.
    /// </summary>
    public class RoadNode : MonoBehaviour
    {
        [SerializeField] private string nodeId;

        public string NodeId
        {
            get => string.IsNullOrEmpty(nodeId) ? name : nodeId;
            set => nodeId = value;
        }

        public Vector3 Position => transform.position;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.35f, 1.15f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2.4f);
        }
    }
}
