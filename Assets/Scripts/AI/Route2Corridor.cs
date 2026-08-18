using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Skybridge / Route 2 street geometry. Independent of NavMesh.
    /// Origin is Dest_67; heading is Dest_67 → Dest_62.
    /// </summary>
    public static class Route2Corridor
    {
        public const float CenterX = 804.2f;
        public const float HalfWidth = 24f;
        public const float RightLaneMeters = 3.2f;
        public static readonly Vector3 Origin = new Vector3(CenterX, 0f, 91.3f);
        public static readonly Vector3 DefaultEnd = new Vector3(700.2f, 0f, 135.5f);

        private static Vector3 s_heading = Vector3.forward;
        private static bool s_headingReady;

        public static Vector3 Heading
        {
            get
            {
                EnsureHeading();
                return s_heading;
            }
        }

        public static void ResetHeading()
        {
            s_headingReady = false;
            s_heading = Vector3.forward;
        }

        public static void EnsureHeading()
        {
            if (s_headingReady) return;

            Vector3 origin = Origin;
            Vector3 target = DefaultEnd;
            var dests = TrafficDestinationSet.Instance;
            if (dests != null)
            {
                Transform start = dests.FindByName("Dest_67");
                Transform next = dests.FindByName("Dest_62");
                if (next == null) next = dests.FindByName("Dest_61");
                if (start != null) origin = start.position;
                if (next != null) target = next.position;
            }

            Vector3 heading = target - origin;
            heading.y = 0f;
            s_heading = heading.sqrMagnitude > 0.01f ? heading.normalized : Vector3.left;
            s_headingReady = true;
        }

        public static Vector3 CenterAt(float z)
        {
            return Origin + Heading * (z - Origin.z);
        }

        public static Vector3 RightLaneAt(float along)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Heading);
            Vector3 point = Origin + Heading * along + right * RightLaneMeters;
            point.y = 1f;
            return point;
        }

        public static float LaneOffset(Vector3 position)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Heading);
            return Vector3.Dot(position - Origin, right);
        }

        public static bool Contains(Vector3 position)
        {
            Vector3 delta = position - Origin;
            delta.y = 0f;
            float along = Vector3.Dot(delta, Heading);
            float lateral = Vector3.Dot(delta, Vector3.Cross(Vector3.up, Heading));
            if (along >= -12f && along <= 650f && Mathf.Abs(lateral) < HalfWidth)
                return true;
            return TrafficDestinationSet.Instance != null
                   && TrafficDestinationSet.Instance.ClosestChainIndex(position) >= 0;
        }
    }
}
