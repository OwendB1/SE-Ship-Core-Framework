using System;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private const int ConfigSyncRetryIntervalTicks = 5 * 60;
        private static int _configSyncCountdown;
        private static long _lastReportedConfigErrorRevision = long.MinValue;
        private static string _lastReportedConfigError = string.Empty;

        internal static long ConfigRevision { get; private set; }
        internal static long AppliedConfigRevision { get; private set; } = -1;
        internal static bool ConfigSyncReady { get; private set; }

        internal static void ResetConfigSyncState()
        {
            ConfigRevision = 0;
            AppliedConfigRevision = -1;
            ConfigSyncReady = !IsClient || IsServer || !MpActive;
            _configSyncCountdown = -1;
            _lastReportedConfigErrorRevision = long.MinValue;
            _lastReportedConfigError = string.Empty;
        }

        internal static void BeginConfigSync()
        {
            if (!IsClient || IsServer || !MpActive) return;
            ConfigSyncReady = false;
            RequestConfigFromServer();
        }

        private static void RunConfigSyncTick()
        {
            if (!IsClient || IsServer || !MpActive || Networking == null ||
                ConfigSyncReady || _configSyncCountdown < 0) return;
            if (_configSyncCountdown > 0)
            {
                _configSyncCountdown--;
                return;
            }
            RequestConfigFromServer();
        }

        private static void RequestConfigFromServer()
        {
            Networking?.SendToServer(new PacketRequestConfig(), true);
            _configSyncCountdown = ConfigSyncRetryIntervalTicks;
        }

        internal static void CompleteConfigSync(long revision)
        {
            AppliedConfigRevision = revision;
            ConfigSyncReady = true;
            _configSyncCountdown = -1;
            _lastReportedConfigErrorRevision = long.MinValue;
            _lastReportedConfigError = string.Empty;
        }

        internal static void RejectConfigSync(long revision, string error, bool retry)
        {
            error = string.IsNullOrWhiteSpace(error) ? "Configuration synchronization failed." : error;
            ConfigSyncReady = false;
            ModAPI.MarkConfigUnavailable(error);
            _configSyncCountdown = retry ? ConfigSyncRetryIntervalTicks : -1;

            if (_lastReportedConfigErrorRevision == revision &&
                string.Equals(_lastReportedConfigError, error, StringComparison.Ordinal)) return;
            _lastReportedConfigErrorRevision = revision;
            _lastReportedConfigError = error;
            Utils.ShowChatMessage(error, "ShipCores: Config Sync:");
        }

    }
}
