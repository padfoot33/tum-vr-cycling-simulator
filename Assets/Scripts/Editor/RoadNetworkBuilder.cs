#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CyclingExperiment.AI;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Editor
{
    /// <summary>
    /// Seeds Campus_Road_Network from campus arteries, snaps to Dest_* , merges junctions.
    /// </summary>
    public static class RoadNetworkBuilder
    {
        [MenuItem("Cycling Experiment/Build Campus Road Graph (obsolete)", false, 105)]
        public static void BuildCampusRoadGraph()
        {
            EditorUtility.DisplayDialog("Campus Road Graph",
                "Campus_Road_Network is no longer used for ambient cars.\n\n" +
                "Use Cycling Experiment → Create Campus Traffic Path, then enable Edit Path and Shift-click the road in the Scene view.\n" +
                "Two-way streets need two paths with opposite waypoint order.",
                "OK");
        }

        public static RoadNetwork RebuildSilent()
        {
            RoadNetwork network = EnsureNetwork();
            Transform destRoot = FindDestRoot();
            network.RebuildFromCampusSeeds(destRoot);
            WireSceneRefs(network);
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return network;
        }

        public static RoadNetwork EnsureNetwork()
        {
            GameObject root = GameObject.Find(RoadNetwork.RootName);
            if (root == null)
            {
                root = new GameObject(RoadNetwork.RootName);
                GameObject scenarios = GameObject.Find("Scenarios");
                if (scenarios != null) root.transform.SetParent(scenarios.transform);
            }

            var network = root.GetComponent<RoadNetwork>() ?? root.AddComponent<RoadNetwork>();
            return network;
        }

        private static Transform FindDestRoot()
        {
            GameObject dest = GameObject.Find("Traffic_Destinations");
            return dest != null ? dest.transform : null;
        }

        private static void WireSceneRefs(RoadNetwork network)
        {
            var refs = Object.FindObjectOfType<ExperimentRefs>();
            if (refs == null) return;
            refs.campusRoadNetwork = network;
            EditorUtility.SetDirty(refs);

            if (refs.cityTraffic != null)
            {
                var so = new SerializedObject(refs.cityTraffic);
                var prop = so.FindProperty("roadNetwork");
                if (prop != null)
                {
                    prop.objectReferenceValue = network;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(refs.cityTraffic);
                }
            }

            if (refs.intersectionTraffic != null)
            {
                var so = new SerializedObject(refs.intersectionTraffic);
                var prop = so.FindProperty("roadNetwork");
                if (prop != null)
                {
                    prop.objectReferenceValue = network;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(refs.intersectionTraffic);
                }
            }
        }
    }
}
#endif
