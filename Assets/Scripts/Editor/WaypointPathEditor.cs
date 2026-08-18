#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CyclingExperiment.AI;

namespace CyclingExperiment.Editor
{
    /// <summary>
    /// Scene-view authoring for bus-style waypoint paths. Hierarchy child order is travel order.
    /// </summary>
    [CustomEditor(typeof(WaypointPath))]
    public class WaypointPathEditor : UnityEditor.Editor
    {
        private bool _editPath;

        private void OnEnable()
        {
            var path = target as WaypointPath;
            if (path != null && path.transform.childCount <= 1)
            {
                _editPath = true;
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var path = (WaypointPath)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Path Editor", EditorStyles.boldLabel);

            _editPath = GUILayout.Toggle(
                _editPath,
                _editPath ? "Edit Path: ON (Shift-click Scene)" : "Edit Path: OFF",
                "Button");

            if (_editPath)
            {
                EditorGUILayout.HelpBox(
                    "Shift-click the road in the Scene view to append WP_n. Drag children in the Hierarchy to change travel order.",
                    MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Waypoint After Last"))
            {
                AddWaypointAt(path, PositionAfterLast(path));
            }

            if (GUILayout.Button("Delete Last"))
            {
                DeleteLast(path);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reverse Path"))
            {
                Undo.RegisterFullObjectHierarchyUndo(path.gameObject, "Reverse Path");
                path.ReverseChildOrder();
                MarkDirty(path);
            }

            if (GUILayout.Button("Rebuild From Children"))
            {
                Undo.RecordObject(path, "Rebuild Path From Children");
                path.RenameChildrenSequential();
                path.SyncFromChildren();
                MarkDirty(path);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI()
        {
            var path = (WaypointPath)target;
            if (!_editPath || path == null) return;

            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (e.shift) HandleUtility.AddDefaultControl(controlId);

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 320f, 40f));
            GUILayout.Label("Path edit: Shift-click to append a waypoint", EditorStyles.helpBox);
            GUILayout.EndArea();
            Handles.EndGUI();

            if (e.shift && e.button == 0 && e.type == EventType.MouseDown)
            {
                if (TryPickPoint(e.mousePosition, out Vector3 point))
                {
                    AddWaypointAt(path, point);
                    GUIUtility.hotControl = controlId;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        internal static Transform AddWaypointAt(WaypointPath path, Vector3 position)
        {
            Undo.IncrementCurrentGroup();
            var go = new GameObject("WP_" + path.transform.childCount);
            Undo.RegisterCreatedObjectUndo(go, "Add Waypoint");
            Undo.SetTransformParent(go.transform, path.transform, "Add Waypoint");
            go.transform.position = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            path.RenameChildrenSequential();
            path.SyncFromChildren();
            MarkDirty(path);
            return go.transform;
        }

        private static void DeleteLast(WaypointPath path)
        {
            if (path.transform.childCount == 0) return;
            Transform last = path.transform.GetChild(path.transform.childCount - 1);
            Undo.DestroyObjectImmediate(last.gameObject);
            path.RenameChildrenSequential();
            path.SyncFromChildren();
            MarkDirty(path);
        }

        private static Vector3 PositionAfterLast(WaypointPath path)
        {
            path.SyncFromChildren();
            if (path.WaypointCount >= 2)
            {
                Vector3 a = path.GetWaypoint(path.WaypointCount - 2);
                Vector3 b = path.GetWaypoint(path.WaypointCount - 1);
                Vector3 d = b - a;
                d.y = 0f;
                if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
                return b + d.normalized * 10f;
            }

            if (path.WaypointCount == 1)
            {
                Vector3 fwd = Vector3.forward;
                if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                {
                    fwd = SceneView.lastActiveSceneView.camera.transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                }

                return path.GetWaypoint(0) + fwd.normalized * 10f;
            }

            if (SceneView.lastActiveSceneView != null) return SceneView.lastActiveSceneView.pivot;
            return path.transform.position;
        }

        private static bool TryPickPoint(Vector2 guiPoint, out Vector3 point)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
            if (Physics.Raycast(ray, out RaycastHit hit, 8000f, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            var plane = new Plane(Vector3.up, new Vector3(0f, 0.2f, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private static void MarkDirty(WaypointPath path)
        {
            EditorUtility.SetDirty(path);
            EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
        }
    }
}
#endif
