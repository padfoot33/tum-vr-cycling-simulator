using System.Collections.Generic;
using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Holds an ordered list of waypoints and visualizes them in the editor.
    /// Hierarchy child order is travel order (same as Bus_Overtake_Path).
    /// </summary>
    public class WaypointPath : MonoBehaviour
    {
        public const string CampusRootName = "Campus_Traffic_Paths";

        [Header("Path Settings")]
        [Tooltip("The ordered list of waypoints.")]
        public List<Transform> waypoints = new List<Transform>();

        [Tooltip("If true, the path loops from the last waypoint back to the first.")]
        public bool isLoop = false;

        /// <summary>
        /// The total number of waypoints.
        /// </summary>
        public int WaypointCount => waypoints != null ? waypoints.Count : 0;

        private void Awake()
        {
            SyncFromChildren();
        }

        /// <summary>
        /// Rebuild the waypoint list from sibling order under this transform.
        /// </summary>
        public void SyncFromChildren()
        {
            if (waypoints == null) waypoints = new List<Transform>();
            waypoints.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null) waypoints.Add(child);
            }
        }

        public void RenameChildrenSequential()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null) child.name = "WP_" + i;
            }
        }

        public void ReverseChildOrder()
        {
            int count = transform.childCount;
            var children = new List<Transform>(count);
            for (int i = 0; i < count; i++) children.Add(transform.GetChild(i));
            children.Reverse();
            for (int i = 0; i < children.Count; i++)
            {
                children[i].SetSiblingIndex(i);
            }

            RenameChildrenSequential();
            SyncFromChildren();
        }

        public Transform CreateChildWaypoint(Vector3 position)
        {
            var go = new GameObject("WP_" + transform.childCount);
            go.transform.SetParent(transform, true);
            go.transform.position = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            SyncFromChildren();
            return go.transform;
        }

        public bool DeleteLastChild()
        {
            if (transform.childCount == 0) return false;
            Transform last = transform.GetChild(transform.childCount - 1);
            if (last == null) return false;
            if (Application.isPlaying) Destroy(last.gameObject);
            else DestroyImmediate(last.gameObject);
            RenameChildrenSequential();
            SyncFromChildren();
            return true;
        }

        public static bool IsReservedScenarioPath(WaypointPath path)
        {
            if (path == null) return true;
            string n = path.gameObject.name;
            return n.IndexOf("Bus_Overtake", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("RightTurn_Overtaking", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Gets the position of the waypoint at the specified index.
        /// </summary>
        public Vector3 GetWaypoint(int index)
        {
            if (waypoints == null || waypoints.Count == 0) return Vector3.zero;
            if (index < 0) index = 0;
            if (index >= waypoints.Count) index = waypoints.Count - 1;
            if (waypoints[index] == null) return Vector3.zero;
            return waypoints[index].position;
        }

        /// <summary>
        /// Calculates the total length of the path by summing distances between consecutive waypoints.
        /// </summary>
        public float GetTotalLength()
        {
            if (waypoints == null || waypoints.Count < 2) return 0f;

            float length = 0f;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    length += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
                }
            }

            if (isLoop && waypoints[waypoints.Count - 1] != null && waypoints[0] != null)
            {
                length += Vector3.Distance(waypoints[waypoints.Count - 1].position, waypoints[0].position);
            }

            return length;
        }

        /// <summary>
        /// Sample a point along the path. t is 0..1. nextWaypointIndex is the waypoint to drive toward.
        /// </summary>
        public bool TryGetPointAlongPath(float t, out Vector3 position, out Vector3 forward, out int nextWaypointIndex)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            nextWaypointIndex = 0;

            if (waypoints == null || waypoints.Count == 0) return false;
            if (waypoints.Count == 1)
            {
                position = waypoints[0].position;
                return true;
            }

            float total = GetTotalLength();
            if (total <= 0.01f)
            {
                position = waypoints[0].position;
                return true;
            }

            float target = Mathf.Clamp01(t) * total;
            float walked = 0f;
            int segmentCount = isLoop ? waypoints.Count : waypoints.Count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                int a = i;
                int b = (i + 1) % waypoints.Count;
                if (waypoints[a] == null || waypoints[b] == null) continue;

                float seg = Vector3.Distance(waypoints[a].position, waypoints[b].position);
                if (walked + seg >= target || i == segmentCount - 1)
                {
                    float localT = seg > 0.01f ? Mathf.Clamp01((target - walked) / seg) : 0f;
                    position = Vector3.Lerp(waypoints[a].position, waypoints[b].position, localT);
                    forward = (waypoints[b].position - waypoints[a].position);
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                    else forward.Normalize();
                    nextWaypointIndex = b;
                    return true;
                }

                walked += seg;
            }

            position = waypoints[waypoints.Count - 1].position;
            nextWaypointIndex = waypoints.Count - 1;
            return true;
        }

        private void OnValidate()
        {
            SyncFromChildren();
        }

        private void OnTransformChildrenChanged()
        {
            SyncFromChildren();
        }

        private void OnDrawGizmos()
        {
            int count = transform.childCount;
            if (count > 0)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < count; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child == null) continue;
                    Gizmos.DrawSphere(child.position, 0.5f);
                    if (i < count - 1)
                    {
                        Transform next = transform.GetChild(i + 1);
                        if (next != null) DrawPathLine(child.position, next.position);
                    }
                }

                if (isLoop && count > 1)
                {
                    Transform last = transform.GetChild(count - 1);
                    Transform first = transform.GetChild(0);
                    if (last != null && first != null)
                    {
                        DrawPathLine(last.position, first.position);
                    }
                }

                return;
            }

            if (waypoints == null || waypoints.Count == 0) return;

            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;

                Gizmos.DrawSphere(waypoints[i].position, 0.5f);

                if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
                {
                    DrawPathLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }

            if (isLoop && waypoints.Count > 1 && waypoints[waypoints.Count - 1] != null && waypoints[0] != null)
            {
                DrawPathLine(waypoints[waypoints.Count - 1].position, waypoints[0].position);
            }
        }

        private void DrawPathLine(Vector3 start, Vector3 end)
        {
            Gizmos.DrawLine(start, end);

            // Draw an arrow indicator
            Vector3 direction = (end - start).normalized;
            if (direction != Vector3.zero)
            {
                Vector3 midPoint = start + (end - start) * 0.5f;
                Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * new Vector3(0, 0, 1);
                Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * new Vector3(0, 0, 1);
                Gizmos.DrawRay(midPoint, right * 1.5f);
                Gizmos.DrawRay(midPoint, left * 1.5f);
            }
        }
    }
}
