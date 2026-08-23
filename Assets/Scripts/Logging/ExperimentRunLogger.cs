using System;
using System.Globalization;
using System.IO;
using CyclingExperiment.Scenarios;
using UnityEngine;

namespace CyclingExperiment.Logging
{
    /// <summary>
    /// 20 Hz experiment CSV matching the client run-log schema (no LOD).
    /// Every row has wall-clock timestamps for EdaMove / Movesense sync.
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

        [Header("Operator sync")]
        [SerializeField] private KeyCode syncPreKey = KeyCode.F9;
        [SerializeField] private KeyCode syncPostKey = KeyCode.F10;

        private ICyclistMotion _cyclist;
        private string _scenarioName = "Route1_BusStop_RightTurn";
        private string _segmentId = "C1";
        private string _taskContext = "approach";
        private string _scriptedEvent = "NONE";
        private string _scriptedPhase = "NONE";
        private string _closePassEvent = "NONE";

        private int _trafficEnabled = -1;
        private string _conditionId = "UNKNOWN";

        private float _eventVehicleX = float.NaN;
        private float _eventVehicleZ = float.NaN;
        private float _eventVehicleSpeedKph = float.NaN;

        private string _runId;
        private string _runFilePath;
        private StreamWriter _writer;
        private bool _isLogging;
        private DateTimeOffset _runStartUtc;
        private float _nextLogUnscaled;
        private float _nextFlushUnscaled;

        private Vector3 _lastBikePos;
        private DateTimeOffset _lastSampleUtc;
        private float _smoothedSpeedKph;
        private Vector3 _lastVelocityWorld;
        private Vector3 _lastAccelWorld;
        private float _lastHeadingDeg;
        private float _lastYawRateDegS;
        private float _lastSteeringAngleDeg;
        private float _lastSteeringRateDegS;
        private float _lastMovementDirDeg;
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
                _lastSampleUtc = DateTimeOffset.UtcNow;
                _lastHeadingDeg = bikeTransform.eulerAngles.y;
            }
        }

        private void Update()
        {
            PollSyncKeys();

            if (!_isLogging || bikeTransform == null)
                return;

            float interval = 1f / Mathf.Max(1, logHz);
            if (Time.unscaledTime >= _nextLogUnscaled)
            {
                WriteSampleRow();
                _nextLogUnscaled += interval;
                if (_nextLogUnscaled < Time.unscaledTime)
                    _nextLogUnscaled = Time.unscaledTime;
            }

            if (Time.unscaledTime >= _nextFlushUnscaled)
            {
                _writer?.Flush();
                _nextFlushUnscaled = Time.unscaledTime + 1f;
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

            CaptureConditionInfo();

            BindReferencePathForScenario();

            if (IsRoute2(_scenarioName))
                ExperimentSceneRefs.ResetRoute2SegmentTriggers();

            if (_cyclist != null)
                maxBikeSpeedKph = Mathf.Max(maxBikeSpeedKph, _cyclist.MaxSpeedMps * 3.6f);

            string safeParticipant = Safe(participantId);
            string safeScenario = Safe(_scenarioName);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _runId = $"{safeParticipant}_{safeScenario}_{timestamp}";

            string baseLogs = Path.Combine(Application.persistentDataPath, "Logs");
            string directory = useParticipantFolder
                ? Path.Combine(baseLogs, safeParticipant)
                : baseLogs;
            Directory.CreateDirectory(directory);

            _runFilePath = Path.Combine(directory, $"{_runId}.csv");
            _writer = new StreamWriter(_runFilePath, false);
            _writer.AutoFlush = false;

            _runStartUtc = DateTimeOffset.UtcNow;
            _nextLogUnscaled = Time.unscaledTime;
            _nextFlushUnscaled = Time.unscaledTime + 1f;
            _lastBikePos = bikeTransform.position;
            _lastSampleUtc = _runStartUtc;
            _smoothedSpeedKph = 0f;
            _lastVelocityWorld = Vector3.zero;
            _lastAccelWorld = Vector3.zero;
            _lastHeadingDeg = bikeTransform.eulerAngles.y;
            _lastYawRateDegS = 0f;
            _lastMovementDirDeg = float.NaN;
            _steeringZeroOffset = GetRawSteeringAngleDeg();
            if (float.IsNaN(_steeringZeroOffset))
                _steeringZeroOffset = 0f;
            _lastSteeringAngleDeg = 0f;
            _lastSteeringRateDegS = float.NaN;

            WriteMetadataBlock();
            _writer.WriteLine(
                "run_id,participant_id,scenario_name,condition_id,traffic_enabled,segment_id,task_context,t,timestamp_utc,unix_time_ms,event,event_phase,x,y,z,speed_kph,vel_x,vel_z,accel_x,accel_z,heading_deg,movement_dir_deg,yaw_rate_deg_s,steering_angle_deg,steering_rate_deg_s,brake_left,brake_right,brake_active,deviation_from_path,event_vehicle_x,event_vehicle_z,event_vehicle_speed_kph,fps,frame_time_ms,technical_issue_flag");
            _writer.Flush();

            _isLogging = true;
            WriteEventRow("RUN_START");
            WriteEventRow("START");
            _writer.Flush();
            Debug.Log($"[ExperimentRunLogger] Logging started: {_runFilePath}");
        }

        public void StopLogging()
        {
            if (!_isLogging)
                return;

            WriteEventRow("FINISH");
            WriteEventRow("RUN_END");
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

            WriteEventRow(markerName);
            _writer.Flush();
        }

        private void PollSyncKeys()
        {
            if (Input.GetKeyDown(syncPreKey))
                TryWriteSyncMarker("SYNC_PRE");
            if (Input.GetKeyDown(syncPostKey))
                TryWriteSyncMarker("SYNC_POST");
        }

        private void TryWriteSyncMarker(string markerName)
        {
            if (!_isLogging)
            {
                Debug.LogWarning($"[ExperimentRunLogger] {markerName} ignored: logging is not running.");
                return;
            }

            long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (EventMarkerLogger.Instance != null)
                EventMarkerLogger.Instance.LogEvent(markerName);
            else
                MarkEvent(markerName);

            Debug.Log($"[ExperimentRunLogger] {markerName} unix_time_ms={unixMs}");
        }

        private void BindReferencePathForScenario()
        {
            var refs = ExperimentSceneRefs.Instance;
            if (refs == null)
                return;

            referencePathTracker = IsRoute2(_scenarioName)
                ? refs.route2PathTracker
                : refs.route1PathTracker;

            if (referencePathTracker != null && bikeTransform != null)
                referencePathTracker.bikeTransform = bikeTransform;
        }

        private void CaptureConditionInfo()
        {
            var refs = ExperimentSceneRefs.Instance;

            if (ExperimentBuildSession.IsActive)
            {
                _trafficEnabled = ExperimentBuildSession.TrafficEnabled ? 1 : 0;
            }
            else if (refs != null && refs.cityTraffic != null)
            {
                _trafficEnabled = refs.cityTraffic.IsTrafficEnabled ? 1 : 0;
            }
            else
            {
                _trafficEnabled = -1;
            }

            if (IsRoute2(_scenarioName))
            {
                _conditionId = _trafficEnabled == 1 ? "S2_T"
                             : _trafficEnabled == 0 ? "S2_NT"
                             : "S2_UNKNOWN";
            }
            else if (!string.IsNullOrEmpty(_scenarioName) &&
                     _scenarioName.IndexOf("Route1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _conditionId = _trafficEnabled == 1 ? "S1_T"
                             : _trafficEnabled == 0 ? "S1_NT"
                             : "S1_UNKNOWN";
            }
            else
            {
                _conditionId = "OTHER";
            }
        }

        private static bool IsRoute2(string scenarioName)
        {
            return !string.IsNullOrEmpty(scenarioName) &&
                   scenarioName.IndexOf("Route2", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void WriteSampleRow()
        {
            WriteRow(CurrentEventName(), updateKinematics: true);
        }

        private void WriteEventRow(string eventValue)
        {
            WriteRow(eventValue, updateKinematics: false);
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

        private void WriteRow(string eventValue, bool updateKinematics)
        {
            if (_writer == null || bikeTransform == null)
                return;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            float t = (float)(now - _runStartUtc).TotalSeconds;
            string timestampUtc = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            long unixMs = now.ToUnixTimeMilliseconds();

            Vector3 p = bikeTransform.position;
            float headingDeg = bikeTransform.eulerAngles.y;
            float steeringAngleDeg = GetSteeringAngleDeg();

            Vector3 velocityWorld = _lastVelocityWorld;
            Vector3 accelWorld = _lastAccelWorld;
            float movementDirDeg = _lastMovementDirDeg;
            float yawRateDegS = _lastYawRateDegS;
            float steeringRateDegS = _lastSteeringRateDegS;

            if (updateKinematics)
            {
                float dt = (float)(now - _lastSampleUtc).TotalSeconds;
                if (dt <= 0.0001f)
                    dt = 0.0001f;

                Vector3 delta = p - _lastBikePos;
                velocityWorld = delta / dt;
                accelWorld = (velocityWorld - _lastVelocityWorld) / dt;

                float rawSpeedKph = (delta.magnitude / dt) * 3.6f;
                if (t < ignoreFirstSeconds)
                    rawSpeedKph = 0f;

                rawSpeedKph = Mathf.Min(rawSpeedKph, maxBikeSpeedKph);
                _smoothedSpeedKph = Mathf.Lerp(_smoothedSpeedKph, rawSpeedKph, speedSmoothing);

                yawRateDegS = Mathf.DeltaAngle(_lastHeadingDeg, headingDeg) / dt;

                movementDirDeg = float.NaN;
                Vector3 flatVel = new Vector3(velocityWorld.x, 0f, velocityWorld.z);
                if (flatVel.sqrMagnitude > 0.0001f)
                    movementDirDeg = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;

                steeringRateDegS = float.NaN;
                if (!float.IsNaN(steeringAngleDeg))
                    steeringRateDegS = (steeringAngleDeg - _lastSteeringAngleDeg) / dt;

                _lastBikePos = p;
                _lastSampleUtc = now;
                _lastVelocityWorld = velocityWorld;
                _lastAccelWorld = accelWorld;
                _lastHeadingDeg = headingDeg;
                _lastYawRateDegS = yawRateDegS;
                _lastMovementDirDeg = movementDirDeg;
                if (!float.IsNaN(steeringAngleDeg))
                    _lastSteeringAngleDeg = steeringAngleDeg;
                _lastSteeringRateDegS = steeringRateDegS;
            }

            float deviation = float.NaN;
            if (referencePathTracker != null)
                deviation = referencePathTracker.currentDeviation;

            GetBrakeInputs(out float brakeLeft, out float brakeRight, out int brakeActive);

            float fps = Time.unscaledDeltaTime > 0.0001f ? 1f / Time.unscaledDeltaTime : 0f;
            float frameMs = Time.unscaledDeltaTime * 1000f;
            int issue = (fps > 0f && fps < technicalIssueFps) || p.y < implausibleMinY || p.y > implausibleMaxY
                ? 1
                : 0;

            _writer.WriteLine(string.Join(",",
                Safe(_runId),
                Safe(participantId),
                Safe(_scenarioName),
                Safe(_conditionId),
                _trafficEnabled.ToString(CultureInfo.InvariantCulture),
                Safe(_segmentId),
                Safe(_taskContext),
                F(t),
                timestampUtc,
                unixMs.ToString(CultureInfo.InvariantCulture),
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
                F(brakeLeft),
                F(brakeRight),
                brakeActive.ToString(CultureInfo.InvariantCulture),
                FV(deviation),
                FV(_eventVehicleX),
                FV(_eventVehicleZ),
                FV(_eventVehicleSpeedKph),
                F(fps),
                F(frameMs),
                issue.ToString(CultureInfo.InvariantCulture)
            ));
        }

        private void GetBrakeInputs(out float left, out float right, out int active)
        {
            left = 0f;
            right = 0f;
            active = 0;
            if (_cyclist == null)
                return;

            left = _cyclist.GetLeftBrake();
            right = _cyclist.GetRightBrake();
            active = _cyclist.IsBrakeActive() ? 1 : 0;
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
            _writer.WriteLine($"# Condition ID: {_conditionId}");
            _writer.WriteLine($"# Traffic Enabled: {_trafficEnabled}");
            _writer.WriteLine($"# Initial Segment ID: {_segmentId}");
            _writer.WriteLine($"# Initial Task Context: {_taskContext}");
            _writer.WriteLine("# Initial Event Phase: NONE");
            _writer.WriteLine("# Logging Enabled: True");
            _writer.WriteLine($"# Path Deviation Enabled: {referencePathTracker != null}");
            _writer.WriteLine("# Event Vehicle Enabled: True");
            _writer.WriteLine($"# Log Frequency (Hz): {logHz}");
            _writer.WriteLine($"# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _writer.WriteLine($"# Simulation Start Time MS: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _writer.WriteLine($"# Simulation Start UTC: {_runStartUtc.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ}");
            _writer.WriteLine($"# Simulation Start Unix MS: {_runStartUtc.ToUnixTimeMilliseconds()}");
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
