#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

namespace CyclingExperiment.Editor
{
    /// <summary>
    /// Bakes a car NavMesh from TUM_Campus_Container road meshes only, then hides the wrong yellow waypoint paths.
    /// </summary>
    public static class RoadNavMeshBaker
    {
        public const string SurfaceObjectName = "Campus_Road_NavMesh";
        private const string NavMeshAssetPath = "Assets/Settings/Campus_Road_NavMesh.asset";

        [MenuItem("Cycling Experiment/Bake Road NavMesh", false, 5)]
        public static void BakeRoadNavMesh()
        {
            GameObject campus = GameObject.Find("TUM_Campus_Container");
            if (campus == null)
            {
                EditorUtility.DisplayDialog("Bake Road NavMesh", "Could not find TUM_Campus_Container in the scene.", "OK");
                return;
            }

            List<MeshFilter> roads = CollectRoadMeshes(campus.transform);
            if (roads.Count == 0)
            {
                EditorUtility.DisplayDialog("Bake Road NavMesh",
                    "No road meshes found. Expected child names containing Roads, Road, or Asphalt.", "OK");
                return;
            }

            var sources = new List<NavMeshBuildSource>(roads.Count);
            var worldBounds = new Bounds(roads[0].transform.position, Vector3.one);
            foreach (var mf in roads)
            {
                if (mf.sharedMesh == null) continue;

                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    sourceObject = mf.sharedMesh,
                    transform = mf.transform.localToWorldMatrix,
                    area = 0
                });

                worldBounds.Encapsulate(mf.GetComponent<Renderer>() != null
                    ? mf.GetComponent<Renderer>().bounds
                    : new Bounds(mf.transform.position, Vector3.one * 10f));
            }

            if (sources.Count == 0)
            {
                EditorUtility.DisplayDialog("Bake Road NavMesh", "Road meshes had no usable mesh data.", "OK");
                return;
            }

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 1.1f;
            settings.agentHeight = 2f;
            settings.agentSlope = 15f;
            settings.agentClimb = 0.4f;
            settings.minRegionArea = 4f;

            var data = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                worldBounds,
                Vector3.zero,
                Quaternion.identity);

            if (data == null)
            {
                EditorUtility.DisplayDialog("Bake Road NavMesh", "NavMesh bake failed.", "OK");
                return;
            }

            data.name = "Campus_Road_NavMesh";
            Directory.CreateDirectory(Path.GetDirectoryName(NavMeshAssetPath) ?? "Assets/Settings");
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(data, existing);
                data = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(data, NavMeshAssetPath);
            }

            GameObject surfaceObj = GameObject.Find(SurfaceObjectName);
            if (surfaceObj == null) surfaceObj = new GameObject(SurfaceObjectName);

            var surface = surfaceObj.GetComponent<NavMeshSurface>() ?? surfaceObj.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.2f;
            surface.RemoveData();
            surface.navMeshData = data;
            surface.AddData();

            HideWrongCityWaypointPaths();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[CyclingExperiment] Baked road NavMesh from {sources.Count} road meshes. Yellow city waypoint paths hidden.");
            EditorUtility.DisplayDialog("Bake Road NavMesh",
                $"Baked NavMesh from {sources.Count} road meshes under TUM_Campus_Container.\n\n" +
                "Wrong yellow City_Traffic_Paths gizmos are now hidden.\n" +
                "Route 1 Bus_Overtake_Path and RightTurn_Overtaking_Car_Path were left alone.\n\n" +
                "Blue NavMesh overlay should sit on roads only. Then press Play.",
                "OK");
        }

        public static void HideWrongCityWaypointPaths()
        {
            var city = GameObject.Find("City_Traffic_Paths");
            if (city != null) city.SetActive(false);

            HideByName("Path_Route2_Northbound");
            HideByName("Path_Route2_Southbound");
            HideByName("Path_Traffic_EastToWest");
            HideByName("Path_Traffic_WestToEast");
            HideByName("Path_Traffic_SouthToNorth");
            HideByName("Path_Traffic_NorthToSouth");
        }

        private static void HideByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go != null) go.SetActive(false);
        }

        public static List<MeshFilter> CollectRoadMeshes(Transform campus)
        {
            var roads = new List<MeshFilter>();
            var filters = campus.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (IsRoadName(mf.gameObject.name) || IsRoadName(mf.transform.parent != null ? mf.transform.parent.name : string.Empty))
                {
                    roads.Add(mf);
                }
            }

            return roads;
        }

        private static bool IsRoadName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Roads", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Asphalt", StringComparison.OrdinalIgnoreCase) >= 0
                   || (name.IndexOf("Road", StringComparison.OrdinalIgnoreCase) >= 0
                       && name.IndexOf("Railroad", StringComparison.OrdinalIgnoreCase) < 0);
        }
    }
}
#endif
