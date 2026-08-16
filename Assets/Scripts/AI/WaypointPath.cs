using System.Collections.Generic;
using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Holds an ordered list of waypoints and visualizes them in the editor.
    /// </summary>
    public class WaypointPath : MonoBehaviour
    {
        [Header("Path Settings")]
        [Tooltip("The ordered list of waypoints.")]
        public List<Transform> waypoints = new List<Transform>();

        [Tooltip("If true, the path loops from the last waypoint back to the first.")]
        public bool isLoop = false;

        /// <summary>
        /// The total number of waypoints.
        /// </summary>
        public int WaypointCount => waypoints != null ? waypoints.Count : 0;

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

        private void OnDrawGizmos()
        {
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
