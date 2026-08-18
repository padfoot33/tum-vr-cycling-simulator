using UnityEngine;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Union of child trigger boxes that define a route's allowed riding space.
    /// Resize the children in the Scene view (cyan wire gizmos).
    /// </summary>
    public class PlayAreaBounds : MonoBehaviour
    {
        public const string Route1RootName = "Route1_PlayArea";
        public const string Route2RootName = "Route2_PlayArea";

        [SerializeField] private BoxCollider[] volumes;

        private const float SurfaceEpsilon = 0.05f;

        private void Awake()
        {
            RefreshVolumes();
        }

        private void OnTransformChildrenChanged()
        {
            RefreshVolumes();
        }

        public void RefreshVolumes()
        {
            volumes = GetComponentsInChildren<BoxCollider>(true);
        }

        public bool Contains(Vector3 worldPoint)
        {
            if (volumes == null) RefreshVolumes();
            for (int i = 0; i < volumes.Length; i++)
            {
                if (PointInBox(volumes[i], worldPoint, SurfaceEpsilon))
                    return true;
            }

            return false;
        }

        public Vector3 ClosestPointOnUnion(Vector3 worldPoint)
        {
            if (volumes == null) RefreshVolumes();

            Vector3 best = worldPoint;
            float bestDist = float.MaxValue;
            bool any = false;
            for (int i = 0; i < volumes.Length; i++)
            {
                BoxCollider box = volumes[i];
                if (box == null) continue;
                Vector3 p = box.ClosestPoint(worldPoint);
                float d = (p - worldPoint).sqrMagnitude;
                if (!any || d < bestDist)
                {
                    best = p;
                    bestDist = d;
                    any = true;
                }
            }

            return any ? best : worldPoint;
        }

        private void OnDrawGizmos()
        {
            BoxCollider[] boxes = GetComponentsInChildren<BoxCollider>(true);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.85f);
            for (int i = 0; i < boxes.Length; i++)
            {
                BoxCollider box = boxes[i];
                if (box == null) continue;
                Matrix4x4 matrix = box.transform.localToWorldMatrix;
                Gizmos.matrix = matrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        public static PlayAreaBounds FindOrCreateRoute1(ExperimentRefs refs)
        {
            Transform parent = refs != null && refs.route1 != null ? refs.route1.transform : null;
            PlayAreaBounds existing = FindNamed(Route1RootName, parent);
            if (existing != null) return existing;

            GameObject root = CreateRoot(Route1RootName, parent);

            Vector3 spawn = refs != null && refs.route1CyclistSpawn != null
                ? refs.route1CyclistSpawn.position
                : new Vector3(436.1f, 0.2f, -80f);
            Vector3 bus = refs != null && refs.busStopTrigger != null
                ? refs.busStopTrigger.position
                : spawn + Vector3.forward * 40f;
            Vector3 rightTurn = FindRightTurnTriggerPosition(spawn);

            AddCorridorBox(root.transform, "Box_SpawnToBus", spawn, bus, 28f, 24f);
            AddCorridorBox(root.transform, "Box_BusToRightTurn", bus, rightTurn, 32f, 30f);

            var bounds = root.AddComponent<PlayAreaBounds>();
            bounds.RefreshVolumes();
            return bounds;
        }

        public static PlayAreaBounds FindOrCreateRoute2(ExperimentRefs refs)
        {
            GameObject scenario2 = GameObject.Find("Scenario_2");
            Transform parent = scenario2 != null ? scenario2.transform : null;
            PlayAreaBounds existing = FindNamed(Route2RootName, parent);
            if (existing != null) return existing;

            GameObject root = CreateRoot(Route2RootName, parent);

            Transform spawn = refs != null ? refs.route2CyclistSpawn : null;
            Vector3 a = spawn != null ? spawn.position : Scenario3_ConstructionNarrowing.ApproachPosition;
            Vector3 forward = spawn != null ? Flatten(spawn.forward) : Vector3.forward;
            Vector3 chute = Scenario3_ConstructionNarrowing.ChuteCenter;
            Vector3 far = a + forward * 140f;

            AddCorridorBox(root.transform, "Box_SkybridgeStreet", a, far, 28f, 20f);
            if (HorizontalDistance(a, chute) > 8f && HorizontalDistance(far, chute) > 8f)
                AddCorridorBox(root.transform, "Box_ConstructionChute", a, chute, 24f, 40f);

            var bounds = root.AddComponent<PlayAreaBounds>();
            bounds.RefreshVolumes();
            return bounds;
        }

        private static PlayAreaBounds FindNamed(string name, Transform parent)
        {
            if (parent != null)
            {
                Transform child = parent.Find(name);
                if (child != null)
                    return child.GetComponent<PlayAreaBounds>() ?? child.gameObject.AddComponent<PlayAreaBounds>();
            }

            var go = GameObject.Find(name);
            if (go == null) return null;
            return go.GetComponent<PlayAreaBounds>() ?? go.AddComponent<PlayAreaBounds>();
        }

        private static GameObject CreateRoot(string name, Transform parent)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            return root;
        }

        private static Vector3 FindRightTurnTriggerPosition(Vector3 fallback)
        {
            var trigger = GameObject.Find("Trigger_Scenario1_RightTurn");
            return trigger != null ? trigger.transform.position : fallback + new Vector3(80f, 0f, 180f);
        }

        public static void AddCorridorBox(Transform parent, string name, Vector3 a, Vector3 b, float width, float extraLength)
        {
            Vector3 flat = Flatten(b - a);
            float length = flat.magnitude;
            if (length < 4f)
            {
                length = 4f;
                if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            }

            Vector3 mid = (a + b) * 0.5f;
            mid.y = Mathf.Max(a.y, b.y) + 4f;

            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = mid;
            go.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(width, 16f, length + extraLength);
        }

        private static bool PointInBox(BoxCollider box, Vector3 worldPoint, float pad)
        {
            if (box == null) return false;
            Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x + pad
                   && Mathf.Abs(local.y) <= half.y + pad
                   && Mathf.Abs(local.z) <= half.z + pad;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
