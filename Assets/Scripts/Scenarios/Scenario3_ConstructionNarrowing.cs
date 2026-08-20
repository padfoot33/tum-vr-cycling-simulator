using UnityEngine;
using UnityEngine.AI;
using CyclingExperiment.AI;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Route 2 construction chute at the skybridge street (~723, 128).
    /// Cones stay visual; graph cars use the remaining right-lane edge.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Scenario3_ConstructionNarrowing : MonoBehaviour
    {
        public static readonly Vector3 ChuteCenter = new Vector3(723.15f, 0.2f, 128.08f);
        public static readonly Vector3 ApproachPosition = new Vector3(721.5f, 0.2f, 70f);
        public const float ApproachHeading = 0f;

        public const string CampusRoadNavMeshName = "Campus_Road_NavMesh";
        private const string PropsRootName = "Route2_Construction_Props";
        private static Transform s_propsRoot;

        [SerializeField] private Transform constructionPropsRoot;

        private void Awake()
        {
            if (constructionPropsRoot != null) s_propsRoot = constructionPropsRoot;
            RemoveDemoBusMarker();
            HideLegacyRoute2WaypointPaths();
            EnsureConstructionProps();
            DisableCampusRoadNavMesh();
            StripConstructionNavMeshObstacles();
        }

        public static void RemoveDemoBusMarker()
        {
            // Demo leftover only. One-time scene cleanup, not a gameplay lookup.
            var marker = GameObject.Find("BusOpenSource");
            if (marker == null) marker = GameObject.Find("BusOpenSource(Clone)");
            if (marker == null || marker.transform.parent != null) return;

            Vector3 delta = marker.transform.position - ChuteCenter;
            delta.y = 0f;
            if (delta.sqrMagnitude < 20f * 20f)
            {
                Debug.Log("[Scenario2] Removing demo BusOpenSource marker at Route 2 chute.");
                if (Application.isPlaying) UnityEngine.Object.Destroy(marker);
                else UnityEngine.Object.DestroyImmediate(marker);
            }
        }

        public static void HideLegacyRoute2WaypointPaths()
        {
            HideByName("Path_Route2_Northbound");
            HideByName("Path_Route2_Southbound");
        }

        public static void EnsureConstructionProps()
        {
            if (s_propsRoot != null) return;

            var scenario2 = GameObject.Find("Scenario_2");
            if (scenario2 == null)
            {
                var scenarios = GameObject.Find("Scenarios");
                scenario2 = new GameObject("Scenario_2");
                if (scenarios != null) scenario2.transform.SetParent(scenarios.transform);
            }

            var existing = scenario2.transform.Find(PropsRootName);
            if (existing != null)
            {
                s_propsRoot = existing;
                return;
            }

            var props = new GameObject(PropsRootName);
            props.transform.SetParent(scenario2.transform);
            s_propsRoot = props.transform;

            int coneIndex = 0;
            for (float z = 108f; z <= 152f; z += 7f)
            {
                CreateCone(props.transform, $"Cone_{coneIndex++}", LeftLaneWorld(z, 0.05f));
            }

            CreateBarrier(props.transform, "Barrier_South", LeftLaneWorld(106f, 0.45f));
            CreateBarrier(props.transform, "Barrier_North", LeftLaneWorld(154f, 0.45f));
        }

        public static void MoveRoute2PropsToLeftLane()
        {
            if (s_propsRoot == null) return;
            foreach (Transform child in s_propsRoot)
            {
                if (child == null) continue;
                child.position = LeftLaneWorld(child.position.z, child.position.y);
            }
        }

        private static Vector3 LeftLaneWorld(float z, float y)
        {
            Vector3 left = -Vector3.Cross(Vector3.up, Route2Corridor.Heading);
            Vector3 pos = Route2Corridor.CenterAt(z) + left * 3.6f;
            pos.y = y;
            return pos;
        }

        private static void HideByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go != null) go.SetActive(false);
        }

        private static void CreateCone(Transform parent, string name, Vector3 position)
        {
            var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = name;
            cone.transform.SetParent(parent);
            cone.transform.position = position;
            cone.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
            TintRenderer(cone, new Color(1f, 0.45f, 0.08f));
        }

        private static void CreateBarrier(Transform parent, string name, Vector3 position)
        {
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = name;
            barrier.transform.SetParent(parent);
            barrier.transform.position = position;
            barrier.transform.localScale = new Vector3(0.4f, 0.9f, 1.6f);
            TintRenderer(barrier, new Color(0.95f, 0.95f, 0.92f));
        }

        public static int DisableCampusRoadNavMesh()
        {
            var navMesh = GameObject.Find(CampusRoadNavMeshName);
            if (navMesh == null || !navMesh.activeSelf)
                return 0;

            navMesh.SetActive(false);
            return 1;
        }

        public static int StripConstructionNavMeshObstacles()
        {
            int removed = 0;
            removed += StripObstaclesUnder(GameObject.Find("Scenario_2"));
            if (s_propsRoot != null)
                removed += StripObstaclesUnder(s_propsRoot.gameObject);
            else
                removed += StripObstaclesUnder(GameObject.Find(PropsRootName));
            return removed;
        }

        private static int StripObstaclesUnder(GameObject root)
        {
            if (root == null) return 0;

            int removed = 0;
            var obstacles = root.GetComponentsInChildren<NavMeshObstacle>(true);
            for (int i = 0; i < obstacles.Length; i++)
            {
                NavMeshObstacle obstacle = obstacles[i];
                if (obstacle == null || !ShouldStripObstacle(obstacle.transform)) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(obstacle);
                else UnityEngine.Object.DestroyImmediate(obstacle);
                removed++;
            }

            return removed;
        }

        public static int AddObstaclesToCampusConstructionProps()
        {
            return StripConstructionNavMeshObstacles();
        }

        private static bool ShouldStripObstacle(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                if (cur.name == PropsRootName || cur.name == "Scenario_2") return true;
                if (IsConstructionPropName(cur.name)) return true;
                cur = cur.parent;
            }

            return false;
        }

        private static bool IsConstructionPropName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            return objectName.IndexOf("ChevronSign", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || objectName.IndexOf("Dumpster", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || objectName.IndexOf("Skip", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || objectName.IndexOf("Waste", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || objectName.StartsWith("Cone_", System.StringComparison.Ordinal)
                   || objectName.StartsWith("Barrier_", System.StringComparison.Ordinal);
        }

        private static void TintRenderer(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            var mat = renderer.material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else             if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
    }
}
