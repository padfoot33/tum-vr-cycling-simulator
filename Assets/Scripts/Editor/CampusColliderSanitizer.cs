#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyclingExperiment.Editor
{
    /// <summary>
    /// Campus MeshColliders were added to every child mesh. PhysX then warns on SketchUp
    /// signal posts (triangles &gt; 500 units) and on tiles over 2,097,152 triangles with Fast Midphase.
    /// Keep ground/road triangle colliders; strip props; never cook decorative 2M+ meshes.
    /// </summary>
    public static class CampusColliderSanitizer
    {
        public const string CampusObjectName = "TUM_Campus_Container";
        public const int FastMidphaseTriangleLimit = 2_097_152;
        public const float LargeBoundsMeters = 500f;

        private const float TallPropMinHeight = 2.5f;
        private const float CompactPropMaxWidth = 8f;

        private static readonly string[] GroundTokens =
        {
            "Ground", "Sidewalk", "Pavement", "Terrain", "Cobble", "Plaza"
        };

        private static readonly string[] PropTokens =
        {
            "signal", "post", "tree", "plant", "light", "lamp", "sign", "pole",
            "furniture", "vehicle", "window", "glass", "person"
        };

        private enum SurfaceKind
        {
            GroundRoad,
            Prop,
            Other
        }

        [MenuItem("Cycling Experiment/Report Campus MeshColliders", false, 10)]
        public static void ReportMenu()
        {
            GameObject campus = FindCampus();
            if (campus == null)
            {
                EditorUtility.DisplayDialog("Campus MeshColliders",
                    "Could not find TUM_Campus_Container in the scene.", "OK");
                return;
            }

            Report(campus);
        }

        [MenuItem("Cycling Experiment/Sanitize Campus MeshColliders", false, 11)]
        public static void SanitizeMenu()
        {
            GameObject campus = FindCampus();
            if (campus == null)
            {
                EditorUtility.DisplayDialog("Sanitize Campus MeshColliders",
                    "Could not find TUM_Campus_Container in the scene.", "OK");
                return;
            }

            SanitizeResult result = Apply(campus, addMissingGround: true);
            EditorUtility.SetDirty(campus);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Sanitize Campus MeshColliders",
                "Campus colliders updated. Save MainScene after this.\n\n" +
                $"Removed MeshColliders: {result.RemovedMeshColliders}\n" +
                $"Disabled Fast Midphase: {result.DisabledFastMidphase}\n" +
                $"Added BoxColliders (tall props): {result.AddedBoxColliders}\n" +
                $"Added ground MeshColliders: {result.AddedMeshColliders}\n" +
                $"Kept MeshColliders: {result.KeptMeshColliders}",
                "OK");
        }

        public static GameObject FindCampus()
        {
            return GameObject.Find(CampusObjectName);
        }

        public static void Report(GameObject campus)
        {
            var colliders = campus.GetComponentsInChildren<MeshCollider>(true);
            var sb = new StringBuilder(colliders.Length * 80);
            int flagged = 0;
            int overLimit = 0;
            int signalPost = 0;
            int largeBounds = 0;

            sb.AppendLine($"[CampusColliders] {colliders.Length} MeshColliders under {campus.name}");
            sb.AppendLine("flags: SIGNAL_POST  OVER_2M  BOUNDS_500  PROP  GROUND");

            foreach (var mc in colliders)
            {
                Mesh mesh = mc.sharedMesh;
                int tris = CountTriangles(mesh);
                Bounds worldBounds = WorldBounds(mc.gameObject, mesh);
                SurfaceKind kind = Classify(mc.gameObject, mesh);
                bool isSignal = NameContains(mesh != null ? mesh.name : string.Empty, "signal_post")
                                || NameContains(mc.gameObject.name, "signal_post");
                bool over = tris >= FastMidphaseTriangleLimit;
                bool big = MaxAxis(worldBounds.size) >= LargeBoundsMeters;
                bool hasFast = (mc.cookingOptions & MeshColliderCookingOptions.UseFastMidphase) != 0;
                bool walkableSheet = over && kind != SurfaceKind.GroundRoad
                                     && LooksLikeWalkableSheet(mc.gameObject, mesh);

                if (isSignal) signalPost++;
                if (over) overLimit++;
                if (big) largeBounds++;
                if (isSignal || over || big) flagged++;

                string flags = string.Empty;
                if (isSignal) flags += " SIGNAL_POST";
                if (over) flags += " OVER_2M";
                if (big) flags += " BOUNDS_500";
                if (kind == SurfaceKind.Prop) flags += " PROP";
                if (kind == SurfaceKind.GroundRoad) flags += " GROUND";
                if (walkableSheet) flags += " WALKABLE_SHEET";
                if (hasFast) flags += " FAST_MIDPHASE";

                string meshName = mesh != null ? mesh.name : "(none)";
                string line =
                    $"{PathOf(mc.transform)} | mesh={meshName} | tris={tris} | bounds={worldBounds.size} |{flags}";
                sb.AppendLine(line);

                if (isSignal || over || big)
                    Debug.LogWarning("[CampusColliders] " + line, mc);
            }

            sb.AppendLine(
                $"flagged={flagged}  signal_post={signalPost}  over_2M={overLimit}  bounds_500={largeBounds}");
            Debug.Log(sb.ToString(), campus);

            EditorUtility.DisplayDialog("Campus MeshColliders",
                $"{colliders.Length} MeshColliders on {campus.name}.\n\n" +
                $"Flagged: {flagged}\n" +
                $"signal_post: {signalPost}\n" +
                $"triangles ≥ {FastMidphaseTriangleLimit:N0}: {overLimit}\n" +
                $"bounds ≥ {LargeBoundsMeters} m: {largeBounds}\n\n" +
                "See Console for paths and mesh names.",
                "OK");
        }

        public static SanitizeResult Apply(GameObject campus, bool addMissingGround)
        {
            var result = new SanitizeResult();
            Undo.SetCurrentGroupName("Sanitize Campus MeshColliders");
            int undoGroup = Undo.GetCurrentGroup();

            var existing = campus.GetComponentsInChildren<MeshCollider>(true);
            foreach (var mc in existing)
            {
                if (mc == null) continue;
                GameObject go = mc.gameObject;
                Mesh mesh = mc.sharedMesh;
                int tris = CountTriangles(mesh);
                SurfaceKind kind = Classify(go, mesh);

                if (mesh == null)
                {
                    Undo.DestroyObjectImmediate(mc);
                    result.RemovedMeshColliders++;
                    continue;
                }

                if (kind == SurfaceKind.Prop)
                {
                    Undo.DestroyObjectImmediate(mc);
                    result.RemovedMeshColliders++;
                    if (TryAddCompactPropBox(go))
                        result.AddedBoxColliders++;
                    continue;
                }

                bool overLimit = tris >= FastMidphaseTriangleLimit;
                if (overLimit && kind != SurfaceKind.GroundRoad
                    && !LooksLikeWalkableSheet(go, mesh))
                {
                    Undo.DestroyObjectImmediate(mc);
                    result.RemovedMeshColliders++;
                    continue;
                }

                if (overLimit && DisableFastMidphase(mc))
                    result.DisabledFastMidphase++;

                result.KeptMeshColliders++;
            }

            if (addMissingGround)
                result.AddedMeshColliders = AddMissingGroundColliders(campus);

            EditorUtility.SetDirty(campus);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                $"[CampusColliders] Sanitize: removed {result.RemovedMeshColliders} MeshColliders, " +
                $"disabled Fast Midphase on {result.DisabledFastMidphase}, " +
                $"added {result.AddedBoxColliders} BoxColliders, " +
                $"added {result.AddedMeshColliders} ground MeshColliders, " +
                $"kept {result.KeptMeshColliders}.",
                campus);
            return result;
        }

        public static int AddMissingGroundColliders(GameObject campus)
        {
            int added = 0;
            var filters = campus.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue;
                if (!ShouldKeepMeshCollider(mf.gameObject, mf.sharedMesh, out bool disableFastMidphase))
                    continue;

                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                ApplyCooking(mc, disableFastMidphase);
                added++;
            }

            return added;
        }

        public struct SanitizeResult
        {
            public int RemovedMeshColliders;
            public int DisabledFastMidphase;
            public int AddedBoxColliders;
            public int AddedMeshColliders;
            public int KeptMeshColliders;
        }

        public static int CountTriangles(Mesh mesh)
        {
            if (mesh == null) return 0;
            int count = 0;
            int sub = mesh.subMeshCount;
            for (int i = 0; i < sub; i++)
                count += (int)(mesh.GetIndexCount(i) / 3);
            return count;
        }

        public static bool ShouldKeepMeshCollider(GameObject go, Mesh mesh, out bool disableFastMidphase)
        {
            disableFastMidphase = false;
            if (go == null || mesh == null) return false;

            SurfaceKind kind = Classify(go, mesh);
            if (kind == SurfaceKind.Prop) return false;

            int tris = CountTriangles(mesh);
            bool overLimit = tris >= FastMidphaseTriangleLimit;
            if (overLimit && kind != SurfaceKind.GroundRoad && !LooksLikeWalkableSheet(go, mesh))
                return false;
            disableFastMidphase = overLimit;
            return true;
        }

        private static bool LooksLikeWalkableSheet(GameObject go, Mesh mesh)
        {
            Bounds bounds = WorldBounds(go, mesh);
            float xz = Mathf.Max(bounds.size.x, bounds.size.z);
            if (xz < 20f) return false;
            float maxHeight = Mathf.Max(12f, xz * 0.25f);
            return bounds.size.y <= maxHeight;
        }

        private static SurfaceKind Classify(GameObject go, Mesh mesh)
        {
            string meshName = mesh != null ? mesh.name : string.Empty;
            if (IsPropName(go.name) || IsPropName(meshName))
                return SurfaceKind.Prop;

            if (IsGroundOrRoadName(go.name) || IsGroundOrRoadName(meshName))
                return SurfaceKind.GroundRoad;

            Transform parent = go.transform.parent;
            if (parent != null && IsGroundOrRoadName(parent.name))
                return SurfaceKind.GroundRoad;

            return SurfaceKind.Other;
        }

        public static bool IsGroundOrRoadName(string name)
        {
            if (RoadNavMeshBaker.IsRoadName(name)) return true;
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < GroundTokens.Length; i++)
            {
                if (name.IndexOf(GroundTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        public static bool IsPropName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.IndexOf("signal_post", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            for (int i = 0; i < PropTokens.Length; i++)
            {
                if (name.IndexOf(PropTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool TryAddCompactPropBox(GameObject go)
        {
            if (go.GetComponent<Collider>() != null) return false;
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return false;

            Bounds world = renderer.bounds;
            if (world.size.y < TallPropMinHeight) return false;
            if (Mathf.Max(world.size.x, world.size.z) > CompactPropMaxWidth) return false;

            var box = Undo.AddComponent<BoxCollider>(go);
            Bounds local = LocalRendererBounds(go, renderer);
            box.center = local.center;
            box.size = local.size;
            return true;
        }

        private static Bounds LocalRendererBounds(GameObject go, Renderer renderer)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                return mf.sharedMesh.bounds;

            Bounds world = renderer.bounds;
            Vector3 center = go.transform.InverseTransformPoint(world.center);
            Vector3 lossy = go.transform.lossyScale;
            Vector3 size = new Vector3(
                SafeDiv(world.size.x, lossy.x),
                SafeDiv(world.size.y, lossy.y),
                SafeDiv(world.size.z, lossy.z));
            return new Bounds(center, size);
        }

        private static float SafeDiv(float value, float scale)
        {
            float abs = Mathf.Abs(scale);
            return abs > 1e-5f ? value / abs : value;
        }

        private static bool DisableFastMidphase(MeshCollider mc)
        {
            if ((mc.cookingOptions & MeshColliderCookingOptions.UseFastMidphase) == 0)
                return false;
            Undo.RecordObject(mc, "Disable Fast Midphase");
            mc.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
            EditorUtility.SetDirty(mc);
            return true;
        }

        private static void ApplyCooking(MeshCollider mc, bool disableFastMidphase)
        {
            var options = MeshColliderCookingOptions.CookForFasterSimulation
                          | MeshColliderCookingOptions.EnableMeshCleaning
                          | MeshColliderCookingOptions.WeldColocatedVertices;
            if (!disableFastMidphase)
                options |= MeshColliderCookingOptions.UseFastMidphase;
            mc.cookingOptions = options;
        }

        private static Bounds WorldBounds(GameObject go, Mesh mesh)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) return renderer.bounds;
            if (mesh == null) return new Bounds(go.transform.position, Vector3.zero);
            var local = mesh.bounds;
            Vector3 worldCenter = go.transform.TransformPoint(local.center);
            Vector3 worldSize = Vector3.Scale(local.size, go.transform.lossyScale);
            return new Bounds(worldCenter, worldSize);
        }

        private static float MaxAxis(Vector3 size)
        {
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }

        private static bool NameContains(string name, string token)
        {
            return !string.IsNullOrEmpty(name)
                   && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string PathOf(Transform t)
        {
            if (t == null) return string.Empty;
            string path = t.name;
            Transform p = t.parent;
            int depth = 0;
            while (p != null && depth < 12)
            {
                path = p.name + "/" + path;
                p = p.parent;
                depth++;
            }

            return path;
        }
    }
}
#endif
