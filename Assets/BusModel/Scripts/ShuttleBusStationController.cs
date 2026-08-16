using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using tum_shuttle_bus_controller;
using tumvt.sumounity.PedestrianModel;

namespace tum_bus_controller
{
    public class BusStationController : MonoBehaviour
    {
        // Debug
        private DebugLogsManager debugLogsManager;
        private bool enableDebugLogs;

        //ScenarioManager
        [SerializeField]
        private ScenarioManager scenarioManager;

        // Bus Station
        private BoxCollider busStationCollider;
        private string stationNumber;
        private GameObject busStationOnBoardingArea;

        // Bus
        private GameObject bus;
        private float busSpeed = 0f;
        private const string busWheelTag = "BusWheel";
        private TaxiDoorController taxiDoorController; // Store reference to door controller
        private GameObject waitingArea;

        //HMI
        private GameObject busHmiParent;
        private string hmiType;
        private int currentHMIState = -1;  // Track current HMI state
        private const string setStateMethodName = "SetState";
        private TrafficLightController _lastYellowCtl;
        private TrafficLightController _lastRedCtl;

        // Debug toggle for boarding logs
        [SerializeField] private bool forceBoardingLogs = true;
        private bool BoardingLogs => forceBoardingLogs || enableDebugLogs;

        // Boarding
        private bool startedBoarding = false;
        private bool endedBoarding = false;
        private int frameCounter = 0;
        private const int boardingFrameThreshold = 25; // Threshold for frames to wait before starting boarding
        private const string busOnboardingMethodName = "BeginOnboarding"; // Method name for boarding passengers
        private List<GameObject> waitingPassengers = new List<GameObject>();
        private bool boardedWaitingPassengers = false;
        private const string pedestrianTag = "Person";
        private const string goWaitingMethodName = "GoToWaitingSpot"; // Method name for sending pedestrians to waiting spot

        // Bike sensor state
        private bool bikePresent = false;

        // Nav Mesh
        private bool busNavMeshCreated = false; // For dynamic NavMesh
        private bool waitingAreaNavMeshCreated = false; // For WaitingArea NavMesh
        private NavMeshSurface waitingAreaNavMeshSurface;
        private GameObject tempBoardingLinkObj;
        private NavMeshLink tempBoardingLink;

        [Header("Dynamic NavMesh")]
        [Tooltip("Dynamically generated (do not populate!)")]
        public GameObject tempNavMeshSurfaceObj = null;
        public NavMeshSurface tempNavMeshSurface = null;
        private const string busNavMeshLayerName = "BusNavMesh"; // Layer for bus NavMesh
        private const string waitingAreaNavMeshLayerName = "WaitingAreaNavMesh"; // Layer for waiting area NavMesh
        private string thisBusStationNavMeshLayerName; // Layer for this bus station NavMesh
        private string thisWaitingAreaNavMeshLayerName; // Layer for this waiting area NavMesh


        // Method names for resetting pedestrians
        private const string setSumoVehicleMethodName = "SetToSumoVehicle"; // Method name for resetting pedestrians to Sumo vehicles
        private const string characterControllerMethodName = "SetCharacterController"; // Method name for toggling CharacterController

        #region Unity Methods

        void Awake()
        {
            debugLogsManager = GameObject.Find("DebugLogsManager")?.GetComponent<DebugLogsManager>();
        }

        void Start()
        {
            enableDebugLogs = debugLogsManager != null && debugLogsManager.EnableDebugLogs;

            // get station number from game object name
            stationNumber = gameObject.name.Split('_').LastOrDefault();
            thisBusStationNavMeshLayerName = busNavMeshLayerName + "_" + stationNumber;
            // thisWaitingAreaNavMeshLayerName = waitingAreaNavMeshLayerName + "_" + stationNumber;

            // Set NavMesh layers for this bus station
            // gameObject.layer = LayerMask.NameToLayer(thisBusStationNavMeshLayerName);

            // busStationOnBoardingArea = transform.Find("OnboardingArea")?.gameObject;
            // busStationOnBoardingArea.layer = LayerMask.NameToLayer(thisBusStationNavMeshLayerName);

            // if (enableDebugLogs)
            // {
            //     Debug.Log($"Bus Station {stationNumber} initialized with NavMesh layers: {thisBusStationNavMeshLayerName}, {thisWaitingAreaNavMeshLayerName}");
            // }

            busStationCollider = GetComponent<BoxCollider>();

            if (busStationCollider == null)
            {
                Debug.LogError("BusStationController requires a BoxCollider component.");
            }
            else
            {
                busStationCollider.isTrigger = true; // Ensure the collider is set as a trigger
            }

            CreateWaitingAreaNavMesh();

            scenarioManager = FindObjectOfType<ScenarioManager>();
            hmiType = scenarioManager.GetHMIType(stationNumber);
            Debug.Log($"[HMIBus] HMI type for station {stationNumber} is {hmiType}");
            // hmiType = "bus";

        }

        void FixedUpdate()
        {
            if (bus != null) // bus in station
            {
                busSpeed = bus.GetComponent<ShuttleBusController>().currentSpeed;

                if (!startedBoarding && !endedBoarding)
                {
                    // We are checking if the bus is stationary only when it is in the station and has not started boarding yet
                    if (busSpeed == 0)
                        frameCounter++;
                    else
                        frameCounter = 0; // Reset if bus is moving

                    if (busSpeed == 0 && frameCounter > boardingFrameThreshold)
                    {
                        startedBoarding = true;
                        SetHMI(3, stationNumber); // bus stopped -> red light

                        if (taxiDoorController != null)
                        {
                            taxiDoorController.OpenDoors();
                            Invoke(nameof(CreateNavMeshForBoarding), 5.2f); // Delay to ensure doors are fully open
                        }
                        else
                        {
                            Debug.LogError("Cannot open doors - TaxiDoorController is null!");
                        }

                    }
                }
                else if (CheckBoardingAvailability() && !boardedWaitingPassengers)
                {
                    boardedWaitingPassengers = true;
                    OnboardWaitingPassengers();
                }
                else if (busSpeed > 0 && startedBoarding && !endedBoarding) // leaving the station
                {
                    endedBoarding = true;
                    SetHMI(4, stationNumber); // departure sequence: red+yellow, then red off
                    taxiDoorController.CloseDoors();
                    // ResetPedestriansToSumoVehicles(); // all pedestrians that are in the trigger zone, waiting for the next bus
                    RemoveBusNavMeshForBoarding();
                    DeactivateCharacterControllerForBusPassengers();

                }
            }
            else if (bus == null && startedBoarding && endedBoarding) // bus left the station
            {
                startedBoarding = false;
                endedBoarding = false;
                frameCounter = 0;
            }
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(TurnOffBoth));
        }
        #endregion

        #region  Trigger Methods

        // Triggers
        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(busWheelTag))
            {
                // Prevent multiple triggers for the same bus
                if (bus != null)
                {
                    Debug.LogWarning("Bus already in station, ignoring new trigger");
                    return;
                }
                else if (enableDebugLogs)
                {
                    Debug.Log("Bus entered the station area.");
                }

                bus = other.transform.root.gameObject;
                taxiDoorController = bus.GetComponent<TaxiDoorController>();
                
                // Ensure the bus floor is baked into the boarding NavMesh
                Transform busFloor = bus.transform.Find("BusFloor");
                if (busFloor != null)
                {
                    int busNavMeshLayer = LayerMask.NameToLayer(busNavMeshLayerName);
                    if (busNavMeshLayer != -1)
                    {
                        busFloor.gameObject.layer = busNavMeshLayer;
                        // also set all children of BusFloor, to guarantee collider inclusion
                        foreach (Transform child in busFloor.GetComponentsInChildren<Transform>())
                        {
                            child.gameObject.layer = busNavMeshLayer;
                        }
                        if (enableDebugLogs)
                        {
                            Debug.Log($"BusFloor layer set to '{busNavMeshLayerName}' for navmesh baking.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Layer '{busNavMeshLayerName}' not found. Please create it in Project Settings > Tags & Layers.");
                    }
                }
                else
                {
                    Debug.LogWarning("BusFloor not found on bus; boarding NavMesh may be missing.");
                }

                SetHMI(1, stationNumber); // Enter station -> HMI texture 1

                // GameObject busFloor = bus.transform.Find("BusFloor")?.gameObject;
                // busFloor.layer = LayerMask.NameToLayer(thisBusStationNavMeshLayerName); // Set layer for bus floor

                if (taxiDoorController == null)
                {
                    Debug.LogError($"TaxiDoorController not found on bus {bus.name}");
                    return;
                }
            }
            else
            {
                // Find the SUMO pedestrian controller on this collider's hierarchy
                ThirdPersonController pedController = other.GetComponentInParent<ThirdPersonController>();
                if (pedController == null)
                {
                    return;
                }

                GameObject pedRoot = pedController.gameObject;
                if (!waitingPassengers.Contains(pedRoot))
                {
                    if (BoardingLogs)
                    {
                        Debug.Log($"[Boarding] Pedestrian {pedRoot.name} entered the station (collider={other.GetType().Name}).");
                    }

                    if (CheckBoardingAvailability())
                    {
                        // Bus ready -> onboard immediately
                        OnboardOnePedestrian(pedRoot);
                    }
                    else
                    {
                        // Not ready -> always make them wait
                        waitingPassengers.Add(pedRoot);
                        if (BoardingLogs)
                        {
                            Debug.Log($"[Boarding] Pedestrian {pedRoot.name} added to waiting list (bus not ready).");
                        }
                        MakePassengerWaitForTheBus(pedRoot);
                    }
                }
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(busWheelTag))
            {
                if (bus == null)
                {
                    Debug.LogWarning("Bus already exited the station, ignoring exit trigger");
                    return;
                }
                if (enableDebugLogs)
                {
                    Debug.Log("Bus has exited the station area.");
                }
                SetChildrenBusHMITextures(0); // Set HMI texture to BLACK
                bus = null;
                taxiDoorController = null;
                busHmiParent = null;
                SetHMI(bikePresent ? 2 : 5, stationNumber); // keep yellow if bike present, otherwise lights off
            }
        }
        #endregion

        #region Boarding Methods

        private bool CheckBoardingAvailability()
        {
            if (bus == null || taxiDoorController == null)
            {

                
                Debug.LogError("Cannot board passengers - bus or taxi door controller is null.");
                return false;
            }

            if (!startedBoarding || endedBoarding || busSpeed > 0 || !busNavMeshCreated)
            {
                Debug.LogWarning($"Cannot board passengers - startedBoarding: {startedBoarding}, endedBoarding: {endedBoarding}, busSpeed: {busSpeed}, busNavMeshCreated: {busNavMeshCreated}.");
                return false;
            }

            return true;
        }

        void OnboardWaitingPassengers()
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Boarding {waitingPassengers.Count} passengers.");
            }

            for (int i = 0; i < waitingPassengers.Count; i++)
            {
                GameObject passenger = waitingPassengers[i];
                OnboardOnePedestrian(passenger);
            }

            // Clear the list after boarding
            waitingPassengers.Clear();
        }

        private void OnboardOnePedestrian(GameObject pedestrian)
        {
            if (BoardingLogs)
            {
                Debug.Log($"[Boarding] Pedestrian {pedestrian.name} is boarding the bus.");
            }
            if (CheckBoardingAvailability()) // only if we have a bus in the station
            {
                Transform busFloor = bus.transform.Find("BusFloor");
                if (busFloor == null)
                {
                    Debug.LogWarning($"[Boarding] BusFloor missing on bus {bus?.name}. Cannot compute boarding target.");
                    return;
                }
                if (BoardingLogs)
                {
                    Debug.Log($"[Boarding] Sending BeginOnboarding to {pedestrian.name} with target {busFloor.position}");
                }
                pedestrian.SendMessage(busOnboardingMethodName, (busFloor.transform.position, bus.transform));
            }
            else
            {
                if (BoardingLogs)
                {
                    Debug.Log($"[Boarding] CheckBoardingAvailability=false. startedBoarding={startedBoarding} endedBoarding={endedBoarding} busSpeed={busSpeed} busNavMeshCreated={busNavMeshCreated} busNull={bus==null} doorNull={taxiDoorController==null}");
                }
            }
        }


        private void MakePassengerWaitForTheBus(GameObject passenger)
        {
            if (BoardingLogs)
            {
                Debug.Log($"[Boarding] Pedestrian {passenger.name} is waiting for the bus.");
            }
            
            Vector3 waitingSpot = GetRandomWaitingSpotOnNavMesh();
            passenger.SendMessage(goWaitingMethodName, waitingSpot);
        }
        #endregion

        #region Pers. Deactivation
        void ResetPedestriansToSumoVehicles()
        {
            for (int i = 0; i < waitingPassengers.Count; i++)
            {
                GameObject passenger = waitingPassengers[i];
                if (passenger != null)
                {
                    passenger.SendMessage(setSumoVehicleMethodName, false); // sort of they miss the bus
                }
            }
        }
        void DeactivateCharacterControllerForBusPassengers()
        {
            // Get all child objects with Person tag
            Transform[] allChildren = bus.GetComponentsInChildren<Transform>();
            GameObject[] busPassengers = allChildren
                                         .Where(t => t.gameObject.CompareTag(pedestrianTag))
                                         .Select(t => t.gameObject)
                                         .ToArray();

            // Deactivate character controller for each passenger
            foreach (GameObject passenger in busPassengers)
            {
                if (passenger != null)
                {
                    passenger.SendMessage(characterControllerMethodName, false); // set character controller to false
                }
            }
        }
        #endregion

        #region HMI Methods

        
        public int CurrentHMIState
        {
            get { return currentHMIState; }
        }

        public void SetHMI(int value = -1, string stationNumber = "-1")
        {
            currentHMIState = value;  // Track the current HMI state
            Debug.Log($"[HMIBus] set hmi: {hmiType} for station {stationNumber}");
            bool useBus = hmiType == "bus" || hmiType == "both" || hmiType == "bus+inf" || hmiType == "inf+bus";
            bool useInf = hmiType == "inf" || hmiType == "both" || hmiType == "bus+inf" || hmiType == "inf+bus";

            // Split value ranges to keep bus/inf independent:
            // bus: 0,1   inf: 2,3,4,5
            bool busValue = value == 0 || value == 1;
            bool infValue = value >= 2 && value <= 5;
            useBus = useBus && busValue;
            useInf = useInf && infValue;

            if (useBus)
            {
                Debug.Log($"[HMIBus] applying texture index {value} on bus {bus?.name ?? "null"}");
                SetChildrenBusHMITextures(value);
            }

            if (useInf)
            {
                Debug.Log($"[HMIBus] inf light state index {value} for station {stationNumber}");
                SetInfrastructureHMITextures(value, stationNumber);
            }

            if (!useBus && !useInf)
            {
                Debug.LogWarning($"[HMIBus] Unknown hmiType '{hmiType}' for station {stationNumber}");
            }

        }

        // Dictionary for BUS HMI texture indexes and their names
        private readonly Dictionary<int, string> busHmiTextureNames = new Dictionary<int, string>
        {
            { 0, "BLACK" },
            { 1, "STOP" }, // Change to BIKE or other as need, then change the corresponding HMI texture of shuttle bus
            { 3, "STOP" }
        };

        private void SetChildrenBusHMITextures(int textureIndex)
        {
            busHmiParent = bus.transform.Find("HMIParent")?.gameObject;

            if (busHmiParent != null)
            {
                Debug.Log($"[HMIBus] HMIParent found on bus {bus.name}, children: {busHmiParent.transform.childCount}");
                for (int i = 0; i < busHmiParent.transform.childCount; i++)
                {
                    Transform child = busHmiParent.transform.GetChild(i);
                    HMIController hmiController = child.GetComponent<HMIController>();
                    if (hmiController != null)
                    {
                        hmiController.SetTexture(textureIndex); // Use provided index
                        if (enableDebugLogs)
                        {
                            string textureName = busHmiTextureNames.ContainsKey(textureIndex) ? busHmiTextureNames[textureIndex] : "UNKNOWN";
                            Debug.Log($"HMIController found on bus {bus.name}, setting texture to {textureName}.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"HMIController not found on child {child.name} of bus {bus.name}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"HMIParent not found on bus {bus.name}");
            }
        }

        private void SetInfrastructureHMITextures(int textureIndex, string stationNumber)
        {
            // Debug.Log($"Setting infrastructure HMI texture to index: {textureIndex}");
            //search for lights in scene
            GameObject Yellow_lights = GameObject.Find("Bs_" + stationNumber + "_Inf_lights_yellow");
            GameObject Red_lights = GameObject.Find("Bs_" + stationNumber + "_Inf_lights_red");
            if (Yellow_lights == null && Red_lights == null)
            {
                Debug.LogWarning($"Lights not found on infrastructure {gameObject.name}, cannot set HMI texture.");
                return;
            }

            // First try to get component from the lights GameObject itself
            TrafficLightController Yellow_trafficLightController = Yellow_lights.GetComponent<TrafficLightController>();
            TrafficLightController Red_trafficLightController = Red_lights.GetComponent<TrafficLightController>();
            if (textureIndex == 2)
            {
                // Yellow light on, Red light off (stable state)
                CancelInvoke(nameof(TurnOffBoth)); // Deactivate any pending turn-off for Red light
                if (Yellow_trafficLightController != null) Yellow_trafficLightController.SetState(true);
                if (Red_trafficLightController != null) Red_trafficLightController.SetState(false);
            }
            else if (textureIndex == 3)
            {
                // Red light on, Yellow light off
                CancelInvoke(nameof(TurnOffBoth)); // Deactivate any pending turn-off for Red light
                if (Red_trafficLightController != null) Red_trafficLightController.SetState(true);
                if (Yellow_trafficLightController != null) Yellow_trafficLightController.SetState(false);
            }
            else if (textureIndex == 4)
            {
                // Red light and Yellow light on for 1 second, then Red light off and Yellow light remain on
                CancelInvoke(nameof(TurnOffBoth)); // Prevent the previous delayed turn-off from "catching up"
                if (Yellow_trafficLightController != null) Yellow_trafficLightController.SetState(true);
                if (Red_trafficLightController != null) Red_trafficLightController.SetState(true);

                // Record the controllers to turn off
                _lastYellowCtl = Yellow_trafficLightController;
                _lastRedCtl = Red_trafficLightController;

                // Red light off after 1 second 
                Invoke(nameof(TurnOffRedKeepYellow), 1f);
                Invoke(nameof(TurnOffBoth), 2f);
            }
            else if (textureIndex == 5)
            {
                // All off
                CancelInvoke(nameof(TurnOffBoth));
                if (Yellow_trafficLightController != null) Yellow_trafficLightController.SetState(false);
                if (Red_trafficLightController != null) Red_trafficLightController.SetState(false);
            }
        }
        
        private void TurnOffBoth()
        {
            if (bikePresent)
            {
                if (_lastRedCtl != null) _lastRedCtl.SetState(false);
                if (_lastYellowCtl != null) _lastYellowCtl.SetState(true);
            }
            else
            {
                if (_lastYellowCtl != null) _lastYellowCtl.SetState(false);
                if (_lastRedCtl != null)   _lastRedCtl.SetState(false);
            }
            _lastYellowCtl = null;
            _lastRedCtl = null;
        }

        private void TurnOffRedKeepYellow()
        {
            if (_lastRedCtl != null) 
                _lastRedCtl.SetState(false);   // 关闭红灯
            if (_lastYellowCtl != null) 
                _lastYellowCtl.SetState(true); // 确保黄灯继续亮
        }

        public void SetBikePresence(bool present)
        {
            bikePresent = present;
        }


        #endregion


        #region NavMesh Methods

        void CreateWaitingAreaNavMesh()
        {
            // Find WaitingArea child object
            if (waitingArea == null)
            {
                waitingArea = transform.Find("WaitingArea")?.gameObject;
                waitingAreaNavMeshSurface = waitingArea?.GetComponent<NavMeshSurface>();
                // waitingArea.layer = LayerMask.NameToLayer(thisBusStationNavMeshLayerName); // Set layer for waiting area
            }

           
            if (waitingArea != null && waitingAreaNavMeshSurface != null)
            {
                // Only build if no data exists
                if (!waitingAreaNavMeshCreated)
                {
                    // Set to current obj hierarchy
                    waitingAreaNavMeshSurface.collectObjects = CollectObjects.Children;

                    // Build it
                    waitingAreaNavMeshSurface.BuildNavMesh();

                    waitingAreaNavMeshCreated = true;

                    if (enableDebugLogs)
                    {
                        Debug.Log("WaitingArea initialized.");
                    }
                }
            }
            else
            {
                Debug.LogError("WaitingArea not found in the scene.");
            }
            
        }

        void CreateNavMeshForBoarding()
        {
            // Create a new GameObject for the NavMeshSurface
            tempNavMeshSurfaceObj = new GameObject("TempNavMeshSurface");
            tempNavMeshSurface = tempNavMeshSurfaceObj.AddComponent<NavMeshSurface>();

            // Use render meshes to ensure BusFloor without colliders is baked
            tempNavMeshSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

            // Collect a bounded volume around the bus floor (Children would collect nothing here)
            tempNavMeshSurface.collectObjects = CollectObjects.Volume;
            tempNavMeshSurface.center = Vector3.zero;
            tempNavMeshSurface.size = new Vector3(10f, 4f, 10f); // covers bus floor area

            Transform busFloor = bus != null ? bus.transform.Find("BusFloor") : null;
            Vector3 volumePos = busFloor != null ? busFloor.position : (bus != null ? bus.transform.position : transform.position);
            tempNavMeshSurfaceObj.transform.position = volumePos;
            tempNavMeshSurfaceObj.transform.rotation = Quaternion.identity;

            if (waitingAreaNavMeshSurface != null)
            {
                tempNavMeshSurface.agentTypeID = waitingAreaNavMeshSurface.agentTypeID;
            }

            // Set Include Layers to only your BusNavMesh layer
            tempNavMeshSurface.layerMask = LayerMask.GetMask(busNavMeshLayerName, waitingAreaNavMeshLayerName);

            // Bake the navmesh and guard against failure
            tempNavMeshSurface.BuildNavMesh();
            busNavMeshCreated = tempNavMeshSurface.navMeshData != null;
            if (BoardingLogs)
            {
                Debug.Log($"[Boarding] Build bus NavMesh result: {busNavMeshCreated}, bounds={tempNavMeshSurface.navMeshData.sourceBounds}");
            }
            if (!busNavMeshCreated && tempNavMeshSurface.navMeshData != null)
            {
                // Fallback: slightly expand bounds and retry once
                tempNavMeshSurface.collectObjects = CollectObjects.Volume;
                tempNavMeshSurface.center = Vector3.zero;
                tempNavMeshSurface.size = new Vector3(20f, 8f, 20f);
                tempNavMeshSurface.BuildNavMesh();
                busNavMeshCreated = tempNavMeshSurface.navMeshData != null;
                if (BoardingLogs)
                {
                    Debug.Log($"[Boarding] Retry Build NavMesh with volume result: {busNavMeshCreated}, size={tempNavMeshSurface.size}");
                }
            }

            // Create a NavMeshLink between waiting area and bus floor to ensure a valid path
            if (waitingArea != null && bus != null)
            {
                if (busFloor != null)
                {
                    if (tempBoardingLinkObj == null)
                    {
                        tempBoardingLinkObj = new GameObject("TempBoardingLink");
                        tempBoardingLink = tempBoardingLinkObj.AddComponent<NavMeshLink>();
                        tempBoardingLink.bidirectional = true;
                        tempBoardingLink.width = 1.5f;
                        tempBoardingLink.agentTypeID = tempNavMeshSurface.agentTypeID;
                    }

                    Vector3 start = waitingArea.transform.position;
                    Vector3 end = busFloor.position;
                    if (NavMesh.SamplePosition(start, out var startHit, 5f, NavMesh.AllAreas))
                    {
                        start = startHit.position;
                    }
                    if (NavMesh.SamplePosition(end, out var endHit, 5f, NavMesh.AllAreas))
                    {
                        end = endHit.position;
                    }
                    tempBoardingLinkObj.transform.position = start;
                    tempBoardingLink.startPoint = Vector3.zero;
                    tempBoardingLink.endPoint = tempBoardingLinkObj.transform.InverseTransformPoint(end);
                    tempBoardingLink.UpdateLink();

                    if (BoardingLogs)
                    {
                        Debug.Log($"[Boarding] NavMeshLink created start={start} end={end} agentTypeID={tempBoardingLink.agentTypeID}");
                    }
                }
                else if (BoardingLogs)
                {
                    Debug.LogWarning("[Boarding] BusFloor not found; cannot create NavMeshLink.");
                }
            }

            // Keep waiting-area navmesh active during boarding so agents remain on a valid NavMesh
            if (!waitingAreaNavMeshCreated)
            {
                CreateWaitingAreaNavMesh();
                if (BoardingLogs)
                {
                    Debug.Log("[Boarding] WaitingAreaNavMeshSurface active for boarding.");
                }
            }
        }

        void RemoveBusNavMeshForBoarding()
        {
            if (tempNavMeshSurface != null)
            {
                tempNavMeshSurface.RemoveData(); // Remove the baked NavMesh
                Destroy(tempNavMeshSurfaceObj);  // Destroy the GameObject
                tempNavMeshSurface = null;
                tempNavMeshSurfaceObj = null;
                busNavMeshCreated = false; // Reset the flag
            }
            if (tempBoardingLinkObj != null)
            {
                Destroy(tempBoardingLinkObj);
                tempBoardingLinkObj = null;
                tempBoardingLink = null;
            }

            // Reactivate the waiting area NavMeshSurface if it exists
            CreateWaitingAreaNavMesh();
        }

                /// <summary>
        /// Computes a random free point on the NavMesh within the bus floor area
        /// </summary>
        /// <param name="maxDistance">Maximum distance from bus floor center to search for a point</param>
        /// <returns>A random valid NavMesh position, or Vector3.zero if no valid position found</returns>
        public Vector3 GetRandomWaitingSpotOnNavMesh(float maxDistance = 1f)
        {
            // Get the position of the waiting area in world space
            Vector3 waitingAreaCenter = waitingArea.transform.position;
            if (enableDebugLogs)
            {
                Debug.Log($"Waiting area world position: {waitingAreaCenter}");
            }

            if (waitingArea == null)
            {
                if (enableDebugLogs)
                    Debug.Log("Cannot get random NavMesh point - WaitingArea not found");
                return Vector3.zero;
            }

            int maxAttempts = 30;
            for (int i = 0; i < maxAttempts; i++)
            {
                
                // Get the bounds from the NavMeshSurface component
                Bounds navMeshBounds = waitingAreaNavMeshSurface.navMeshData.sourceBounds;
                Vector3 randomPoint = new Vector3(
                    Random.Range(navMeshBounds.min.x, navMeshBounds.max.x),
                    waitingAreaCenter.y,
                    Random.Range(navMeshBounds.min.z, navMeshBounds.max.z)
                );

                randomPoint += waitingAreaCenter; // Offset by the waiting area center

                if (enableDebugLogs)
                {
                    Debug.Log($"Attempt {i + 1}: Random world point: {randomPoint}");
                }

                // Sample the NavMesh to find the nearest valid position
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPoint, out hit, maxDistance, NavMesh.AllAreas))
                {
                    float heightDifference = Mathf.Abs(hit.position.y - waitingAreaCenter.y);
                    if (heightDifference <= 0.1f)
                    {
                        // Create a small red sphere for visualization
                        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        sphere.transform.position = hit.position;
                        sphere.transform.localScale = Vector3.one * 0.2f;
                        sphere.GetComponent<Renderer>().material.color = Color.red;
                        if (enableDebugLogs)
                            Debug.Log($"Found valid NavMesh world point: {hit.position}");
                        return hit.position;
                    }
                }
            }

            if (enableDebugLogs)
                Debug.Log($"Could not find valid NavMesh point after {maxAttempts} attempts");

            return waitingAreaCenter;
        }

        #endregion

        

    }
}
