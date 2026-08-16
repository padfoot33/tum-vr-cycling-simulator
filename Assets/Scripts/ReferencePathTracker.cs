using UnityEngine;
using System.Linq;

[ExecuteAlways]
public class ReferencePathTracker : MonoBehaviour
{
    [Header("Path")]
    public Transform[] waypoints;
    public bool autoCollectChildren = true;

    [Header("Tracked Object")]
    public Transform bikeTransform;

    [Header("Read Only")]
    public float currentDeviation = 0f;
    public Vector3 closestPointOnPath;
    public int closestSegmentIndex = -1;

    private void Update()
    {
        if (autoCollectChildren)
            CollectChildren();

        if (bikeTransform == null || waypoints == null || waypoints.Length < 2)
            return;

        UpdateDeviation();
    }

    private void CollectChildren()
    {
        waypoints = GetComponentsInChildren<Transform>()
            .Where(t => t != transform && t.name.StartsWith("P_"))
            .OrderBy(t => t.name)
            .ToArray();
    }

    public void UpdateDeviation()
    {
        Vector3 bikePos = bikeTransform.position;

        float minDist = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;
        int bestSegment = -1;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Vector3 a = waypoints[i].position;
            Vector3 b = waypoints[i + 1].position;

            Vector3 closest = GetClosestPointOnSegmentXZ(a, b, bikePos);
            float dist = Vector2.Distance(
                new Vector2(bikePos.x, bikePos.z),
                new Vector2(closest.x, closest.z)
            );

            if (dist < minDist)
            {
                minDist = dist;
                bestPoint = closest;
                bestSegment = i;
            }
        }

        currentDeviation = minDist;
        closestPointOnPath = bestPoint;
        closestSegmentIndex = bestSegment;
    }

    private Vector3 GetClosestPointOnSegmentXZ(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);
        Vector2 p2 = new Vector2(p.x, p.z);

        Vector2 ab = b2 - a2;
        float abLenSq = ab.sqrMagnitude;

        if (abLenSq < 0.000001f)
            return a;

        float t = Vector2.Dot(p2 - a2, ab) / abLenSq;
        t = Mathf.Clamp01(t);

        Vector2 c2 = a2 + t * ab;
        float y = Mathf.Lerp(a.y, b.y, t);

        return new Vector3(c2.x, y, c2.y);
    }
}