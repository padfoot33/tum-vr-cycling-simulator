namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// Static run config set by a launcher scene before MainScene loads.
    /// Playing MainScene directly leaves this inactive (editor default: menu + Route 1).
    /// </summary>
    public static class ExperimentBuildSession
    {
        public static bool IsActive { get; private set; }
        public static int RouteIndex { get; private set; }
        public static bool TrafficEnabled { get; private set; }
        public static bool LockParticipantUi { get; private set; }

        public static bool LocksParticipantUi => IsActive && LockParticipantUi;

        public static string LoadingLabel
        {
            get
            {
                string scenario = RouteIndex == 2 ? "Scenario 2" : "Scenario 1";
                string traffic = TrafficEnabled ? "with traffic" : "without traffic";
                return "Loading " + scenario + " " + traffic + "...";
            }
        }

        public static void Apply(int routeIndex, bool trafficEnabled, bool lockParticipantUi)
        {
            IsActive = true;
            RouteIndex = routeIndex < 2 ? 1 : 2;
            TrafficEnabled = trafficEnabled;
            LockParticipantUi = lockParticipantUi;
        }
    }
}
