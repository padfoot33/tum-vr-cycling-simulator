using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO.Ports;
using System.Globalization;

using tumvt.sumounity;

    // namespace DebugStuff
    // {
    //     public class ConsoleToGUI : MonoBehaviour
    //     {
    // //#if !UNITY_EDITOR
    //         static string myLog = "";
    //         private string output;
    //         private string stack;
    
    //         void OnEnable()
    //         {
    //             Application.logMessageReceived += Log;
    //         }
    
    //         void OnDisable()
    //         {
    //             Application.logMessageReceived -= Log;
    //         }
    
    //         public void Log(string logString, string stackTrace, LogType type)
    //         {
    //             output = logString;
    //             stack = stackTrace;
    //             myLog = output + "" + myLog;
    //             if (myLog.Length > 5000)
    //             {
    //             myLog = myLog.Substring(0, 4000);
    //             }
    //             }

    //         void OnGUI()
    //         {
    //             //if (!Application.isEditor) //Do not display in editor ( or you can use the UNITY_EDITOR macro to also disable the rest)
    //             {
    //                 myLog = GUI.TextArea(new Rect(10, 10, Screen.width - 10, Screen.height - 10), myLog);
    //             }
    //         }
    // //#endif
    //     }
    // }

// Please use using SBPScripts; directive to refer to or append the SBP library
namespace SBPScripts.Simulator
{
// Cycle Geometry Class - Holds Gameobjects pertaining to the specific bicycle
    [System.Serializable]
    public class CycleGeometry
    {
        public GameObject handles, lowerFork, fWheelVisual, RWheel, crank, lPedal, rPedal, fGear, rGear;
    }
    //Pedal Adjustments Class - Manipulates pedals and their positioning.  
    [System.Serializable]
    public class PedalAdjustments
    {
        public float crankRadius;
        public Vector3 lPedalOffset, rPedalOffset;
        public float pedalingSpeed;
    }
    // Wheel Friction Settings Class - Uses Physics Materials and Physics functions to control the 
    // static / dynamic slipping of the wheels 
    [System.Serializable]
    public class WheelFrictionSettings
    {
        public PhysicsMaterial fPhysicMaterial, rPhysicMaterial;
        public Vector2 fFriction, rFriction;
    }
    // Way Point System Class - Replay Ghosting system
    [System.Serializable]
    public class WayPointSystem
    {
        public enum RecordingState { DoNothing, Record, Playback };
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
        [Range(0.5f, 10)]
        public float heightThreshold;
        public float groundSnapSensitivity;
    }
    public class BicycleSimulatorController : MonoBehaviour, IVehicleController 
    {
        ///for brake
        [Header("Arduino Configuration")]
        [SerializeField] private string defaultComPort = "COM3";
        [SerializeField] private int baudRate = 9600;
        [SerializeField] private int readTimeoutMs = 500; // Increased timeout for build mode
        [SerializeField] private bool autoDetectComPort = true;
        [SerializeField] private bool enableArduinoDebugLogs = true;
        
        SerialPort sp;
        private bool arduinoConnected = false;
        private string arduinoErrorMessage = "";
        private string actualComPort = "";
        
        // Public properties to access Arduino connection status
        public bool IsArduinoConnected => arduinoConnected;
        public string ArduinoErrorMessage => arduinoErrorMessage;
        public string ActualComPort => actualComPort;
        public float leftBrakeSignal = 0.0f;   // left
        public float rightBrakeSignal = 0.0f;  // right
        public float brakeSensitivity = 10.0f;
        public float mass_kg = 80.0f; // mass of the bike + rider in kg
        float velocity = 0.0f; // velocity of the bike in m/s
        float acceleration = 0.0f; // acceleration of the bike in m/s^2  
        /// 
    	public bool useCommandLineInput = false;
        private bool useConstantVelocity = false;
        private float constantSimVelocity = 0.0f;

        public string id { get; set; } // SUMO identifier in Vehicle Dictionary
        public bool isSumoVehicle = true;   // TUM Used for sumo integration

        [Header("Wahoo Simulator")]
        public bool isSimulatorVehicle = false;   // TUM Used for simulator integration
        // public InputActionAsset wahooInputActions;
        // InputActionMap inputBikerActionMap;
        // InputAction steeringAction;
        float target_velocity_wahoo_bike;
        float actual_velocity_unity_bike;
        public GameObject BikeConnectorTCP;
        private tcp_client tcp_bike_connection;
        // [Header("CSV File")]
        // todo: local project filepath method
        public float globalSteeringGain= 60f;

        // speed controller for simulator bike (Wahoo)
        private PIController piControllerSpeedWahoo;
        public float proportionalTermWahoo = 2125.0f;
        public float globalSpeedGainSimBike = 0.5f;
        public float globalaccGainSimBike = 0.03f;
        
        // ============================================
        // SUMO Teleport Spawn Lock (prevent overwrite)
        // ============================================
        private bool allowSumoTeleport = true;  // Default: allow; disabled during spawn
        private string sumoTeleportDisabledReason = "";  // Track why it's disabled
        private int sumoTeleportDisableCount = 0;  // Reference count (support nested disable/enable)
        
        // ============================================
        // WayPoint Playback Spawn Lock (prevent overwrite)
        // ============================================
        private bool allowWayPointPlayback = true;  // Default: allow; disabled during spawn
        private string wayPointPlaybackDisabledReason = "";  // Track why it's disabled
        private int wayPointPlaybackDisableCount = 0;  // Reference count (support nested disable/enable)
        
        private bool toggle = false;

        private Vector2 rbMarker;
        private Vector2 SUMOMarker;


        float steeringValueFanatec;
        FFBInspectorBike steeringInput;
        SimBikeSpawnController spawnController;

        public CycleGeometry cycleGeometry;
        public GameObject fPhysicsWheel, rPhysicsWheel;
        public WheelFrictionSettings wheelFrictionSettings;
        // Curve of Power Exerted over Input time by the cyclist
        // This class sets the physics materials on to the
        // tires of the bicycle. F Friction pertains to the front tire friction and R Friction to
        // the rear. They are of the Vector2 type. X field edits the static friction
        // information and Y edits the dynamic friction. Please keep the values over 0.5.
        // For more information, please read the commented scripts.
        public AnimationCurve accelerationCurve;
        [Tooltip("Steer Angle over Speed")]
        public AnimationCurve steerAngle;
        public float axisAngle;
        // Defines the leaning curve of the bicycle
        public AnimationCurve leanCurve;
        // The slider refers to the ratio of Relaxed mode to Top Speed. 
        // Torque is a physics based function which acts as the actual wheel driving force.
        public float torque, topSpeed;
        [Range(0.1f, 0.9f)]
        [Tooltip("Ratio of Relaxed mode to Top Speed")]
        public float relaxedSpeed;
        public float reversingSpeed;
        public Vector3 centerOfMassOffset;
        [HideInInspector]
        public bool isReversing, isAirborne, stuntMode;
        // Controls Cycle sway from left to right.
        // The degree of cycle waddling side to side upon pedaling.
        // Higher values correspond to higher waddling. This property also affects
        // character IK. 

        [Range(0, 8)]
        public float oscillationAmount;
        // Following the natural movement of a cyclist, the
        // oscillation of the cycle from side to side also affects the steering to a certain
        // extent. This value refers to the counter steer upon cycle oscillation. Higher
        // values correspond to a higher percentage of the oscillation being transferred
        // to the steering handles. 

        [Range(0, 1)]
        public float oscillationAffectSteerRatio;
        float oscillationSteerEffect;
        [HideInInspector]
        public float cycleOscillation;
        [HideInInspector]
        public Rigidbody rb, fWheelRb, rWheelRb;
        float turnAngle;
        float xQuat, zQuat;
        [HideInInspector]
        public float crankSpeed, crankCurrentQuat, crankLastQuat, restingCrank;
        public PedalAdjustments pedalAdjustments;
        [HideInInspector]
        public float turnLeanAmount;
        RaycastHit hit;
        [HideInInspector]
        public float customSteerAxis, customLeanAxis, customAccelerationAxis, rawCustomAccelerationAxis;
        bool isRaw, sprint;
        [HideInInspector]
        public bool wheelieInput;
        [HideInInspector]
        public float wheeliePower;
        public bool wheelieToggle;
        [HideInInspector]
        public int bunnyHopInputState;
        [HideInInspector]
        public float currentTopSpeed, pickUpSpeed;
        Quaternion initialLowerForkLocalRotaion, initialHandlesRotation;
        ConfigurableJoint fPhysicsWheelConfigJoint, rPhysicsWheelConfigJoint;
        // Ground Conformity refers to vehicles that do not need a gyroscopic force to keep them upright.
        // For non-gyroscopic wheel systems like the tricycle,
        // enabling ground conformity ensures that the tricycle is not always upright and
        // follows the curvature of the terrain. 
        public bool groundConformity;
        RaycastHit hitGround;
        Vector3 theRay;
        float groundZ;
        JointDrive fDrive, rYDrive, rZDrive;
        // Attempts to Reduce/eliminate bouncing of the bicycle after a fall impact 
        public bool inelasticCollision;
        [HideInInspector]
        public Vector3 lastVelocity, deceleration, lastDeceleration;
        int impactFrames;
        bool isBunnyHopping;
        [HideInInspector]
        public float bunnyHopAmount;
        // The upward force the rider can bunny hop with. 
        public float bunnyHopStrength;
        public WayPointSystem wayPointSystem;
        public AirTimeSettings airTimeSettings;

        // for sumo integration
        SumoSocketClient sock;
        private PIDController pidControllerSpeed;
        private PIDController pidControllerDist;

        private bool bDrawGizmo;
        private Vector2 lookAheadMarker;
        private float steeringValue;

        private float simulationTime;
        private string filePath;

        void GetConstantVelocityUsageFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string text = "";
            bool enableFFB = false;

            for (int i = 0; i < args.Length; i++)
            {
                if(args[i] == "--velocity" && i<args.Length-1)
                {
                    int payload = int.Parse(args[i+1]);
                    useConstantVelocity = true;
                    constantSimVelocity = (float)payload/3.6f; // convert to m/s
                }
            }

            return;
        }

        /// <summary>
        /// Initialize or reinitialize Arduino serial connection.
        /// Called from Start() and OnEnable() to ensure connection after script re-enable.
        /// </summary>
        void InitializeArduinoConnection()
        {
            // Skip if already connected
            if (sp != null && sp.IsOpen)
            {
                Debug.Log($"[BicycleSimulatorController] Arduino already connected on {actualComPort}");
                return;
            }

            // Determine which COM port to use
            if (autoDetectComPort)
            {
                actualComPort = DetectArduinoComPort();
            }
            else
            {
                actualComPort = defaultComPort;
            }

            if (!ArduinoPortIsAvailable(actualComPort))
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino port " + actualComPort + " is not present. Keyboard brake fallback.";
                return;
            }

            // Initialize SerialPort with detected/configured port
            sp = new SerialPort(actualComPort, baudRate);
            
            try
            {
                sp.Open();
                sp.ReadTimeout = readTimeoutMs;
                sp.WriteTimeout = readTimeoutMs;

                if (sp.BytesToRead > 0)
                {
                    string initialData = sp.ReadExisting();
                }
                
                arduinoConnected = true;
                arduinoErrorMessage = "";
                Debug.Log($"[BicycleSimulatorController] ✅ Arduino connected successfully on {actualComPort}");
            }
            catch (TimeoutException ex)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino connection timeout: The operation has timed out. Please check if the Arduino is properly connected to " + actualComPort + ".";
                Debug.LogWarning($"[BicycleSimulatorController] {arduinoErrorMessage}");
            }
            catch (UnauthorizedAccessException ex)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino access denied: Another application may be using " + actualComPort + ". Please close other applications using the serial port.";
                Debug.LogWarning($"[BicycleSimulatorController] {arduinoErrorMessage}");
            }
            catch (System.IO.IOException ex)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino connection failed: The specified port " + actualComPort + " was not found. Please check if the Arduino is connected to the correct port.";
                Debug.LogWarning($"[BicycleSimulatorController] {arduinoErrorMessage}");
            }
            catch (Exception ex)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino connection error: " + ex.Message;
                Debug.LogWarning($"[BicycleSimulatorController] {arduinoErrorMessage}");
            }
        }

        static bool ArduinoPortIsAvailable(string portName)
        {
            if (string.IsNullOrEmpty(portName)) return false;
            try
            {
                string[] availablePorts = SerialPort.GetPortNames();
                if (availablePorts == null || availablePorts.Length == 0) return false;
                return Array.Exists(availablePorts, port =>
                    port.Equals(portName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Automatically detects available COM ports and tries to find Arduino
        /// </summary>
        string DetectArduinoComPort()
        {
            try
            {
                string[] availablePorts = SerialPort.GetPortNames();
                if (availablePorts == null || availablePorts.Length == 0)
                    return defaultComPort;

                // First, try the default COM port
                if (Array.Exists(availablePorts, port => port.Equals(defaultComPort, StringComparison.OrdinalIgnoreCase)))
                {
                    // if (enableArduinoDebugLogs)
                      //  Debug.Log("Default COM port " + defaultComPort + " is available, trying it first.");
                    return defaultComPort;
                }

                // If default port is not available, try other common Arduino ports
                string[] commonArduinoPorts = { "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
                foreach (string testPort in commonArduinoPorts)
                {
                    if (Array.Exists(availablePorts, port => port.Equals(testPort, StringComparison.OrdinalIgnoreCase)))
                        return testPort;
                }

                for (int i = 0; i < availablePorts.Length; i++)
                {
                    if (LooksLikeArduinoPort(availablePorts[i]))
                        return availablePorts[i];
                }

                return defaultComPort;
            }
            catch (Exception)
            {
                return defaultComPort;
            }
        }

        static bool LooksLikeArduinoPort(string portName)
        {
            if (string.IsNullOrEmpty(portName)) return false;
            if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && portName.Length > 3)
                return true;
            string lower = portName.ToLowerInvariant();
            return lower.Contains("usbmodem")
                   || lower.Contains("usbserial")
                   || lower.Contains("arduino")
                   || lower.Contains("ttyacm")
                   || lower.Contains("ttyusb");
        }

        /// <summary>
        /// Tests if a COM port can be opened and might have an Arduino
        /// </summary>
        bool TestComPortConnection(string portName)
        {
            try
            {
                using (SerialPort testPort = new SerialPort(portName, baudRate))
                {
                    testPort.ReadTimeout = 1000; // Longer timeout for testing
                    testPort.WriteTimeout = 1000;
                    testPort.Open();
                    
                    // Try to read some data or send a simple command
                    System.Threading.Thread.Sleep(100); // Give Arduino time to respond
                    
                    if (testPort.BytesToRead > 0)
                    {
                        string testData = testPort.ReadExisting();
                        // if (enableArduinoDebugLogs)
                          //  Debug.Log("Test data from " + portName + ": " + testData);
                        return true;
                    }
                    
                    return true; // Port opened successfully, assume it's good
                }
            }
            catch
            {
                return false;
            }
        }


        void setCsvFile()
        {
            // Clear CSV file
            // File.WriteAllText(csvFilePath,string.Empty);

            // Add header target, actual
            // using (StreamWriter outputFile = File.AppendText(csvFilePath))
            // {
            //     outputFile.WriteLine("target_speed,actual_speed,torque," + proportionalTermWahoo);
            // }
        }
        
        /// <summary>
        /// Helper method to get the full parent hierarchy path for debugging.
        /// Example output: "SimBike(Clone)/Handles/..."
        /// </summary>
        private string GetFullHierarchyPath()
        {
            System.Collections.Generic.List<string> path = new System.Collections.Generic.List<string>();
            Transform current = transform;
            while (current != null)
            {
                path.Add(current.name);
                current = current.parent;
            }
            path.Reverse();
            return string.Join("/", path);
        }

        void Awake()
        {
            string parentPath = GetFullHierarchyPath();
            Debug.Log($"[BicycleSimulatorController] 🎯 AWAKE: Scene='{gameObject.scene.name}' | GameObject='{gameObject.name}' | Path='{parentPath}'");
            
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            spawnController = GetComponent<SimBikeSpawnController>();
            Debug.Log($"[BicycleSimulatorController] FLAGS isSumoVehicle={isSumoVehicle}, isSimulatorVehicle={isSimulatorVehicle}, id={id}");

            // Input from bike
            // inputBikerActionMap = wahooInputActions.FindActionMap("Biker");
            // steeringAction = inputBikerActionMap.FindAction("SteeringWindows");
        }

        void Start()
        {
            string parentPath = GetFullHierarchyPath();
            Debug.Log($"[BicycleSimulatorController] 🚀 START: Scene='{gameObject.scene.name}' | GameObject='{gameObject.name}' | Path='{parentPath}' | Active={gameObject.activeInHierarchy}");
            
            // Initialize Arduino connection for brake input
            InitializeArduinoConnection();

            bool logData = false;
            if (logData){
                filePath = "C:\\Users\\TUBVVTK-VTSIM14\\Downloads\\BikeData.csv";

                // Create the CSV file and write the header
                using (StreamWriter writer = new StreamWriter(filePath, false))
                {
                    writer.WriteLine("SimulationTime,TargetVelocityWahooBike,ActualVelocityUnityBike,TorqueInput");
                }

                // Start the coroutine to log data
                StartCoroutine(LogData());
            }

            rb = GetComponent<Rigidbody>();
            rb.maxAngularVelocity = Mathf.Infinity;
            rb.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
            spawnController = GetComponent<SimBikeSpawnController>();

            fWheelRb = fPhysicsWheel.GetComponent<Rigidbody>();
            fWheelRb.maxAngularVelocity = Mathf.Infinity;

            rWheelRb = rPhysicsWheel.GetComponent<Rigidbody>();
            rWheelRb.maxAngularVelocity = Mathf.Infinity;

            currentTopSpeed = topSpeed;

            initialHandlesRotation = cycleGeometry.handles.transform.localRotation;
            initialLowerForkLocalRotaion = cycleGeometry.lowerFork.transform.localRotation;

            fPhysicsWheelConfigJoint = fPhysicsWheel.GetComponent<ConfigurableJoint>();
            rPhysicsWheelConfigJoint = rPhysicsWheel.GetComponent<ConfigurableJoint>();

            //Recording is set to 0 to remove the recording previous data if not set to playback
            if (wayPointSystem.recordingState == WayPointSystem.RecordingState.Record || wayPointSystem.recordingState == WayPointSystem.RecordingState.DoNothing)
            {
                wayPointSystem.bicyclePositionTransform.Clear();
                wayPointSystem.bicycleRotationTransform.Clear();
                wayPointSystem.movementInstructionSet.Clear();
                wayPointSystem.sprintInstructionSet.Clear();
                wayPointSystem.bHopInstructionSet.Clear();
            }

            // get the socketclient with the step info
            // sock = GameObject.FindWithTag("GameController").GetComponent<SumoSocketClient>();
            sock = GameObject.FindObjectOfType<SumoSocketClient>();

            // velocity controller
            pidControllerDist = new PIDController(15.0f, 0.0f, 0.0f); 
            pidControllerSpeed = new PIDController(1.0f, 0.0f, 0.0f); 
            bDrawGizmo = true;
            
            Debug.Log($"[BicycleSimulatorController] Start() completed - Position after initialization: {transform.position}, Y={transform.position.y}");

            piControllerSpeedWahoo = new PIController(proportionalTermWahoo, 0.000000f); // PIController(P,I)
            // P - reaktiongeschw des reglers !!!!

            // get TCP Bike Connector Object
            if(isSimulatorVehicle)
            {
                if (BikeConnectorTCP != null)
                    tcp_bike_connection = BikeConnectorTCP.GetComponent<tcp_client>();
                steeringInput = GetComponent<FFBInspectorBike>();
            }

            // get the constant velocity from command line
            GetConstantVelocityUsageFromCommandLine();
           
        }
        
        /// <summary>
        /// Disable SUMO teleport to prevent overwriting spawn or other critical operations.
        /// Can be nested (multiple disable calls require equal enable calls).
        /// </summary>
        public void DisableSumoTeleport(string reason)
        {
            sumoTeleportDisableCount++;
            sumoTeleportDisabledReason = reason;
            allowSumoTeleport = false;
            Debug.Log($"[BicycleSimulatorController] 🔐 SUMO teleport DISABLED ({sumoTeleportDisableCount}x): {reason}");
        }
        
        /// <summary>
        /// Enable SUMO teleport after spawn or critical operation is complete.
        /// Decrements reference count; only enables if count reaches 0.
        /// </summary>
        public void EnableSumoTeleport(string reason)
        {
            sumoTeleportDisableCount = Mathf.Max(0, sumoTeleportDisableCount - 1);
            if (sumoTeleportDisableCount == 0)
            {
                allowSumoTeleport = true;
                Debug.Log($"[BicycleSimulatorController] 🔓 SUMO teleport ENABLED: {reason}");
            }
            else
            {
                Debug.Log($"[BicycleSimulatorController] ⏱️ SUMO teleport still disabled ({sumoTeleportDisableCount} nested): {reason}");
            }
        }
        
        /// <summary>
        /// Disable WayPoint playback to prevent overwriting spawn or other critical operations.
        /// Can be nested (multiple disable calls require equal enable calls).
        /// </summary>
        public void DisableWayPointPlayback(string reason)
        {
            wayPointPlaybackDisableCount++;
            wayPointPlaybackDisabledReason = reason;
            allowWayPointPlayback = false;
            Debug.Log($"[BicycleSimulatorController] 🔐 WayPoint playback DISABLED ({wayPointPlaybackDisableCount}x): {reason}");
        }
        
        /// <summary>
        /// Enable WayPoint playback after spawn or critical operation is complete.
        /// Decrements reference count; only enables if count reaches 0.
        /// </summary>
        public void EnableWayPointPlayback(string reason)
        {
            wayPointPlaybackDisableCount = Mathf.Max(0, wayPointPlaybackDisableCount - 1);
            if (wayPointPlaybackDisableCount == 0)
            {
                allowWayPointPlayback = true;
                Debug.Log($"[BicycleSimulatorController] 🔓 WayPoint playback ENABLED: {reason}");
            }
            else
            {
                Debug.Log($"[BicycleSimulatorController] ⏱️ WayPoint playback still disabled ({wayPointPlaybackDisableCount} nested): {reason}");
            }
        }

        public float GetSpeedMps()
        {
            if (rb == null) return 0f;
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            return v.magnitude;
        }

        public float GetSpeedKph() => GetSpeedMps() * 3.6f;

        public void HaltIntegratedVelocity()
        {
            velocity = 0f;
            acceleration = 0f;
            customAccelerationAxis = 0f;
            rawCustomAccelerationAxis = 0f;
        }

        bool IsWahooConnected()
        {
            return tcp_bike_connection != null && tcp_bike_connection.IsConnected();
        }

        bool HasRiderDriveInput()
        {
            if (IsWahooConnected())
                return true;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S)
                || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.Space)
                || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)
                || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
                return true;
            if (Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
                return true;
            if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f)
                return true;
            return false;
        }

        bool ShouldHoldIdle()
        {
            if (IsWahooConnected())
                return false;
            return !HasRiderDriveInput();
        }

        void HoldIdlePhysics()
        {
            HaltIntegratedVelocity();
            customSteerAxis = 0f;
            customLeanAxis = 0f;
            ZeroRigidbody(rb);
            ZeroRigidbody(fWheelRb);
            ZeroRigidbody(rWheelRb);
            spawnController?.ZeroAllVelocities();
        }

        static void ZeroRigidbody(Rigidbody body)
        {
            if (body == null) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        IEnumerator LogData()
        {
            while (true)
            {
                // Update the simulation time
                simulationTime += Time.deltaTime;

                // Write the data to the CSV file
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine($"{simulationTime},{target_velocity_wahoo_bike},{actual_velocity_unity_bike},{customAccelerationAxis}");
                }

                // Wait for the next frame
                yield return new WaitForEndOfFrame();
            }
        }


        private void OnEnable()
        {
            string parentPath = GetFullHierarchyPath();
            Debug.Log($"[BicycleSimulatorController] ✅ ONENABLE: Scene='{gameObject.scene.name}' | GameObject='{gameObject.name}' | Path='{parentPath}'");
            
            // Reinitialize Arduino connection when script is re-enabled (e.g., after spawn)
            // This fixes brake input loss when SimBikeSpawnController temporarily disables this script
            if (sp != null && !sp.IsOpen)
            {
                Debug.Log($"[BicycleSimulatorController] Arduino disconnected, attempting reconnection...");
                InitializeArduinoConnection();
            }
            
            // steeringAction.Enable();
            // inputBikerActionMap.Enable();
        }

        private void OnDisable()
        {
            // steeringAction.Disable();
            // inputBikerActionMap.Disable();
            
            // Clean up Arduino connection
            if (sp != null && sp.IsOpen)
            {
                try
                {
                    sp.Close();
                    sp.Dispose();
                    Debug.Log($"[BicycleSimulatorController] Arduino connection closed on {actualComPort} (will reconnect on re-enable)");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BicycleSimulatorController] Error closing Arduino connection: {ex.Message}");
                }
            }
        }

        void FixedUpdate()
        {
            if (wayPointSystem.recordingState == WayPointSystem.RecordingState.DoNothing || wayPointSystem.recordingState == WayPointSystem.RecordingState.Record)
            {
                if(isSumoVehicle){
                    bool isInsidePhsyicsArea = Vehicle.SumoVehicleDetect(ref sock, id);
                    if (isInsidePhsyicsArea)
                    {
                        ApplyPhysicsUpdate();
                    }

                }
            }
            if(isSimulatorVehicle)
            {
                ApplyPhysicsUpdate();
            }


        }
        void Update()
        {
            ApplyCustomInput();
            
            // Display Arduino connection status and errors
            DisplayArduinoStatus();

            //GetKeyUp/Down requires an Update Cycle
            //BunnyHopping
            if (bunnyHopInputState == 1)
            {
                isBunnyHopping = true;
                bunnyHopAmount += Time.deltaTime * 8f;
            }
            if (bunnyHopInputState == -1)
                StartCoroutine(DelayBunnyHop());

            if (bunnyHopInputState == -1 && !isAirborne)
                rb.AddForce(transform.up * bunnyHopAmount * bunnyHopStrength, ForceMode.VelocityChange);
            else
                bunnyHopAmount = Mathf.Lerp(bunnyHopAmount, 0, Time.deltaTime * 8f);

            bunnyHopAmount = Mathf.Clamp01(bunnyHopAmount);


        }
        float GroundConformity(bool toggle)
        {
            if (toggle)
            {
                groundZ = transform.rotation.eulerAngles.z;
            }
            return groundZ;

        }

        void ApplyPhysicsUpdate()
        {
            if (isSimulatorVehicle && ShouldHoldIdle())
            {
                HoldIdlePhysics();
                return;
            }

            //Physics based Steering Control.
            fPhysicsWheel.transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y + customSteerAxis * steerAngle.Evaluate(rb.linearVelocity.magnitude) + oscillationSteerEffect, 0);
            fPhysicsWheelConfigJoint.axis = new Vector3(1, 0, 0); 

            //Power Control. Wheel Torque + Acceleration curves

            //cache rb velocity
            float currentSpeed = rb.linearVelocity.magnitude;
            // Debug.Log("currentSpeed:"+currentSpeed);


            if (!sprint)
                currentTopSpeed = Mathf.Lerp(currentTopSpeed, topSpeed * relaxedSpeed, Time.deltaTime);
            else
                currentTopSpeed = Mathf.Lerp(currentTopSpeed, topSpeed, Time.deltaTime);
            

            if (currentSpeed < currentTopSpeed && rawCustomAccelerationAxis > 0){
                // Debug.Log("11111");
                rWheelRb.AddTorque(transform.right * torque * customAccelerationAxis);
            }

            if (currentSpeed < currentTopSpeed && rawCustomAccelerationAxis > 0 && !isAirborne && !isBunnyHopping){
                // Debug.Log("22222");
                rb.AddForce(transform.forward * accelerationCurve.Evaluate(customAccelerationAxis));
            }

            if (currentSpeed < reversingSpeed && rawCustomAccelerationAxis < 0 && !isAirborne && !isBunnyHopping){
                // Debug.Log("3333");
                rb.AddForce(-transform.forward * accelerationCurve.Evaluate(customAccelerationAxis) * 0.5f);
            }

            if (transform.InverseTransformDirection(rb.linearVelocity).z < 0){
                // Debug.Log("4444");
                isReversing = true;
            }
            else
                isReversing = false;

            if (rawCustomAccelerationAxis < 0 && isReversing == false && !isAirborne && !isBunnyHopping){
                // Debug.Log("5555");

                rb.AddForce(-transform.forward * accelerationCurve.Evaluate(customAccelerationAxis) * 2);
            }


            // Center of Mass handling
            if (stuntMode)
                rb.centerOfMass = GetComponent<BoxCollider>().center;
            else
                rb.centerOfMass = Vector3.zero + centerOfMassOffset;

            //Handles
            cycleGeometry.handles.transform.localRotation = Quaternion.Euler(0, customSteerAxis * steerAngle.Evaluate(currentSpeed) + oscillationSteerEffect * 5, 0) * initialHandlesRotation;

            //LowerFork
            cycleGeometry.lowerFork.transform.localRotation = Quaternion.Euler(0, customSteerAxis * steerAngle.Evaluate(currentSpeed) + oscillationSteerEffect * 5, customSteerAxis * -axisAngle) * initialLowerForkLocalRotaion;

            //FWheelVisual
            xQuat = Mathf.Sin(Mathf.Deg2Rad * (transform.rotation.eulerAngles.y));
            zQuat = Mathf.Cos(Mathf.Deg2Rad * (transform.rotation.eulerAngles.y));
            cycleGeometry.fWheelVisual.transform.rotation = Quaternion.Euler(xQuat * (customSteerAxis * -axisAngle), customSteerAxis * steerAngle.Evaluate(currentSpeed) + oscillationSteerEffect * 5, zQuat * (customSteerAxis * -axisAngle));
            cycleGeometry.fWheelVisual.transform.GetChild(0).transform.localRotation = cycleGeometry.RWheel.transform.rotation;

            //Crank
            crankCurrentQuat = cycleGeometry.RWheel.transform.rotation.eulerAngles.x;
            if (customAccelerationAxis > 0 && !isAirborne && !isBunnyHopping)
            {
                crankSpeed += Mathf.Sqrt(customAccelerationAxis * Mathf.Abs(Mathf.DeltaAngle(crankCurrentQuat, crankLastQuat) * pedalAdjustments.pedalingSpeed));
                crankSpeed %= 360;
            }
            else if (Mathf.Floor(crankSpeed) > restingCrank)
                crankSpeed += -6;
            else if (Mathf.Floor(crankSpeed) < restingCrank)
                crankSpeed = Mathf.Lerp(crankSpeed, restingCrank, Time.deltaTime * 5);

            crankLastQuat = crankCurrentQuat;
            cycleGeometry.crank.transform.localRotation = Quaternion.Euler(crankSpeed, 0, 0);

            //Pedals
            cycleGeometry.lPedal.transform.localPosition = pedalAdjustments.lPedalOffset + new Vector3(0, Mathf.Cos(Mathf.Deg2Rad * (crankSpeed + 180)) * pedalAdjustments.crankRadius, Mathf.Sin(Mathf.Deg2Rad * (crankSpeed + 180)) * pedalAdjustments.crankRadius);
            cycleGeometry.rPedal.transform.localPosition = pedalAdjustments.rPedalOffset + new Vector3(0, Mathf.Cos(Mathf.Deg2Rad * (crankSpeed)) * pedalAdjustments.crankRadius, Mathf.Sin(Mathf.Deg2Rad * (crankSpeed)) * pedalAdjustments.crankRadius);

            //FGear
            if (cycleGeometry.fGear != null)
                cycleGeometry.fGear.transform.rotation = cycleGeometry.crank.transform.rotation;
            //RGear
            if (cycleGeometry.rGear != null)
                cycleGeometry.rGear.transform.rotation = rPhysicsWheel.transform.rotation;

            //CycleOscillation
            if ((sprint && currentSpeed > 5 && isReversing == false) || isAirborne || isBunnyHopping)
                pickUpSpeed += Time.deltaTime * 2;
            else
                pickUpSpeed -= Time.deltaTime * 2;

            pickUpSpeed = Mathf.Clamp(pickUpSpeed, 0.1f, 1);

            cycleOscillation = -Mathf.Sin(Mathf.Deg2Rad * (crankSpeed + 90)) * (oscillationAmount * (Mathf.Clamp(currentTopSpeed / currentSpeed, 1f, 1.5f))) * pickUpSpeed;
            //turnLeanAmount = -leanCurve.Evaluate(customLeanAxis) * Mathf.Clamp(currentSpeed * 0.1f, 0, 1);
            turnLeanAmount = 0.0f;
            oscillationSteerEffect = cycleOscillation * Mathf.Clamp01(customAccelerationAxis) * (oscillationAffectSteerRatio * (Mathf.Clamp(topSpeed / currentSpeed, 1f, 1.5f)));

            //FrictionSettings
            wheelFrictionSettings.fPhysicMaterial.staticFriction = wheelFrictionSettings.fFriction.x;
            wheelFrictionSettings.fPhysicMaterial.dynamicFriction = wheelFrictionSettings.fFriction.y;
            wheelFrictionSettings.rPhysicMaterial.staticFriction = wheelFrictionSettings.rFriction.x;
            wheelFrictionSettings.rPhysicMaterial.dynamicFriction = wheelFrictionSettings.rFriction.y;

            if (Physics.Raycast(fPhysicsWheel.transform.position, Vector3.down, out hit, Mathf.Infinity))
                if (hit.distance < 0.5f)
                {
                    Vector3 velf = fPhysicsWheel.transform.InverseTransformDirection(fWheelRb.linearVelocity);
                    velf.x *= Mathf.Clamp01(1 / (wheelFrictionSettings.fFriction.x + wheelFrictionSettings.fFriction.y));
                    fWheelRb.linearVelocity = fPhysicsWheel.transform.TransformDirection(velf);
                }
            if (Physics.Raycast(rPhysicsWheel.transform.position, Vector3.down, out hit, Mathf.Infinity))
                if (hit.distance < 0.5f)
                {
                    Vector3 velr = rPhysicsWheel.transform.InverseTransformDirection(rWheelRb.linearVelocity);
                    velr.x *= Mathf.Clamp01(1 / (wheelFrictionSettings.rFriction.x + wheelFrictionSettings.rFriction.y));
                    rWheelRb.linearVelocity = rPhysicsWheel.transform.TransformDirection(velr);
                }

            //Impact sensing
            deceleration = (fWheelRb.linearVelocity - lastVelocity) / Time.fixedDeltaTime;
            lastVelocity = fWheelRb.linearVelocity;
            impactFrames--;
            impactFrames = Mathf.Clamp(impactFrames, 0, 15);
            if (deceleration.y > 200 && lastDeceleration.y < -1)
                impactFrames = 30;

            lastDeceleration = deceleration;

            if (impactFrames > 0 && inelasticCollision)
            {
                fWheelRb.linearVelocity = new Vector3(fWheelRb.linearVelocity.x, -Mathf.Abs(fWheelRb.linearVelocity.y), fWheelRb.linearVelocity.z);
                rWheelRb.linearVelocity = new Vector3(rWheelRb.linearVelocity.x, -Mathf.Abs(rWheelRb.linearVelocity.y), rWheelRb.linearVelocity.z);
            }

            //AirControl
            if (Physics.Raycast(transform.position + new Vector3(0, 1f, 0), Vector3.down, out hit, Mathf.Infinity))
            {
                if (hit.distance > 2f || impactFrames > 0)
                {
                    isAirborne = true;
                    restingCrank = 100;
                }
                else if (isBunnyHopping)
                {
                    restingCrank = 100;
                }
                else
                {
                    isAirborne = false;
                    restingCrank = 10;
                }
                // For stunts
                // 5f is the snap to ground distance
                if (hit.distance > airTimeSettings.heightThreshold && airTimeSettings.freestyle)
                {
                    stuntMode = true;
                    // Stunt + flips controls (Not available for Waypoint system as of yet)
                    // You may use Numpad Inputs as well.
                    rb.AddTorque(Vector3.up * customSteerAxis * 4 * airTimeSettings.airTimeRotationSensitivity, ForceMode.Impulse);
                    rb.AddTorque(transform.right * rawCustomAccelerationAxis * -3 * airTimeSettings.airTimeRotationSensitivity, ForceMode.Impulse);
                }
                else
                    stuntMode = false;
            }

            // Setting the Main Rotational movements of the bicycle
            if (airTimeSettings.freestyle)
            {
                if (!stuntMode && isAirborne)
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, transform.rotation.eulerAngles.y, turnLeanAmount + cycleOscillation + GroundConformity(groundConformity)), Time.deltaTime * airTimeSettings.groundSnapSensitivity);
                else if (!stuntMode && !isAirborne)
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, turnLeanAmount + cycleOscillation + GroundConformity(groundConformity)), Time.deltaTime * 10 * airTimeSettings.groundSnapSensitivity);
            }
            else
            {
                //Pre-version 1.5
                transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, turnLeanAmount + cycleOscillation + GroundConformity(groundConformity));
            }
            //Wheelie
            if(!isAirborne && wheelieInput && rawCustomAccelerationAxis>0)
            {
                rb.angularDamping = 15;
                wheeliePower = customAccelerationAxis*150*System.Convert.ToInt32(wheelieToggle);
                var rot = Quaternion.FromToRotation(transform.forward, new Vector3(transform.forward.x,0.75f,transform.forward.z));
                rb.AddTorque(new Vector3(rot.x, rot.y, rot.z) * wheeliePower, ForceMode.Acceleration);
            }
            else
            {
                rb.angularDamping = 1;
            }
        }

        void ApplyCustomInput()
        {
          //  Debug.Log("Target Velocity: "   + target_velocity_wahoo_bike.ToString("F1"));
          //  Debug.Log("Actual Velocity: "   + actual_velocity_unity_bike.ToString("F1"));
          //  Debug.Log("Torque Input: "   + customAccelerationAxis);
            if (wayPointSystem.recordingState == WayPointSystem.RecordingState.DoNothing || wayPointSystem.recordingState == WayPointSystem.RecordingState.Record)
            {
                if(isSumoVehicle){

                    // if (toggle)
                    // {
                    //     rbMarker.x = rb.position.x+2;
                    //     toggle = false;
                    // }
                    // else
                    // {
                    //     rbMarker.x = rb.position.x;
                    //     toggle = true;
                    // }
                    rbMarker.x = rb.position.x;
                    rbMarker.y = rb.position.z;


                    // extract bool from sumo stepinfo
                    bool isInsidePhsyicsArea = Vehicle.SumoVehicleDetect(ref sock, id);
                    Vector2 SUMO_groundtruth_xy = Vehicle.SUMO_groundtruth_back(ref sock, id);
                    SUMOMarker.x = SUMO_groundtruth_xy.x;
                    SUMOMarker.y = SUMO_groundtruth_xy.y;
                    if(isInsidePhsyicsArea){
                    // if(false){
                        if (rb.isKinematic){
                            rb.isKinematic = false;
                            float steeringGain = (float)0.2;
                            //check for sumo vehicle state lad (look ahead disgance bool)
                            var (steeringValue, torqueInput, desiredVelocity) = Vehicle.SumoVehicleControlWarmup(ref sock, id, rb, steeringGain, ref pidControllerSpeed, ref pidControllerDist, ref lookAheadMarker);

                            // set steering
                            customSteerAxis = steeringValue;
                            customLeanAxis = steeringValue/2;
                            customSteerAxis = Mathf.Clamp(customSteerAxis,-1f,1f);
                            customLeanAxis = Mathf.Clamp(customLeanAxis, -1f,1f);

                            // set torque
                            customAccelerationAxis = torqueInput; 
                            rawCustomAccelerationAxis = torqueInput;
                            customAccelerationAxis = Mathf.Clamp(customAccelerationAxis,-1f,1f);
                            rawCustomAccelerationAxis = Mathf.Clamp(rawCustomAccelerationAxis,-1f,1f);

                            if (desiredVelocity <0.1)
                            {
                                // To immediately stop the Rigidbody from moving:
                                rb.isKinematic=true;
                            } else
                                rb.isKinematic=false;
                        }
                        else{
                            float steeringGain = (float)0.2;
                            //check for sumo vehicle state lad (look ahead disgance bool)
                            var (steeringValue, torqueInput, desiredVelocity) = Vehicle.SumoVehicleControl(ref sock, id, rb, steeringGain, ref pidControllerSpeed, ref pidControllerDist, ref lookAheadMarker);

                            // set steering
                            customSteerAxis = steeringValue;
                            customLeanAxis = steeringValue/2;
                            customSteerAxis = Mathf.Clamp(customSteerAxis,-1f,1f);
                            customLeanAxis = Mathf.Clamp(customLeanAxis, -1f,1f);

                            // set torque
                            customAccelerationAxis = torqueInput; 
                            rawCustomAccelerationAxis = torqueInput;
                            customAccelerationAxis = Mathf.Clamp(customAccelerationAxis,-1f,1f);
                            rawCustomAccelerationAxis = Mathf.Clamp(rawCustomAccelerationAxis,-1f,1f);

                            if (desiredVelocity <0.1)
                            {
                                // To immediately stop the Rigidbody from moving:
                                rb.isKinematic=true;
                            } else
                                rb.isKinematic=false;
                        } 
                    }
                    else
                    {
                        // SUMO teleport guard: only apply if spawn is not in progress
                        if (allowSumoTeleport)
                        {
                            float steeringGain = (float)0.2;

                            // set steering
                            customSteerAxis = 0;
                            customLeanAxis = 0;

                            // set torque
                            customAccelerationAxis = 0; 
                            rawCustomAccelerationAxis = 0;

                            rb.linearVelocity = new Vector3(0, 0, 5);

                            rb = Vehicle.SumoVehicleTeleport(ref sock, id, rb, steeringGain, ref pidControllerSpeed, ref pidControllerDist, ref lookAheadMarker);
                            rb.isKinematic = true;
                        }
                        // else: SUMO teleport is disabled (likely spawn in progress); do nothing
       
                    }

                }
                else if (isSimulatorVehicle) 
                {
                    bool wahooOk = tcp_bike_connection != null && tcp_bike_connection.IsConnected();
                    bool fanatecOk = steeringInput != null && steeringInput.HasSteeringDevice;
                    bool arduinoOk = arduinoConnected && sp != null && sp.IsOpen;

                    actual_velocity_unity_bike = rb.linearVelocity.magnitude;

                    if (useConstantVelocity || wahooOk)
                    {
                        if (useConstantVelocity)
                            target_velocity_wahoo_bike = constantSimVelocity;
                        else
                            target_velocity_wahoo_bike = ((float)tcp_bike_connection.targetOutputVelocity / 3.6f) * globalSpeedGainSimBike;

                        acceleration = wahooOk
                            ? ((float)tcp_bike_connection.targetOutputPower) / mass_kg
                            : 0f;
                        if (useConstantVelocity && !wahooOk)
                            velocity = Mathf.Clamp(constantSimVelocity, 0f, topSpeed);

                        if (arduinoOk)
                            ApplyArduinoBrakeToAcceleration();
                        else
                            ApplyKeyboardBrakeToWahooLoop();

                        if (velocity <= 0.0f && acceleration < 0.0f)
                            acceleration = 0.0f;

                        velocity += globalaccGainSimBike * acceleration * Time.deltaTime;
                        if (velocity < 0f) velocity = 0f;
                        if (velocity > topSpeed) velocity = topSpeed;

                        float torqueInput = piControllerSpeedWahoo != null
                            ? piControllerSpeedWahoo.Control(velocity, actual_velocity_unity_bike)
                            : 0f;

                        if (velocity < 0.1f)
                        {
                            float stopGain = 0.5f;
                            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * stopGain);
                            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.deltaTime * stopGain);
                        }

                        customAccelerationAxis = Mathf.Clamp(torqueInput, -1f, 1f);
                        rawCustomAccelerationAxis = customAccelerationAxis;
                    }
                    else
                    {
                        leftBrakeSignal = 0.0f;
                        rightBrakeSignal = 0.0f;
                        CustomInput("Vertical", ref customAccelerationAxis, 1, 1, false);
                        CustomInput("Vertical", ref rawCustomAccelerationAxis, 1, 1, true);
                        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space))
                        {
                            customAccelerationAxis = Mathf.Min(customAccelerationAxis, -1f);
                            rawCustomAccelerationAxis = customAccelerationAxis;
                        }
                    }

                    if (fanatecOk)
                    {
                        steeringValueFanatec = steeringInput.steeringInputCorrected;
                        float steeringGain = globalSteeringGain;
                        customSteerAxis = (float)steeringValueFanatec * steeringGain;
                        customLeanAxis = (float)steeringValueFanatec * steeringGain / 2;
                        customSteerAxis = Mathf.Clamp(customSteerAxis, -1f, 1f);
                        customLeanAxis = Mathf.Clamp(customLeanAxis / 2, -1f, 1f);
                    }
                    else
                    {
                        CustomInput("Horizontal", ref customSteerAxis, 5, 5, false);
                        CustomInput("Horizontal", ref customLeanAxis, 1, 1, false);
                    }
                }

                else {
                    CustomInput("Horizontal",   ref customSteerAxis, 5, 5, false);
                    CustomInput("Vertical",     ref customAccelerationAxis, 1, 1, false);
                    CustomInput("Horizontal",   ref customLeanAxis, 1, 1, false);
                    CustomInput("Vertical",     ref rawCustomAccelerationAxis, 1, 1, true);
                }

                sprint = Input.GetKey(KeyCode.LeftShift);

                wheelieInput = !isSimulatorVehicle && Input.GetKey(KeyCode.LeftControl);

                // Bunny hop is disabled on the hardware/experiment bike; Space is brake fallback.
                if (isSimulatorVehicle)
                    bunnyHopInputState = 0;
                else if (Input.GetKey(KeyCode.Space))
                    bunnyHopInputState = 1;
                else if (Input.GetKeyUp(KeyCode.Space))
                    bunnyHopInputState = -1;
                else
                    bunnyHopInputState = 0;

                //Record
                if (wayPointSystem.recordingState == WayPointSystem.RecordingState.Record)
                {
                  //  Debug.LogWarning("---------> this is not intended!");

                    if (Time.frameCount % wayPointSystem.frameIncrement == 0)
                    {
                        wayPointSystem.bicyclePositionTransform.Add(new Vector3(Mathf.Round(transform.position.x * 100f) * 0.01f, Mathf.Round(transform.position.y * 100f) * 0.01f, Mathf.Round(transform.position.z * 100f) * 0.01f));
                        wayPointSystem.bicycleRotationTransform.Add(transform.rotation);
                        wayPointSystem.movementInstructionSet.Add(new Vector2Int((int)Input.GetAxisRaw("Horizontal"), (int)Input.GetAxisRaw("Vertical")));
                        wayPointSystem.sprintInstructionSet.Add(sprint);
                        wayPointSystem.bHopInstructionSet.Add(bunnyHopInputState);
                    }
                }
            }
            else
            {
              //  Debug.LogWarning("---------> this is not intended!");
                if (wayPointSystem.recordingState == WayPointSystem.RecordingState.Playback && allowWayPointPlayback)
                {
                    if (wayPointSystem.movementInstructionSet.Count - 1 > Time.frameCount / wayPointSystem.frameIncrement)
                    {
                        transform.position = Vector3.Lerp(transform.position, wayPointSystem.bicyclePositionTransform[Time.frameCount / wayPointSystem.frameIncrement], Time.deltaTime * wayPointSystem.frameIncrement);
                        transform.rotation = Quaternion.Lerp(transform.rotation, wayPointSystem.bicycleRotationTransform[Time.frameCount / wayPointSystem.frameIncrement], Time.deltaTime * wayPointSystem.frameIncrement);
                        WayPointInput(wayPointSystem.movementInstructionSet[Time.frameCount / wayPointSystem.frameIncrement].x, ref customSteerAxis, 5, 5, false);
                        WayPointInput(wayPointSystem.movementInstructionSet[Time.frameCount / wayPointSystem.frameIncrement].y, ref customAccelerationAxis, 1, 1, false);
                        WayPointInput(wayPointSystem.movementInstructionSet[Time.frameCount / wayPointSystem.frameIncrement].x, ref customLeanAxis, 1, 1, false);
                        WayPointInput(wayPointSystem.movementInstructionSet[Time.frameCount / wayPointSystem.frameIncrement].y, ref rawCustomAccelerationAxis, 1, 1, true);
                        sprint = wayPointSystem.sprintInstructionSet[Time.frameCount / wayPointSystem.frameIncrement];
                        bunnyHopInputState = wayPointSystem.bHopInstructionSet[Time.frameCount / wayPointSystem.frameIncrement];
                    }
                }
            }
        }

        void OnDrawGizmos(){
            if (bDrawGizmo){
                Gizmos.color = Color.red;
                Vector3 LadPoint = new Vector3(lookAheadMarker.x, 0.1f, lookAheadMarker.y);
                Gizmos.DrawSphere(LadPoint, 1.0f);

                Gizmos.color = Color.blue;
                Vector3 rbMarkerPoint = new Vector3(rbMarker.x, 0.1f, rbMarker.y);
                Gizmos.DrawSphere(rbMarkerPoint, 1.0f);

                Gizmos.color = Color.green;
                Vector3 SUMO_groundtruth = new Vector3(SUMOMarker.x, 0.1f, SUMOMarker.y);
                Gizmos.DrawSphere(SUMO_groundtruth, 1.0f);
            }
        }

        void ApplyArduinoBrakeToAcceleration()
        {
            try
            {
                if (sp.BytesToRead <= 0) return;

                string data;
                try
                {
                    data = sp.ReadLine().Trim();
                }
                catch (TimeoutException)
                {
                    data = sp.ReadExisting().Trim();
                    string[] lines = data.Split(new char[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                        data = lines[lines.Length - 1];
                }

                if (string.IsNullOrEmpty(data)) return;

                string[] values = data.Split(',');
                if (values.Length < 2) return;

                if (float.TryParse(values[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float rawLeftBrake) &&
                    float.TryParse(values[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float rawRightBrake))
                {
                    leftBrakeSignal = rawLeftBrake - 16.5f;
                    rightBrakeSignal = rawRightBrake - 17.0f;
                    float totalBrakedeacceleration = (leftBrakeSignal + rightBrakeSignal) / 200.0f * brakeSensitivity;
                    acceleration -= totalBrakedeacceleration;
                }
            }
            catch (TimeoutException) { }
            catch (System.IO.IOException)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino connection lost: The connection to the Arduino has been interrupted.";
            }
            catch (InvalidOperationException)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino port error: Serial port is not open or has been closed.";
            }
            catch (Exception) { }
        }

        void ApplyKeyboardBrakeToWahooLoop()
        {
            leftBrakeSignal = 0.0f;
            rightBrakeSignal = 0.0f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space))
            {
                acceleration -= brakeSensitivity;
                velocity *= Mathf.Clamp01(1f - Time.deltaTime * 4f);
            }
        }

        //Input Manager Controls
        float CustomInput(string name, ref float axis, float sensitivity, float gravity, bool isRaw)
        {
            var r = Input.GetAxisRaw(name);
            var s = sensitivity;
            var g = gravity;
            var t = Time.unscaledDeltaTime;

            if (isRaw)
                axis = r;
            else
            {
                if (r != 0)
                    axis = Mathf.Clamp(axis + r * s * t, -1f, 1f);
                else
                    axis = Mathf.Clamp01(Mathf.Abs(axis) - g * t) * Mathf.Sign(axis);
            }
            return axis;
        }

        float WayPointInput(float instruction, ref float axis, float sensitivity, float gravity, bool isRaw)
        {
            var r = instruction;
            var s = sensitivity;
            var g = gravity;
            var t = Time.unscaledDeltaTime;

            if (isRaw)
                axis = r;
            else
            {
                if (r != 0)
                    axis = Mathf.Clamp(axis + r * s * t, -1f, 1f);
                else
                    axis = Mathf.Clamp01(Mathf.Abs(axis) - g * t) * Mathf.Sign(axis);
            }

            return axis;
        }

        IEnumerator DelayBunnyHop()
        {
            yield return new WaitForSeconds(0.5f);
            isBunnyHopping = false;
            yield return null;
        }
        
        /// <summary>
        /// Attempts to reconnect to the Arduino
        /// </summary>
        public void AttemptArduinoReconnection()
        {
          //  Debug.Log("Attempting Arduino reconnection...");
            
            if (sp != null && sp.IsOpen)
            {
                try
                {
                  //  Debug.Log("Closing existing Arduino connection...");
                    sp.Close();
                  //  Debug.Log("Existing connection closed successfully");
                }
                catch (Exception ex)
                {
                  //  Debug.LogWarning("Error closing Arduino connection: " + ex.Message);
                }
            }
            
            try
            {
                // Reinitialize with current port settings
                if (sp == null)
                {
                    if (!ArduinoPortIsAvailable(actualComPort))
                    {
                        arduinoConnected = false;
                        return;
                    }
                    sp = new SerialPort(actualComPort, baudRate);
                }
                
                // if (enableArduinoDebugLogs)
                  //  Debug.Log("Opening serial port " + actualComPort + " for reconnection...");
                sp.Open();
                // if (enableArduinoDebugLogs)
                  //  Debug.Log("Serial port opened successfully for reconnection");
                
                // if (enableArduinoDebugLogs)
                  //  Debug.Log("Setting timeouts to " + readTimeoutMs + "ms...");
                sp.ReadTimeout = readTimeoutMs;
                sp.WriteTimeout = readTimeoutMs;
                // if (enableArduinoDebugLogs)
                  //  Debug.Log("Timeouts set successfully");
                
                arduinoConnected = true;
                arduinoErrorMessage = "";
                // if (enableArduinoDebugLogs)
                  //  Debug.Log("Arduino reconnected successfully on " + actualComPort);
            }
            catch (TimeoutException ex)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino reconnection timeout: The operation has timed out. Please check if the Arduino is properly connected to " + actualComPort + ".";
              //  Debug.LogError("Arduino Reconnection Error: " + arduinoErrorMessage);
                // if (enableArduinoDebugLogs)
                  //  Debug.LogError("Timeout Exception Details: " + ex.ToString());
            }
            catch (UnauthorizedAccessException ex)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino access denied: Another application may be using " + actualComPort + ". Please close other applications using the serial port.";
              //  Debug.LogError("Arduino Reconnection Error: " + arduinoErrorMessage);
                // if (enableArduinoDebugLogs)
                  //  Debug.LogError("Unauthorized Access Exception Details: " + ex.ToString());
            }
            catch (System.IO.IOException ex)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino reconnection failed: The specified port " + actualComPort + " was not found. Please check if the Arduino is connected to the correct port.";
              //  Debug.LogError("Arduino Reconnection Error: " + arduinoErrorMessage);
                // if (enableArduinoDebugLogs)
                  //  Debug.LogError("IO Exception Details: " + ex.ToString());
            }
            catch (Exception ex)
            {
                arduinoConnected = false;
                arduinoErrorMessage = "Arduino reconnection error: " + ex.Message;
              //  Debug.LogError("Arduino Reconnection Error: " + arduinoErrorMessage);
              //  Debug.LogError("General Exception Details: " + ex.ToString());
            }
        }
        
        /// <summary>
        /// Debug method to test Arduino communication
        /// </summary>
        public void TestArduinoCommunication()
        {
            if (!arduinoConnected)
            {
              //  Debug.LogWarning("Cannot test Arduino communication - not connected");
                return;
            }
            
          //  Debug.Log("Testing Arduino communication...");
          //  Debug.Log("Serial port status - IsOpen: " + sp.IsOpen + ", BytesToRead: " + sp.BytesToRead);
            
            try
            {
                // Try to read any available data
                if (sp.BytesToRead > 0)
                {
                    string testData = sp.ReadLine();
                  //  Debug.Log("Test read successful - Data: " + testData);
                }
                else
                {
                  //  Debug.Log("No data available to read from Arduino");
                }
            }
            catch (Exception ex)
            {
              //  Debug.LogError("Test read failed: " + ex.Message);
            }
        }
        
        /// <summary>
        /// Displays Arduino connection status and error messages to the user
        /// </summary>
        public void DisplayArduinoStatus()
        {
            if (!arduinoConnected && !string.IsNullOrEmpty(arduinoErrorMessage))
            {
              //  Debug.LogWarning("ARDUINO CONNECTION ERROR: " + arduinoErrorMessage);
                // You can also display this in the UI if you have a UI system
                // For example: UIManager.Instance.ShowError(arduinoErrorMessage);
            }
            
            // Add periodic status logging for debugging
            if (enableArduinoDebugLogs && Time.frameCount % 300 == 0) // Log every 300 frames (about 5 seconds at 60fps)
            {
                if (arduinoConnected && sp != null && sp.IsOpen)
                {
                   Debug.Log("Arduino Status: Connected - Port: " + actualComPort + ", BaudRate: " + baudRate + 
                             ", IsOpen: " + sp.IsOpen + ", BytesToRead: " + sp.BytesToRead);
                }
                else
                {
                  //  Debug.LogWarning("Arduino Status: Disconnected - Port: " + actualComPort + " - Last Error: " + arduinoErrorMessage);
                }
            }
        }
    }
}
