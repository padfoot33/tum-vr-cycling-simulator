using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace CyclingExperiment.Logging
{
    /// <summary>
    /// 20 Hz experiment CSV matching the client run-log schema (no LOD).
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class ExperimentRunLogger : MonoBehaviour
    {
        public static ExperimentRunLogger Instance { get; private set; }

        [Header("Assign in Inspector")]
        [SerializeField] private Transform bikeTransform;
        [SerializeField] private MonoBehaviour cyclistMotion;
        [SerializeField] private ReferencePathTracker referencePathTracker;

        [Header("Participant / Trial")]
        [SerializeField] private string participantId = "P01";
        [SerializeField] private int trialIndex = 1;

        [Header("Logging")]
        [SerializeField, Range(1, 100)] private int logHz = 20;
        [SerializeField] private bool useParticipantFolder = true;
        [SerializeField] private float speedSmoothing = 0.2f;
        [SerializeField] private float ignoreFirstSeconds = 0.5f;
        [SerializeField] private float maxBikeSpeedKph = 28.8f;
        [SerializeField] private float technicalIssueFps = 30f;
        [SerializeField] private float implausibleMinY = -5f;
        [SerializeField] private float implausibleMaxY = 20f;

        private ICyclistMotion _cyclist;
        private string _scenarioName = "Route1_BusStop_RightTurn";
        private string _segmentId = "C1";
        private string _taskContext = "approach";
        private string _scriptedEvent = "NONE";
        private string _scriptedPhase = "NONE";
        private string _closePassEvent = "NONE";

        private float _eventVehicleX = float.NaN;
        private float _eventVehicleZ = float.NaN;
        private float _eventVehicleSpeedKph = float.NaN;

        private string _runId;
        private string _runFilePath;
        private StreamWriter _writer;
        private bool _isLogging;
        private float _startTime;
        private float _nextLogTime;

        private Vector3 _lastBikePos;
        private float _lastBikePosTime;
        private float _smoothedSpeedKph;
        private Vector3 _lastVelocityWorld;
        private float _lastHeadingDeg;
        private float _lastSteeringAngleDeg;
        private float _steeringZeroOffset;

        public bool IsLogging => _isLogging;
        public string RunId => _runId;
        public string ScenarioName => _scenarioName;
        public bool HasScriptedEvent =>
            !string.IsNullOrEmpty(_scriptedEvent) &&
            !string.Equals(_scriptedEvent, "NONE", StringComparison.Ordinal);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            BindRefs();
            if (bikeTransform != null)
            {
                _lastBikePos = bikeTransform.position;
                _lastBikePosTime = Time.time;
                _lastHeadingDeg = bikeTransform.eulerAngles.y;
            }
        }

        private void Update()
        {
            if (!_isLogging || bikeTransform == null)
                return;

            MaybeAdvanceRoute2Segment();

            if (Time.time >= _nextLogTime)
            {
                WriteSampleRow();
                _nextLogTime = Time.time + (1f / Mathf.Max(1, logHz));
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            StopLogging();
        }

        private void OnApplicationQuit()
        {
            StopLogging();
        }

        public void BindRefs()
        {
            var refs = ExperimentSceneRefs.Instance;
            if (refs != null)
            {
                if (bikeTransform == null) bikeTransform = refs.bicycleTransform;
                if (cyclistMotion == null && refs.Cyclist != null)
                    cyclistMotion = refs.Cyclist as MonoBehaviour;
                if (referencePathTracker == null)
                    referencePathTracker = refs.route1PathTracker;
            }

            _cyclist = cyclistMotion as ICyclistMotion;
            if (_cyclist == null && refs != null)
                _cyclist = refs.Cyclist;
        }

        public void StartLogging(string scenarioName, string segmentId = "C1", string taskContext = "approach")
        {
            BindRefs();
            if (bikeTransform == null)
            {
                Debug.LogError("[ExperimentRunLogger] Bike Transform is not assigned.");
                return;
            }

            if (_isLogging)
                StopLogging();

            _scenarioName = string.IsNullOrEmpty(scenarioName) ? "Unnamed" : scenarioName;
            _segmentId = string.IsNullOrEmpty(segmentId) ? "C1" : segmentId;
            _taskContext = string.IsNullOrEmpty(taskContext) ? "approach" : taskContext;
            _scriptedEvent = "NONE";
            _scriptedPhase = "NONE";
            _closePassEvent = "NONE";
            ClearEventVehicleData();

            if (_cyclist != null)
                maxBikeSpeedKph = Mathf.Max(maxBikeSpeedKph, _cyclist.MaxSpeedMps * 3.6f);

            string safeParticipant = Safe(participantId);
            string safeScenario = Safe(_scenarioName);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _runId = $"{safeParticipant}_{safeScenario}_{timestamp}";

            string baseLogs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            string directory = useParticipantFolder
                ? Path.Combine(baseLogs, safeParticipant)
                : baseLogs;
            Directory.CreateDirectory(directory);

            _runFilePath = Path.Combine(directory, $"{_runId}.csv");
            _writer = new StreamWriter(_runFilePath, false);

            _startTime = Time.time;
            _nextLogTime = Time.time;
            _lastBikePos = bikeTransform.position;
            _lastBikePosTime = Time.time;
            _smoothedSpeedKph = 0f;
            _lastVelocityWorld = Vector3.zero;
            _lastHeadingDeg = bikeTransform.eulerAngles.y;
            _steeringZeroOffset = GetRawSteeringAngleDeg();
            if (float.IsNaN(_steeringZeroOffset))
                _steeringZeroOffset = 0f;
            _lastSteeringAngleDeg = 0f;

            WriteMetadataBlock();
            _writer.WriteLine(
                "run_id,participant_id,scenario_name,segment_id,task_context,t,event,event_phase,x,y,z,speed_kph,vel_x,vel_z,accel_x,accel_z,heading_deg,movement_dir_deg,yaw_rate_deg_s,steering_angle_deg,steering_rate_deg_s,deviation_from_path,event_vehicle_x,event_vehicle_z,event_vehicle_speed_kph,fps,frame_time_ms,technical_issue_flag");
            _writer.Flush();

            _isLogging = true;
            WriteRow("RUN_START");
            WriteRow("START");
            _writer.Flush();
            Debug.Log($"[ExperimentRunLogger] Logging started: {_runFilePath}");
        }

        public void StopLogging()
        {
            if (!_isLogging)
                return;

            WriteRow("FINISH");
            WriteRow("RUN_END");
            _isLogging = false;
            _writer?.Flush();
            _writer?.Close();
            _writer = null;
            Debug.Log("[ExperimentRunLogger] Logging stopped.");
        }

        public void SetSegment(string segmentId, string taskContext)
        {
            if (!string.IsNullOrEmpty(segmentId))
                _segmentId = segmentId;
            if (!string.IsNullOrEmpty(taskContext))
                _taskContext = taskContext;
        }

        public void SetScriptedEvent(string evt, string phase)
        {
            _scriptedEvent = string.IsNullOrEmpty(evt) ? "NONE" : evt;
            _scriptedPhase = string.IsNullOrEmpty(phase) ? "NONE" : phase;
        }

        public void ClearScriptedEvent()
        {
            _scriptedEvent = "NONE";
            _scriptedPhase = "NONE";
        }

        public void SetClosePassEvent(string evt)
        {
            _closePassEvent = string.IsNullOrEmpty(evt) ? "NONE" : evt;
        }

        public void UpdateEventVehicleData(float x, float z, float speedKph)
        {
            _eventVehicleX = x;
            _eventVehicleZ = z;
            _eventVehicleSpeedKph = speedKph;
        }

        public void ClearEventVehicleData()
        {
            _eventVehicleX = float.NaN;
            _eventVehicleZ = float.NaN;
            _eventVehicleSpeedKph = float.NaN;
        }

        public void MarkEvent(string markerName)
        {
            if (!_isLogging || _writer == null || bikeTransform == null)
                return;
            if (string.IsNullOrEmpty(markerName))
                return;

            WriteRow(markerName);
            _writer.Flush();
        }

        private void WriteSampleRow()
        {
            WriteRow(CurrentEventName());
        }

        private string CurrentEventName()
        {
            if (HasScriptedEvent)
                return _scriptedEvent;
            if (!string.IsNullOrEmpty(_closePassEvent) &&
                !string.Equals(_closePassEvent, "NONE", StringComparison.Ordinal))
                return _closePassEvent;
            return "NONE";
        }

        private string CurrentEventPhase()
        {
            if (HasScriptedEvent)
                return _scriptedPhase;
            if (!string.IsNullOrEmpty(_closePassEvent) &&
                !string.Equals(_closePassEvent, "NONE", StringComparison.Ordinal))
                return "CLOSE_PASS_ACTIVE";
            return "NONE";
        }

        private void WriteRow(string eventValue)
        {
            if (_writer == null || bikeTransform == null)
                return;

            float t = Time.time - _startTime;
            Vector3 p = bikeTransform.position;

            float dt = Time.time - _lastBikePosTime;
            if (dt <= 0.0001f)
                dt = 0.0001f;

            Vector3 delta = p - _lastBikePos;
            Vector3 velocityWorld = delta / dt;
            Vector3 accelWorld = (velocityWorld - _lastVelocityWorld) / dt;

            float rawSpeedKph = (delta.magnitude / dt) * 3.6f;
            if (t < ignoreFirstSeconds)
                rawSpeedKph = 0f;

            rawSpeedKph = Mathf.Min(rawSpeedKph, maxBikeSpeedKph);
            _smoothedSpeedKph = Mathf.Lerp(_smoothedSpeedKph, rawSpeedKph, speedSmoothing);

            float headingDeg = bikeTransform.eulerAngles.y;
            float yawRateDegS = Mathf.DeltaAngle(_lastHeadingDeg, headingDeg) / dt;

            float movementDirDeg = float.NaN;
            Vector3 flatVel = new Vector3(velocityWorld.x, 0f, velocityWorld.z);
            if (flatVel.sqrMagnitude > 0.0001f)
                movementDirDeg = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;

            float steeringAngleDeg = GetSteeringAngleDeg();
            float steeringRateDegS = float.NaN;
            if (!float.IsNaN(steeringAngleDeg))
                steeringRateDegS = (steeringAngleDeg - _lastSteeringAngleDeg) / dt;

            float deviation = float.NaN;
            if (referencePathTracker != null)
                deviation = referencePathTracker.currentDeviation;

            float fps = Time.unscaledDeltaTime > 0.0001f ? 1f / Time.unscaledDeltaTime : 0f;
            float frameMs = Time.unscaledDeltaTime * 1000f;
            int issue = (fps > 0f && fps < technicalIssueFps) || p.y < implausibleMinY || p.y > implausibleMaxY
                ? 1
                : 0;

            _writer.WriteLine(string.Join(",",
                Safe(_runId),
                Safe(participantId),
                Safe(_scenarioName),
                Safe(_segmentId),
                Safe(_taskContext),
                F(t),
                Safe(string.IsNullOrEmpty(eventValue) ? "NONE" : eventValue),
                Safe(CurrentEventPhase()),
                F(p.x),
                F(p.y),
                F(p.z),
                F(_smoothedSpeedKph),
                F(velocityWorld.x),
                F(velocityWorld.z),
                F(accelWorld.x),
                F(accelWorld.z),
                F(headingDeg),
                FV(movementDirDeg),
                FV(yawRateDegS),
                FV(steeringAngleDeg),
                FV(steeringRateDegS),
                FV(deviation),
                FV(_eventVehicleX),
                FV(_eventVehicleZ),
                FV(_eventVehicleSpeedKph),
                F(fps),
                F(frameMs),
                issue.ToString(CultureInfo.InvariantCulture)
            ));

            _lastBikePos = p;
            _lastBikePosTime = Time.time;
            _lastVelocityWorld = velocityWorld;
            _lastHeadingDeg = headingDeg;
            if (!float.IsNaN(steeringAngleDeg))
                _lastSteeringAngleDeg = steeringAngleDeg;
        }

        private void MaybeAdvanceRoute2Segment()
        {
            if (bikeTransform == null)
                return;
            if (_scenarioName == null || _scenarioName.IndexOf("Route2", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            if (_segmentId == "C2")
                return;
            if (bikeTransform.position.z >= 110f)
                SetSegment("C2", "interaction");
        }

        private float GetRawSteeringAngleDeg()
        {
            if (_cyclist != null)
                return _cyclist.GetSteeringAngleDeg();
            return float.NaN;
        }

        private float GetSteeringAngleDeg()
        {
            float raw = GetRawSteeringAngleDeg();
            if (float.IsNaN(raw))
                return float.NaN;

            float relative = raw - _steeringZeroOffset;
            while (relative > 180f) relative -= 360f;
            while (relative < -180f) relative += 360f;
            return relative;
        }

        private void WriteMetadataBlock()
        {
            _writer.WriteLine("# ------------------------------------------------------------");
            _writer.WriteLine("# VR Cycling Experiment Log");
            _writer.WriteLine($"# Participant ID: {participantId}");
            _writer.WriteLine($"# Trial Index: {trialIndex}");
            _writer.WriteLine($"# Run ID: {_runId}");
            _writer.WriteLine($"# Scenario: {_scenarioName}");
            _writer.WriteLine($"# Initial Segment ID: {_segmentId}");
            _writer.WriteLine($"# Initial Task Context: {_taskContext}");
            _writer.WriteLine("# Initial Event Phase: NONE");
            _writer.WriteLine("# Logging Enabled: True");
            _writer.WriteLine($"# Path Deviation Enabled: {referencePathTracker != null}");
            _writer.WriteLine("# Event Vehicle Enabled: True");
            _writer.WriteLine($"# Log Frequency (Hz): {logHz}");
            _writer.WriteLine($"# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _writer.WriteLine($"# Simulation Start Time MS: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _writer.WriteLine($"# Log Path: {_runFilePath}");
            _writer.WriteLine("# ------------------------------------------------------------");
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FV(float value)
        {
            return float.IsNaN(value) ? "NA" : value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace(",", "_");
        }
    }
}
