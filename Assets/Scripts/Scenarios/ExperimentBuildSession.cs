using CyclingExperiment.AI;

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

        public static void SetPlayTestRun(bool enabled)
        {
            _playTestRun = enabled;
        }
    }
}
