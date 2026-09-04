using System;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private const int ConfigSyncRetryIntervalTicks = 5 * 60;
        private const int ConfigSyncPollIntervalTicks = 30 * 60;
        private static int _configSyncCountdown;
        private static bool _configSyncRetrying;
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
            _configSyncCountdown = 0;
            _configSyncRetrying = false;
            _lastReportedConfigErrorRevision = long.MinValue;
            _lastReportedConfigError = string.Empty;
        }

        internal static long AdvanceConfigRevision()
        {
            return ++ConfigRevision;
        }

        internal static void BeginConfigSync()
        {
            if (!IsClient || IsServer || !MpActive) return;
            ConfigSyncReady = false;
            _configSyncRetrying = true;
            RequestConfigFromServer();
        }

        private static void RunConfigSyncTick()
        {
            if (!IsClient || IsServer || !MpActive || Networking == null) return;
            if (_configSyncCountdown > 0)
            {
                _configSyncCountdown--;
                return;
            }
            RequestConfigFromServer();
        }

        private static void RequestConfigFromServer()
        {
            var fingerprint = Config?.ContentFingerprint ?? string.Empty;
            var sent = Networking != null && Networking.SendToServer(
                new PacketRequestConfig(AppliedConfigRevision, fingerprint), true);
            if (!sent) _configSyncRetrying = true;
            _configSyncCountdown = _configSyncRetrying
                ? ConfigSyncRetryIntervalTicks
                : ConfigSyncPollIntervalTicks;
        }

        internal static void CompleteConfigSync(long revision)
        {
            AppliedConfigRevision = revision;
            ConfigSyncReady = true;
            _configSyncRetrying = false;
            _configSyncCountdown = ConfigSyncPollIntervalTicks;
            _lastReportedConfigErrorRevision = long.MinValue;
            _lastReportedConfigError = string.Empty;
        }

        internal static void RejectConfigSync(long revision, string error, bool retry)
        {
            error = string.IsNullOrWhiteSpace(error) ? "Configuration synchronization failed." : error;
            ConfigSyncReady = false;
            ModAPI.MarkConfigUnavailable(error);
            _configSyncRetrying = retry;
            _configSyncCountdown = retry ? ConfigSyncRetryIntervalTicks : ConfigSyncPollIntervalTicks;

            if (_lastReportedConfigErrorRevision == revision &&
                string.Equals(_lastReportedConfigError, error, StringComparison.Ordinal)) return;
            _lastReportedConfigErrorRevision = revision;
            _lastReportedConfigError = error;
            Utils.ShowChatMessage(error, "ShipCores: Config Sync:");
        }

        internal static void SendConfigAck(long revision, bool applied, string error = null)
        {
            if (!IsClient || IsServer || Networking == null) return;
            Networking.SendToServer(new PacketConfigAck(revision, applied, error), true);
        }
    }
}
