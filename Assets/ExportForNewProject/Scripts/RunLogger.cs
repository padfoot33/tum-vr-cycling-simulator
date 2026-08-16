using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class RunLogger : MonoBehaviour
{
    public enum SteeringAxis
    {
        X,
        Y,
        Z
    }

    [Header("Assign in Inspector")]
    public Transform bikeTransform;
    public ReferencePathTracker referencePathTracker;

    [Header("Steering")]
    public Transform steeringTransform;
    public SteeringAxis steeringAxis = SteeringAxis.Z;

    [Header("Participant / Trial")]
    public string participantId = "P01";
    public int trialIndex = 1;

    [Header("Scenario Info")]
    public string scenarioName = "S1_Baseline";
    public string initialLOD = "LOW";

    [Header("Logging")]
    [Range(1, 100)]
    public int logHz = 20;
    public bool useParticipantFolder = true;

    [Header("Bike Speed (Position-Based)")]
    public float maxBikeSpeedKph = 28.8f;
    public float speedSmoothing = 0.2f;
    public float ignoreFirstSeconds = 0.5f;

    private string currentLOD;
    private string currentEvent = "";

    private float eventVehicleX = float.NaN;
    private float eventVehicleZ = float.NaN;
    private float eventVehicleSpeedKph = float.NaN;

    private string runId;
    private string baseLogsDirectory;
    private string participantDirectory;
    private string runFilePath;

    private StreamWriter writer;
    private bool isLogging = false;
    private float startTime;
    private float nextLogTime;

    private Vector3 lastBikePos;
    private float lastBikePosTime;
    private float smoothedSpeedKph = 0f;
    private Vector3 lastVelocityWorld = Vector3.zero;

    private float lastHeadingDeg = 0f;
    private float lastSteeringAngleDeg = 0f;

    private float steeringZeroOffset = 0f;

    public bool IsLogging => isLogging;

    private void Awake()
    {
        currentLOD = initialLOD;

        baseLogsDirectory = Path.Combine(Application.dataPath, "..", "Logs");
        if (!Directory.Exists(baseLogsDirectory))
            Directory.CreateDirectory(baseLogsDirectory);
    }

    private void Start()
    {
        if (bikeTransform != null)
        {
            lastBikePos = bikeTransform.position;
            lastBikePosTime = Time.time;
            lastHeadingDeg = bikeTransform.eulerAngles.y;
        }
    }

    private void Update()
    {
        if (!isLogging || bikeTransform == null)
            return;

        if (Time.time >= nextLogTime)
        {
            WriteSampleRow();
            nextLogTime = Time.time + (1f / Mathf.Max(1, logHz));
        }
    }

    private void OnDestroy()
    {
        StopLogging();
    }

    public void StartLogging()
    {
        if (isLogging)
            return;

        if (bikeTransform == null)
        {
            Debug.LogError("[RunLogger] Bike Transform is not assigned.");
            return;
        }

        currentLOD = initialLOD;
        currentEvent = "";
        ClearEventVehicleData();

        string safeParticipant = Safe(participantId);
        string safeScenario = Safe(scenarioName);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        runId = $"{safeParticipant}_T{trialIndex}_{safeScenario}_{timestamp}";

        participantDirectory = useParticipantFolder
            ? Path.Combine(baseLogsDirectory, safeParticipant)
            : baseLogsDirectory;

        if (!Directory.Exists(participantDirectory))
            Directory.CreateDirectory(participantDirectory);

        runFilePath = Path.Combine(participantDirectory, $"{runId}.csv");
        writer = new StreamWriter(runFilePath, false);

        startTime = Time.time;
        nextLogTime = Time.time;

        lastBikePos = bikeTransform.position;
        lastBikePosTime = Time.time;
        smoothedSpeedKph = 0f;
        lastVelocityWorld = Vector3.zero;

        lastHeadingDeg = bikeTransform.eulerAngles.y;

        steeringZeroOffset = GetRawSteeringAngleDeg();
        lastSteeringAngleDeg = 0f;

        WriteMetadataBlock();
        writer.WriteLine(
            "t,x,y,z,speed_kph,vel_x,vel_z,accel_x,accel_z,heading_deg,movement_dir_deg,yaw_rate_deg_s,steering_angle_deg,steering_rate_deg_s,deviation_from_path,lod,event,event_vehicle_x,event_vehicle_z,event_vehicle_speed_kph"
        );
        writer.Flush();

        isLogging = true;
        Debug.Log($"[RunLogger] Logging started: {runFilePath}");
    }

    public void StopLogging()
    {
        if (!isLogging)
            return;

        isLogging = false;

        writer?.Flush();
        writer?.Close();
        writer = null;

        Debug.Log("[RunLogger] Logging stopped.");
    }

    public void SetLOD(string lod)
    {
        currentLOD = lod;
    }

    public void SetEvent(string evt)
    {
        currentEvent = string.IsNullOrEmpty(evt) ? "" : evt;
    }

    public void UpdateEventVehicleData(float x, float z, float speedKph)
    {
        eventVehicleX = x;
        eventVehicleZ = z;
        eventVehicleSpeedKph = speedKph;
    }

    public void ClearEventVehicleData()
    {
        eventVehicleX = float.NaN;
        eventVehicleZ = float.NaN;
        eventVehicleSpeedKph = float.NaN;
    }

    public void LogMarker(string markerName)
    {
        if (!isLogging || writer == null || bikeTransform == null)
            return;

        WriteRow(markerName);
        writer.Flush();

        Debug.Log($"[RunLogger] Marker logged: {markerName}");
    }

    public void MarkEvent(string markerName)
    {
        LogMarker(markerName);
    }

    public void MarkEventAndStop(string markerName)
    {
        LogMarker(markerName);
        StopLogging();
    }

    private void WriteSampleRow()
    {
        if (writer == null || bikeTransform == null)
            return;

        WriteRow(currentEvent);
    }

    private void WriteRow(string eventValue)
    {
        float t = Time.time - startTime;
        Vector3 p = bikeTransform.position;

        float dt = Time.time - lastBikePosTime;
        if (dt <= 0.0001f)
            dt = 0.0001f;

        Vector3 delta = p - lastBikePos;
        Vector3 velocityWorld = delta / dt;
        Vector3 accelWorld = (velocityWorld - lastVelocityWorld) / dt;

        float rawSpeedKph = (delta.magnitude / dt) * 3.6f;
        if (t < ignoreFirstSeconds)
            rawSpeedKph = 0f;

        rawSpeedKph = Mathf.Min(rawSpeedKph, maxBikeSpeedKph);
        smoothedSpeedKph = Mathf.Lerp(smoothedSpeedKph, rawSpeedKph, speedSmoothing);

        float velX = velocityWorld.x;
        float velZ = velocityWorld.z;
        float accelX = accelWorld.x;
        float accelZ = accelWorld.z;

        float headingDeg = bikeTransform.eulerAngles.y;
        float yawRateDegS = Mathf.DeltaAngle(lastHeadingDeg, headingDeg) / dt;

        float movementDirDeg = float.NaN;
        Vector3 flatVel = new Vector3(velocityWorld.x, 0f, velocityWorld.z);
        if (flatVel.sqrMagnitude > 0.0001f)
            movementDirDeg = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;

        float steeringAngleDeg = GetSteeringAngleDeg();
        float steeringRateDegS = float.NaN;
        if (!float.IsNaN(steeringAngleDeg))
            steeringRateDegS = (steeringAngleDeg - lastSteeringAngleDeg) / dt;

        float deviation = float.NaN;
        if (referencePathTracker != null)
            deviation = referencePathTracker.currentDeviation;

        writer.WriteLine(string.Join(",",
            F(t),
            F(p.x),
            F(p.y),
            F(p.z),
            F(smoothedSpeedKph),
            F(velX),
            F(velZ),
            F(accelX),
            F(accelZ),
            F(headingDeg),
            FV(movementDirDeg),
            FV(yawRateDegS),
            FV(steeringAngleDeg),
            FV(steeringRateDegS),
            FV(deviation),
            Safe(currentLOD),
            Safe(eventValue),
            FV(eventVehicleX),
            FV(eventVehicleZ),
            FV(eventVehicleSpeedKph)
        ));

        lastBikePos = p;
        lastBikePosTime = Time.time;
        lastVelocityWorld = velocityWorld;
        lastHeadingDeg = headingDeg;

        if (!float.IsNaN(steeringAngleDeg))
            lastSteeringAngleDeg = steeringAngleDeg;
    }

    private float GetRawSteeringAngleDeg()
    {
        if (steeringTransform == null)
            return float.NaN;

        float angle = 0f;

        switch (steeringAxis)
        {
            case SteeringAxis.X:
                angle = steeringTransform.localEulerAngles.x;
                break;
            case SteeringAxis.Y:
                angle = steeringTransform.localEulerAngles.y;
                break;
            case SteeringAxis.Z:
                angle = steeringTransform.localEulerAngles.z;
                break;
        }

        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    private float GetSteeringAngleDeg()
    {
        float raw = GetRawSteeringAngleDeg();
        if (float.IsNaN(raw))
            return float.NaN;

        float relative = raw - steeringZeroOffset;

        while (relative > 180f)
            relative -= 360f;

        while (relative < -180f)
            relative += 360f;

        return relative;
    }

    private void WriteMetadataBlock()
    {
        writer.WriteLine("# ------------------------------------------------------------");
        writer.WriteLine("# VR Cycling Experiment Log");
        writer.WriteLine($"# Participant ID: {participantId}");
        writer.WriteLine($"# Trial Index: {trialIndex}");
        writer.WriteLine($"# Run ID: {runId}");
        writer.WriteLine($"# Scenario: {scenarioName}");
        writer.WriteLine($"# Initial LOD: {initialLOD}");
        writer.WriteLine($"# Log Frequency (Hz): {logHz}");
        writer.WriteLine($"# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine("# ------------------------------------------------------------");
    }

    private string F(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private string FV(float value)
    {
        return float.IsNaN(value) ? "NA" : value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private string Safe(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.Replace(",", "_");
    }
}