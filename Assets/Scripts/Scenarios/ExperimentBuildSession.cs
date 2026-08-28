using CyclingExperiment.AI;
using System;
using UnityEngine;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Static run config set by a launcher scene before MainScene loads.
    /// Playing MainScene directly leaves this inactive (editor default: menu + Route 1).
    /// </summary>
    public static class ExperimentBuildSession
    {
        public const int TestRunRouteIndex = 3;

        public static bool IsActive { get; private set; }
        public static int RouteIndex { get; private set; }
        public static bool TrafficEnabled { get; private set; }
        public static bool LockParticipantUi { get; private set; }
        
        public static string ParticipantId { get; private set; } = "P01";
        public static int TrialIndex { get; private set; } = 1;

        static bool _playTestRun;

        public static bool LocksParticipantUi => IsActive && LockParticipantUi;

        public static bool IsTestRun => _playTestRun || (IsActive && RouteIndex == TestRunRouteIndex);

        /// <summary>
        /// Scripted Route 1 bus / overtaking car and leftover stress spawners.
        /// Off during Test Run, and whenever ambient traffic is off.
        /// </summary>
        public static bool AllowsScriptedVehicles
        {
            get
            {
                if (IsTestRun) return false;

                var traffic = GlobalCityTrafficManager.Instance;
                if (traffic != null)
                    return traffic.IsTrafficEnabled;

                if (IsActive) return TrafficEnabled;
                return true;
            }
        }

        public static string LoadingLabel
        {
            get
            {
                if (RouteIndex == TestRunRouteIndex)
                    return "Loading test run...";

                string scenario = RouteIndex == 2 ? "Scenario 2" : "Scenario 1";
                string traffic = TrafficEnabled ? "with traffic" : "without traffic";
                return "Loading " + scenario + " " + traffic + "...";
            }
        }

        public static int NormalizeRouteIndex(int routeIndex)
        {
            return routeIndex == TestRunRouteIndex ? TestRunRouteIndex : (routeIndex < 2 ? 1 : 2);
        }

        public static void Apply(int routeIndex, bool trafficEnabled, bool lockParticipantUi)
        {
            IsActive = true;
            _playTestRun = false;
            RouteIndex = NormalizeRouteIndex(routeIndex);
            TrafficEnabled = RouteIndex == TestRunRouteIndex ? false : trafficEnabled;
            LockParticipantUi = lockParticipantUi;
        }
        
        public static void SetParticipantTrial(string participantId, int trialIndex)
        {
            ParticipantId = string.IsNullOrWhiteSpace(participantId) ? "P01" : participantId.Trim();
            TrialIndex = trialIndex < 1 ? 1 : trialIndex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadCommandLineSession()
        {
            string[] args = Environment.GetCommandLineArgs();

            string participant = null;
            int trial = 1;
            int route = 0;
            bool traffic = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();

                // SimManager: --participantid
                // Old launcher: --participant
                if ((arg == "--participantid" || arg == "--participant") &&
                    i + 1 < args.Length)
                {
                    participant = args[++i];
                }

                // SimManager: --id
                // Old launcher: --trial
                else if ((arg == "--id" || arg == "--trial") &&
                         i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out trial);
                }

                else if (arg == "--route" && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out route);
                }

                else if (arg == "--traffic" && i + 1 < args.Length)
                {
                    string value = args[++i].ToLowerInvariant();

                    traffic =
                        value == "1" ||
                        value == "true" ||
                        value == "on";
                }
            }

            if (!string.IsNullOrWhiteSpace(participant))
            {
                if (int.TryParse(participant, out int participantNumber))
                {
                    participant = "P" + participantNumber.ToString("00");
                }

                SetParticipantTrial(participant, trial);

                Debug.Log(
                    $"[ExperimentBuildSession] SimManager participant/trial detected: Participant={ParticipantId}, Trial={TrialIndex}"
                );
            }

            if (route == 1 || route == 2 || route == TestRunRouteIndex)
            {
                Apply(route, traffic, true);

                Debug.Log(
                    $"[ExperimentBuildSession] Participant={ParticipantId}, Trial={TrialIndex}, Route={RouteIndex}, Traffic={TrafficEnabled}"
                );
            }
        }

        public static void SetPlayTestRun(bool enabled)
        {
            _playTestRun = enabled;
        }
    }
}


