using System.Collections.Generic;
using System.Threading;
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

        internal int GroupBlocksCount => Interlocked.CompareExchange(ref _groupBlocksCount, 0, 0);
        private float BoostDuration => SpeedModifiers.BoostDuration;
        private float BoostCoolDown => SpeedModifiers.BoostCoolDown;

        private void AddGroupBlocksCount(int delta)
        {
            int current;
            int updated;
            do
            {
                current = Interlocked.CompareExchange(ref _groupBlocksCount, 0, 0);
                updated = current + delta;
                if (updated < 0) updated = 0;
            }
            while (Interlocked.CompareExchange(ref _groupBlocksCount, updated, current) != current);
        }

        internal void ObserveClientBlockCount(int delta)
        {
            if (Session.IsServer) return;
            if (_runtimeStateReceived) return;
            AddGroupBlocksCount(delta);
            InvalidateGameThreadStateCache(true);
        }

        internal bool PunishModifiers;
        internal bool PunishSpeed;
        internal bool PunishLimitedBlocks;
        internal bool BoostEnabled;
        internal bool Deactivated;

        internal bool FrictionEnforcementEnabled = true;
        internal float BaseSpeedLimitMetersPerSecond = 100f;
        internal float EffectiveSpeedLimitMetersPerSecond = 100f;
        internal bool EffectiveBoostEnabled;
        internal long SpeedSourceGroupGridId;
        internal IMyGridGroupData SpeedClusterPhysicalGroup;
        internal bool IsSpeedClusterRepresentative;

        internal bool PostBoostRampActive;
        internal float PostBoostRampCap = -1f;

        private readonly object _connectedGroupsLock = new object();
        private bool _connectorNetworkRefreshQueued;
        private readonly object _abilityStateLock = new object();
        internal readonly object SpeedStateLock = new object();
        private IMyGridGroupData _trackedPhysicalGroup;
        private readonly HashSet<IMyGridGroupData> _connectedNoCoreGroups = new HashSet<IMyGridGroupData>();
        private readonly HashSet<IMyGridGroupData> _connectedCoreGroups = new HashSet<IMyGridGroupData>();

        internal float FrictionMaximumDecelerationOverride = -1f;
        internal float MinimumFrictionSpeedAbsoluteOverride = -1f;
        internal float MaximumFrictionSpeedAbsoluteOverride = -1f;
        internal float MinimumFrictionSpeedModifierOverride = -1f;
        internal float MaximumFrictionSpeedModifierOverride = -1f;

        private long _lastOwnerId;
        private bool _runtimeStateReceived;
        private long _runtimeOwnerId;
        private long _runtimeMainCoreBlockId;
        private int _runtimeCoreCount;
        private string _runtimeCoreSubtypeId = string.Empty;

        internal int CoreCount => !Session.IsServer && _runtimeStateReceived
            ? _runtimeCoreCount
            : CoreDictionary.Count;
        private float _boostCooldownTimer;
        private float _boostDurationTimer;

        private bool _activeDefenseEnabled;
        private float _activeDefenseCooldownTimer;
        private float _activeDefenseDurationTimer;
        private bool _powerOverclockActive;
        private float _powerOverclockCooldownTimer;
        private float _powerOverclockDurationTimer;
        private float _powerOverclockDamageTimer;
        private int _groupBlocksCount;

        private bool _closing;
        private bool _refreshingUpgradeModules;
        private int _gridInitializationDepth;
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

        internal float ActiveDefenseDuration => ShipCore.ActiveDefenseModifiers.Duration;
        internal float ActiveDefenseCoolDown => ShipCore.ActiveDefenseModifiers.Cooldown;
        internal bool IsInitializingGrids => _gridInitializationDepth > 0;

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
