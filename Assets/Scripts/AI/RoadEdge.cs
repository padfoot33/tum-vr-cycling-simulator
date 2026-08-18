using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Directed street segment from one <see cref="RoadNode"/> to another.
    /// </summary>
    [Serializable]
    public class RoadEdge
    {
        public string id;
        public RoadNode from;
        public RoadNode to;
        public List<Vector3> shapePoints = new List<Vector3>();
        public float cruiseSpeed = 9.5f;
        public float laneOffset = 0f;
        public bool isRoute2;

        [NonSerialized] public Vector3[] Polyline;
        [NonSerialized] public float Length;

        public bool IsValid => from != null && to != null && from != to;

        public void RebuildPolyline()
        {
            if (!IsValid)
            {
                Polyline = Array.Empty<Vector3>();
                Length = 0f;
                return;
            }

            int extra = shapePoints != null ? shapePoints.Count : 0;
            var raw = new Vector3[2 + extra];
            raw[0] = from.Position;
            for (int i = 0; i < extra; i++)
            {
                raw[i + 1] = shapePoints[i];
            }

            raw[raw.Length - 1] = to.Position;

            if (Mathf.Abs(laneOffset) > 0.01f)
            {
                OffsetPolyline(raw, laneOffset);
            }

            Polyline = raw;
            Length = 0f;
            for (int i = 0; i < Polyline.Length - 1; i++)
            {
                Vector3 a = Polyline[i];
                Vector3 b = Polyline[i + 1];
                a.y = 0f;
                b.y = 0f;
                Length += Vector3.Distance(a, b);
            }
        }

        public bool Sample(float distanceAlong, out Vector3 position, out Vector3 forward)
        {
            if (Polyline == null || Polyline.Length == 0) RebuildPolyline();
            if (Polyline == null || Polyline.Length == 0)
            {
                position = Vector3.zero;
                forward = Vector3.forward;
                return false;
            }

            if (Polyline.Length == 1)
            {
                position = Polyline[0];
                forward = Vector3.forward;
                return true;
            }

            float remaining = Mathf.Max(0f, distanceAlong);
            for (int i = 0; i < Polyline.Length - 1; i++)
            {
                Vector3 a = Polyline[i];
                Vector3 b = Polyline[i + 1];
                Vector3 delta = b - a;
                delta.y = 0f;
                float seg = delta.magnitude;
                if (seg < 0.01f) continue;

                if (remaining <= seg || i == Polyline.Length - 2)
                {
                    float t = seg > 0.01f ? Mathf.Clamp01(remaining / seg) : 1f;
                    position = Vector3.Lerp(a, b, t);
                    forward = delta / seg;
                    return true;
                }

                remaining -= seg;
            }

            position = Polyline[Polyline.Length - 1];
            Vector3 last = Polyline[Polyline.Length - 1] - Polyline[Polyline.Length - 2];
            last.y = 0f;
            forward = last.sqrMagnitude > 0.01f ? last.normalized : Vector3.forward;
            return true;
        }

        public float ClosestDistanceAlong(Vector3 world, out float lateral)
        {
            if (Polyline == null || Polyline.Length < 2) RebuildPolyline();
            lateral = float.PositiveInfinity;
            if (Polyline == null || Polyline.Length < 2) return 0f;

            float bestAlong = 0f;
            float bestDist = float.PositiveInfinity;
            float walked = 0f;
            Vector3 flat = world;
            flat.y = 0f;

            for (int i = 0; i < Polyline.Length - 1; i++)
            {
                Vector3 a = Polyline[i];
                Vector3 b = Polyline[i + 1];
                a.y = 0f;
                b.y = 0f;
                Vector3 ab = b - a;
                float seg = ab.magnitude;
                if (seg < 0.01f) continue;

                Vector3 dir = ab / seg;
                float t = Mathf.Clamp(Vector3.Dot(flat - a, dir), 0f, seg);
                Vector3 proj = a + dir * t;
                float dist = Vector3.Distance(flat, proj);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestAlong = walked + t;
                    lateral = dist;
                }

                walked += seg;
            }

            return bestAlong;
        }

        public bool IsReverseOf(RoadEdge other)
        {
            return other != null && from == other.to && to == other.from;
        }

        private static void OffsetPolyline(Vector3[] points, float offset)
        {
            if (points == null || points.Length == 0) return;

            var shifted = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 heading;
                if (i == 0)
                {
                    heading = points[Mathf.Min(1, points.Length - 1)] - points[0];
                }
                else if (i == points.Length - 1)
                {
                    heading = points[i] - points[i - 1];
                }
                else
                {
                    heading = points[i + 1] - points[i - 1];
                }

                heading.y = 0f;
                if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
                else heading.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, heading);
                shifted[i] = points[i] + right * offset;
            }

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = shifted[i];
            }
        }
    }
}
