using System;
using System.Collections.Generic;
using UnityEngine;

public class TrackLine : MonoBehaviour
{
    [Header("References")]
    public Transform pointsParent;   // Your RouteWaypoints / TrackLinePoints
    public Transform player;         // ego_bike

    [Header("Track Stats (read-only)")]
    public float totalLength;
    public float progressS;          // distance along track
    public float lateralError;       // distance to centerline

    private readonly List<Vector3> pts = new List<Vector3>();
    private readonly List<float> cumulative = new List<float>();

    void Start()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        pts.Clear();
        cumulative.Clear();
        totalLength = 0f;

        if (pointsParent == null)
        {
            Debug.LogError("[TrackLine] pointsParent not assigned.");
            return;
        }

        // Collect children as (index, position)
        var temp = new List<(int idx, Vector3 pos)>();
        for (int i = 0; i < pointsParent.childCount; i++)
        {
            Transform ch = pointsParent.GetChild(i);
            int idx = ExtractFirstInt(ch.name, i); // fallback to hierarchy order if parse fails
            temp.Add((idx, ch.position));
        }

        // Sort by parsed index
        temp.Sort((a, b) => a.idx.CompareTo(b.idx));

        // Fill pts in correct order
        for (int i = 0; i < temp.Count; i++)
            pts.Add(temp[i].pos);

        cumulative.Add(0f);

        for (int i = 1; i < pts.Count; i++)
        {
            totalLength += Vector3.Distance(pts[i - 1], pts[i]);
            cumulative.Add(totalLength);
        }

        Debug.Log($"[TrackLine] Loaded {pts.Count} points. Total length = {totalLength:F1}m");
    }

    void Update()
    {
        if (player == null || pts.Count < 2) return;

        float bestDistSq = float.PositiveInfinity;
        float bestS = 0f;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];

            Vector3 closest = ClosestPointOnSegment(player.position, a, b, out float t);
            float dSq = (player.position - closest).sqrMagnitude;

            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;

                float segLen = Vector3.Distance(a, b);
                bestS = cumulative[i] + (segLen * t);
            }
        }

        progressS = bestS;
        lateralError = Mathf.Sqrt(bestDistSq);
    }

    static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
    {
        Vector3 ab = b - a;
        float abLenSq = Vector3.Dot(ab, ab);

        if (abLenSq < 0.000001f)
        {
            t = 0f;
            return a;
        }

        t = Vector3.Dot(p - a, ab) / abLenSq;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    static int ExtractFirstInt(string s, int fallback)
    {
        // Finds the first integer in a string like "WP -01" or "WP 32"
        // Returns fallback if none found
        int sign = 1;
        bool foundDigit = false;
        int value = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (!foundDigit && c == '-')
            {
                // allow negative sign before digits
                sign = -1;
                continue;
            }

            if (char.IsDigit(c))
            {
                foundDigit = true;
                value = value * 10 + (c - '0');
            }
            else if (foundDigit)
            {
                break; // stop after finishing the first number
            }
        }

        if (!foundDigit) return fallback;
        return sign * value;
    }

    void OnDrawGizmos()
    {
        if (pointsParent == null) return;

        // Draw track line
        Gizmos.color = Color.cyan;

        // Draw in sorted order too (so visuals match math)
        var temp = new List<(int idx, Vector3 pos)>();
        for (int i = 0; i < pointsParent.childCount; i++)
        {
            Transform ch = pointsParent.GetChild(i);
            int idx = ExtractFirstInt(ch.name, i);
            temp.Add((idx, ch.position));
        }
        temp.Sort((a, b) => a.idx.CompareTo(b.idx));

        for (int i = 0; i < temp.Count; i++)
        {
            Gizmos.DrawSphere(temp[i].pos, 0.25f);
            if (i > 0)
                Gizmos.DrawLine(temp[i - 1].pos, temp[i].pos);
        }
    }
}