using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Route 2 construction chute at the skybridge street (~723, 128).
    /// No event trigger and no waypoint lines: cones carve the road NavMesh so city cars share the remaining lane.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Scenario3_ConstructionNarrowing : MonoBehaviour
    {
        public static readonly Vector3 ChuteCenter = new Vector3(723.15f, 0.2f, 128.08f);
        public static readonly Vector3 ApproachPosition = new Vector3(721.5f, 0.2f, 70f);
        public const float ApproachHeading = 0f;

        private const string PropsRootName = "Route2_Construction_Props";
        private static Transform s_propsRoot;

        [SerializeField] private Transform constructionPropsRoot;

        private void Awake()
        {
            if (constructionPropsRoot != null) s_propsRoot = constructionPropsRoot;
            RemoveDemoBusMarker();
            HideLegacyRoute2WaypointPaths();
            EnsureConstructionProps();
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
                if (Application.isPlaying) Object.Destroy(marker);
                else Object.DestroyImmediate(marker);
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
                CreateCone(props.transform, $"Cone_{coneIndex++}", new Vector3(727.2f, 0.05f, z));
            }

            CreateBarrier(props.transform, "Barrier_South", new Vector3(727.6f, 0.45f, 106f));
            CreateBarrier(props.transform, "Barrier_North", new Vector3(727.6f, 0.45f, 154f));
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
            AddCarveObstacle(cone, new Vector3(0.5f, 1f, 0.5f));
        }

        private static void CreateBarrier(Transform parent, string name, Vector3 position)
        {
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = name;
            barrier.transform.SetParent(parent);
            barrier.transform.position = position;
            barrier.transform.localScale = new Vector3(0.4f, 0.9f, 1.6f);
            TintRenderer(barrier, new Color(0.95f, 0.95f, 0.92f));
            AddCarveObstacle(barrier, new Vector3(0.8f, 1.2f, 2.0f));
        }

        private static void AddCarveObstacle(GameObject obj, Vector3 size)
        {
            var obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = obj.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = size;
        }

        private static void TintRenderer(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            var mat = renderer.material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
    }
}
