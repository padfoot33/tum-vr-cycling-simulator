using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Shared spawn helper: disable SUMO controllers, freeze physics, attach waypoint or NavMesh AI.
    /// </summary>
    public static class VehicleRuntimeFactory
    {
        public struct SpawnSettings
        {
            public WaypointPath Path;
            public float Speed;
            public bool DestroyAtEnd;
            public bool IsExperimentStressVehicle;
            public bool StopSmoothlyAtPathEnd;
            public bool PreserveSpawnPosition;
            public int StartWaypointIndex;
            public string Name;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, SpawnSettings settings)
        {
            return SpawnOnWaypointPath(prefab, position, rotation, settings);
        }

        /// <summary>
        /// Scripted scenario vehicle: follow a fixed waypoint path. No city-traffic SmartVehicleAI.
        /// </summary>
        public static GameObject SpawnOnWaypointPath(GameObject prefab, Vector3 position, Quaternion rotation, SpawnSettings settings)
        {
            if (prefab == null) return null;

            GameObject vehicle = Object.Instantiate(prefab, position, rotation);
            if (!string.IsNullOrEmpty(settings.Name))
            {
                vehicle.name = settings.Name;
            }

            Prepare(vehicle);

            var smart = vehicle.GetComponent<SmartVehicleAI>();
            if (smart != null) smart.enabled = false;

            var nav = vehicle.GetComponent<NavMeshVehicleAI>();
            if (nav != null) nav.enabled = false;

            var agent = vehicle.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            var follower = GetOrAdd<WaypointFollower>(vehicle);
            follower.enabled = true;
            follower.Path = settings.Path;
            follower.Speed = settings.Speed;
            follower.DestroyAtEnd = settings.DestroyAtEnd;
            follower.PreserveSpawnPosition = settings.PreserveSpawnPosition;

            return vehicle;
        }

        public static GameObject SpawnOnNavMesh(GameObject prefab, Vector3 position, Quaternion rotation, float speed, string name)
        {
            if (prefab == null) return null;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                position = hit.position;
            }

            GameObject vehicle = Object.Instantiate(prefab, position, rotation);
            if (!string.IsNullOrEmpty(name)) vehicle.name = name;

            Prepare(vehicle, disableRigidbody: true);

            var waypointAi = vehicle.GetComponent<SmartVehicleAI>();
            if (waypointAi != null) waypointAi.enabled = false;

            var agent = GetOrAdd<NavMeshAgent>(vehicle);
            agent.height = 1.0f;
            agent.baseOffset = EstimateBaseOffset(vehicle, groundBias: 0f);
            var ai = GetOrAdd<NavMeshVehicleAI>(vehicle);
            ai.CruiseSpeed = speed;
            ai.IsExperimentStressVehicle = false;
            ai.BindAgent(agent);

            if (NavMesh.SamplePosition(position, out hit, 16f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            ai.AssignRoadCorridorRoute();
            return vehicle;
        }

        public static void Prepare(GameObject vehicle, bool disableRigidbody = false)
        {
            if (vehicle == null) return;

            DisableNamed(vehicle, "TaxiController");
            DisableNamed(vehicle, "CarController");
            DisableNamed(vehicle, "BusController");

            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (disableRigidbody)
                {
                    Object.Destroy(rb);
                }
                else
                {
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.detectCollisions = true;
                }
            }

            var follower = vehicle.GetComponent<WaypointFollower>();
            if (follower != null) follower.enabled = false;

            var physicsBus = vehicle.GetComponent<PhysicsBusController>();
            if (physicsBus != null) physicsBus.enabled = false;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }

        private static float EstimateBaseOffset(GameObject vehicle, float groundBias)
        {
            float minY = float.MaxValue;
            var renderers = vehicle.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) minY = Mathf.Min(minY, renderers[i].bounds.min.y);
            }

            if (minY > 999f) return 0.05f;
            float offset = vehicle.transform.position.y - minY - groundBias;
            return Mathf.Clamp(offset, 0f, 1.2f);
        }

        private static void DisableNamed(GameObject vehicle, string typeName)
        {
            var behaviour = vehicle.GetComponent(typeName) as MonoBehaviour;
            if (behaviour != null) behaviour.enabled = false;
        }
    }
}
