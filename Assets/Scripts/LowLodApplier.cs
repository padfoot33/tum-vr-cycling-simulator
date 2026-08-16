using System.Collections.Generic;
using UnityEngine;

public class RestoreLowFromHigh : MonoBehaviour
{
    [Header("Assign these")]
    public Transform cityHigh;
    public Transform cityLow;

    [ContextMenu("RESTORE City_LOW materials from City_HIGH")]
    public void Restore()
    {
        if (cityHigh == null || cityLow == null)
        {
            Debug.LogError("[RestoreLowFromHigh] Assign cityHigh and cityLow.");
            return;
        }

        // Build a lookup from HIGH renderer path -> HIGH shared materials
        var highMap = new Dictionary<string, Material[]>();
        foreach (var r in cityHigh.GetComponentsInChildren<Renderer>(true))
        {
            string path = GetPath(cityHigh, r.transform);
            highMap[path] = r.sharedMaterials; // original shared materials
        }

        int restored = 0;
        int missing = 0;

        foreach (var rLow in cityLow.GetComponentsInChildren<Renderer>(true))
        {
            string path = GetPath(cityLow, rLow.transform);

            if (highMap.TryGetValue(path, out var mats))
            {
                rLow.sharedMaterials = mats;  // restore original material list
                restored++;
            }
            else
            {
                missing++;
            }
        }

        Debug.Log($"[RestoreLowFromHigh] Restored {restored} renderers. Missing matches: {missing} (ok if LOW differs).");
    }

    static string GetPath(Transform root, Transform t)
    {
        // relative path from root to t (matches if hierarchy structure is identical)
        var stack = new Stack<string>();
        while (t != null && t != root)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }
}