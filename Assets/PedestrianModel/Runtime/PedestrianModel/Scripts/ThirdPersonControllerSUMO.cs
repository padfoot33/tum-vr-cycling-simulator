using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;

#endif

using tumvt.sumounity;
using static tumvt.sumounity.Vehicle;
using UnityEngine.AI;
using Unity.AI.Navigation;

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace tumvt.sumounity.PedestrianModel
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour, IVehicleController {

        [Header("SUMO Integration")]
        [Tooltip("SUMO Vehicle/Pedestrian ID")]
        [SerializeField]
        private string _id;
        public string id { 
            get { return _id; }
            set { _id = value; } } // SUMO Identifiert in Vehicle Dictionary
        
        private SumoSocketClient sock;  // Reference to SUMO socket client
        private PIDController pidControllerSpeed;
        private PIDController pidControllerDist;
        private bool bDrawGizmo;

        [SerializeField]
        private Vector2 lookAheadMarker;

        public bool isSumoVehicle = true;
        private Vector2 rbMarker;
        private float stopState;

        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 100.0f)]
        public float RotationSmoothTime = 1.0f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        [Header("Animation")]
        [Range(0, 3)]
        [SerializeField]
        float _walkingAnimationSpeed = 1.5f;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
#endif

        [Header("VR Settings")]
        [SerializeField]
        [Tooltip("VR Input Actions Asset")]
        // VR Input
        private InputActionAsset inputActions; // Drag your Input Actions asset here in the Inspector
        private InputActionMap xrInputActionMap;
        private InputAction thumbstickAction;
        private Vector2 thumbstickPosition;
        public float rotationSpeedGain;
        public float walkSpeedGain;
        private AnimationCurve _speedDensityCurve;

        public float speedMultiplier = 1.0f;
        private const float _threshold = 0.01f;
        private Animator _animator;
        private CharacterController _controller;
        private GameObject _mainCamera;

        private bool _hasAnimator;
        private Vector3 _targetDirection;
        [SerializeField] private bool forceBoardingLogs = true;
        private bool BoardingLogs => forceBoardingLogs || enableDebugLogs;

        [Header("SUMO Smoothing")]
        [Tooltip("Enable direction smoothing. May cause path curvature if enabled.")]
        [SerializeField] private bool smoothSumoDirection = false;
        [Tooltip("Seconds for direction smoothing. 0 = no smoothing.")]
        [SerializeField] private float sumoMoveSmoothTime = 0.12f;
        [Tooltip("Seconds for speed smoothing. 0 = no smoothing.")]
        [SerializeField] private float sumoSpeedSmoothTime = 0.12f;
        [Tooltip("Smooth towards SUMO target position to reduce jitter.")]
        [SerializeField] private bool smoothSumoPosition = true;
        [Tooltip("Seconds for position smoothing. 0 = no smoothing.")]
        [SerializeField] private float sumoPositionSmoothTime = 0.12f;
        private Vector3 _sumoMoveSmoothed;
        private Vector3 _sumoMoveVelocity;
        private float _sumoSpeedSmoothed;
        private float _sumoSpeedVelocity;
        private Vector3 _sumoPosVelocity;
        

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }

        Rigidbody rb;

        private bool isCurrentlyInsideVehicle = false;
        private float teleportTimer = 0f;
        private const float TELEPORT_DELAY = 4f;

        // Ana: WIP: navigation mesh agent for pathfinding
        [Header("Navigation")]
        private UnityEngine.AI.NavMeshAgent navMeshAgent;
        private bool isBoarding = false;
        private bool inBus = false;
        private bool isWaiting = false;
        private bool isGoingTowardsWaitingSpot = false;


        [Header("Pedestrian Interaction Area")]
        const float _area = 9.996f;
        List<int> _objInInteractionArea = new List<int>();
        int _otherPersonsInInteractionArea = 0;
        private const string pedestrianTag = "Person";


        [Header("Bus")]
        private Transform boardingTarget;
        private float busHeightInWorld = 0f;
        private float yPosInWorld = 0.0f;
        private Transform busTransform;
        [SerializeField] private bool disableOnBoarding = true;
        private Transform pedestrianRoot;


        [Header("Debug")]
        private DebugLogsManager debugLogsManager;
        private bool enableDebugLogs = true;

#region Unity Events
        // UNITY COROUTINES

        private void Awake()
        {
            debugLogsManager = GameObject.Find("DebugLogsManager")?.GetComponent<DebugLogsManager>();
            pedestrianRoot = transform.root;
        }

        private void Start()
        {
            enableDebugLogs = debugLogsManager != null && debugLogsManager.EnableDebugLogs;
            
            // Cinemachine target is optional for SUMO pedestrians; default to self if missing to avoid null refs
            if (CinemachineCameraTarget == null)
            {
                CinemachineCameraTarget = this.gameObject;
                Debug.LogWarning($"[Boarding] CinemachineCameraTarget not assigned on {name}, defaulting to self.");
            }
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
#if ENABLE_INPUT_SYSTEM 
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;


            // Sumo: Get the socketclient with the step info
            rb = GetComponent<Rigidbody>();

            InitializeSumoIntegration();

            // NavMeshAgent
            navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            navMeshAgent.enabled = false; // SUMO controls movement initially

            InitializeSpeedDensityCurve();

        }

        void OnDrawGizmos(){
            float gizmoSize = 0.5f;
            if (bDrawGizmo){
                Gizmos.color = Color.red;
                Vector3 LadPoint = new Vector3(lookAheadMarker.x, 0.1f, lookAheadMarker.y);
                Gizmos.DrawSphere(LadPoint, gizmoSize);

                Gizmos.color = Color.blue;
                Vector3 rbMarkerPoint = new Vector3(rbMarker.x, 0.1f, rbMarker.y);
                Gizmos.DrawSphere(rbMarkerPoint, gizmoSize);
            }
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            JumpAndGravity();
            GroundedCheck();

            if (isSumoVehicle)
            {
                bool isInsideVehicle = PedestrianIsInsideVehicle(ref sock, id);

                if (isInsideVehicle)
                {
                    teleportTimer += Time.deltaTime;
                    if (teleportTimer >= TELEPORT_DELAY)
                    {
                        TeleportSumo();
                    }
                    else
                    {
                        MoveSumo();
                    }
                }
                else
                {
                    teleportTimer = 0f;  // Reset timer when not inside vehicle
                    MoveSumo();

                    
                }

                isCurrentlyInsideVehicle = isInsideVehicle;
            }
            else
            {
                Grounded = true; // Force grounded state for manual control

                if (enableDebugLogs)
                {
                    Debug.Log("Bus: No SUMO; MANUAL CONTROL");
                    Debug.Log("(BUS Joe) Look ahead marker of: " + lookAheadMarker);
                    Debug.Log("(BUS Joe) rbMarker of: " + rbMarker);
                    Debug.Log("(BUS Joe) Grounded: " + Grounded);
                }

                // if (isBoarding && navMeshAgent.enabled)
                if (navMeshAgent.enabled)
                {
                    // todo: maybe make function
                    Vector3 nextPos = navMeshAgent.nextPosition;
                    Vector3 moveDir = nextPos - transform.position;
                    moveDir.y = 0f; // ignore Y jitter

                    navMeshAgent.speed = _speedDensityCurve.Evaluate(_otherPersonsInInteractionArea / _area) * speedMultiplier;

                    if (_hasAnimator)
                    {
                        _animator.SetFloat(_animIDSpeed, navMeshAgent.speed * _walkingAnimationSpeed);
                        _animator.SetFloat(_animIDMotionSpeed, 1f);
                    }

                    if (enableDebugLogs)
                    {
                        Debug.Log("Remaining distance to bus: " + navMeshAgent.remainingDistance);

                    }

                    // Arrived at destination
                    if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
                    {   
                        if (enableDebugLogs)
                        {
                            Debug.Log("Joe arrived at destination: " + navMeshAgent.destination);
                        }
                        if (isBoarding)
                        {
                            if (enableDebugLogs)
                            {
                                Debug.Log("Joe arrived at boarding spot: " + navMeshAgent.destination);
                            }
                            FinishOnboarding();
                        }
                        if (isGoingTowardsWaitingSpot)
                        {
                            if (enableDebugLogs)
                            {
                                Debug.Log("Joe arrived at waiting spot: " + navMeshAgent.destination);
                            }
                            WaitForBus();
                        }

                        // *************************************************
                        // ATTENTION! WILL HAVE TO ADD THIS WHEN OFFBOARDING:
                        // *************************************************
                        // transform.SetParent(null);
                        // sumo vehicle is still false
                        // _controller.enabled = false;
                        // *************************************************

                    }
                }
            }
        }

        private void InitializeSpeedDensityCurve()
        {
            //_animCurve
            _speedDensityCurve = new AnimationCurve();
            _speedDensityCurve.AddKey(0, 1.34f);
            _speedDensityCurve.AddKey(0.5f, 1.23f);
            _speedDensityCurve.AddKey(1, 1.03f);
            _speedDensityCurve.AddKey(1.5f, 0.77f);
            _speedDensityCurve.AddKey(2, 0.56f);
            _speedDensityCurve.AddKey(2.5f, 0.41f);
            _speedDensityCurve.AddKey(3, 0.31f);
            _speedDensityCurve.AddKey(3.5f, 0.25f);
            _speedDensityCurve.AddKey(4, 0.2f);
            _speedDensityCurve.AddKey(4.5f, 0.16f);
            _speedDensityCurve.AddKey(5, 0.12f);
            _speedDensityCurve.AddKey(5.5f, 0.06f);
            _speedDensityCurve.AddKey(6, 0.01f);
        }
        #endregion

        #region Onboarding
        //Pathfinding and boarding logic
        public void BeginOnboarding((Vector3 goalPointVector, Transform busTransform) boardingInfo)
        {
            Vector3 targetPosition = boardingInfo.goalPointVector;
            busHeightInWorld = targetPosition.y; // Store the bus height for teleporting later
            this.busTransform = boardingInfo.busTransform;

            isWaiting = false;
            isBoarding = true;

            SetPedestrianControlToNavAgent(targetPosition);

            if (BoardingLogs)
            {
                Debug.Log($"[Boarding] BeginOnboarding -> target={targetPosition}, bus={busTransform?.name ?? "null"}, navMeshAgent.enabled={navMeshAgent.enabled}");
                Debug.Log("Joe PersonController: Begin boarding bus: " + gameObject.name + " towards " + targetPosition);
                Debug.Log("joe sumo vehicle set: " + isSumoVehicle);
                Debug.Log("joe after enabling navMeshAgent: " + navMeshAgent.enabled);
                Debug.Log($" joe NavMeshAgent destination set to: {navMeshAgent.destination}");
                Debug.Log("joe isBoarding set to true: " + isBoarding);
                Debug.Log("joe Bus height in world: " + busHeightInWorld);
            }

        }

        void GoToWaitingSpot(Vector3 waitingSpot)
        {
            if (enableDebugLogs)
            {
                Debug.Log("Joe PersonController: Going to waiting spot: " + waitingSpot);
            }
            // Logic to find a waiting spot near the bus stop
            isGoingTowardsWaitingSpot = true;
            SetPedestrianControlToNavAgent(waitingSpot);

        }

        void WaitForBus()
        {
            if (enableDebugLogs)
            {
                Debug.Log("Joe PersonController: Waiting for bus at: " + transform.position);
            }

            isWaiting = true;
            isGoingTowardsWaitingSpot = false;

            StandingStillBehavior();
        }

        void FinishOnboarding()
        {
            if (enableDebugLogs)
            {
                Debug.Log("Joe PersonController: Boarding completed for: " + gameObject.name);
            }

            isBoarding = false;
            inBus = true;

            StandingStillBehavior();

            transform.SetParent(busTransform, worldPositionStays: true); // Set parent to bus

            if (enableDebugLogs)
            {
                Debug.Log("joe sumo vehicle set: " + isSumoVehicle);
            }

            if (disableOnBoarding)
            {
                // Disable only the original pedestrian root (not the bus)
                if (pedestrianRoot != null)
                {
                    pedestrianRoot.gameObject.SetActive(false);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }
        #endregion

        #region Trigger Methods
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(pedestrianTag))
            {
                if (!_objInInteractionArea.Contains(other.gameObject.GetInstanceID()))
                {
                    _objInInteractionArea.Add(other.gameObject.GetInstanceID());
                    _otherPersonsInInteractionArea = _objInInteractionArea.Count;
                }

            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(pedestrianTag))
            {
                if (_objInInteractionArea.Contains(other.gameObject.GetInstanceID()))
                {
                    _objInInteractionArea.Remove(other.gameObject.GetInstanceID());
                    _otherPersonsInInteractionArea = _objInInteractionArea.Count;
                }

            }
        }
        #endregion



#region SUMONITY
        // SUMONITY
        private void InitializeSumoIntegration()
        {
            // Get the socketclient with the step info
            sock = GameObject.FindObjectOfType<SumoSocketClient>();

            // Initialize controllers
            pidControllerDist = new PIDController(15.0f, 0.0f, 0.0f); 
            pidControllerSpeed = new PIDController(1.0f, 0.0f, 0.0f); 
            bDrawGizmo = true;
        }

        private void TeleportSumo()
        {
            Debug.LogWarning("Teleporting Sumo");
            Vector2 pos = PedestrianGetPosition(ref sock, id);
            transform.position = new Vector3(pos.x, 0.0f, pos.y);
            rbMarker.x = pos.x;
            rbMarker.y = pos.y;
            lookAheadMarker = rbMarker;
        }

        private void MoveSumo()
        {
            // rb.isKinematic = true;
            rbMarker.x = rb.position.x;
            rbMarker.y = rb.position.z;

            var (worldMovementVector, worldMovementSpeed, worldMovementDirection, absolutePositionError, lookAheadPoint) =
                SumoPedestrianControl(
                    ref sock,
                    id,
                    rb,
                    ref lookAheadMarker
                );

            // set target speed based on move speed, sprint speed and if sprint is pressed

            // increase speed if error is large:
            float targetSpeed = worldMovementSpeed;
            if (absolutePositionError > 0.1f)
            {
                targetSpeed = SprintSpeed;
            }
            // float targetSpeed = worldMovementSpeed;

            // _input.move = worldMovementVector;

            if (worldMovementVector == Vector2.zero) targetSpeed = 0;


            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = 1.0f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;

                //Debug.Log($"_speed: {_speed}");
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;



            // normalise input direction
            Vector3 inputDirection = new Vector3(worldMovementVector.x, 0.0f, worldMovementVector.y).normalized;
            if (smoothSumoDirection && sumoMoveSmoothTime > 0f)
            {
                _sumoMoveSmoothed = Vector3.SmoothDamp(_sumoMoveSmoothed, inputDirection, ref _sumoMoveVelocity, sumoMoveSmoothTime);
                inputDirection = _sumoMoveSmoothed;
            }

            float targetYaw = worldMovementDirection;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _rotationVelocity,
                RotationSmoothTime);

            if (worldMovementVector != Vector2.zero)
            {
                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            float appliedSpeed = _speed;
            if (sumoSpeedSmoothTime > 0f)
            {
                _sumoSpeedSmoothed = Mathf.SmoothDamp(_sumoSpeedSmoothed, _speed, ref _sumoSpeedVelocity, sumoSpeedSmoothTime);
                appliedSpeed = _sumoSpeedSmoothed;
            }
            if (smoothSumoPosition && sumoPositionSmoothTime > 0f)
            {
                Vector3 targetPos = new Vector3(rb.position.x + worldMovementVector.x, transform.position.y, rb.position.z + worldMovementVector.y);
                Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, targetPos, ref _sumoPosVelocity, sumoPositionSmoothTime);
                Vector3 delta = smoothedPos - transform.position;
                _controller.Move(delta + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
            }
            else
            {
                _controller.Move(inputDirection.normalized * (appliedSpeed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
            }

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        #endregion
        #region Pedestrian Methods
        private void SetPedestrianControlToNavAgent(Vector3 destination)
        {
            if (BoardingLogs)
            {
                Debug.Log("Set to NavMeshAgent for Joe: " + gameObject.name);
            }

            isSumoVehicle = false;
            _controller.enabled = false;

            navMeshAgent.enabled = true;
            navMeshAgent.updatePosition = true;
            navMeshAgent.updateRotation = true;
            navMeshAgent.autoTraverseOffMeshLink = true;
            navMeshAgent.isStopped = false;

            // Ensure the agent is on a NavMesh before setting destination
            if (!navMeshAgent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas))
                {
                    navMeshAgent.Warp(hit.position);
                    if (BoardingLogs)
                    {
                        Debug.Log($"[Boarding] Warped agent to NavMesh at {hit.position}");
                    }
                }
                else if (NavMesh.SamplePosition(destination, out hit, 5f, NavMesh.AllAreas))
                {
                    navMeshAgent.Warp(hit.position);
                    if (BoardingLogs)
                    {
                        Debug.Log($"[Boarding] Warped agent near destination at {hit.position}");
                    }
                }
                else if (BoardingLogs)
                {
                    Debug.LogWarning("[Boarding] No NavMesh found near agent or destination; cannot move.");
                }
            }

            // Snap destination onto NavMesh if needed
            Vector3 finalDest = destination;
            if (NavMesh.SamplePosition(destination, out var destHit, 5f, NavMesh.AllAreas))
            {
                finalDest = destHit.position;
            }
            else if (BoardingLogs)
            {
                Debug.LogWarning($"[Boarding] Destination not on NavMesh: {destination}");
            }
            navMeshAgent.SetDestination(finalDest);

            if (BoardingLogs)
            {
                Debug.Log($"[Boarding] NavMesh destination set: {finalDest}, pathStatus={navMeshAgent.pathStatus}, remainingDistance={navMeshAgent.remainingDistance}");
            }

        }

        private void StandingStillBehavior()
        {
            if (enableDebugLogs)
            {
                Debug.Log("Set to InBus Behaviour for Joe: " + gameObject.name);
            }

            isSumoVehicle = false;
            _controller.enabled = false;
            navMeshAgent.enabled = false;

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, 0f);
                _animator.SetFloat(_animIDMotionSpeed, 0f);
            }
        }

        public void SetCharacterController(bool enable)
        {
            _controller.enabled = enable;
        }

        // Make sure pedestrians that missed the bus/are in the bus are set to SUMO vehicle control
        public void SetToSumoVehicle(bool controllerEnabled = true)
        // if the pedestrian is in the bus, we want to disable the controller
        {
            if (enableDebugLogs)
            {
                Debug.Log("Set to Sumo Vehicle for Joe: " + gameObject.name);
            }
            isSumoVehicle = true;
            // _animator.applyRootMotion = true;
            navMeshAgent.enabled = false;
            _controller.enabled = controllerEnabled;
        }

        
        // Other Methods
        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                
                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }

    }

    
}
#endregion
