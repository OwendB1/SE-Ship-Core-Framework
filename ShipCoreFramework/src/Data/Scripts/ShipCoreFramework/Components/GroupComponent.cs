using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using MyCubeGrid = Sandbox.Game.Entities.MyCubeGrid;

namespace ShipCoreFramework
{
    public partial class GroupComponent
    {
        private const int MinimumBlocksRecheckIntervalTicks = 10 * 60 * 60;

        internal ShipCore ShipCore => Session.Config.GetShipCoreByTypeId(MainCoreComponent?.SubtypeId ?? string.Empty);
        internal GridModifiers Modifiers => CubeGridModifiers.GetActiveModifiers(this);
        internal SpeedModifiers SpeedModifiers => CubeGridModifiers.GetActiveSpeedModifiers(this);

        internal IMyFaction OwningFaction => MyAPIGateway.Session.Factions.TryGetPlayerFaction(OwnerId);
        internal int GroupBlocksCount => GridDictionary.Sum(g => g.Key.BlocksCount);
        internal int GroupPCU => GridDictionary.Sum(g => g.Key.BlocksPCU);
        internal float GroupMass {
            get
            {
                float dryMass = 0;
                float wetMass = 0;
                GridDictionary.Keys.FirstOrDefault()?.GetCurrentMass(out dryMass, out wetMass, GridLinkTypeEnum.Mechanical);
                return Session.Config.MassTypeMode == MassTypeMode.Dry ? dryMass : wetMass;
            }
        }
        private float BoostDuration => SpeedModifiers.BoostDuration;
        private float BoostCoolDown => SpeedModifiers.BoostCoolDown;

        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;

        internal readonly Dictionary<BlockLimit, LimitBucket> Limits = new Dictionary<BlockLimit, LimitBucket>();

        internal readonly ConcurrentDictionary<MyCubeGrid, GridComponent> GridDictionary =
            new ConcurrentDictionary<MyCubeGrid, GridComponent>();

        internal Dictionary<IMyCubeBlock, CoreComponent> CoreDictionary =>
            Utils.Flatten(GridDictionary.Values, component => component.CoreDictionary);

        internal bool TryAddGridComponent(MyCubeGrid grid, GridComponent gridComponent)
        {
            return GridDictionary.TryAdd(grid, gridComponent);
        }

        internal bool TryGetGridComponent(MyCubeGrid grid, out GridComponent component)
        {
            return GridDictionary.TryGetValue(grid, out component);
        }

        internal bool RemoveGridComponent(MyCubeGrid grid)
        {
            return GridDictionary.TryRemove(grid, out _);
        }

        internal int GridCount
        {
            get { return GridDictionary.Count; }
        }

        internal void ClearGridDictionary()
        {
            GridDictionary.Clear();
        }

        internal bool PunishModifiers;
        internal bool PunishSpeed;
        internal bool BoostEnabled;

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

        private bool _closing;
        private bool _refreshingUpgradeModules;

        internal float ActiveDefenseDuration => GetActiveDefenseModifiers().Duration;
        internal float ActiveDefenseCoolDown => GetActiveDefenseModifiers().Cooldown;
    }
}
