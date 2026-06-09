using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
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
        internal GridModifiers Modifiers => GetCachedActiveGridModifiers();
        internal SpeedModifiers SpeedModifiers => GetCachedActiveSpeedModifiers();

        internal IMyFaction OwningFaction => MyAPIGateway.Session.Factions.TryGetPlayerFaction(OwnerId);
        internal int GroupBlocksCount => Interlocked.CompareExchange(ref _groupBlocksCount, 0, 0);
        internal int GroupPCU {
            get
            {
                if (!Session.IsGameThread)
                    return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);

                RefreshGridStateCache();
                return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);
            }
        }
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

        internal struct CachedGridState
        {
            internal long EntityId;
            internal string CustomName;
            internal Vector3D Position;
            internal long FirstOwnerId;
        }

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
                current = Interlocked.CompareExchange(ref _groupBlocksCount, 0, 0);
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
        private readonly object _abilityStateLock = new object();
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
        private int _cachedGroupPCU;
        private long _cachedRepresentativeGridId;
        private MyCubeGrid[] _cachedMovableGrids = new MyCubeGrid[0];
        private long[] _cachedMechanicalGridIds = new long[0];
        private CachedGridState[] _cachedGridStates = new CachedGridState[0];
        private bool _cachedIsIgnoredGroup;
        private GridModifiers _cachedActiveGridModifiers = new GridModifiers();
        private SpeedModifiers _cachedActiveSpeedModifiers = new SpeedModifiers();
        private GridDefenseModifiers _cachedPassiveDefenseModifiers = new GridDefenseModifiers();
        private GridDefenseModifiers _cachedActiveDefenseModifiers = new GridDefenseModifiers();

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
            RefreshGridStateCache();
            RefreshMassCache();
            RefreshModifierStateCache();
            _cachedIsIgnoredGroup = ComputeIsIgnoredGroup();
        }

        internal long GetCachedRepresentativeGridId()
        {
            return Interlocked.CompareExchange(ref _cachedRepresentativeGridId, 0L, 0L);
        }

        internal long[] GetCachedMechanicalGridIds()
        {
            return _cachedMechanicalGridIds ?? new long[0];
        }

        internal MyCubeGrid[] GetCachedMovableGrids()
        {
            return _cachedMovableGrids ?? new MyCubeGrid[0];
        }

        internal CachedGridState[] GetCachedGridStates()
        {
            return _cachedGridStates ?? new CachedGridState[0];
        }

        internal bool GetCachedIsIgnoredGroup()
        {
            return _cachedIsIgnoredGroup;
        }

        internal GridModifiers GetCachedActiveGridModifiers()
        {
            return _cachedActiveGridModifiers ?? new GridModifiers();
        }

        internal SpeedModifiers GetCachedActiveSpeedModifiers()
        {
            return _cachedActiveSpeedModifiers ?? new SpeedModifiers();
        }

        internal GridDefenseModifiers GetCachedPassiveDefenseModifiers()
        {
            return _cachedPassiveDefenseModifiers ?? new GridDefenseModifiers();
        }

        internal GridDefenseModifiers GetCachedActiveDefenseModifiers()
        {
            return _cachedActiveDefenseModifiers ?? new GridDefenseModifiers();
        }

        internal void RefreshModifierStateCache()
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return;

            _cachedActiveGridModifiers = CubeGridModifiers.GetActiveModifiers(this);
            _cachedActiveSpeedModifiers = CubeGridModifiers.GetActiveSpeedModifiers(this);
            _cachedPassiveDefenseModifiers = ComputePassiveDefenseModifiers();
            _cachedActiveDefenseModifiers = ComputeActiveDefenseModifiers();
        }

        private void RefreshGridStateCache()
        {
            var groupPcu = 0;
            var representativeGridId = 0L;
            var representativeBlocks = -1;
            var movableGrids = new List<MyCubeGrid>();
            var mechanicalGridIds = new List<long>();
            var gridStates = new List<CachedGridState>();

            var mainGrid = MainCoreComponent?.GridComponent?.Grid;
            if (mainGrid != null && !mainGrid.MarkedForClose && !mainGrid.Closed)
                representativeGridId = mainGrid.EntityId;

            foreach (var grid in GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                groupPcu += grid.BlocksPCU;
                mechanicalGridIds.Add(grid.EntityId);
                var apiGrid = (IMyCubeGrid)grid;
                var entity = (ModEntity)grid;
                gridStates.Add(new CachedGridState
                {
                    EntityId = grid.EntityId,
                    CustomName = apiGrid.CustomName ?? string.Empty,
                    Position = entity.GetPosition(),
                    FirstOwnerId = grid.BigOwners == null || grid.BigOwners.Count == 0 ? 0 : grid.BigOwners[0]
                });

                if (!grid.IsStatic)
                    movableGrids.Add(grid);

                if (representativeGridId == 0)
                {
                    var blocks = grid.BlocksCount;
                    if (blocks > representativeBlocks || blocks == representativeBlocks && grid.EntityId < representativeGridId)
                    {
                        representativeBlocks = blocks;
                        representativeGridId = grid.EntityId;
                    }
                }
            }

            Interlocked.Exchange(ref _cachedGroupPCU, groupPcu);
            Interlocked.Exchange(ref _cachedRepresentativeGridId, representativeGridId);
            _cachedMovableGrids = movableGrids.ToArray();
            _cachedMechanicalGridIds = mechanicalGridIds.ToArray();
            _cachedGridStates = gridStates.ToArray();
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
