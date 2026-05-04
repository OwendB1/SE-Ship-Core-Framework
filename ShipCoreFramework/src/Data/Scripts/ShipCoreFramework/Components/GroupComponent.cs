using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using MyCubeGrid = Sandbox.Game.Entities.MyCubeGrid;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private const int MinimumBlocksRecheckIntervalTicks = 10 * 60 * 60;
        private const int ExternalLimitValidationDelayTicks = 2 * 60;

        internal ShipCore ShipCore => Session.Config.GetShipCoreByTypeId(MainCoreComponent?.SubtypeId ?? string.Empty);
        internal GridModifiers Modifiers => CubeGridModifiers.GetActiveModifiers(this);
        internal SpeedModifiers SpeedModifiers => CubeGridModifiers.GetActiveSpeedModifiers(this);

        internal IMyFaction OwningFaction => MyAPIGateway.Session.Factions.TryGetPlayerFaction(OwnerId);
        internal int GroupBlocksCount => GridDictionary.Sum(g => g.Key.BlocksCount);
        internal int GroupPCU => GridDictionary.Sum(g => g.Key.BlocksPCU);
        internal float GroupMass {
            get
            {
                var referenceGrid = GridDictionary.Keys.FirstOrDefault(grid =>
                    grid != null &&
                    !grid.MarkedForClose &&
                    !grid.Closed &&
                    grid.Physics != null);
                if (referenceGrid == null) return 0f;

                float dryMass = 0;
                float wetMass = 0;
                try
                {
                    referenceGrid.GetCurrentMass(out dryMass, out wetMass, GridLinkTypeEnum.Mechanical);
                }
                catch (NullReferenceException)
                {
                    return 0f;
                }

                return Session.Config.MassTypeMode == MassTypeMode.Dry ? dryMass : wetMass;
            }
        }
        private float BoostDuration => SpeedModifiers.BoostDuration;
        private float BoostCoolDown => SpeedModifiers.BoostCoolDown;

        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;

        internal readonly Dictionary<BlockLimit, LimitBucket> Limits = new Dictionary<BlockLimit, LimitBucket>();
        internal readonly Dictionary<MyCubeGrid, GridComponent> GridDictionary = new Dictionary<MyCubeGrid, GridComponent>();

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

        internal bool PunishModifiers;
        internal bool PunishSpeed;
        internal bool BoostEnabled;
        internal bool Deactivated;

        internal bool FrictionEnforcementEnabled = true;

        internal bool PostBoostRampActive;
        internal float PostBoostRampCap = -1f;

        private readonly HashSet<IMyGridGroupData> _connectedNoCoreGroups = new HashSet<IMyGridGroupData>();

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
        private bool _minimumBlocksPunishmentActive;
        private int _nextMinimumBlocksCheckTick;
        private int _pendingExternalLimitValidationTick;

        private bool _closing;
        private bool _refreshingUpgradeModules;
        private int _gridInitializationDepth;
        private bool _ignoredStateInitialized;
        private bool _wasIgnoredGroup;

        internal float ActiveDefenseDuration => ShipCore.ActiveDefenseModifiers.Duration;
        internal float ActiveDefenseCoolDown => ShipCore.ActiveDefenseModifiers.Cooldown;
        internal bool IsInitializingGrids => _gridInitializationDepth > 0;
    }
}
