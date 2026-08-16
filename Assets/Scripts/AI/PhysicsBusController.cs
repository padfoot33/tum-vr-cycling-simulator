using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Realistic Physics & Kinematics Bus Controller for Scenario 1.
    /// Handles smooth acceleration, overtaking past the cyclist, smooth deceleration/braking,
    /// turning into the bus bay, and permanently parking at the bus stand (no disappearing).
    /// </summary>
    public class PhysicsBusController : MonoBehaviour
    {
        [Header("Path & Navigation")]
        [SerializeField, Tooltip("The waypoint route for the bus to follow")]
        private WaypointPath path;

        [SerializeField, Tooltip("Index of the current waypoint")]
        private int currentWaypointIndex = 0;

        [SerializeField, Tooltip("Distance threshold to consider waypoint reached")]
        private float waypointThreshold = 3.0f;

        [Header("Speed & Physics")]
        [SerializeField, Tooltip("Target cruising / overtaking speed in m/s (11 m/s = ~40 km/h)")]
        private float targetSpeed = 11.0f;

        [SerializeField, Tooltip("Acceleration rate in m/s^2")]
        private float acceleration = 3.5f;

        [SerializeField, Tooltip("Braking deceleration rate in m/s^2")]
        private float brakeDeceleration = 4.5f;

        [SerializeField, Tooltip("Steering rotation responsiveness")]
        private float turnResponse = 3.5f;

        [Header("Bus Stop & Parking")]
        [SerializeField, Tooltip("Stay permanently parked at the last waypoint")]
        private bool stayParkedAtBusStop = true;

        [SerializeField, Tooltip("Distance before last waypoint to start braking for the stop")]
        private float brakeDistanceToStop = 25.0f;

        [Header("State")]
        [SerializeField] private float currentSpeed = 0f;
        [SerializeField] private bool isParked = false;

        public WaypointPath Path
        {
            get => path;
            set => path = value;
        }

        public float TargetSpeed
        {
            get => targetSpeed;
            set => targetSpeed = value;
        }

        public bool IsParked => isParked;
        public float CurrentSpeedKph => currentSpeed * 3.6f;

        // Events
        public event System.Action OnBusStopReached;

        private void Start()
        {
            if (path != null && path.WaypointCount > 0)
            {
                transform.position = path.GetWaypoint(0);
                if (path.WaypointCount > 1)
                {
                    Vector3 dir = (path.GetWaypoint(1) - transform.position).normalized;
                    if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        private void FixedUpdate()
        {
            if (path == null || path.WaypointCount == 0) return;
            if (isParked) return;

            int lastIdx = path.WaypointCount - 1;
            Vector3 targetWp = path.GetWaypoint(currentWaypointIndex);
            Vector3 toTarget = targetWp - transform.position;
            toTarget.y = 0; // Flat horizontal plane
            float distToCurrentWp = toTarget.magnitude;

            // Check distance to the final bus stop waypoint
            Vector3 finalStopPos = path.GetWaypoint(lastIdx);
            float distToFinalStop = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(finalStopPos.x, 0, finalStopPos.z));

            // Determine desired speed
            float desiredSpeed = targetSpeed;

            // If approaching the last waypoint, decelerate smoothly to park
            if (currentWaypointIndex >= lastIdx - 1 || distToFinalStop < brakeDistanceToStop)
            {
                // Smooth deceleration curve towards final stop
                float stopRatio = Mathf.Clamp01(distToFinalStop / Mathf.Max(5f, brakeDistanceToStop));
                desiredSpeed = targetSpeed * Mathf.Pow(stopRatio, 1.2f);

                // If very close to final stop and at the last waypoint, come to complete halt
                if (currentWaypointIndex >= lastIdx && distToCurrentWp <= waypointThreshold)
                {
                    desiredSpeed = 0f;
                }
            }

            // Smoothly accelerate or brake toward desired speed
            if (currentSpeed < desiredSpeed)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.fixedDeltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, brakeDeceleration * Time.fixedDeltaTime);
            }

            // Move bus along its current forward direction
            if (currentSpeed > 0.05f)
            {
                // Steering: rotate toward current waypoint
                if (toTarget != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnResponse * Time.fixedDeltaTime);
                }

                transform.position += transform.forward * (currentSpeed * Time.fixedDeltaTime);
            }
            else if (currentWaypointIndex >= lastIdx && desiredSpeed == 0f)
            {
                // Bus is now parked at the bus stop!
                currentSpeed = 0f;
                isParked = true;
                Debug.Log("[PhysicsBusController] Bus successfully parked at the bus stand.");
                OnBusStopReached?.Invoke();
                return;
            }

            // Advance waypoint when reached
            if (distToCurrentWp <= waypointThreshold && currentWaypointIndex < lastIdx)
            {
                currentWaypointIndex++;
            }
        }
    }
}
