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
        private readonly object _gridDictionaryLock = new object();

        internal ShipCore ShipCore => Session.Config.GetShipCoreByTypeId(MainCoreComponent?.SubtypeId ?? string.Empty);
        internal GridModifiers Modifiers => CubeGridModifiers.GetActiveModifiers(this);
        internal SpeedModifiers SpeedModifiers => CubeGridModifiers.GetActiveSpeedModifiers(this);

        internal IMyFaction OwningFaction => MyAPIGateway.Session.Factions.TryGetPlayerFaction(OwnerId);
        internal int GroupBlocksCount => GetGridEntriesCopy().Sum(g => g.Key.BlocksCount);
        internal int GroupPCU => GetGridEntriesCopy().Sum(g => g.Key.BlocksPCU);
        internal float GroupMass {
            get
            {
                float dryMass = 0;
                float wetMass = 0;
                GetGridsCopy().FirstOrDefault()?.GetCurrentMass(out dryMass, out wetMass, GridLinkTypeEnum.Mechanical);
                return Session.Config.MassTypeMode == MassTypeMode.Dry ? dryMass : wetMass;
            }
        }
        private float BoostDuration => SpeedModifiers.BoostDuration;
        private float BoostCoolDown => SpeedModifiers.BoostCoolDown;

        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;

        internal readonly Dictionary<BlockLimit, LimitBucket> Limits = new Dictionary<BlockLimit, LimitBucket>();

        internal readonly Dictionary<MyCubeGrid, GridComponent> GridDictionary =
            new Dictionary<MyCubeGrid, GridComponent>();

        internal Dictionary<IMyCubeBlock, CoreComponent> CoreDictionary =>
            Utils.Flatten(GetGridComponentsCopy(), component => component.CoreDictionary);

        internal bool TryAddGridComponent(MyCubeGrid grid, GridComponent gridComponent)
        {
            lock (_gridDictionaryLock)
            {
                if (GridDictionary.ContainsKey(grid)) return false;
                GridDictionary.Add(grid, gridComponent);
                return true;
            }
        }

        internal bool TryGetGridComponent(MyCubeGrid grid, out GridComponent component)
        {
            lock (_gridDictionaryLock)
            {
                return GridDictionary.TryGetValue(grid, out component);
            }
        }

        internal bool RemoveGridComponent(MyCubeGrid grid)
        {
            lock (_gridDictionaryLock)
            {
                return GridDictionary.Remove(grid);
            }
        }

        internal int GridCount
        {
            get
            {
                lock (_gridDictionaryLock)
                {
                    return GridDictionary.Count;
                }
            }
        }

        internal void ClearGridDictionary()
        {
            lock (_gridDictionaryLock)
            {
                GridDictionary.Clear();
            }
        }

        internal List<KeyValuePair<MyCubeGrid, GridComponent>> GetGridEntriesCopy()
        {
            lock (_gridDictionaryLock)
            {
                return new List<KeyValuePair<MyCubeGrid, GridComponent>>(GridDictionary);
            }
        }

        internal List<MyCubeGrid> GetGridsCopy()
        {
            lock (_gridDictionaryLock)
            {
                return new List<MyCubeGrid>(GridDictionary.Keys);
            }
        }

        internal List<GridComponent> GetGridComponentsCopy()
        {
            lock (_gridDictionaryLock)
            {
                return new List<GridComponent>(GridDictionary.Values);
            }
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
