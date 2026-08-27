#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CyclingExperiment.Scenarios;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Editor
{
    public static class PlayAreaSetupMenu
    {
        [MenuItem("Cycling Experiment/Create Route Play Areas", false, 8)]
        public static void CreateRoutePlayAreas()
        {
            var refs = Object.FindObjectOfType<ExperimentRefs>();
            if (refs == null)
                refs = ExperimentRefs.EnsureExists();

            refs.route1PlayArea = PlayAreaBounds.FindOrCreateRoute1(refs);
            refs.route2PlayArea = PlayAreaBounds.FindOrCreateRoute2(refs);

            if (refs.playAreaConstraint == null)
                refs.playAreaConstraint = refs.GetComponent<PlayAreaConstraint>();
            if (refs.playAreaConstraint == null)
                refs.playAreaConstraint = refs.gameObject.AddComponent<PlayAreaConstraint>();
            refs.playAreaConstraint.Bind(refs);

            EditorUtility.SetDirty(refs);
            if (refs.route1PlayArea != null) EditorUtility.SetDirty(refs.route1PlayArea.gameObject);
            if (refs.route2PlayArea != null) EditorUtility.SetDirty(refs.route2PlayArea.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (refs.route1PlayArea != null)
                Selection.activeGameObject = refs.route1PlayArea.gameObject;

            EditorUtility.DisplayDialog("Route Play Areas",
                "Cyan wire boxes are the allowed riding space for Route 1 and Route 2.\n\n" +
                "Move and scale the Box_* children in the Scene view. The rider cannot leave that union.\n" +
                "[1] / [2] (and locked builds) enable the matching set. Free roam has no fence.",
                "OK");
        }
    }
}
#endif
