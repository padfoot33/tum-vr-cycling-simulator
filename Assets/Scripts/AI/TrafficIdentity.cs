using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Shared cyclist / vehicle checks. Uses ExperimentSceneRefs, not scene search.
    /// </summary>
    public static class TrafficIdentity
    {
        public static Transform Cyclist
        {
            get
            {
                var refs = ExperimentSceneRefs.Instance;
                return refs != null ? refs.bicycleTransform : null;
            }
        }

        public static bool IsCyclist(Collider collider)
        {
            if (collider == null) return false;

            Transform bike = Cyclist;
            if (bike != null && (collider.transform == bike || collider.transform.IsChildOf(bike)))
            {
                return true;
            }

            Transform root = collider.transform.root;
            if (root.GetComponent<BikeURP.BicyclePhysicsController>() != null) return true;

            string name = root.name;
            return name.IndexOf("bicyle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("bicycle", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsVehicle(Collider collider)
        {
            if (collider == null) return false;

            string name = collider.transform.root.name;
            return name.StartsWith("CityTraffic_") ||
                   name.StartsWith("TrafficFlow_") ||
                   name.StartsWith("Scenario1_") ||
                   name.IndexOf("car", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("bus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("taxi", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static float SpeedForPointAhead(
            Vector3 selfPosition,
            Vector3 selfForward,
            Vector3 obstacle,
            float maxLateral,
            float lookAhead,
            float stopBuffer,
            float cruiseSpeed)
        {
            Vector3 to = obstacle - selfPosition;
            to.y = 0f;
            selfForward.y = 0f;
            if (selfForward.sqrMagnitude < 0.01f) return cruiseSpeed;
            selfForward.Normalize();

            float ahead = Vector3.Dot(to, selfForward);
            float lateral = Mathf.Abs(Vector3.Dot(to, Vector3.Cross(Vector3.up, selfForward)));
            if (ahead < -1.2f || ahead > lookAhead || lateral > maxLateral) return cruiseSpeed;
            if (ahead <= stopBuffer) return 0f;

            float available = Mathf.Max(0.01f, lookAhead - stopBuffer);
            return cruiseSpeed * Mathf.Clamp01((ahead - stopBuffer) / available);
        }
    }
}
