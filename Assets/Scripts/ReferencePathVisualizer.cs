using UnityEngine;
using System.Linq;

[ExecuteAlways]
public class ReferencePathVisualizer : MonoBehaviour
{
    public Transform[] waypoints;
    public LineRenderer lineRenderer;
    public bool autoCollectChildren = true;

    private void Update()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (autoCollectChildren)
            CollectChildren();

        DrawLine();
    }

    private void CollectChildren()
    {
        waypoints = GetComponentsInChildren<Transform>()
            .Where(t => t != transform)
            .OrderBy(t => t.name)
            .ToArray();
    }

    public void DrawLine()
    {
        if (lineRenderer == null || waypoints == null || waypoints.Length == 0)
            return;

        lineRenderer.positionCount = waypoints.Length;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
                lineRenderer.SetPosition(i, waypoints[i].position);
        }
    }
}