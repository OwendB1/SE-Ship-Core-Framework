using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private const int MissingCoreRescanInitialDelayTicks = 30;
        private const int MissingCoreRescanRetryDelayTicks = 120;
        private const int MissingCoreRescanMaxAttempts = 10;
        private const int TicksPerSecond = 60;
        private const int CoreRecoveryGraceStartDelayTicks = 60;

        private readonly object _connectedGroupsLock = new object();
        private bool _connectorNetworkRefreshQueued;

        private IMyGridGroupData _trackedPhysicalGroup;
        private readonly HashSet<IMyGridGroupData> _connectedNoCoreGroups = new HashSet<IMyGridGroupData>();
        private readonly HashSet<IMyGridGroupData> _connectedCoreGroups = new HashSet<IMyGridGroupData>();

        private bool _refreshingUpgradeModules;

        private bool _ignoredStateInitialized;
        private bool _wasIgnoredGroup;
        private bool _noCoreLimitsRegistered;
        private string _registeredNoCoreLimitSubtypeId = string.Empty;
        private long _registeredNoCoreLimitOwnerId;
        private long _registeredNoCoreLimitFactionId = -1;
        private bool _coreLimitsRegistered;
        private string _registeredCoreLimitSubtypeId = string.Empty;
        private long _registeredCoreLimitOwnerId;
        private long _registeredCoreLimitFactionId = -1;
        private bool _coreRecoveryGraceActive;
        private bool _missingCoreConfirmedAbsent;
        private int _coreRecoveryGraceStartTick;
        private int _coreRecoveryGraceExpireTick;
        private int _nextCoreRecoveryGraceNotificationTick;
        private int _lastCoreRecoveryGraceNotificationSeconds = -1;
        private readonly HashSet<long> _coreRecoveryGraceNotificationRecipients = new HashSet<long>();
        private int _nextMissingCoreRescanTick;
        private int _missingCoreRescanAttempts;
        internal int LastSpeedStateUpdateTick = -1;

        private static int SecondsToTicks(int seconds)
        {
            if (seconds <= 0) return 0;
            if (seconds > int.MaxValue / TicksPerSecond) return int.MaxValue;
            return seconds * TicksPerSecond;
        }

        private static int TicksToCeilingSeconds(int ticks)
        {
            if (ticks <= 0) return 0;
            return (ticks + TicksPerSecond - 1) / TicksPerSecond;
        }
    }
}
