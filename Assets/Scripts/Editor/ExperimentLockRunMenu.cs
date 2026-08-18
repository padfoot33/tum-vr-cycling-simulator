#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Editor
{
    public static class ExperimentLockRunMenu
    {
        [MenuItem("Cycling Experiment/Lock Run/Scenario 1 With Traffic", false, 21)]
        public static void LockScenario1WithTraffic() => ApplyLock(true, 1, true);

        [MenuItem("Cycling Experiment/Lock Run/Scenario 1 No Traffic", false, 22)]
        public static void LockScenario1NoTraffic() => ApplyLock(true, 1, false);

        [MenuItem("Cycling Experiment/Lock Run/Scenario 2 With Traffic", false, 23)]
        public static void LockScenario2WithTraffic() => ApplyLock(true, 2, true);

        [MenuItem("Cycling Experiment/Lock Run/Scenario 2 No Traffic", false, 24)]
        public static void LockScenario2NoTraffic() => ApplyLock(true, 2, false);

        [MenuItem("Cycling Experiment/Lock Run/Unlock for editor Play", false, 26)]
        public static void UnlockForEditorPlay() => ApplyLock(false, 1, true);

        private static void ApplyLock(bool lockRun, int route, bool traffic)
        {
            var refs = Object.FindFirstObjectByType<ExperimentRefs>();
            if (refs == null)
            {
                EditorUtility.DisplayDialog("Lock Run",
                    "Open MainScene and select or wait for Experiment_Scene_Refs, then run this menu again.",
                    "OK");
                return;
            }

            Undo.RecordObject(refs, "Lock participant run");
            refs.SetLockedRun(lockRun, route, traffic);
            EditorUtility.SetDirty(refs);
            EditorSceneManager.MarkSceneDirty(refs.gameObject.scene);

            string msg = lockRun
                ? "Locked Route " + refs.lockedRouteIndex + (refs.lockedTrafficEnabled ? " with traffic" : " without traffic") +
                  ".\nSave MainScene, then File → Build Settings → Build."
                : "Unlocked. Editor Play will show the scenario menu again. Save MainScene.";
            Debug.Log("[CyclingExperiment] " + msg.Replace("\n", " "));
            EditorUtility.DisplayDialog("Lock Run", msg, "OK");
        }
    }
}
#endif
