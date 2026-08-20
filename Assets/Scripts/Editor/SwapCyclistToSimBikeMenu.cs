#if UNITY_EDITOR
using SBPScripts.Simulator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CyclingExperiment.Scenarios;
using CyclingExperiment.UI;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Editor
{
    [InitializeOnLoad]
    public static class SwapCyclistToSimBikeMenu
    {
        const string SimBikePrefabPath = "Assets/BicycleSimulatorModel/Prefabs/SimBike.prefab";

        static SwapCyclistToSimBikeMenu()
        {
            EditorApplication.delayCall += TryAutoSwapOpenMainScene;
        }

        static void TryAutoSwapOpenMainScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path) || !scene.path.Contains("MainScene"))
                return;
            if (FindSceneObject("SimBike") != null)
                return;

            try
            {
                ApplySwap();
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[SwapCyclistToSimBike] Auto-applied SimBike swap to the open MainScene. Save the scene.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SwapCyclistToSimBike] Auto-swap failed: " + e);
            }
        }

        [MenuItem("Cycling Experiment/Swap Cyclist to SimBike", false, 2)]
        public static void SwapCyclistToSimBike()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.Contains("MainScene"))
            {
                if (!EditorUtility.DisplayDialog("Swap Cyclist to SimBike",
                    "Active scene is not MainScene. Continue on the current scene anyway?",
                    "Continue", "Cancel"))
                    return;
            }

            ApplySwap();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Swap Cyclist to SimBike",
                "SimBike is the experiment cyclist.\n\n" +
                "- bicyle_animated_human is disabled (not deleted)\n" +
                "- Scene chase Camera is disabled; SimBike Main Camera is used\n" +
                "- Tag is Player; extra AudioListeners on CAVE eyes are off\n" +
                "- WASD works until Wahoo/Fanatec connect\n\n" +
                "Save MainScene.",
                "OK");
        }

        public static void SwapCyclistToSimBikeBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
            ApplySwap();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SwapCyclistToSimBike] Saved MainScene with SimBike as the experiment cyclist.");
        }

        private static void ApplySwap()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimBikePrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException("Could not load " + SimBikePrefabPath);

            GameObject simBike = FindSceneObject("SimBike");
            if (simBike == null)
            {
                simBike = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                simBike.name = "SimBike";
                Undo.RegisterCreatedObjectUndo(simBike, "Instance SimBike");
            }

            Undo.RecordObject(simBike, "Configure SimBike");
            simBike.SetActive(true);
            simBike.tag = "Player";

            var oldBike = FindSceneObject("bicyle_animated_human");
            if (oldBike != null && oldBike != simBike)
            {
                Undo.RecordObject(oldBike, "Disable keyboard cyclist");
                oldBike.SetActive(false);
            }

            DisableChaseCamera();
            EnsureMotionAdapter(simBike);
            DisableSpawnOnAwake(simBike);
            SimBikeCyclistMotion.ConfigureExperimentPhysics(simBike);
            DisableCyclistRootMotion(simBike);

            var refs = ExperimentRefs.EnsureExists();
            Undo.RecordObject(refs, "Wire SimBike refs");
            var motion = simBike.GetComponent<SimBikeCyclistMotion>();
            refs.SetCyclist(simBike, motion);
            refs.followCamera = null;
            var refsSo = new SerializedObject(refs);
            var cyclistProp = refsSo.FindProperty("cyclistMotion");
            if (cyclistProp != null) cyclistProp.objectReferenceValue = motion;
            var bikeProp = refsSo.FindProperty("bicycle");
            if (bikeProp != null) bikeProp.objectReferenceValue = simBike;
            var xformProp = refsSo.FindProperty("bicycleTransform");
            if (xformProp != null) xformProp.objectReferenceValue = simBike.transform;
            var followProp = refsSo.FindProperty("followCamera");
            if (followProp != null) followProp.objectReferenceValue = null;
            refsSo.ApplyModifiedPropertiesWithoutUndo();

            var spawn = simBike.GetComponent<SimBikeSpawnController>();
            if (spawn != null)
            {
                spawn.SetSpawnPoints(refs.route1CyclistSpawn, refs.route2CyclistSpawn);
                var spawnSo = new SerializedObject(spawn);
                var s1 = spawnSo.FindProperty("scenario1Spawn");
                if (s1 != null) s1.objectReferenceValue = refs.route1CyclistSpawn;
                var s2 = spawnSo.FindProperty("scenario2Spawn");
                if (s2 != null) s2.objectReferenceValue = refs.route2CyclistSpawn;
                spawnSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(spawn);
            }

            if (refs.hud != null)
            {
                Undo.RecordObject(refs.hud, "HUD SimBike");
                refs.hud.SetBicycleController(motion);
                var hudSo = new SerializedObject(refs.hud);
                var bikeCtrl = hudSo.FindProperty("_bicycleController");
                if (bikeCtrl != null) bikeCtrl.objectReferenceValue = motion;
                hudSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(refs.hud);
            }

            if (refs.route1 != null)
            {
                var so = new SerializedObject(refs.route1);
                var player = so.FindProperty("playerTransform");
                if (player != null) player.objectReferenceValue = simBike.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(refs.route1);
            }

            var logger = Object.FindFirstObjectByType<EventMarkerLogger>();
            if (logger != null)
            {
                var so = new SerializedObject(logger);
                var player = so.FindProperty("playerTransform");
                if (player != null) player.objectReferenceValue = simBike.transform;
                var cyclist = so.FindProperty("cyclistMotion");
                if (cyclist != null) cyclist.objectReferenceValue = motion;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(logger);
            }

            EditorUtility.SetDirty(simBike);
            EditorUtility.SetDirty(refs);
        }

        private static GameObject FindSceneObject(string objectName)
        {
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in transforms)
            {
                if (t == null || t.name != objectName) continue;
                if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;
                return t.gameObject;
            }

            return null;
        }

        private static void DisableChaseCamera()
        {
            var cameras = Resources.FindObjectsOfTypeAll<UnityEngine.Camera>();
            foreach (var cam in cameras)
            {
                if (cam == null || !cam.gameObject.scene.IsValid()) continue;
                if (cam.transform.parent != null) continue;
                if (cam.GetComponent<CyclingExperiment.Camera.SmoothFollowBicycleCamera>() == null &&
                    cam.gameObject.name != "Camera")
                    continue;

                Undo.RecordObject(cam.gameObject, "Disable chase camera");
                cam.gameObject.SetActive(false);
            }
        }

        private static void EnsureMotionAdapter(GameObject simBike)
        {
            if (simBike.GetComponent<SimBikeCyclistMotion>() == null)
                Undo.AddComponent<SimBikeCyclistMotion>(simBike);
            if (simBike.GetComponent<BicycleSimulatorController>() == null)
                Debug.LogWarning("[SwapCyclistToSimBike] SimBike is missing BicycleSimulatorController.");
        }

        private static void DisableSpawnOnAwake(GameObject simBike)
        {
            var spawn = simBike.GetComponent<SimBikeSpawnController>();
            if (spawn == null) return;
            var so = new SerializedObject(spawn);
            var spawnOnAwake = so.FindProperty("spawnOnAwake");
            if (spawnOnAwake != null) spawnOnAwake.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawn);
        }

        private static void DisableCyclistRootMotion(GameObject simBike)
        {
            var cyclist = simBike.transform.Find("Old Cycle Cyclist");
            if (cyclist == null) return;
            var animator = cyclist.GetComponent<Animator>();
            if (animator == null || !animator.applyRootMotion) return;
            Undo.RecordObject(animator, "Disable SimBike root motion");
            animator.applyRootMotion = false;
        }
    }
}
#endif
