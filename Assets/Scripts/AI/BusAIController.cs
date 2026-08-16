using UnityEngine;
using CyclingExperiment.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Bus-specific AI controller that extends WaypointFollower behaviour 
    /// with bus stop dwelling and overtaking manoeuvres.
    /// Attach this alongside or instead of WaypointFollower for bus-specific scenarios.
    /// </summary>
    [RequireComponent(typeof(WaypointFollower))]
    public class BusAIController : MonoBehaviour
    {
        [Header("Bus Stop Behaviour")]
        [SerializeField, Tooltip("Index of the waypoint where the bus should stop (bus stop location)")]
        private int _busStopWaypointIndex = -1;

        [SerializeField, Tooltip("How long the bus dwells at the bus stop (seconds)")]
        private float _dwellTime = 5f;

        [Header("Overtake Behaviour")]
        [SerializeField, Tooltip("Reference to the player/cyclist transform for proximity detection")]
        private Transform _playerTransform;

        [SerializeField, Tooltip("Speed boost when overtaking the cyclist (multiplier)")]
        private float _overtakeSpeedMultiplier = 1.3f;

        [SerializeField, Tooltip("Distance at which the bus considers it has passed the cyclist")]
        private float _overtakeCompleteDistance = 15f;

        [Header("Audio (Optional)")]
        [SerializeField, Tooltip("Engine audio source")]
        private AudioSource _engineAudio;

        [SerializeField, Tooltip("Horn audio clip")]
        private AudioClip _hornClip;

        // Internal state
        private WaypointFollower _follower;
        private float _baseSpeed;
        private bool _isDwelling;
        private float _dwellTimer;
        private bool _isOvertaking;
        private bool _hasOvertaken;

        /// <summary>
        /// Whether the bus has completed its overtaking manoeuvre past the cyclist.
        /// </summary>
        public bool HasOvertaken => _hasOvertaken;

        /// <summary>
        /// Whether the bus is currently dwelling at a bus stop.
        /// </summary>
        public bool IsDwelling => _isDwelling;

        /// <summary>
        /// Fired when the bus begins overtaking the cyclist.
        /// </summary>
        public event System.Action OnOvertakeStarted;

        /// <summary>
        /// Fired when the bus has passed the cyclist.
        /// </summary>
        public event System.Action OnOvertakeCompleted;

        /// <summary>
        /// Fired when the bus arrives at the bus stop.
        /// </summary>
        public event System.Action OnBusStopArrived;

        /// <summary>
        /// Fired when the bus departs from the bus stop.
        /// </summary>
        public event System.Action OnBusStopDeparted;

        private void Awake()
        {
            _follower = GetComponent<WaypointFollower>();
        }

        private void Start()
        {
            _baseSpeed = _follower.Speed;

            // Subscribe to waypoint events
            _follower.OnWaypointReached += HandleWaypointReached;
        }

        private void Update()
        {
            if (_isDwelling)
            {
                HandleDwelling();
                return;
            }

            if (_isOvertaking && _playerTransform != null)
            {
                CheckOvertakeCompletion();
            }
        }

        private void HandleWaypointReached(int waypointIndex)
        {
            if (waypointIndex == _busStopWaypointIndex && !_isDwelling)
            {
                StartDwelling();
            }
        }

        private void StartDwelling()
        {
            _isDwelling = true;
            _dwellTimer = _dwellTime;
            _follower.Pause();
            OnBusStopArrived?.Invoke();
            Debug.Log("[BusAI] Bus arrived at bus stop, dwelling...");
        }

        private void HandleDwelling()
        {
            _dwellTimer -= Time.deltaTime;
            if (_dwellTimer <= 0f)
            {
                _isDwelling = false;
                _follower.Resume();
                OnBusStopDeparted?.Invoke();
                Debug.Log("[BusAI] Bus departing from bus stop.");
            }
        }

        /// <summary>
        /// Initiate the overtaking manoeuvre. Call this when the scenario triggers.
        /// The bus will speed up to pass the cyclist.
        /// </summary>
        public void StartOvertake()
        {
            if (_hasOvertaken || _isOvertaking) return;

            _isOvertaking = true;
            _follower.SetSpeed(_baseSpeed * _overtakeSpeedMultiplier);
            OnOvertakeStarted?.Invoke();

            // Optional horn
            if (_engineAudio != null && _hornClip != null)
            {
                _engineAudio.PlayOneShot(_hornClip);
            }

            Debug.Log("[BusAI] Bus overtake manoeuvre started.");
        }

        private void CheckOvertakeCompletion()
        {
            if (_playerTransform == null) return;

            // Check if the bus is ahead of the player and far enough away
            Vector3 toPlayer = _playerTransform.position - transform.position;
            float dot = Vector3.Dot(transform.forward, toPlayer);

            // Bus is ahead of player (dot < 0 means player is behind the bus)
            if (dot < 0 && toPlayer.magnitude > _overtakeCompleteDistance)
            {
                _isOvertaking = false;
                _hasOvertaken = true;
                _follower.SetSpeed(_baseSpeed); // Return to normal speed
                OnOvertakeCompleted?.Invoke();
                Debug.Log("[BusAI] Bus overtake completed — cyclist is behind.");
            }
        }

        /// <summary>
        /// Set the player transform for proximity detection.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }

        /// <summary>
        /// Set the bus stop waypoint index.
        /// </summary>
        public void SetBusStopWaypoint(int index)
        {
            _busStopWaypointIndex = index;
        }

        private void OnDestroy()
        {
            if (_follower != null)
            {
                _follower.OnWaypointReached -= HandleWaypointReached;
            }
        }
    }
}
