using System;
using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Moves a GameObject along a WaypointPath smoothly.
    /// Can either destroy the object at path end, or stay parked cleanly at the final destination.
    /// </summary>
    public class WaypointFollower : MonoBehaviour
    {
        [Header("Path Settings")]
        [Tooltip("The path to follow.")]
        [SerializeField] private WaypointPath path;

        [Tooltip("Movement speed in m/s (15 m/s = 54 km/h).")]
        [SerializeField] private float speed = 15f;

        [Tooltip("Distance to waypoint to consider it reached.")]
        [SerializeField] private float waypointThreshold = 2.5f;

        [Tooltip("Smooth turn rate.")]
        [SerializeField] private float rotationSpeed = 6f;

        [Tooltip("Destroy object when reaching the last waypoint on a non-looping path. If false, stays parked at final stop.")]
        [SerializeField] private bool destroyAtEnd = false;

        [SerializeField] private bool preserveSpawnPosition;

        /// <summary>
        /// Fires when a waypoint is reached with its index.
        /// </summary>
        public event Action<int> OnWaypointReached;

        /// <summary>
        /// Fires when the path ends (non-looping only).
        /// </summary>
        public event Action OnPathComplete;

        private int _currentWaypointIndex = 0;
        private bool _isMoving = true;
        private bool _isAtEnd = false;

        public WaypointPath Path
        {
            get => path;
            set => path = value;
        }

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        public bool DestroyAtEnd
        {
            get => destroyAtEnd;
            set => destroyAtEnd = value;
        }

        public bool PreserveSpawnPosition
        {
            get => preserveSpawnPosition;
            set => preserveSpawnPosition = value;
        }

        public bool IsAtEnd => _isAtEnd;

        private void Start()
        {
            if (path == null || path.WaypointCount == 0) return;

            if (!preserveSpawnPosition)
            {
                transform.position = path.GetWaypoint(0);
                _currentWaypointIndex = 0;
            }

            if (path.WaypointCount > 1)
            {
                int lookIndex = Mathf.Clamp(_currentWaypointIndex + (preserveSpawnPosition ? 0 : 1), 0, path.WaypointCount - 1);
                Vector3 dir = (path.GetWaypoint(lookIndex) - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude < 0.01f && lookIndex + 1 < path.WaypointCount)
                {
                    dir = path.GetWaypoint(lookIndex + 1) - path.GetWaypoint(lookIndex);
                    dir.y = 0;
                }
                if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }

        private void Update()
        {
            if (!_isMoving || _isAtEnd || path == null || path.WaypointCount == 0) return;

            Vector3 targetPosition = path.GetWaypoint(_currentWaypointIndex);
            Vector3 direction = (targetPosition - transform.position);
            direction.y = 0; // Flat movement
            float distance = direction.magnitude;

            if (distance > 0.05f)
            {
                Vector3 moveDir = direction.normalized;
                transform.position += moveDir * (speed * Time.deltaTime);

                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Check if waypoint reached
            if (distance <= waypointThreshold)
            {
                OnWaypointReached?.Invoke(_currentWaypointIndex);

                _currentWaypointIndex++;
                if (_currentWaypointIndex >= path.WaypointCount)
                {
                    if (path.isLoop)
                    {
                        _currentWaypointIndex = 0;
                    }
                    else
                    {
                        _isAtEnd = true;
                        _isMoving = false;
                        Debug.Log($"[WaypointFollower] {gameObject.name} reached destination and parked.");
                        OnPathComplete?.Invoke();
                        
                        if (destroyAtEnd)
                        {
                            Destroy(gameObject);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Pauses movement.
        /// </summary>
        public void Pause()
        {
            _isMoving = false;
        }

        /// <summary>
        /// Resumes movement.
        /// </summary>
        public void Resume()
        {
            _isMoving = true;
        }

        /// <summary>
        /// Sets the movement speed.
        /// </summary>
        /// <param name="newSpeed">Speed in m/s.</param>
        public void SetSpeed(float newSpeed)
        {
            speed = newSpeed;
        }

        /// <summary>
        /// Gets the current waypoint index.
        /// </summary>
        public int GetCurrentWaypointIndex()
        {
            return _currentWaypointIndex;
        }
    }
}
