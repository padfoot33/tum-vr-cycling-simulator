using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Scene-placed road destinations. Ambient cars path from one child empty to the next.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class TrafficDestinationSet : MonoBehaviour
    {
        public static TrafficDestinationSet Instance { get; private set; }

        [SerializeField] private Transform[] points;

        private void Awake()
        {
            Instance = this;
            RefreshFromChildren();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RefreshFromChildren()
        {
            int count = transform.childCount;
            points = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = transform.GetChild(i);
            }
        }

        public bool TryPickNext(Vector3 from, Vector3 forward, Transform current, out Transform next)
        {
            next = null;
            if (points == null || points.Length == 0) RefreshFromChildren();
            if (points == null || points.Length == 0) return false;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            float bestAhead = float.NegativeInfinity;
            Transform bestAheadPoint = null;
            float bestAny = float.PositiveInfinity;
            Transform bestAnyPoint = null;

            bool onRoute2 = NavMeshVehicleAI.IsRoute2Corridor(from);
            float bestCorridor = float.NegativeInfinity;
            Transform bestCorridorPoint = null;

            for (int i = 0; i < points.Length; i++)
            {
                Transform point = points[i];
                if (point == null || point == current) continue;

                Vector3 to = point.position - from;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < 4f) continue;

                if (onRoute2 && NavMeshVehicleAI.IsRoute2Corridor(point.position) &&
                    point.position.z < from.z - 2f)
                {
                    continue;
                }

                if (dist < bestAny)
                {
                    bestAny = dist;
                    bestAnyPoint = point;
                }

                float ahead = Vector3.Dot(to.normalized, forward);
                if (ahead > 0.15f && ahead * dist > bestAhead)
                {
                    bestAhead = ahead * dist;
                    bestAheadPoint = point;
                }

                if (onRoute2 && NavMeshVehicleAI.IsRoute2Corridor(point.position) &&
                    ahead > 0.15f && point.position.z >= from.z - 1f && ahead * dist > bestCorridor)
                {
                    bestCorridor = ahead * dist;
                    bestCorridorPoint = point;
                }
            }

            next = bestCorridorPoint != null ? bestCorridorPoint
                : bestAheadPoint != null ? bestAheadPoint
                : bestAnyPoint;
            return next != null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                Gizmos.DrawWireSphere(child.position + Vector3.up * 0.4f, 1.1f);
                Gizmos.DrawLine(child.position, child.position + Vector3.up * 2.2f);
            }
        }
    }
}
