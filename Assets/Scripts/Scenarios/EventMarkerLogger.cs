using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using BikeURP;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Singleton that logs timestamped event markers to a CSV file.
    /// </summary>
    public class EventMarkerLogger : MonoBehaviour
    {
        public static EventMarkerLogger Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField, Tooltip("Reference to the player's transform")]
        private Transform playerTransform;

        [SerializeField, Tooltip("Reference to the player's physics controller")]
        private BicyclePhysicsController bicycleController;

        private StreamWriter _csvWriter;
        private string _filePath;
        private string _baseHeaders = "Timestamp,EventName,PlayerPosX,PlayerPosY,PlayerPosZ,PlayerSpeedKph,PlayerHeading";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeLogFile();
        }

        private void Start()
        {
            Debug.Log($"[EventMarkerLogger] Log file created at: {_filePath}");
        }

        private void InitializeLogFile()
        {
            string dateTimeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _filePath = Path.Combine(Application.persistentDataPath, $"experiment_log_{dateTimeStr}.csv");

            try
            {
                _csvWriter = new StreamWriter(_filePath, false);
                _csvWriter.WriteLine(_baseHeaders);
                _csvWriter.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventMarkerLogger] Failed to initialize log file: {e.Message}");
            }
        }

        /// <summary>
        /// Logs a basic event with a timestamp and player state.
        /// </summary>
        public void LogEvent(string eventName)
        {
            LogEvent(eventName, null);
        }

        /// <summary>
        /// Logs an event with additional extra data columns.
        /// </summary>
        public void LogEvent(string eventName, Dictionary<string, string> extraData)
        {
            if (_csvWriter == null) return;

            string timestamp = DateTime.Now.ToString("O");
            string posX = "0", posY = "0", posZ = "0";
            string speed = "0", heading = "0";

            if (playerTransform != null)
            {
                posX = playerTransform.position.x.ToString("F3", CultureInfo.InvariantCulture);
                posY = playerTransform.position.y.ToString("F3", CultureInfo.InvariantCulture);
                posZ = playerTransform.position.z.ToString("F3", CultureInfo.InvariantCulture);
                heading = playerTransform.eulerAngles.y.ToString("F3", CultureInfo.InvariantCulture);
            }

            if (bicycleController != null)
            {
                speed = bicycleController.GetSpeedKph().ToString("F3", CultureInfo.InvariantCulture);
            }

            string line = $"{timestamp},{eventName},{posX},{posY},{posZ},{speed},{heading}";

            if (extraData != null && extraData.Count > 0)
            {
                string extraColumns = string.Join(",", extraData.Select(kv => $"{kv.Key}={kv.Value}"));
                line += $",{extraColumns}";
            }

            try
            {
                _csvWriter.WriteLine(line);
                _csvWriter.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventMarkerLogger] Failed to write event {eventName}: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            CloseFile();
        }

        private void OnApplicationQuit()
        {
            CloseFile();
        }

        private void CloseFile()
        {
            if (_csvWriter != null)
            {
                _csvWriter.Flush();
                _csvWriter.Close();
                _csvWriter = null;
            }
        }
    }
}
