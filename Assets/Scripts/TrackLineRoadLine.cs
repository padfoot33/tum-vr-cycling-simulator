using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TrackLineRoadLine : MonoBehaviour
{
    [Header("References")]
    public Transform pointsParent;   // RouteWaypoints (WP -01 .. WP 32)

    [Header("Line Settings")]
    public float yOffset = 0.05f;    // lift slightly above road to avoid z-fighting
    public float lineWidth = 0.15f;

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;

        lr.widthMultiplier = lineWidth;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
    }

    void Start()
    {
        BuildLine();
    }

    public void BuildLine()
    {
        if (pointsParent == null)
        {
            Debug.LogError("[TrackLineRoadLine] pointsParent not assigned.");
            return;
        }

        var temp = new List<(int idx, Vector3 pos)>();

        for (int i = 0; i < pointsParent.childCount; i++)
        {
            Transform ch = pointsParent.GetChild(i);
            int idx = ExtractFirstInt(ch.name, i);
            Vector3 p = ch.position;
            p.y += yOffset;
            temp.Add((idx, p));
        }

        temp.Sort((a, b) => a.idx.CompareTo(b.idx));

        lr.positionCount = temp.Count;
        for (int i = 0; i < temp.Count; i++)
            lr.SetPosition(i, temp[i].pos);
    }

    static int ExtractFirstInt(string s, int fallback)
    {
        int sign = 1;
        bool foundDigit = false;
        int value = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (!foundDigit && c == '-')
            {
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
                break;
            }
        }

        if (!foundDigit) return fallback;
        return sign * value;
    }
}