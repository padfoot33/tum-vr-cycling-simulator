#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CyclingExperiment.Scenarios;

namespace CyclingExperiment.Editor
{
    /// <summary>
    /// Builds a spawnable bus prefab from the imported Bogdan A092 FBX and wires Route 1 to it.
    /// </summary>
    public static class BogdanBusInstaller
    {
        public const string FbxPath = "Assets/BusModel/BogdanA092/bogdan_a092.fbx";
        public const string PrefabPath = "Assets/BusModel/Prefabs/BogdanA092.prefab";
        private const float TargetLengthMeters = 11f;

        [MenuItem("Cycling Experiment/Install Bogdan A092 Bus", false, 8)]
        public static void InstallFromMenu()
        {
            if (Install())
            {
                EditorUtility.DisplayDialog("Bogdan Bus",
                    "Installed Bogdan A092 as the Route 1 bus prefab.\n\nPress Play and trigger Route 1 to see it.",
                    "OK");
            }
        }

        public static bool Install()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Bogdan Bus",
                    "Unity has not finished importing bogdan_a092.fbx yet. Wait for the import to finish, then run this again.",
                    "OK");
                return false;
            }

            GameObject root = new GameObject("BogdanA092");
            try
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (visual == null) visual = Object.Instantiate(model);
                visual.name = "bogdan_a092";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                FitToBusSize(root);
                AddColliderAndBody(root);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Bogdan Bus", "Install failed: " + ex.Message, "OK");
                return false;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            RemoveLeftoverSceneInstances();
            AssignToScene();
            return true;
        }

        private static void RemoveLeftoverSceneInstances()
        {
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null || t.name != "BogdanA092") continue;
                if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;
                if (PrefabUtility.IsPartOfPrefabAsset(t.gameObject)) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
            }
        }

        private static void FitToBusSize(GameObject root)
        {
            Bounds bounds = EncapsulateRenderers(root);
            if (bounds.size.sqrMagnitude < 0.01f) return;

            float length = Mathf.Max(bounds.size.x, bounds.size.z);
            if (length < 0.05f) return;

            float scale = 1f;
            if (length > 40f) scale = 0.01f;
            else if (length < 3f || length > 16f) scale = TargetLengthMeters / length;

            if (!Mathf.Approximately(scale, 1f))
            {
                root.transform.localScale = Vector3.one * scale;
                bounds = EncapsulateRenderers(root);
            }

            Vector3 localBottom = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            foreach (Transform child in root.transform)
            {
                child.localPosition -= new Vector3(localBottom.x, localBottom.y, localBottom.z);
            }
        }

        private static void AddColliderAndBody(GameObject root)
        {
            Bounds bounds = EncapsulateRenderers(root);
            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();

            Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = root.transform.InverseTransformVector(bounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            localSize.x = Mathf.Clamp(localSize.x, 2.2f, 2.6f);
            localSize.y = Mathf.Clamp(localSize.y, 2.8f, 3.3f);
            localSize.z = Mathf.Clamp(localSize.z, 10.5f, 12f);
            localCenter.x = 0f;
            localCenter.z = 0f;
            localCenter.y = localSize.y * 0.5f;
            box.center = localCenter;
            box.size = localSize;

            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private static Bounds EncapsulateRenderers(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(root.transform.position, Vector3.zero);
            bool started = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (!started)
                {
                    bounds = renderers[i].bounds;
                    started = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }

        private static void AssignToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;

            var combined = Object.FindFirstObjectByType<Scenario1_CombinedController>();
            if (combined != null)
            {
                var so = new SerializedObject(combined);
                var prop = so.FindProperty("busPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = prefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(combined);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }

    public class BogdanBusImportHook : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            for (int i = 0; i < imported.Length; i++)
            {
                if (imported[i] != BogdanBusInstaller.FbxPath) continue;
                EditorApplication.delayCall += () =>
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(BogdanBusInstaller.PrefabPath) == null)
                    {
                        BogdanBusInstaller.Install();
                    }
                };
                return;
            }
        }
    }
}
#endif
