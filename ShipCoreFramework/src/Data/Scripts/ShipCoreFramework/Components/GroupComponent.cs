using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using ModEntity = VRage.ModAPI.IMyEntity;
using MyCubeGrid = Sandbox.Game.Entities.MyCubeGrid;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private const int LimitedBlockMinimumBlocksRecheckIntervalTicks = 10 * 60 * 60;
        private const int ExternalLimitValidationDelayTicks = 2 * 60;
        private const int PostInitializationLimitValidationDelayTicks = 0;

        internal ShipCore ShipCore => Session.Config.GetShipCoreByTypeId(MainCoreComponent?.SubtypeId ?? string.Empty);
        internal GridModifiers Modifiers => CubeGridModifiers.GetActiveModifiers(this);
        internal SpeedModifiers SpeedModifiers => CubeGridModifiers.GetActiveSpeedModifiers(this);

        internal IMyFaction OwningFaction => MyAPIGateway.Session.Factions.TryGetPlayerFaction(OwnerId);
        internal int GroupBlocksCount => Volatile.Read(ref _groupBlocksCount);
        internal int GroupPCU => GridDictionary.Sum(g => g.Key.BlocksPCU);
        internal float GroupMass {
            get
            {
                if (!Session.IsGameThread)
                    return _cachedConfiguredMass;

                RefreshMassCache();
                return _cachedConfiguredMass;
            }
        }
        internal float GroupDryMass {
            get
            {
                if (!Session.IsGameThread)
                    return _cachedDryMass;

                RefreshMassCache();
                return _cachedDryMass;
            }
        }
        private float BoostDuration => SpeedModifiers.BoostDuration;
        private float BoostCoolDown => SpeedModifiers.BoostCoolDown;

        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;

        internal readonly ConcurrentDictionary<BlockLimit, LimitBucket> Limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridComponent> GridDictionary = new ConcurrentDictionary<MyCubeGrid, GridComponent>();

        internal Dictionary<IMyCubeBlock, CoreComponent> CoreDictionary =>
            Utils.Flatten(GridDictionary.Values, component => component.CoreDictionary);
        
        private bool TryGetGridComponent(MyCubeGrid grid, out GridComponent component)
        {
            return GridDictionary.TryGetValue(grid, out component);
        }

        private int GridCount => GridDictionary.Count;

        private void ClearGridDictionary()
        {
            GridDictionary.Clear();
        }

        private void AddGroupBlocksCount(int delta)
        {
            int current;
            int updated;
            do
            {
                current = Volatile.Read(ref _groupBlocksCount);
                updated = current + delta;
                if (updated < 0) updated = 0;
            }
            while (Interlocked.CompareExchange(ref _groupBlocksCount, updated, current) != current);
        }

        private MyCubeGrid GetMobilityReferenceGrid()
        {
            var mainGrid = MainCoreComponent?.CoreBlock?.CubeGrid as MyCubeGrid;
            if (mainGrid != null) return mainGrid;

            return GridDictionary.Keys
                .Where(grid => grid != null && !grid.MarkedForClose && !grid.Closed)
                .OrderByDescending(grid => grid.BlocksCount)
                .ThenBy(grid => grid.EntityId)
                .FirstOrDefault();
        }

        internal string GetThreadWorkKey()
        {
            return MyGroup != null ? MyGroup.GetHashCode().ToString() : GetRepresentativeGridId().ToString();
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
        internal readonly object SpeedStateLock = new object();
        private IMyGridGroupData _trackedPhysicalGroup;
        private readonly HashSet<IMyGridGroupData> _connectedPhysicalGroups = new HashSet<IMyGridGroupData>();
        private readonly HashSet<IMyGridGroupData> _connectedNoCoreGroups = new HashSet<IMyGridGroupData>();
        private readonly HashSet<IMyGridGroupData> _connectedCoreGroups = new HashSet<IMyGridGroupData>();

        internal float FrictionMaximumDecelerationOverride = -1f;
        internal float MinimumFrictionSpeedAbsoluteOverride = -1f;
        internal float MaximumFrictionSpeedAbsoluteOverride = -1f;
        internal float MinimumFrictionSpeedModifierOverride = -1f;
        internal float MaximumFrictionSpeedModifierOverride = -1f;

        private long _lastOwnerId;
        private float _boostCooldownTimer;
        private float _boostDurationTimer;

        private bool _activeDefenseEnabled;
        private float _activeDefenseCooldownTimer;
        private float _activeDefenseDurationTimer;
        private int _groupBlocksCount;
        private bool _minimumBlocksLimitedBlockGateActive;
        private int _nextMinimumBlocksGateCheckTick;
        private int _pendingExternalLimitValidationTick;
        private int _deferLimitPunishmentUntilTick;
        private int _pendingLimitPunishmentValidationTick;
        private float _cachedDryMass;
        private float _cachedWetMass;
        private float _cachedConfiguredMass;

        private bool _closing;
        private bool _refreshingUpgradeModules;
        private int _gridInitializationDepth;
        private bool _ignoredStateInitialized;
        private bool _wasIgnoredGroup;
        private bool _noCoreLimitsRegistered;
        private string _registeredNoCoreLimitSubtypeId = string.Empty;
        internal int LastSpeedStateUpdateTick = -1;

        internal float ActiveDefenseDuration => ShipCore.ActiveDefenseModifiers.Duration;
        internal float ActiveDefenseCoolDown => ShipCore.ActiveDefenseModifiers.Cooldown;
        internal bool IsInitializingGrids => _gridInitializationDepth > 0;

        internal void RefreshGameThreadStateCache()
        {
            if (_closing || Session.IsShuttingDown) return;
            RefreshMassCache();
        }

        private void RefreshMassCache()
        {
            var referenceGrid = GridDictionary.Keys.FirstOrDefault(grid =>
                grid != null &&
                !grid.MarkedForClose &&
                !grid.Closed &&
                (grid as ModEntity)?.Physics != null);
            if (referenceGrid == null)
            {
                _cachedDryMass = 0f;
                _cachedWetMass = 0f;
                _cachedConfiguredMass = 0f;
                return;
            }

            float dryMass;
            float wetMass;
            try
            {
                referenceGrid.GetCurrentMass(out dryMass, out wetMass, GridLinkTypeEnum.Mechanical);
            }
            catch (NullReferenceException)
            {
                dryMass = 0f;
                wetMass = 0f;
            }

            _cachedDryMass = dryMass;
            _cachedWetMass = wetMass;
            _cachedConfiguredMass = Session.Config.MassTypeMode == MassTypeMode.Dry ? dryMass : wetMass;
        }
    }
}
