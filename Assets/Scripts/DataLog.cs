using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System;
using tum_bus_controller;

public class DataLog : MonoBehaviour
{
    [Header("Target Objects")]
    [SerializeField] private GameObject simBike;
    [SerializeField] private Transform handlesTransform;
    [SerializeField] private ScenarioManager scenarioManager;
    
    [Header("Data Logging Settings")]
    [SerializeField] private string csvFileName = "SimulationData";
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private float loggingInterval = 0.02f; // 50 Hz by default
    
    // Data storage
    private List<SimBikeData> simBikeDataList = new List<SimBikeData>();
    private List<DynamicObjectData> dynamicObjectDataList = new List<DynamicObjectData>();
    private List<DynamicObjectData> staticObjectDataList = new List<DynamicObjectData>();
    
    // Tracking
    private HashSet<GameObject> trackedObjects = new HashSet<GameObject>();
    private HashSet<GameObject> recordedStaticObjects = new HashSet<GameObject>();
    private Dictionary<BikeSensor, int> bikeSensorToStationNumber = new Dictionary<BikeSensor, int>();
    private Dictionary<int, Transform> stationNumberToStopPoint = new Dictionary<int, Transform>();
    
    // HMI Tracking
    private BusStationController stationController5;
    private BusStationController stationController6;
    private GameObject activeBusInStation;
    
    // File paths
    private string simBikeFilePath;
    private string dynamicObjectsFilePath;
    private string staticObjectsFilePath;
    
    // Timing
    private float nextLogTime = 0f;
    private float startTime;
    
    // Cached values
    private string cachedPermutationId;
    private Rigidbody simBikeRigidbody;

    [Header("Bus Station Tracking")]
    [SerializeField] private float stationActiveDistance = 70f;
    private int activeStationNumber = 0;
    private Vector3 activeStopPointPosition = Vector3.zero;
    private bool hasActiveStation = false;

    [System.Serializable]
    public class SimBikeData
    {
        public string time;
        public long unixTimestamp;
        public string permutationId;
        public Vector3 position;
        public Vector3 velocity;
        public float forwardVelocity;
        public Vector3 rotation;
        public float bikeYaw;
        public Vector3 handlesRotation;
        public float handlebarYaw;
        public int busStationNumber;
        public int busHMIState;
        public int infraHMI5;
        public int infraHMI6;
        
        public string ToCSVString()
        {
            return $"{time}," +
                   $"{unixTimestamp}," +
                   $"{permutationId}," +
                   $"{position.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{position.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{position.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{velocity.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{velocity.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{velocity.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{forwardVelocity.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{rotation.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{rotation.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{rotation.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{bikeYaw.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{handlesRotation.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{handlesRotation.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{handlesRotation.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{handlebarYaw.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{busStationNumber}," +
                   $"{busHMIState}," +
                   $"{infraHMI5}," +
                   $"{infraHMI6}";
        }
        
        public static string GetCSVHeader()
        {
            return "Time,UnixTimestamp,PermutationId,PosX,PosY,PosZ,VelX,VelY,VelZ,ForwardVel,RotX,RotY,RotZ,BikeYaw,HandlesRotX,HandlesRotY,HandlesRotZ,HandlebarYaw,BusStationNumber,BusHMIState,Infra_HMI_5,Infra_HMI_6";
        }
    }
    
    [System.Serializable]
    public class DynamicObjectData
    {
        public string time;
        public long unixTimestamp;
        public string permutationId;
        public string objectName;
        public Vector3 position;
        public Vector3 rotation;
        public float yaw;
        public float sizeX;
        public float sizeY;
        
        public string ToCSVString()
        {
            return $"{time}," +
                   $"{unixTimestamp}," +
                   $"{permutationId}," +
                   $"{objectName}," +
                   $"{position.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{position.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{position.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{rotation.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{rotation.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{rotation.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{yaw.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{sizeX.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{sizeY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}";
        }
        
        public static string GetCSVHeader()
        {
            return "Time,UnixTimestamp,PermutationId,ObjectName,PosX,PosY,PosZ,RotX,RotY,RotZ,Yaw,SizeX,SizeY";
        }
    }

    void Start()
    {
        startTime = Time.time;
        cachedPermutationId = GetPermutationId();
        SetupFilesPaths();
        FindSimBikeAndHandles();
        SetupBusStationSensors();
        InitializeCSVFiles();
        
        // Subscribe to collision events from BikeSensor
        BikeSensor[] bikeSensors = FindObjectsOfType<BikeSensor>();
        foreach (var sensor in bikeSensors)
        {
            // We'll use a custom event system or check collision in Update
        }
    }

    void Update()
    {
        UpdateActiveStationFromSensors();
        UpdateActiveStationDistance();
        UpdateActiveBusInStation();
        
        // Log static objects once
        LogStaticObjectsOnce();

        if (!enableLogging) return;
        
        if (Time.time >= nextLogTime)
        {
            LogSimBikeData();
            LogDynamicObjectsData();
            nextLogTime = Time.time + loggingInterval;
        }
        
        CheckForNewObjects();
    }

    private void UpdateActiveStationFromSensors()
    {
        foreach (var kvp in bikeSensorToStationNumber)
        {
            BikeSensor sensor = kvp.Key;
            int stationNumber = kvp.Value;

            if (sensor != null && sensor.isTriggered)
            {
                if (activeStationNumber != stationNumber)
                {
                    activeStationNumber = stationNumber;
                    hasActiveStation = true;

                    if (stationNumberToStopPoint.TryGetValue(stationNumber, out Transform stopPoint) && stopPoint != null)
                    {
                        activeStopPointPosition = stopPoint.position;
                    }
                    else
                    {
                        activeStopPointPosition = sensor.transform.position;
                        Debug.LogWarning($"DataLog: StopPoint not found for station {stationNumber}. Using BikeSensor position instead.");
                    }
                }

                return; // Only one station should be active at a time
            }
        }
    }

    private void UpdateActiveStationDistance()
    {
        if (!hasActiveStation || simBike == null) return;

        float distance = Vector3.Distance(simBike.transform.position, activeStopPointPosition);
        if (distance >= stationActiveDistance)
        {
            activeStationNumber = 0;
            hasActiveStation = false;
            activeStopPointPosition = Vector3.zero;
        }
    }

    private void UpdateActiveBusInStation()
    {
        // Find the active bus in the currently active station
        activeBusInStation = null;
        
        if (activeStationNumber > 0 && activeStationNumber <= 6)
        {
            GameObject busStationParent = GameObject.Find("BusStationParent");
            if (busStationParent != null)
            {
                string stationName = $"BusStation_{activeStationNumber}";
                Transform stationTransform = busStationParent.transform.Find(stationName);
                
                if (stationTransform != null)
                {
                    BusStationController controller = stationTransform.GetComponent<BusStationController>();
                    if (controller != null)
                    {
                        // Use reflection to get the bus object from the controller
                        System.Reflection.FieldInfo busField = typeof(BusStationController)
                            .GetField("bus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (busField != null)
                        {
                            activeBusInStation = (GameObject)busField.GetValue(controller);
                        }
                    }
                }
            }
        }
    }
    
    private void SetupFilesPaths()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folderPath = Path.Combine("C:\\Users\\TUBVVTK-VTSIM14\\unity_2025_4", "DataLogging");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        
        // Include PermutationID in filename
        string baseFileName = string.IsNullOrEmpty(cachedPermutationId) 
            ? $"{csvFileName}_{timestamp}" 
            : $"{csvFileName}_P{cachedPermutationId}_{timestamp}";
            
        simBikeFilePath = Path.Combine(folderPath, $"{baseFileName}_SimBike.csv");
        dynamicObjectsFilePath = Path.Combine(folderPath, $"{baseFileName}_DynamicObjects.csv");
        staticObjectsFilePath = Path.Combine(folderPath, $"{baseFileName}_StaticObjects.csv");
    }
    
    private string GetPermutationId()
    {
        // Try to get ScenarioManager if not assigned in inspector
        if (scenarioManager == null)
        {
            GameObject scenarioManagerObject = GameObject.Find("ScenarioManager");
            if (scenarioManagerObject != null)
            {
                scenarioManager = scenarioManagerObject.GetComponent<ScenarioManager>();
            }
        }
        
        if (scenarioManager != null)
        {
            // Use reflection to access private permutationId field
            System.Reflection.FieldInfo permutationIdField = typeof(ScenarioManager)
                .GetField("permutationId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (permutationIdField != null)
            {
                string permutationId = (string)permutationIdField.GetValue(scenarioManager);
                return string.IsNullOrEmpty(permutationId) ? "Unknown" : permutationId;
            }
        }
        
        Debug.LogWarning("DataLog: Could not retrieve PermutationID from ScenarioManager");
        return "Unknown";
    }
    
    private void FindSimBikeAndHandles()
    {
        if (simBike == null)
        {
            simBike = GameObject.Find("SimBike");
            if (simBike == null)
            {
                Debug.LogError("DataLog: SimBike GameObject not found! Please assign it in the inspector.");
                return;
            }
            else
            {
                Debug.Log("DataLog: Found SimBike GameObject in the scene.");
            }
        }
        
        if (handlesTransform == null)
        {
            Transform handlesChild = simBike.transform.Find("Handles");
            if (handlesChild != null)
            {
                handlesTransform = handlesChild;
                Debug.Log("DataLog: Found Handles child object in SimBike.");
            }
            else
            {
                Debug.LogError("DataLog: Handles child object not found in SimBike!");
            }
        }
        
        // Cache Rigidbody for velocity tracking
        if (simBike != null)
        {
            simBikeRigidbody = simBike.GetComponent<Rigidbody>();
            if (simBikeRigidbody == null)
            {
                Debug.LogWarning("DataLog: SimBike does not have a Rigidbody component. Velocity will be zero.");
            }
        }
    }
    
    private void SetupBusStationSensors()
    {
        GameObject busStationParent = GameObject.Find("BusStationParent");
        if (busStationParent == null)
        {
            Debug.LogWarning("DataLog: BusStationParent not found!");
            return;
        }
        
        // Find all BusStation children and their BikeSensor components
        for (int i = 1; i <= 6; i++)
        {
            string stationName = $"BusStation_{i}";
            Transform stationTransform = busStationParent.transform.Find(stationName);
            if (stationTransform != null)
            {
                Transform bikeSensorTransform = stationTransform.Find("BikeSensor");
                if (bikeSensorTransform != null)
                {
                    BikeSensor bikeSensor = bikeSensorTransform.GetComponent<BikeSensor>();
                    if (bikeSensor != null)
                    {
                        bikeSensorToStationNumber[bikeSensor] = i;
                    }
                }

                Transform stopPointTransform = stationTransform.Find($"Station_{i}_StopPoint");
                if (stopPointTransform != null)
                {
                    stationNumberToStopPoint[i] = stopPointTransform;
                }
                else
                {
                    Debug.LogWarning($"DataLog: Station_{i}_StopPoint not found under {stationName}.");
                }
                
                // Cache BusStationController for stations 5 and 6
                if (i == 5)
                {
                    stationController5 = stationTransform.GetComponent<BusStationController>();
                    if (stationController5 == null)
                        Debug.LogWarning("DataLog: BusStationController not found for Station 5");
                }
                else if (i == 6)
                {
                    stationController6 = stationTransform.GetComponent<BusStationController>();
                    if (stationController6 == null)
                        Debug.LogWarning("DataLog: BusStationController not found for Station 6");
                }
            }
        }
    }
    
    private void InitializeCSVFiles()
    {
        // Initialize SimBike CSV file
        using (StreamWriter writer = new StreamWriter(simBikeFilePath, false, Encoding.UTF8))
        {
            writer.WriteLine(SimBikeData.GetCSVHeader());
        }
        
        // Initialize Dynamic Objects CSV file
        using (StreamWriter writer = new StreamWriter(dynamicObjectsFilePath, false, Encoding.UTF8))
        {
            writer.WriteLine(DynamicObjectData.GetCSVHeader());
        }
        
        // Initialize Static Objects CSV file
        using (StreamWriter writer = new StreamWriter(staticObjectsFilePath, false, Encoding.UTF8))
        {
            writer.WriteLine(DynamicObjectData.GetCSVHeader());
        }
        
        Debug.Log($"DataLog: CSV files initialized at:\n{simBikeFilePath}\n{dynamicObjectsFilePath}\n{staticObjectsFilePath}");
    }
    
    private int GetBusHMIState()
    {
        if (activeBusInStation == null)
            return -1;
        
        // Find HMIParent on the active bus
        Transform hmiParent = activeBusInStation.transform.Find("HMIParent");
        if (hmiParent == null)
            return -1;
        
        // Get the first HMIController child and return its state
        for (int i = 0; i < hmiParent.childCount; i++)
        {
            Transform child = hmiParent.GetChild(i);
            HMIController hmiController = child.GetComponent<HMIController>();
            if (hmiController != null)
            {
                return hmiController.CurrentHMIState;
            }
        }
        
        return -1;
    }
    
    private int GetInfraHMIState(BusStationController controller)
    {
        if (controller == null)
            return -1;
        
        // Return the current HMI state from the controller
        return controller.CurrentHMIState;
    }
    
    private void LogSimBikeData()
    {
        if (simBike == null) return;
        
        Vector3 currentVelocity = simBikeRigidbody != null ? simBikeRigidbody.linearVelocity : Vector3.zero;
        float forwardSpeed = simBikeRigidbody != null ? Vector3.Dot(currentVelocity, simBike.transform.forward) : 0f;
        long unixTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        SimBikeData data = new SimBikeData
        {
            time = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"),
            unixTimestamp = unixTimestamp,
            permutationId = cachedPermutationId,
            position = simBike.transform.position,
            velocity = currentVelocity,
            forwardVelocity = forwardSpeed,
            rotation = simBike.transform.eulerAngles,
            bikeYaw = simBike.transform.eulerAngles.y,
            handlesRotation = handlesTransform != null ? handlesTransform.eulerAngles : Vector3.zero,
            handlebarYaw = handlesTransform != null ? handlesTransform.eulerAngles.y : 0f,
            busStationNumber = hasActiveStation ? activeStationNumber : 0,
            busHMIState = GetBusHMIState(),
            infraHMI5 = GetInfraHMIState(stationController5),
            infraHMI6 = GetInfraHMIState(stationController6)
        };
        
        simBikeDataList.Add(data);
        
        // Write to file every 100 entries to prevent memory overflow
        if (simBikeDataList.Count >= 100)
        {
            WriteSimBikeDataToFile();
            simBikeDataList.Clear();
        }
    }
    
    private void LogDynamicObjectsData()
    {
        long unixTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        foreach (GameObject obj in trackedObjects)
        {
            if (obj == null) continue;
            
            string objName = obj.name.ToLower();
            
            // Only log buses and pedestrians matching specific patterns
            bool isBus = objName.Contains("bus_") && objName.Contains("-bus-");
            bool isPedestrian = objName.Contains("pf_") && objName.Contains("-pedestrian-");
            
            if (!isBus && !isPedestrian)
                continue;
            
            Vector3 objectRotation = obj.transform.eulerAngles;
            Vector2 objectSize = GetObjectSizeXZ(obj);
            
            DynamicObjectData data = new DynamicObjectData
            {
                time = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"),
                unixTimestamp = unixTimestamp,
                permutationId = cachedPermutationId,
                objectName = obj.name,
                position = obj.transform.position,
                rotation = objectRotation,
                yaw = objectRotation.y,
                sizeX = objectSize.x,
                sizeY = objectSize.y
            };
            
            dynamicObjectDataList.Add(data);
        }
        
        // Write to file every 500 entries to prevent memory overflow
        if (dynamicObjectDataList.Count >= 500)
        {
            WriteDynamicObjectsDataToFile();
            dynamicObjectDataList.Clear();
        }
    }
    
    private void CheckForNewObjects()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            // Skip if already tracked or if it's a static/UI object
            if (trackedObjects.Contains(obj) || obj == simBike) continue;
            
            // Check if this is a dynamically created object (you may need to adjust this logic)
            if (IsDynamicObject(obj))
            {
                trackedObjects.Add(obj);
                // Debug.Log($"DataLog: Started tracking new dynamic object: {obj.name}");
            }
        }
    }
    
    private bool IsDynamicObject(GameObject obj)
    {
        // Only track pedestrians and buses (moving objects)
        string objName = obj.name.ToLower();
        
        // Check for pedestrians
        if (objName.Contains("pedestrian"))
            return true;
        
        // Check for buses
        if (objName.Contains("bus") && !objName.Contains("station"))
            return true;
        
        return false;
    }
    
    private Vector2 GetObjectSizeXZ(GameObject obj)
    {
        // Try collider bounds first (for pedestrians)
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            return new Vector2(bounds.size.x, bounds.size.z);
        }
        
        // Fall back to rigidbody bounds (for vehicles/bus)
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Bounds bounds = new Bounds(obj.transform.position, Vector3.one);
            Collider[] colliders = rb.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                foreach (Collider c in colliders)
                {
                    bounds.Encapsulate(c.bounds);
                }
            }
            return new Vector2(bounds.size.x, bounds.size.z);
        }
        
        return Vector2.zero;
    }
    
    private void LogStaticObjectsOnce()
    {
        long unixTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bool needsWrite = false;
        
        // Find all GameObjects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
            if (obj == null || recordedStaticObjects.Contains(obj))
                continue;
            
            string objName = obj.name.ToLower();
            
            // Check if this is a static object we want to record
            bool isStaticObject = objName.Contains("passenger") || 
                                  objName == "r1_start" || 
                                  objName == "r2_start";
            
            if (isStaticObject)
            {
                Vector3 objectRotation = obj.transform.eulerAngles;
                Vector2 objectSize = GetObjectSizeXZ(obj);
                
                DynamicObjectData data = new DynamicObjectData
                {
                    time = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"),
                    unixTimestamp = unixTimestamp,
                    permutationId = cachedPermutationId,
                    objectName = obj.name,
                    position = obj.transform.position,
                    rotation = objectRotation,
                    yaw = objectRotation.y,
                    sizeX = objectSize.x,
                    sizeY = objectSize.y
                };
                
                staticObjectDataList.Add(data);
                recordedStaticObjects.Add(obj);
                needsWrite = true;
            }
        }
        
        // Write immediately when static objects are found
        if (needsWrite && staticObjectDataList.Count > 0)
        {
            WriteStaticObjectsDataToFile();
            staticObjectDataList.Clear();
        }
    }
    
    private void WriteSimBikeDataToFile()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(simBikeFilePath, true, Encoding.UTF8))
            {
                foreach (var data in simBikeDataList)
                {
                    writer.WriteLine(data.ToCSVString());
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DataLog: Error writing SimBike data to file: {e.Message}");
        }
    }
    
    private void WriteDynamicObjectsDataToFile()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(dynamicObjectsFilePath, true, Encoding.UTF8))
            {
                foreach (var data in dynamicObjectDataList)
                {
                    writer.WriteLine(data.ToCSVString());
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DataLog: Error writing dynamic objects data to file: {e.Message}");
        }
    }
    
    private void WriteStaticObjectsDataToFile()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(staticObjectsFilePath, true, Encoding.UTF8))
            {
                foreach (var data in staticObjectDataList)
                {
                    writer.WriteLine(data.ToCSVString());
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DataLog: Error writing static objects data to file: {e.Message}");
        }
    }
    
    private void OnApplicationQuit()
    {
        // Save any remaining data when the application quits
        if (simBikeDataList.Count > 0)
        {
            WriteSimBikeDataToFile();
        }
        
        if (dynamicObjectDataList.Count > 0)
        {
            WriteDynamicObjectsDataToFile();
        }
        
        if (staticObjectDataList.Count > 0)
        {
            WriteStaticObjectsDataToFile();
        }
        
        Debug.Log("DataLog: Final data saved on application quit.");
    }
    
    private void OnDisable()
    {
        // Save any remaining data when the component is disabled
        if (simBikeDataList.Count > 0)
        {
            WriteSimBikeDataToFile();
        }
        
        if (dynamicObjectDataList.Count > 0)
        {
            WriteDynamicObjectsDataToFile();
        }
        
        if (staticObjectDataList.Count > 0)
        {
            WriteStaticObjectsDataToFile();
        }
    }
}
