using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO.Ports;
using System.Globalization;

using tumvt.sumounity;

namespace SBPScripts.Simulator
{
    [System.Serializable]
    public class CycleGeometry
    {
        public GameObject handles;
        public GameObject lowerFork;
        public GameObject fWheelVisual;
        public GameObject RWheel;
        public GameObject crank;
        public GameObject lPedal;
        public GameObject rPedal;
        public GameObject fGear;
        public GameObject rGear;
    }

    [System.Serializable]
    public class PedalAdjustments
    {
        public float crankRadius;
        public Vector3 lPedalOffset;
        public Vector3 rPedalOffset;
        public float pedalingSpeed;
    }

    [System.Serializable]
    public class WheelFrictionSettings
    {
        // Unity 2022.3 API
        public PhysicMaterial fPhysicMaterial;
        public PhysicMaterial rPhysicMaterial;

        public Vector2 fFriction;
        public Vector2 rFriction;
    }

    [System.Serializable]
    public class WayPointSystem
    {
        public enum RecordingState
        {
            DoNothing,
            Record,
            Playback
        }

        public RecordingState recordingState = RecordingState.DoNothing;

        [Range(1, 10)]
        public int frameIncrement;

        [HideInInspector]
        public List<Vector3> bicyclePositionTransform;

        [HideInInspector]
        public List<Quaternion> bicycleRotationTransform;

        [HideInInspector]
        public List<Vector2Int> movementInstructionSet;

        [HideInInspector]
        public List<bool> sprintInstructionSet;

        [HideInInspector]
        public List<int> bHopInstructionSet;
    }

    [System.Serializable]
    public class AirTimeSettings
    {
        public bool freestyle;
        public float airTimeRotationSensitivity;

        [Range(0.5f, 10f)]
        public float heightThreshold;

        public float groundSnapSensitivity;
    }

    public class BicycleSimulatorController : MonoBehaviour, IVehicleController
    {
        // ==========================================================
        // Arduino
        // ==========================================================

        [Header("Arduino Configuration")]
        [SerializeField] private string defaultComPort = "COM3";
        [SerializeField] private int baudRate = 9600;
        [SerializeField] private int readTimeoutMs = 500;
        [SerializeField] private bool autoDetectComPort = true;
        [SerializeField] private bool enableArduinoDebugLogs = true;

        private SerialPort sp;

        private bool arduinoConnected = false;
        private string arduinoErrorMessage = "";
        private string actualComPort = "";

        public bool IsArduinoConnected => arduinoConnected;
        public string ArduinoErrorMessage => arduinoErrorMessage;
        public string ActualComPort => actualComPort;

        public float leftBrakeSignal = 0f;
        public float rightBrakeSignal = 0f;
        public float brakeSensitivity = 10f;

        public float mass_kg = 80f;

        private float velocity = 0f;
        private float acceleration = 0f;

        public bool useCommandLineInput = false;

        private bool useConstantVelocity = false;
        private float constantSimVelocity = 0f;

        // ==========================================================
        // SUMO
        // ==========================================================

        public string id { get; set; }

        public bool isSumoVehicle = true;

        private bool allowSumoTeleport = true;
        private string sumoTeleportDisabledReason = "";
        private int sumoTeleportDisableCount = 0;

        private bool allowWayPointPlayback = true;
        private string wayPointPlaybackDisabledReason = "";
        private int wayPointPlaybackDisableCount = 0;

        // ==========================================================
        // Simulator / Wahoo / Fanatec
        // ==========================================================

        [Header("Wahoo Simulator")]
        public bool isSimulatorVehicle = false;

        public GameObject BikeConnectorTCP;

        private tcp_client tcp_bike_connection;

        private float target_velocity_wahoo_bike;
        private float actual_velocity_unity_bike;

        // IMPORTANT:
        // Inspector serialized value for your real SimBike should remain 8.
        public float globalSteeringGain = 60f;

        private PIController piControllerSpeedWahoo;

        public float proportionalTermWahoo = 2125f;
        public float globalSpeedGainSimBike = 0.5f;
        public float globalaccGainSimBike = 0.03f;

        private float steeringValueFanatec;

        private FFBInspectorBike steeringInput;

        // ==========================================================
        // Bicycle setup
        // ==========================================================

        public CycleGeometry cycleGeometry;

        public GameObject fPhysicsWheel;
        public GameObject rPhysicsWheel;

        public WheelFrictionSettings wheelFrictionSettings;

        public AnimationCurve accelerationCurve;

        [Tooltip("Steer Angle over Speed")]
        public AnimationCurve steerAngle;

        [Tooltip(
            "Below this ground speed (m/s), physical front-wheel yaw is scaled down " +
            "so A/D at rest does not twist the whole bike / camera. Visual bars still turn."
        )]
        public float minSteerSpeed = 0.5f;

        public float axisAngle;

        public AnimationCurve leanCurve;

        public float torque;
        public float topSpeed;

        [Range(0.1f, 0.9f)]
        [Tooltip("Ratio of Relaxed mode to Top Speed")]
        public float relaxedSpeed;

        public float reversingSpeed;

        public Vector3 centerOfMassOffset;

        [HideInInspector]
        public bool isReversing;

        [HideInInspector]
        public bool isAirborne;

        [HideInInspector]
        public bool stuntMode;

        [Range(0f, 8f)]
        public float oscillationAmount;

        [Range(0f, 1f)]
        public float oscillationAffectSteerRatio;

        private float oscillationSteerEffect;

        [HideInInspector]
        public float cycleOscillation;

        [HideInInspector]
        public Rigidbody rb;

        [HideInInspector]
        public Rigidbody fWheelRb;

        [HideInInspector]
        public Rigidbody rWheelRb;

        private float xQuat;
        private float zQuat;

        [HideInInspector]
        public float crankSpeed;

        [HideInInspector]
        public float crankCurrentQuat;

        [HideInInspector]
        public float crankLastQuat;

        [HideInInspector]
        public float restingCrank;

        public PedalAdjustments pedalAdjustments;

        [HideInInspector]
        public float turnLeanAmount;

        private RaycastHit hit;

        [HideInInspector]
        public float customSteerAxis;

        [HideInInspector]
        public float customLeanAxis;

        [HideInInspector]
        public float customAccelerationAxis;

        [HideInInspector]
        public float rawCustomAccelerationAxis;

        private bool sprint;

        [HideInInspector]
        public bool wheelieInput;

        [HideInInspector]
        public float wheeliePower;

        public bool wheelieToggle;

        [HideInInspector]
        public int bunnyHopInputState;

        [HideInInspector]
        public float currentTopSpeed;

        [HideInInspector]
        public float pickUpSpeed;

        private Quaternion initialLowerForkLocalRotaion;
        private Quaternion initialHandlesRotation;

        private ConfigurableJoint fPhysicsWheelConfigJoint;
        private ConfigurableJoint rPhysicsWheelConfigJoint;

        public bool groundConformity;

        private float groundZ;

        public bool inelasticCollision;

        [HideInInspector]
        public Vector3 lastVelocity;

        [HideInInspector]
        public Vector3 deceleration;

        [HideInInspector]
        public Vector3 lastDeceleration;

        private int impactFrames;
        private bool isBunnyHopping;

        [HideInInspector]
        public float bunnyHopAmount;

        public float bunnyHopStrength;

        public WayPointSystem wayPointSystem;
        public AirTimeSettings airTimeSettings;

        // ==========================================================
        // SUMO control
        // ==========================================================

        private SumoSocketClient sock;

        private PIDController pidControllerSpeed;
        private PIDController pidControllerDist;

        private bool bDrawGizmo;

        private Vector2 lookAheadMarker;
        private Vector2 rbMarker;
        private Vector2 SUMOMarker;

        // ==========================================================
        // Logging
        // ==========================================================

        private float simulationTime;
        private string filePath;

        // ==========================================================
        // Command line
        // ==========================================================

        private void GetConstantVelocityUsageFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--velocity" && i < args.Length - 1)
                {
                    if (int.TryParse(args[i + 1], out int payload))
                    {
                        useConstantVelocity = true;
                        constantSimVelocity = payload / 3.6f;
                    }
                }
            }
        }

        // ==========================================================
        // Arduino
        // ==========================================================

        private void InitializeArduinoConnection()
        {
            if (sp != null && sp.IsOpen)
            {
                Debug.Log(
                    $"[BicycleSimulatorController] Arduino already connected on {actualComPort}"
                );

                return;
            }

            if (autoDetectComPort)
                actualComPort = DetectArduinoComPort();
            else
                actualComPort = defaultComPort;

            if (!ArduinoPortIsAvailable(actualComPort))
            {
                arduinoConnected = false;

                arduinoErrorMessage =
                    "Arduino port " +
                    actualComPort +
                    " is not present. Keyboard brake fallback.";

                return;
            }

            try
            {
                sp = new SerialPort(actualComPort, baudRate);

                sp.Open();

                sp.ReadTimeout = readTimeoutMs;
                sp.WriteTimeout = readTimeoutMs;

                if (sp.BytesToRead > 0)
                    sp.ReadExisting();

                arduinoConnected = true;
                arduinoErrorMessage = "";

                Debug.Log(
                    $"[BicycleSimulatorController] Arduino connected successfully on {actualComPort}"
                );
            }
            catch (TimeoutException)
            {
                arduinoConnected = false;

                arduinoErrorMessage =
                    "Arduino connection timeout on " +
                    actualComPort;
            }
            catch (UnauthorizedAccessException)
            {
                arduinoConnected = false;

                arduinoErrorMessage =
                    "Arduino access denied on " +
                    actualComPort;
            }
            catch (System.IO.IOException)
            {
                arduinoConnected = false;

                arduinoErrorMessage =
                    "Arduino connection failed on " +
                    actualComPort;
            }
            catch (Exception ex)
            {
                arduinoConnected = false;

                arduinoErrorMessage =
                    "Arduino connection error: " +
                    ex.Message;
            }
        }

        private static bool ArduinoPortIsAvailable(string portName)
        {
            if (string.IsNullOrEmpty(portName))
                return false;

            try
            {
                string[] ports =
                    SerialPort.GetPortNames();

                if (ports == null || ports.Length == 0)
                    return false;

                return Array.Exists(
                    ports,
                    p => p.Equals(
                        portName,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            }
            catch
            {
                return false;
            }
        }

        private string DetectArduinoComPort()
        {
            try
            {
                string[] ports =
                    SerialPort.GetPortNames();

                if (ports == null ||
                    ports.Length == 0)
                {
                    return defaultComPort;
                }

                if (Array.Exists(
                    ports,
                    p => p.Equals(
                        defaultComPort,
                        StringComparison.OrdinalIgnoreCase
                    )))
                {
                    return defaultComPort;
                }

                string[] commonPorts =
                {
                    "COM3",
                    "COM4",
                    "COM5",
                    "COM6",
                    "COM7",
                    "COM8"
                };

                foreach (string port in commonPorts)
                {
                    if (Array.Exists(
                        ports,
                        p => p.Equals(
                            port,
                            StringComparison.OrdinalIgnoreCase
                        )))
                    {
                        return port;
                    }
                }

                foreach (string port in ports)
                {
                    if (LooksLikeArduinoPort(port))
                        return port;
                }

                return defaultComPort;
            }
            catch
            {
                return defaultComPort;
            }
        }

        private static bool LooksLikeArduinoPort(string portName)
        {
            if (string.IsNullOrEmpty(portName))
                return false;

            if (portName.StartsWith(
                "COM",
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return true;
            }

            string lower =
                portName.ToLowerInvariant();

            return
                lower.Contains("usbmodem") ||
                lower.Contains("usbserial") ||
                lower.Contains("arduino") ||
                lower.Contains("ttyacm") ||
                lower.Contains("ttyusb");
        }

        // ==========================================================
        // Hierarchy helper
        // ==========================================================

        private string GetFullHierarchyPath()
        {
            List<string> path =
                new List<string>();

            Transform current =
                transform;

            while (current != null)
            {
                path.Add(current.name);
                current = current.parent;
            }

            path.Reverse();

            return string.Join("/", path);
        }

        // ==========================================================
        // Unity lifecycle
        // ==========================================================

        private void Awake()
        {
            string parentPath =
                GetFullHierarchyPath();

            Debug.Log(
                $"[BicycleSimulatorController] AWAKE Scene='{gameObject.scene.name}' Path='{parentPath}'"
            );

            // Start upright but preserve spawn yaw.
            transform.rotation =
                Quaternion.Euler(
                    0f,
                    transform.rotation.eulerAngles.y,
                    0f
                );

            Debug.Log(
                $"[BicycleSimulatorController] FLAGS isSumoVehicle={isSumoVehicle}, isSimulatorVehicle={isSimulatorVehicle}, id={id}"
            );
        }

        private void Start()
        {
            InitializeArduinoConnection();

            bool logData = false;

            if (logData)
            {
                filePath =
                    "C:\\Users\\TUBVVTK-VTSIM14\\Downloads\\BikeData.csv";

                using (
                    StreamWriter writer =
                        new StreamWriter(
                            filePath,
                            false
                        ))
                {
                    writer.WriteLine(
                        "SimulationTime,TargetVelocityWahooBike,ActualVelocityUnityBike,TorqueInput"
                    );
                }

                StartCoroutine(LogData());
            }

            rb =
                GetComponent<Rigidbody>();
            /*
 * IMPORTANT:
 * Do NOT permanently freeze X/Z here.
 * SimBikeSpawnController performs the short
 * startup stabilization separately.
 */
            rb.constraints &=
                ~(RigidbodyConstraints.FreezeRotationX |
                  RigidbodyConstraints.FreezeRotationZ);

            // Cap spin so wheel surface speed cannot race far past rolling ω = v/r
            // (Infinity + Discrete contacts caused high-speed vertical contact chatter).
            const float physicsWheelRadius = 0.44f;
            const float angularVelocitySafetyFactor = 1.35f;
            float maxWheelAngularVelocity =
                (Mathf.Max(topSpeed, 0.01f) *
                 Mathf.Max(globalSpeedGainSimBike, 0.01f) *
                 angularVelocitySafetyFactor) /
                physicsWheelRadius;

            rb.maxAngularVelocity =
                maxWheelAngularVelocity;

            fWheelRb =
                fPhysicsWheel.GetComponent<Rigidbody>();

            fWheelRb.maxAngularVelocity =
                maxWheelAngularVelocity;

            rWheelRb =
                rPhysicsWheel.GetComponent<Rigidbody>();

            rWheelRb.maxAngularVelocity =
                maxWheelAngularVelocity;

            currentTopSpeed =
                topSpeed;

            initialHandlesRotation =
                cycleGeometry.handles.transform.localRotation;

            initialLowerForkLocalRotaion =
                cycleGeometry.lowerFork.transform.localRotation;

            fPhysicsWheelConfigJoint =
                fPhysicsWheel.GetComponent<ConfigurableJoint>();

            rPhysicsWheelConfigJoint =
                rPhysicsWheel.GetComponent<ConfigurableJoint>();

            if (
                wayPointSystem.recordingState ==
                    WayPointSystem.RecordingState.Record ||
                wayPointSystem.recordingState ==
                    WayPointSystem.RecordingState.DoNothing)
            {
                wayPointSystem.bicyclePositionTransform.Clear();
                wayPointSystem.bicycleRotationTransform.Clear();
                wayPointSystem.movementInstructionSet.Clear();
                wayPointSystem.sprintInstructionSet.Clear();
                wayPointSystem.bHopInstructionSet.Clear();
            }

            sock =
                GameObject.FindObjectOfType<SumoSocketClient>();

            pidControllerDist =
                new PIDController(
                    15f,
                    0f,
                    0f
                );

            pidControllerSpeed =
                new PIDController(
                    1f,
                    0f,
                    0f
                );

            bDrawGizmo =
                true;

            piControllerSpeedWahoo =
                new PIController(
                    proportionalTermWahoo,
                    0f
                );

            if (isSimulatorVehicle)
            {
                if (BikeConnectorTCP != null)
                {
                    tcp_bike_connection =
                        BikeConnectorTCP
                        .GetComponent<tcp_client>();
                }

                steeringInput =
                    GetComponent<FFBInspectorBike>();
            }

            GetConstantVelocityUsageFromCommandLine();
        }

        private void OnEnable()
        {
            if (sp != null &&
                !sp.IsOpen)
            {
                InitializeArduinoConnection();
            }
        }

        private void OnDisable()
        {
            if (sp != null &&
                sp.IsOpen)
            {
                try
                {
                    sp.Close();
                    sp.Dispose();
                }
                catch
                {
                    // Ignore shutdown error.
                }
            }
        }

        // ==========================================================
        // Spawn protection API
        // ==========================================================

        public void DisableSumoTeleport(string reason)
        {
            sumoTeleportDisableCount++;

            sumoTeleportDisabledReason =
                reason;

            allowSumoTeleport =
                false;
        }

        public void EnableSumoTeleport(string reason)
        {
            sumoTeleportDisableCount =
                Mathf.Max(
                    0,
                    sumoTeleportDisableCount - 1
                );

            if (sumoTeleportDisableCount == 0)
                allowSumoTeleport = true;
        }

        public void DisableWayPointPlayback(string reason)
        {
            wayPointPlaybackDisableCount++;

            wayPointPlaybackDisabledReason =
                reason;

            allowWayPointPlayback =
                false;
        }

        public void EnableWayPointPlayback(string reason)
        {
            wayPointPlaybackDisableCount =
                Mathf.Max(
                    0,
                    wayPointPlaybackDisableCount - 1
                );

            if (wayPointPlaybackDisableCount == 0)
                allowWayPointPlayback = true;
        }

        // ==========================================================
        // Public speed helpers
        // ==========================================================

        public float GetSpeedMps()
        {
            if (rb == null)
                return 0f;

            Vector3 horizontalVelocity =
                rb.velocity;

            horizontalVelocity.y =
                0f;

            return horizontalVelocity.magnitude;
        }

        public float GetSpeedKph()
        {
            return GetSpeedMps() * 3.6f;
        }
        public float TargetSpeedKph
        {
            get
            {
                return target_velocity_wahoo_bike * 3.6f;
            }
        }

        
        public void HaltIntegratedVelocity()
        {
            velocity = 0f;
            acceleration = 0f;

            customAccelerationAxis = 0f;
            rawCustomAccelerationAxis = 0f;
        }

        // ==========================================================
        // FixedUpdate / Update
        // ==========================================================

        private void FixedUpdate()
        {
            if (
                wayPointSystem.recordingState ==
                    WayPointSystem.RecordingState.DoNothing ||
                wayPointSystem.recordingState ==
                    WayPointSystem.RecordingState.Record)
            {
                if (isSumoVehicle)
                {
                    bool insidePhysicsArea =
                        Vehicle.SumoVehicleDetect(
                            ref sock,
                            id
                        );

                    if (insidePhysicsArea)
                        ApplyPhysicsUpdate();
                }
            }

            if (isSimulatorVehicle)
            {
                ApplyPhysicsUpdate();
            }
        }

        private void Update()
        {
            ApplyCustomInput();

            DisplayArduinoStatus();

            if (bunnyHopInputState == 1)
            {
                isBunnyHopping = true;

                bunnyHopAmount +=
                    Time.deltaTime * 8f;
            }

            if (bunnyHopInputState == -1)
                StartCoroutine(DelayBunnyHop());

            if (
                bunnyHopInputState == -1 &&
                !isAirborne)
            {
                rb.AddForce(
                    transform.up *
                    bunnyHopAmount *
                    bunnyHopStrength,
                    ForceMode.VelocityChange
                );
            }
            else
            {
                bunnyHopAmount =
                    Mathf.Lerp(
                        bunnyHopAmount,
                        0f,
                        Time.deltaTime * 8f
                    );
            }

            bunnyHopAmount =
                Mathf.Clamp01(
                    bunnyHopAmount
                );
        }

        // ==========================================================
        // Physics
        // ==========================================================

        private float GroundConformity(bool toggle)
        {
            if (toggle)
            {
                groundZ =
                    transform.rotation.eulerAngles.z;
            }

            return groundZ;
        }

        private void ApplyPhysicsUpdate()
        {
            if (rb == null ||
                fWheelRb == null ||
                rWheelRb == null ||
                fPhysicsWheelConfigJoint == null)
            {
                return;
            }

            /*
             * IMPORTANT:
             * No HoldIdlePhysics() here.
             *
             * We do NOT zero all three rigidbodies just because
             * Wahoo/TCP temporarily has no input.
             */

            float currentSpeed =
                rb.velocity.magnitude;

            float safeCurrentSpeed =
                Mathf.Max(
                    currentSpeed,
                    0.1f
                );

            // ======================================================
            // Physical steering
            // ======================================================

            /*
             * KEEP THE MINUS SIGN.
             *
             * This is the established Akhilesh bicycle
             * steering direction.
             *
             * Scale physical front-wheel yaw by ground speed so A/D
             * at rest turns bars only — not the whole chassis/camera.
             * Visual steering below still uses the full steer angle.
             */
            float steerSpeedFactor =
                minSteerSpeed <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        currentSpeed / minSteerSpeed
                    );

            float physicalSteerDeg =
                customSteerAxis *
                steerAngle.Evaluate(currentSpeed) *
                steerSpeedFactor;

            fPhysicsWheel.transform.rotation =
                Quaternion.Euler(
                    transform.rotation.eulerAngles.x,
                    transform.rotation.eulerAngles.y
                        + physicalSteerDeg
                        + oscillationSteerEffect *
                        steerSpeedFactor,
                    0f
                );

            fPhysicsWheelConfigJoint.axis =
                new Vector3(
                    1f,
                    0f,
                    0f
                );

            // Kill residual yaw twist when parked / creeping with no pedal input.
            if (
                currentSpeed < minSteerSpeed &&
                Mathf.Abs(rawCustomAccelerationAxis) < 0.05f)
            {
                Vector3 angularVelocity =
                    rb.angularVelocity;
                angularVelocity.y = 0f;
                rb.angularVelocity =
                    angularVelocity;
            }

            // ======================================================
            // Power
            // ======================================================

                       if (!sprint)
            {
                currentTopSpeed =
                    Mathf.Lerp(
                        currentTopSpeed,
                        topSpeed * relaxedSpeed,
                        Time.deltaTime
                    );
            }
            else
            {
                currentTopSpeed =
                    Mathf.Lerp(
                        currentTopSpeed,
                        topSpeed,
                        Time.deltaTime
                    );
            }

            if (
                currentSpeed < currentTopSpeed &&
                rawCustomAccelerationAxis > 0f)
            {
                rWheelRb.AddTorque(
                    transform.right *
                    torque *
                    customAccelerationAxis
                );
            }

            if (
                currentSpeed < currentTopSpeed &&
                rawCustomAccelerationAxis > 0f &&
                !isAirborne &&
                !isBunnyHopping)
            {
                rb.AddForce(
                    transform.forward *
                    accelerationCurve.Evaluate(
                        customAccelerationAxis
                    )
                );
            }

            

            if (
                currentSpeed < reversingSpeed &&
                rawCustomAccelerationAxis < 0f &&
                !isAirborne &&
                !isBunnyHopping)
            {
                rb.AddForce(
                    -transform.forward *
                    accelerationCurve.Evaluate(
                        customAccelerationAxis
                    ) *
                    0.5f
                );
            }

            isReversing =
                transform
                    .InverseTransformDirection(
                        rb.velocity
                    ).z < 0f;

            if (
                rawCustomAccelerationAxis < 0f &&
                !isReversing &&
                !isAirborne &&
                !isBunnyHopping)
            {
                rb.AddForce(
                    -transform.forward *
                    accelerationCurve.Evaluate(
                        customAccelerationAxis
                    ) *
                    2f
                );
            }

            // ======================================================
            // Center of mass
            // ======================================================

            if (stuntMode)
            {
                rb.centerOfMass =
                    GetComponent<BoxCollider>().center;
            }
            else
            {
                rb.centerOfMass =
                    centerOfMassOffset;
            }

            // ======================================================
            // Visual steering
            // ======================================================

            cycleGeometry.handles.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    customSteerAxis *
                    steerAngle.Evaluate(currentSpeed) +
                    oscillationSteerEffect * 5f,
                    0f
                ) *
                initialHandlesRotation;

            cycleGeometry.lowerFork.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    customSteerAxis *
                    steerAngle.Evaluate(currentSpeed) +
                    oscillationSteerEffect * 5f,

                    customSteerAxis *
                    -axisAngle
                ) *
                initialLowerForkLocalRotaion;

            xQuat =
                Mathf.Sin(
                    Mathf.Deg2Rad *
                    transform.rotation.eulerAngles.y
                );

            zQuat =
                Mathf.Cos(
                    Mathf.Deg2Rad *
                    transform.rotation.eulerAngles.y
                );

            cycleGeometry.fWheelVisual.transform.rotation =
                Quaternion.Euler(
                    xQuat *
                    customSteerAxis *
                    -axisAngle,

                    customSteerAxis *
                    steerAngle.Evaluate(currentSpeed) +
                    oscillationSteerEffect * 5f,

                    zQuat *
                    customSteerAxis *
                    -axisAngle
                );

            if (
                cycleGeometry.fWheelVisual.transform.childCount > 0)
            {
                cycleGeometry
                    .fWheelVisual
                    .transform
                    .GetChild(0)
                    .localRotation =
                    cycleGeometry.RWheel.transform.rotation;
            }

            // ======================================================
            // Crank / pedals
            // ======================================================

            crankCurrentQuat =
                cycleGeometry.RWheel
                .transform
                .rotation
                .eulerAngles
                .x;

            if (
                customAccelerationAxis > 0f &&
                !isAirborne &&
                !isBunnyHopping)
            {
                crankSpeed +=
                    Mathf.Sqrt(
                        customAccelerationAxis *
                        Mathf.Abs(
                            Mathf.DeltaAngle(
                                crankCurrentQuat,
                                crankLastQuat
                            ) *
                            pedalAdjustments.pedalingSpeed
                        )
                    );

                crankSpeed %= 360f;
            }
            else if (
                Mathf.Floor(crankSpeed) >
                restingCrank)
            {
                crankSpeed -= 6f;
            }
            else if (
                Mathf.Floor(crankSpeed) <
                restingCrank)
            {
                crankSpeed =
                    Mathf.Lerp(
                        crankSpeed,
                        restingCrank,
                        Time.deltaTime * 5f
                    );
            }

            crankLastQuat =
                crankCurrentQuat;

            cycleGeometry.crank
                .transform
                .localRotation =
                Quaternion.Euler(
                    crankSpeed,
                    0f,
                    0f
                );

            cycleGeometry.lPedal
                .transform
                .localPosition =
                pedalAdjustments.lPedalOffset +
                new Vector3(
                    0f,
                    Mathf.Cos(
                        Mathf.Deg2Rad *
                        (crankSpeed + 180f)
                    ) *
                    pedalAdjustments.crankRadius,

                    Mathf.Sin(
                        Mathf.Deg2Rad *
                        (crankSpeed + 180f)
                    ) *
                    pedalAdjustments.crankRadius
                );

            cycleGeometry.rPedal
                .transform
                .localPosition =
                pedalAdjustments.rPedalOffset +
                new Vector3(
                    0f,
                    Mathf.Cos(
                        Mathf.Deg2Rad *
                        crankSpeed
                    ) *
                    pedalAdjustments.crankRadius,

                    Mathf.Sin(
                        Mathf.Deg2Rad *
                        crankSpeed
                    ) *
                    pedalAdjustments.crankRadius
                );

            if (cycleGeometry.fGear != null)
            {
                cycleGeometry.fGear
                    .transform
                    .rotation =
                    cycleGeometry.crank
                    .transform
                    .rotation;
            }

            if (cycleGeometry.rGear != null)
            {
                cycleGeometry.rGear
                    .transform
                    .rotation =
                    rPhysicsWheel
                    .transform
                    .rotation;
            }

            // ======================================================
            // Oscillation
            // ======================================================

            if (
                (sprint &&
                 currentSpeed > 5f &&
                 !isReversing) ||
                isAirborne ||
                isBunnyHopping)
            {
                pickUpSpeed +=
                    Time.deltaTime * 2f;
            }
            else
            {
                pickUpSpeed -=
                    Time.deltaTime * 2f;
            }

            pickUpSpeed =
                Mathf.Clamp(
                    pickUpSpeed,
                    0.1f,
                    1f
                );

            /*
             * safeCurrentSpeed prevents division by zero
             * immediately after spawn.
             */
            cycleOscillation =
    -Mathf.Sin(
        Mathf.Deg2Rad *
        (crankSpeed + 90f)
    ) *
    (
        oscillationAmount *
        Mathf.Clamp(
            currentTopSpeed /
            safeCurrentSpeed,
            1f,
            1.5f
        )
    ) *
    pickUpSpeed;

            turnLeanAmount =
                0f;

            /*
             * Hardware simulator:
             * no artificial steering oscillation.
             */
            if (isSimulatorVehicle)
            {
                oscillationSteerEffect =
                    0f;
            }
            else
            {
                oscillationSteerEffect =
                    cycleOscillation *
                    Mathf.Clamp01(
                        customAccelerationAxis
                    ) *
                    (
                        oscillationAffectSteerRatio *
                        Mathf.Clamp(
                            topSpeed /
                            safeCurrentSpeed,
                            1f,
                            1.5f
                        )
                    );
            }

            // ======================================================
            // Friction
            // ======================================================

            if (wheelFrictionSettings.fPhysicMaterial != null)
            {
                wheelFrictionSettings
                    .fPhysicMaterial
                    .staticFriction =
                    wheelFrictionSettings
                    .fFriction.x;

                wheelFrictionSettings
                    .fPhysicMaterial
                    .dynamicFriction =
                    wheelFrictionSettings
                    .fFriction.y;
            }

            if (wheelFrictionSettings.rPhysicMaterial != null)
            {
                wheelFrictionSettings
                    .rPhysicMaterial
                    .staticFriction =
                    wheelFrictionSettings
                    .rFriction.x;

                wheelFrictionSettings
                    .rPhysicMaterial
                    .dynamicFriction =
                    wheelFrictionSettings
                    .rFriction.y;
            }

            if (
                Physics.Raycast(
                    fPhysicsWheel.transform.position,
                    Vector3.down,
                    out hit,
                    Mathf.Infinity
                ) &&
                hit.distance < 0.5f)
            {
                Vector3 localVelocity =
                    fPhysicsWheel
                    .transform
                    .InverseTransformDirection(
                        fWheelRb.velocity
                    );

                float frictionSum =
                    wheelFrictionSettings
                    .fFriction.x +
                    wheelFrictionSettings
                    .fFriction.y;

                if (Mathf.Abs(frictionSum) > 0.0001f)
                {
                    localVelocity.x *=
                        Mathf.Clamp01(
                            1f /
                            frictionSum
                        );
                }

                fWheelRb.velocity =
                    fPhysicsWheel
                    .transform
                    .TransformDirection(
                        localVelocity
                    );
            }

            if (
                Physics.Raycast(
                    rPhysicsWheel.transform.position,
                    Vector3.down,
                    out hit,
                    Mathf.Infinity
                ) &&
                hit.distance < 0.5f)
            {
                Vector3 localVelocity =
                    rPhysicsWheel
                    .transform
                    .InverseTransformDirection(
                        rWheelRb.velocity
                    );

                float frictionSum =
                    wheelFrictionSettings
                    .rFriction.x +
                    wheelFrictionSettings
                    .rFriction.y;

                if (Mathf.Abs(frictionSum) > 0.0001f)
                {
                    localVelocity.x *=
                        Mathf.Clamp01(
                            1f /
                            frictionSum
                        );
                }

                rWheelRb.velocity =
                    rPhysicsWheel
                    .transform
                    .TransformDirection(
                        localVelocity
                    );
            }

            // ======================================================
            // Impact sensing
            // ======================================================

            deceleration =
                (fWheelRb.velocity -
                 lastVelocity) /
                Time.fixedDeltaTime;

            lastVelocity =
                fWheelRb.velocity;

            impactFrames--;

            impactFrames =
                Mathf.Clamp(
                    impactFrames,
                    0,
                    15
                );

            if (
                deceleration.y > 200f &&
                lastDeceleration.y < -1f)
            {
                impactFrames = 30;
            }

            lastDeceleration =
                deceleration;

            if (
                impactFrames > 0 &&
                inelasticCollision)
            {
                fWheelRb.velocity =
                    new Vector3(
                        fWheelRb.velocity.x,
                        -Mathf.Abs(
                            fWheelRb.velocity.y
                        ),
                        fWheelRb.velocity.z
                    );

                rWheelRb.velocity =
                    new Vector3(
                        rWheelRb.velocity.x,
                        -Mathf.Abs(
                            rWheelRb.velocity.y
                        ),
                        rWheelRb.velocity.z
                    );
            }

            // ======================================================
            // Air state
            // ======================================================

            if (
                Physics.Raycast(
                    transform.position +
                    Vector3.up,
                    Vector3.down,
                    out hit,
                    Mathf.Infinity
                ))
            {
                if (
                    hit.distance > 2f ||
                    impactFrames > 0)
                {
                    isAirborne = true;
                    restingCrank = 100f;
                }
                else if (isBunnyHopping)
                {
                    restingCrank = 100f;
                }
                else
                {
                    isAirborne = false;
                    restingCrank = 10f;
                }

                if (
                    hit.distance >
                    airTimeSettings.heightThreshold &&
                    airTimeSettings.freestyle)
                {
                    stuntMode = true;

                    rb.AddTorque(
                        Vector3.up *
                        customSteerAxis *
                        4f *
                        airTimeSettings
                            .airTimeRotationSensitivity,
                        ForceMode.Impulse
                    );

                    rb.AddTorque(
                        transform.right *
                        rawCustomAccelerationAxis *
                        -3f *
                        airTimeSettings
                            .airTimeRotationSensitivity,
                        ForceMode.Impulse
                    );
                }
                else
                {
                    stuntMode = false;
                }
            }

            // ======================================================
            // Bicycle rotational orientation
            // ======================================================

            if (airTimeSettings.freestyle)
            {
                if (
                    !stuntMode &&
                    isAirborne)
                {
                    transform.rotation =
                        Quaternion.Lerp(
                            transform.rotation,
                            Quaternion.Euler(
                                0f,
                                transform.rotation
                                    .eulerAngles.y,
                                turnLeanAmount +
                                cycleOscillation +
                                GroundConformity(
                                    groundConformity
                                )
                            ),
                            Time.deltaTime *
                            airTimeSettings
                                .groundSnapSensitivity
                        );
                }
                else if (
                    !stuntMode &&
                    !isAirborne)
                {
                    transform.rotation =
                        Quaternion.Lerp(
                            transform.rotation,
                            Quaternion.Euler(
                                transform.rotation
                                    .eulerAngles.x,

                                transform.rotation
                                    .eulerAngles.y,

                                turnLeanAmount +
                                cycleOscillation +
                                GroundConformity(
                                    groundConformity
                                )
                            ),
                            Time.deltaTime *
                            10f *
                            airTimeSettings
                                .groundSnapSensitivity
                        );
                }
            }
            else
            {
                transform.rotation =
                    Quaternion.Euler(
                        transform.rotation
                            .eulerAngles.x,

                        transform.rotation
                            .eulerAngles.y,

                        turnLeanAmount +
                        cycleOscillation +
                        GroundConformity(
                            groundConformity
                        )
                    );
            }

            // ======================================================
            // Wheelie
            // ======================================================

            if (
                !isAirborne &&
                wheelieInput &&
                rawCustomAccelerationAxis > 0f)
            {
                // Unity 2022 API
                rb.angularDrag = 15f;

                wheeliePower =
                    customAccelerationAxis *
                    150f *
                    Convert.ToInt32(
                        wheelieToggle
                    );

                Quaternion rot =
                    Quaternion.FromToRotation(
                        transform.forward,
                        new Vector3(
                            transform.forward.x,
                            0.75f,
                            transform.forward.z
                        )
                    );

                rb.AddTorque(
                    new Vector3(
                        rot.x,
                        rot.y,
                        rot.z
                    ) *
                    wheeliePower,
                    ForceMode.Acceleration
                );
            }
            else
            {
                // Unity 2022 API
                rb.angularDrag = 1f;
            }
            }

        // ==========================================================
        // Inputs
        // ==========================================================

        private static bool GameplaySpaceHeld()
        {
            if (
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.LeftCommand) ||
                Input.GetKey(KeyCode.RightCommand))
            {
                return false;
            }

            return Input.GetKey(
                KeyCode.Space
            );
        }

        private void ApplyCustomInput()
        {
            if (
                wayPointSystem.recordingState ==
                    WayPointSystem.RecordingState.DoNothing ||
                wayPointSystem.recordingState ==
                    WayPointSystem.RecordingState.Record)
            {
                // ==================================================
                // SUMO vehicle
                // ==================================================

                if (isSumoVehicle)
                {
                    rbMarker.x =
                        rb.position.x;

                    rbMarker.y =
                        rb.position.z;

                    bool insidePhysicsArea =
                        Vehicle.SumoVehicleDetect(
                            ref sock,
                            id
                        );

                    Vector2 sumoGroundTruth =
                        Vehicle.SUMO_groundtruth_back(
                            ref sock,
                            id
                        );

                    SUMOMarker.x =
                        sumoGroundTruth.x;

                    SUMOMarker.y =
                        sumoGroundTruth.y;

                    if (insidePhysicsArea)
                    {
                        float steeringGain =
                            0.2f;

                        if (rb.isKinematic)
                        {
                            rb.isKinematic =
                                false;

                            var (steeringValue, torqueInput, desiredVelocity) =
    Vehicle.SumoVehicleControlWarmup(
        ref sock,
        id,
        rb,
        steeringGain,
        ref pidControllerSpeed,
        ref pidControllerDist,
        ref lookAheadMarker
    );

                            customSteerAxis = Mathf.Clamp(steeringValue, -1f, 1f);
                            customLeanAxis = Mathf.Clamp(steeringValue / 2f, -1f, 1f);

                            customAccelerationAxis = Mathf.Clamp(torqueInput, -1f, 1f);
                            rawCustomAccelerationAxis = customAccelerationAxis;

                            rb.isKinematic = desiredVelocity < 0.1f;
                        }
                        else
                        {
                            var (steeringValue, torqueInput, desiredVelocity) =
    Vehicle.SumoVehicleControl(
        ref sock,
        id,
        rb,
        steeringGain,
        ref pidControllerSpeed,
        ref pidControllerDist,
        ref lookAheadMarker
    );

                            customSteerAxis = Mathf.Clamp(steeringValue, -1f, 1f);
                            customLeanAxis = Mathf.Clamp(steeringValue / 2f, -1f, 1f);

                            customAccelerationAxis = Mathf.Clamp(torqueInput, -1f, 1f);
                            rawCustomAccelerationAxis = customAccelerationAxis;

                            rb.isKinematic = desiredVelocity < 0.1f;
                        }
                    }
                    else if (allowSumoTeleport)
                    {
                        float steeringGain =
                            0.2f;

                        customSteerAxis =
                            0f;

                        customLeanAxis =
                            0f;

                        customAccelerationAxis =
                            0f;

                        rawCustomAccelerationAxis =
                            0f;

                        rb.velocity =
                            new Vector3(
                                0f,
                                0f,
                                5f
                            );

                        rb =
                            Vehicle.SumoVehicleTeleport(
                                ref sock,
                                id,
                                rb,
                                steeringGain,
                                ref pidControllerSpeed,
                                ref pidControllerDist,
                                ref lookAheadMarker
                            );

                        rb.isKinematic =
                            true;
                    }
                }

                // ==================================================
                // Simulator bike
                // ==================================================

                else if (isSimulatorVehicle)
                {
                                 
                    bool wahooOk =
                        tcp_bike_connection != null &&
                        tcp_bike_connection
                            .IsConnected();

                    bool fanatecOk =
                        steeringInput != null &&
                        steeringInput
                            .HasSteeringDevice;

                    bool arduinoOk =
                        arduinoConnected &&
                        sp != null &&
                        sp.IsOpen;

                    actual_velocity_unity_bike =
                        rb.velocity.magnitude;

                    // ==============================================
                    // Speed / pedalling
                    // ==============================================

                    if (
                        useConstantVelocity ||
                        wahooOk)
                    {
                        if (useConstantVelocity)
                        {
                            target_velocity_wahoo_bike =
                                constantSimVelocity;
                        }
                        else
                        {
                            target_velocity_wahoo_bike =
    ((float)tcp_bike_connection.targetOutputVelocity / 3.6f)
    * globalSpeedGainSimBike;
                        }

                        if (wahooOk)
                        {
                            acceleration =
    ((float)tcp_bike_connection.targetOutputPower) / mass_kg;
                        }
                        else
                        {
                            acceleration =
                                0f;
                        }

                        if (
                            useConstantVelocity &&
                            !wahooOk)
                        {
                            velocity =
                                Mathf.Clamp(
                                    constantSimVelocity,
                                    0f,
                                    topSpeed
                                );
                        }

                        if (arduinoOk)
                        {
                            ApplyArduinoBrakeToAcceleration();
                        }
                        else
                        {
                            ApplyKeyboardBrakeToWahooLoop();
                        }

                        if (
                            velocity <= 0f &&
                            acceleration < 0f)
                        {
                            acceleration =
                                0f;
                        }

                        velocity +=
                            globalaccGainSimBike *
                            acceleration *
                            Time.deltaTime;

                        velocity =
                            Mathf.Clamp(
                                velocity,
                                0f,
                                topSpeed
                            );

                        float torqueInput =
    piControllerSpeedWahoo != null
        ? piControllerSpeedWahoo
            .Control(
                velocity,
                actual_velocity_unity_bike
            )
        : 0f;

                        /*
                         * Only gently settle at true zero target speed.
                         * We DO NOT zero front/rear/root rigidbody
                         * angular velocity every physics frame.
                         */
                        if (velocity < 0.1f)
                        {
                            float stopGain =
                                0.5f;

                            rb.velocity =
                                Vector3.Lerp(
                                    rb.velocity,
                                    Vector3.zero,
                                    Time.deltaTime *
                                    stopGain
                                );

                            rb.angularVelocity =
                                Vector3.Lerp(
                                    rb.angularVelocity,
                                    Vector3.zero,
                                    Time.deltaTime *
                                    stopGain
                                );
                        }

                        customAccelerationAxis =
                            Mathf.Clamp(
                                torqueInput,
                                -1f,
                                1f
                            );

                        rawCustomAccelerationAxis =
                            customAccelerationAxis;
                    }
                    else
                    {
                        leftBrakeSignal =
                            0f;

                        rightBrakeSignal =
                            0f;

                        CustomInput(
                            "Vertical",
                            ref customAccelerationAxis,
                            1f,
                            1f,
                            false
                        );

                        CustomInput(
                            "Vertical",
                            ref rawCustomAccelerationAxis,
                            1f,
                            1f,
                            true
                        );

                        if (
                            Input.GetKey(KeyCode.S) ||
                            GameplaySpaceHeld())
                        {
                            customAccelerationAxis =
                                Mathf.Min(
                                    customAccelerationAxis,
                                    -1f
                                );

                            rawCustomAccelerationAxis =
                                customAccelerationAxis;
                        }
                    }

                    // ==============================================
                    // Fanatec steering
                    // ==============================================

                    if (fanatecOk)
                    {
                        steeringValueFanatec =
                            steeringInput
                                .steeringInputCorrected;

                        const float steeringDeadzone =
                            0.01f;

                        if (
                            Mathf.Abs(
                                steeringValueFanatec
                            ) <
                            steeringDeadzone)
                        {
                            steeringValueFanatec =
                                0f;
                        }

                        float steeringGain =
                            globalSteeringGain;

                        customSteerAxis =
                            steeringValueFanatec *
                            steeringGain;

                        customLeanAxis =
                            steeringValueFanatec *
                            steeringGain /
                            2f;

                        customSteerAxis =
                            Mathf.Clamp(
                                customSteerAxis,
                                -1f,
                                1f
                            );

                        customLeanAxis =
                            Mathf.Clamp(
                                customLeanAxis /
                                2f,
                                -1f,
                                1f
                            );
                    }
                    else
                    {
                        CustomInput(
                            "Horizontal",
                            ref customSteerAxis,
                            5f,
                            5f,
                            false
                        );

                        CustomInput(
                            "Horizontal",
                            ref customLeanAxis,
                            1f,
                            1f,
                            false
                        );
                    }
                }

                // ==================================================
                // Normal keyboard bicycle
                // ==================================================

                else
                {
                    CustomInput(
                        "Horizontal",
                        ref customSteerAxis,
                        5f,
                        5f,
                        false
                    );

                    CustomInput(
                        "Vertical",
                        ref customAccelerationAxis,
                        1f,
                        1f,
                        false
                    );

                    CustomInput(
                        "Horizontal",
                        ref customLeanAxis,
                        1f,
                        1f,
                        false
                    );

                    CustomInput(
                        "Vertical",
                        ref rawCustomAccelerationAxis,
                        1f,
                        1f,
                        true
                    );
                }

                sprint =
                    Input.GetKey(
                        KeyCode.LeftShift
                    );

                /*
                 * Physical experiment bike:
                 * disable accidental wheelie.
                 */
                wheelieInput =
                    !isSimulatorVehicle &&
                    Input.GetKey(
                        KeyCode.LeftControl
                    );

                /*
                 * Physical experiment bike:
                 * Space is braking fallback, NOT bunny hop.
                 */
                if (isSimulatorVehicle)
                {
                    bunnyHopInputState =
                        0;
                }
                else if (
                    Input.GetKey(
                        KeyCode.Space
                    ))
                {
                    bunnyHopInputState =
                        1;
                }
                else if (
                    Input.GetKeyUp(
                        KeyCode.Space
                    ))
                {
                    bunnyHopInputState =
                        -1;
                }
                else
                {
                    bunnyHopInputState =
                        0;
                }

                // ==================================================
                // Recording
                // ==================================================

                if (
                    wayPointSystem.recordingState ==
                    WayPointSystem.RecordingState.Record)
                {
                    if (
                        Time.frameCount %
                        wayPointSystem.frameIncrement ==
                        0)
                    {
                        wayPointSystem
                            .bicyclePositionTransform
                            .Add(
                                new Vector3(
                                    Mathf.Round(
                                        transform.position.x *
                                        100f
                                    ) *
                                    0.01f,

                                    Mathf.Round(
                                        transform.position.y *
                                        100f
                                    ) *
                                    0.01f,

                                    Mathf.Round(
                                        transform.position.z *
                                        100f
                                    ) *
                                    0.01f
                                )
                            );

                        wayPointSystem
                            .bicycleRotationTransform
                            .Add(
                                transform.rotation
                            );

                        wayPointSystem
                            .movementInstructionSet
                            .Add(
                                new Vector2Int(
                                    (int)Input.GetAxisRaw(
                                        "Horizontal"
                                    ),

                                    (int)Input.GetAxisRaw(
                                        "Vertical"
                                    )
                                )
                            );

                        wayPointSystem
                            .sprintInstructionSet
                            .Add(
                                sprint
                            );

                        wayPointSystem
                            .bHopInstructionSet
                            .Add(
                                bunnyHopInputState
                            );
                    }
                }
            }
            else if (
                wayPointSystem.recordingState ==
                    WayPointSystem.RecordingState.Playback &&
                allowWayPointPlayback)
            {
                int index =
                    Time.frameCount /
                    wayPointSystem.frameIncrement;

                if (
                    wayPointSystem
                        .movementInstructionSet
                        .Count -
                    1 >
                    index)
                {
                    transform.position =
                        Vector3.Lerp(
                            transform.position,

                            wayPointSystem
                                .bicyclePositionTransform[
                                    index
                                ],

                            Time.deltaTime *
                            wayPointSystem
                                .frameIncrement
                        );

                    transform.rotation =
                        Quaternion.Lerp(
                            transform.rotation,

                            wayPointSystem
                                .bicycleRotationTransform[
                                    index
                                ],

                            Time.deltaTime *
                            wayPointSystem
                                .frameIncrement
                        );

                    WayPointInput(
                        wayPointSystem
                            .movementInstructionSet[
                                index
                            ].x,

                        ref customSteerAxis,
                        5f,
                        5f,
                        false
                    );

                    WayPointInput(
                        wayPointSystem
                            .movementInstructionSet[
                                index
                            ].y,

                        ref customAccelerationAxis,
                        1f,
                        1f,
                        false
                    );

                    WayPointInput(
                        wayPointSystem
                            .movementInstructionSet[
                                index
                            ].x,

                        ref customLeanAxis,
                        1f,
                        1f,
                        false
                    );

                    WayPointInput(
                        wayPointSystem
                            .movementInstructionSet[
                                index
                            ].y,

                        ref rawCustomAccelerationAxis,
                        1f,
                        1f,
                        true
                    );

                    sprint =
                        wayPointSystem
                            .sprintInstructionSet[
                                index
                            ];

                    bunnyHopInputState =
                        wayPointSystem
                            .bHopInstructionSet[
                                index
                            ];
                }
            }
        }

        // ==========================================================
        // Arduino brake
        // ==========================================================

        private void ApplyArduinoBrakeToAcceleration()
        {
            try
            {
                if (
                    sp == null ||
                    !sp.IsOpen ||
                    sp.BytesToRead <= 0)
                {
                    return;
                }

                string data;

                try
                {
                    data =
                        sp.ReadLine().Trim();
                }
                catch (TimeoutException)
                {
                    data =
                        sp.ReadExisting().Trim();

                    string[] lines =
                        data.Split(
                            new[]
                            {
                                '\r',
                                '\n'
                            },
                            StringSplitOptions
                                .RemoveEmptyEntries
                        );

                    if (lines.Length == 0)
                        return;

                    data =
                        lines[
                            lines.Length - 1
                        ];
                }

                if (string.IsNullOrEmpty(data))
                    return;

                string[] values =
                    data.Split(',');

                if (values.Length < 2)
                    return;

                if (
                    float.TryParse(
                        values[0].Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float rawLeftBrake
                    ) &&
                    float.TryParse(
                        values[1].Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float rawRightBrake
                    ))
                {
                    leftBrakeSignal =
                        rawLeftBrake -
                        16.5f;

                    rightBrakeSignal =
                        rawRightBrake -
                        17f;

                    float totalBrakeDeceleration =
                        (
                            leftBrakeSignal +
                            rightBrakeSignal
                        ) /
                        200f *
                        brakeSensitivity;

                    acceleration -=
                        totalBrakeDeceleration;
                }
            }
            catch (TimeoutException)
            {
            }
            catch (System.IO.IOException)
            {
                arduinoConnected =
                    false;

                arduinoErrorMessage =
                    "Arduino connection lost.";
            }
            catch (InvalidOperationException)
            {
                arduinoConnected =
                    false;

                arduinoErrorMessage =
                    "Arduino serial port is not open.";
            }
            catch
            {
            }
        }

        private void ApplyKeyboardBrakeToWahooLoop()
        {
            leftBrakeSignal =
                0f;

            rightBrakeSignal =
                0f;

            if (
                Input.GetKey(KeyCode.S) ||
                GameplaySpaceHeld())
            {
                acceleration -=
                    brakeSensitivity;

                velocity *=
                    Mathf.Clamp01(
                        1f -
                        Time.deltaTime *
                        4f
                    );
            }
        }

        // ==========================================================
        // Input helpers
        // ==========================================================

        private float CustomInput(
            string name,
            ref float axis,
            float sensitivity,
            float gravity,
            bool isRaw)
        {
            float input =
                Input.GetAxisRaw(name);

            float t =
                Time.unscaledDeltaTime;

            if (isRaw)
            {
                axis = input;
            }
            else
            {
                if (input != 0f)
                {
                    axis =
                        Mathf.Clamp(
                            axis +
                            input *
                            sensitivity *
                            t,
                            -1f,
                            1f
                        );
                }
                else
                {
                    axis =
                        Mathf.Clamp01(
                            Mathf.Abs(axis) -
                            gravity *
                            t
                        ) *
                        Mathf.Sign(axis);
                }
            }

            return axis;
        }

        private float WayPointInput(
            float instruction,
            ref float axis,
            float sensitivity,
            float gravity,
            bool isRaw)
        {
            float t =
                Time.unscaledDeltaTime;

            if (isRaw)
            {
                axis =
                    instruction;
            }
            else
            {
                if (instruction != 0f)
                {
                    axis =
                        Mathf.Clamp(
                            axis +
                            instruction *
                            sensitivity *
                            t,
                            -1f,
                            1f
                        );
                }
                else
                {
                    axis =
                        Mathf.Clamp01(
                            Mathf.Abs(axis) -
                            gravity *
                            t
                        ) *
                        Mathf.Sign(axis);
                }
            }

            return axis;
        }

        private IEnumerator DelayBunnyHop()
        {
            yield return
                new WaitForSeconds(
                    0.5f
                );

            isBunnyHopping =
                false;
        }

        // ==========================================================
        // Logging
        // ==========================================================

        private IEnumerator LogData()
        {
            while (true)
            {
                simulationTime +=
                    Time.deltaTime;

                using (
                    StreamWriter writer =
                        new StreamWriter(
                            filePath,
                            true
                        ))
                {
                    writer.WriteLine(
                        $"{simulationTime}," +
                        $"{target_velocity_wahoo_bike}," +
                        $"{actual_velocity_unity_bike}," +
                        $"{customAccelerationAxis}"
                    );
                }

                yield return
                    new WaitForEndOfFrame();
            }
        }

        // ==========================================================
        // Arduino reconnect / diagnostics
        // ==========================================================

        public void AttemptArduinoReconnection()
        {
            if (
                sp != null &&
                sp.IsOpen)
            {
                try
                {
                    sp.Close();
                }
                catch
                {
                }
            }

            InitializeArduinoConnection();
        }

        public void TestArduinoCommunication()
        {
            if (
                !arduinoConnected ||
                sp == null ||
                !sp.IsOpen)
            {
                return;
            }

            try
            {
                if (sp.BytesToRead > 0)
                    sp.ReadLine();
            }
            catch
            {
            }
        }

        public void DisplayArduinoStatus()
        {
            if (
                enableArduinoDebugLogs &&
                Time.frameCount % 300 == 0 &&
                arduinoConnected &&
                sp != null &&
                sp.IsOpen)
            {
                Debug.Log(
                    "Arduino Status: Connected - Port: " +
                    actualComPort +
                    ", BaudRate: " +
                    baudRate +
                    ", BytesToRead: " +
                    sp.BytesToRead
                );
            }
        }

        // ==========================================================
        // Gizmos
        // ==========================================================

        private void OnDrawGizmos()
        {
            if (!bDrawGizmo)
                return;

            Gizmos.color =
                Color.red;

            Gizmos.DrawSphere(
                new Vector3(
                    lookAheadMarker.x,
                    0.1f,
                    lookAheadMarker.y
                ),
                1f
            );

            Gizmos.color =
                Color.blue;

            Gizmos.DrawSphere(
                new Vector3(
                    rbMarker.x,
                    0.1f,
                    rbMarker.y
                ),
                1f
            );

            Gizmos.color =
                Color.green;

            Gizmos.DrawSphere(
                new Vector3(
                    SUMOMarker.x,
                    0.1f,
                    SUMOMarker.y
                ),
                1f
            );
        }
    }
}