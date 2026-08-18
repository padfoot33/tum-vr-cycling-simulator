#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CyclingExperiment.AI;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Editor
{
    /// <summary>
    /// Creates bus-style campus traffic paths under Campus_Traffic_Paths.
    /// </summary>
    public static class CampusTrafficPathMenu
    {
        [MenuItem("Cycling Experiment/Create Campus Traffic Path", false, 4)]
        public static void CreateCampusTrafficPath()
        {
            GameObject root = EnsureRoot();
            GameObject pathObj = new GameObject(NextPathName(root.transform));
            Undo.RegisterCreatedObjectUndo(pathObj, "Create Campus Traffic Path");
            pathObj.transform.SetParent(root.transform, true);

            var path = pathObj.AddComponent<WaypointPath>();
            Vector3 pos = PlacementOrigin();
            WaypointPathEditor.AddWaypointAt(path, pos);

            Selection.activeGameObject = pathObj;
            WireSceneRefs(root);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CyclingExperiment] Created " + pathObj.name + " under " + WaypointPath.CampusRootName +
                      ". Enable Edit Path and Shift-click the road to append waypoints.");
        }

        [MenuItem("Cycling Experiment/Create Path From Selection", false, 4)]
        public static void CreatePathFromSelection()
        {
            List<Transform> sources = SelectedTransformsInOrder();
            if (sources.Count == 0)
            {
                EditorUtility.DisplayDialog("Campus Traffic Path",
                    "Select one or more objects (Node_0, empties, etc.) in the order cars should travel, then run this menu again.",
                    "OK");
                return;
            }

            GameObject root = EnsureRoot();
            GameObject pathObj = new GameObject(NextPathName(root.transform));
            Undo.RegisterCreatedObjectUndo(pathObj, "Create Path From Selection");
            pathObj.transform.SetParent(root.transform, true);

            var path = pathObj.AddComponent<WaypointPath>();
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] == null) continue;
                WaypointPathEditor.AddWaypointAt(path, sources[i].position);
            }

            path.RenameChildrenSequential();
            path.SyncFromChildren();
            Selection.activeGameObject = pathObj;
            WireSceneRefs(root);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CyclingExperiment] Created " + pathObj.name + " with " + path.WaypointCount +
                      " waypoints copied from the selection. Original objects were left in place.");
        }

        [MenuItem("Cycling Experiment/Create Path From Selection", true)]
        public static bool CreatePathFromSelectionValidate()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }

        public static GameObject EnsureRoot()
        {
            GameObject root = GameObject.Find(WaypointPath.CampusRootName);
            if (root == null)
            {
                root = new GameObject(WaypointPath.CampusRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Campus Traffic Paths");
                GameObject campus = GameObject.Find("TUM_Campus_Container");
                if (campus != null) root.transform.SetParent(campus.transform, true);
                else
                {
                    GameObject scenarios = GameObject.Find("Scenarios");
                    if (scenarios != null) root.transform.SetParent(scenarios.transform, true);
                }
            }

            return root;
        }

        private static void WireSceneRefs(GameObject root)
        {
            var refs = Object.FindObjectOfType<ExperimentRefs>();
            if (refs == null) return;

            refs.campusTrafficPaths = root;
            EditorUtility.SetDirty(refs);

            if (refs.cityTraffic == null) return;
            var so = new SerializedObject(refs.cityTraffic);
            SerializedProperty prop = so.FindProperty("campusTrafficPathsRoot");
            if (prop != null)
            {
                prop.objectReferenceValue = root;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(refs.cityTraffic);
            }
        }

        private static string NextPathName(Transform root)
        {
            int index = root != null ? root.childCount : 0;
            return "Path_" + index;
        }

        private static Vector3 PlacementOrigin()
        {
            if (Selection.activeTransform != null) return Selection.activeTransform.position;
            if (SceneView.lastActiveSceneView != null) return SceneView.lastActiveSceneView.pivot;
            return new Vector3(436f, 0.2f, -80f);
        }

        private static List<Transform> SelectedTransformsInOrder()
        {
            var list = new List<Transform>();
            Object[] objects = Selection.objects;
            for (int i = 0; i < objects.Length; i++)
            {
                var go = objects[i] as GameObject;
                if (go == null) continue;
                if (go.GetComponent<WaypointPath>() != null) continue;
                list.Add(go.transform);
            }

            return list;
        }
    }
}
#endif
