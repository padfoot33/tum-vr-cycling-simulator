using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Dest_* empties used as snap targets when building Campus_Road_Network.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class TrafficDestinationSet : MonoBehaviour
    {
        public static TrafficDestinationSet Instance { get; private set; }

        public static readonly string[] OneWayLane =
        {
            "Dest_67", "Dest_62", "Dest_61", "Dest_60"
        };

        [SerializeField] private Transform[] points;

        private void Awake()
        {
            Instance = this;
            RefreshFromChildren();
            RemoveGeneratedNorthPoints();
            Route2Corridor.ResetHeading();
            Route2Corridor.EnsureHeading();
        }

        public Transform FindByName(string destName)
        {
            if (points == null || points.Length == 0) RefreshFromChildren();
            if (points == null) return null;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null && points[i].name == destName) return points[i];
            }

            return null;
        }

        public bool TryPickOneWayNext(Vector3 from, Transform current, out Transform next)
        {
            next = null;
            int fromIndex = ChainIndex(current);
            if (fromIndex < 0) fromIndex = ClosestChainIndex(from);
            if (fromIndex < 0) return false;

            for (int i = fromIndex + 1; i < OneWayLane.Length; i++)
            {
                Transform point = FindByName(OneWayLane[i]);
                if (point == null) continue;
                Vector3 delta = point.position - from;
                delta.y = 0f;
                if (delta.sqrMagnitude < 8f * 8f) continue;
                next = point;
                return true;
            }

            return false;
        }

        public bool IsAtOneWayEnd(Vector3 from, Transform current)
        {
            Transform last = null;
            for (int i = OneWayLane.Length - 1; i >= 0; i--)
            {
                last = FindByName(OneWayLane[i]);
                if (last != null) break;
            }

            if (last == null) return false;
            if (current != null && current.name == last.name) return true;

            Vector3 delta = last.position - from;
            delta.y = 0f;
            return delta.sqrMagnitude <= 10f * 10f;
        }

        public int ClosestChainIndex(Vector3 from)
        {
            int best = -1;
            float bestDist = 28f;
            for (int i = 0; i < OneWayLane.Length; i++)
            {
                Transform point = FindByName(OneWayLane[i]);
                if (point == null) continue;
                Vector3 delta = point.position - from;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }

        private int ChainIndex(Transform dest)
        {
            if (dest == null) return -1;
            for (int i = 0; i < OneWayLane.Length; i++)
            {
                if (dest.name == OneWayLane[i]) return i;
            }

            return -1;
        }

        private void RemoveGeneratedNorthPoints()
        {
            bool removed = false;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null || !child.name.StartsWith("Dest_67_N")) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
                removed = true;
            }

            if (removed) RefreshFromChildren();
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
            return TryPickNext(from, forward, current, null, out next);
        }

        public bool TryPickNext(Vector3 from, Vector3 forward, Transform current, Transform avoid, out Transform next)
        {
            next = null;
            if (points == null || points.Length == 0) RefreshFromChildren();
            if (points == null || points.Length == 0) return false;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            bool onRoute2 = Route2Corridor.Contains(from);
            if (onRoute2 && TryPickOneWayNext(from, current, out next))
                return true;

            float bestAhead = float.PositiveInfinity;
            Transform bestAheadPoint = null;
            float bestFreeAhead = float.PositiveInfinity;
            Transform bestFreeAheadPoint = null;
            float bestAny = float.PositiveInfinity;
            Transform bestAnyPoint = null;

            for (int i = 0; i < points.Length; i++)
            {
                Transform point = points[i];
                if (point == null || point == current) continue;

                Vector3 to = point.position - from;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < 16f) continue;
                if (onRoute2 && Vector3.Dot(to, Route2Corridor.Heading) < 8f) continue;

                if (dist < bestAny)
                {
                    bestAny = dist;
                    bestAnyPoint = point;
                }

                float ahead = Vector3.Dot(to.normalized, forward);
                if (ahead > 0.15f && dist < 140f && dist < bestAhead)
                {
                    bestAhead = dist;
                    bestAheadPoint = point;
                }

                if (point != avoid && ahead > 0.15f && dist < 140f && dist < bestFreeAhead)
                {
                    bestFreeAhead = dist;
                    bestFreeAheadPoint = point;
                }
            }

            next = bestFreeAheadPoint != null ? bestFreeAheadPoint
                : bestAheadPoint != null ? bestAheadPoint
                : bestAnyPoint;
            return next != null;
        }

        private void OnDrawGizmosSelected()
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
