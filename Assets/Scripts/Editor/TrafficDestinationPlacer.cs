#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using CyclingExperiment.AI;

namespace CyclingExperiment.Editor
{
    /// <summary>
    /// Places Traffic_Destinations from the baked road NavMesh so cars have visible goals.
    /// </summary>
    public class TrafficDestinationPlacer : EditorWindow
    {
        private float spacingMeters = 48f;
        private int maxPoints = 72;

        [MenuItem("Cycling Experiment/Auto Place Traffic Destinations", false, 6)]
        public static void OpenWindow()
        {
            var window = GetWindow<TrafficDestinationPlacer>("Traffic Destinations");
            window.minSize = new Vector2(360f, 220f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Auto Place Traffic Destinations", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Reads the baked road NavMesh (asphalt) and drops yellow Dest_* points along the roads. " +
                "Cars then drive from one point to the next. Bake the road NavMesh first if this finds nothing.",
                MessageType.Info);

            spacingMeters = EditorGUILayout.Slider("Spacing (m)", spacingMeters, 20f, 120f);
            maxPoints = EditorGUILayout.IntSlider("Max points", maxPoints, 8, 150);

            GameObject root = GameObject.Find("Traffic_Destinations");
            int existing = root != null ? root.transform.childCount : 0;
            EditorGUILayout.LabelField("Current Dest_* points", existing.ToString());

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Place points from NavMesh", GUILayout.Height(36f)))
            {
                int count = PlaceFromNavMesh(spacingMeters, maxPoints);
                if (count >= 0)
                {
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    Debug.Log($"[TrafficDestinationPlacer] Placed {count} destination points on the road NavMesh.");
                }
            }

            if (GUILayout.Button("Bake Road NavMesh first"))
            {
                RoadNavMeshBaker.BakeRoadNavMesh();
            }
        }

        public static int PlaceFromNavMesh()
        {
            return PlaceFromNavMesh(48f, 72);
        }

        public static int PlaceFromNavMesh(float spacing, int maxPoints)
        {
            List<Vector3> samples = CollectNavMeshSamples();
            if (samples.Count == 0)
            {
                EditorUtility.DisplayDialog("Traffic Destinations",
                    "No road NavMesh found. Run Cycling Experiment > Bake Road NavMesh first.",
                    "OK");
                return -1;
            }

            List<Vector3> spaced = ThinBySpacing(samples, spacing, maxPoints);

            GameObject root = GameObject.Find("Traffic_Destinations");
            if (root == null)
            {
                root = new GameObject("Traffic_Destinations");
                GameObject scenarios = GameObject.Find("Scenarios");
                if (scenarios != null) root.transform.SetParent(scenarios.transform);
            }

            var set = root.GetComponent<TrafficDestinationSet>() ?? root.AddComponent<TrafficDestinationSet>();

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);
            }

            for (int i = 0; i < spaced.Count; i++)
            {
                var point = new GameObject($"Dest_{i + 1}");
                Undo.RegisterCreatedObjectUndo(point, "Auto Place Traffic Destinations");
                point.transform.SetParent(root.transform);
                point.transform.position = spaced[i];
            }

            set.RefreshFromChildren();
            Undo.RegisterCompleteObjectUndo(root, "Auto Place Traffic Destinations");
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(set);
            return spaced.Count;
        }

        private static List<Vector3> CollectNavMeshSamples()
        {
            var samples = new List<Vector3>();
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                return samples;
            }

            Vector3[] vertices = triangulation.vertices;
            int[] indices = triangulation.indices;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]];
                Vector3 b = vertices[indices[i + 1]];
                Vector3 c = vertices[indices[i + 2]];
                Vector3 centroid = (a + b + c) / 3f;
                if (NavMesh.SamplePosition(centroid, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    samples.Add(hit.position);
                }
            }

            return samples;
        }

        private static List<Vector3> ThinBySpacing(List<Vector3> samples, float spacing, int maxPoints)
        {
            var kept = new List<Vector3>();
            float spacingSq = spacing * spacing;

            for (int i = 0; i < samples.Count && kept.Count < maxPoints; i++)
            {
                Vector3 candidate = samples[i];
                bool tooClose = false;
                for (int k = 0; k < kept.Count; k++)
                {
                    Vector3 delta = candidate - kept[k];
                    delta.y = 0f;
                    if (delta.sqrMagnitude < spacingSq)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose) kept.Add(candidate);
            }

            return kept;
        }
    }
}
#endif
