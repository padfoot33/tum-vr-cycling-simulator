using System.Collections.Generic;
using UnityEngine;

namespace CyclingExperiment.Logging
{
    /// <summary>
    /// Logs close-pass markers for any registered vehicle near the cyclist.
    /// Vehicles are registered at spawn via <see cref="TrackedTrafficVehicle"/>.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class VehicleInteractionTracker : MonoBehaviour
    {
        private static readonly List<TrackedTrafficVehicle> Active = new List<TrackedTrafficVehicle>(64);

        [SerializeField] private float closePassRadius = 6f;
        [SerializeField] private float maxLateral = 4f;
        [SerializeField] private float aheadMin = -2f;
        [SerializeField] private float aheadMax = 12f;
        [SerializeField] private float reenterCooldown = 2f;

        private Transform _bike;
        private TrackedTrafficVehicle _activeVehicle;
        private string _lastRunId = "";
        private readonly Dictionary<int, float> _cooldownUntil = new Dictionary<int, float>(32);

        public static void Register(TrackedTrafficVehicle vehicle)
        {
            if (vehicle == null) return;
            if (!Active.Contains(vehicle))
                Active.Add(vehicle);
        }

        public static void Unregister(TrackedTrafficVehicle vehicle)
        {
            if (vehicle == null) return;
            Active.Remove(vehicle);
        }

        private void LateUpdate()
        {
            var logger = ExperimentRunLogger.Instance;
            if (logger == null || !logger.IsLogging)
                return;
            
            if (_lastRunId != logger.RunId)
            {
                _lastRunId = logger.RunId;
                _activeVehicle = null;
                _cooldownUntil.Clear();
            }

            if (_bike == null)
            {
                var refs = ExperimentSceneRefs.Instance;
                _bike = refs != null ? refs.bicycleTransform : null;
            }

            if (_bike == null)
                return;

            Prune();
            TrackedTrafficVehicle closest = FindClosestPass(out float speedKph);
            if (closest != null)
            {
                if (_activeVehicle != closest)
                {
                    if (_activeVehicle != null)
                        EndPass(logger, _activeVehicle);
                    _activeVehicle = closest;
                    logger.MarkEvent("CLOSE_PASS");
                    logger.SetClosePassEvent("CLOSE_PASS");
                }

                if (!logger.HasScriptedEvent)
                {
                    Vector3 p = closest.transform.position;
                    logger.UpdateEventVehicleData(p.x, p.z, speedKph);
                }
            }
            else if (_activeVehicle != null)
            {
                EndPass(logger, _activeVehicle);
                _activeVehicle = null;
                if (!logger.HasScriptedEvent)
                    logger.ClearEventVehicleData();
            }
        }

        private void EndPass(ExperimentRunLogger logger, TrackedTrafficVehicle vehicle)
        {
            logger.MarkEvent("CLOSE_PASS_END");
            logger.SetClosePassEvent("NONE");
            if (vehicle != null)
                _cooldownUntil[vehicle.GetInstanceID()] = Time.time + reenterCooldown;
        }

        private TrackedTrafficVehicle FindClosestPass(out float speedKph)
        {
            speedKph = float.NaN;
            TrackedTrafficVehicle best = null;
            float bestDist = closePassRadius;
            Vector3 bikePos = _bike.position;
            Vector3 forward = _bike.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            else
                forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            for (int i = 0; i < Active.Count; i++)
            {
                TrackedTrafficVehicle vehicle = Active[i];
                if (vehicle == null || !vehicle.gameObject.activeInHierarchy)
                    continue;

                int id = vehicle.GetInstanceID();
                if (_cooldownUntil.TryGetValue(id, out float until) && Time.time < until)
                    continue;

                Vector3 to = vehicle.transform.position - bikePos;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > closePassRadius || dist < 0.15f)
                    continue;

                float ahead = Vector3.Dot(to, forward);
                float lateral = Mathf.Abs(Vector3.Dot(to, right));
                if (ahead < aheadMin || ahead > aheadMax || lateral > maxLateral)
                    continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = vehicle;
                    speedKph = vehicle.SpeedKph;
                }
            }

            return best;
        }

        private static void Prune()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i] == null)
                    Active.RemoveAt(i);
            }
        }
    }
}
