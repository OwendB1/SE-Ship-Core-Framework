using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class GroupComponent
    {
        internal bool IsIgnoredNpcGroup()
        {
            if (!Session.Config.IgnoreAiFactions) return false;

            var mainGrid = MainCoreComponent?.CoreBlock?.CubeGrid;
            if (mainGrid != null) return mainGrid.IsNpcSpawnedGrid;

            return GridDictionary.Keys.Any(grid => grid != null && grid.IsNpcSpawnedGrid);
        }

        internal long OwnerId
        {
            get
            {
                long ownerId;
                if (MainCoreComponent != null)
                {
                    ownerId = MainCoreComponent.CoreBlock.OwnerId;
                    ownerId = ownerId == 0 ? MainCoreComponent.CoreBlock.SlimBlock.BuiltBy : ownerId;
                    ownerId = ownerId == 0 ? GetSavedOwnerIdFromMainCore() : ownerId;
                }
                else
                {
                    ownerId = this.GetMajorityOwnerId();
                }

                if (IsIgnoredNpcGroup())
                {
                    _lastOwnerId = 0;
                    return 0;
                }

                if (_lastOwnerId != 0 && ownerId != 0 && _lastOwnerId != ownerId)
                {
                    Utils.Log($"OwnerId: Changed from {_lastOwnerId} to {ownerId}", 2);
                    var newOwningFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
                    var oldOwningFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(_lastOwnerId);

                    var coreType = ShipCore.SubtypeId;
                    GridsPerFactionManager.RemoveGridGroup(oldOwningFaction, coreType);
                    GridsPerPlayerManager.RemoveGridGroup(_lastOwnerId, coreType);

                    GridsPerFactionManager.AddGridGroup(newOwningFaction, coreType);
                    GridsPerPlayerManager.AddGridGroup(ownerId, coreType);

                    var isWithinFactionLimits = GridsPerFactionManager.IsGroupWithinFactionLimits(newOwningFaction, ownerId, coreType);
                    var isWithinPlayerLimits = GridsPerPlayerManager.IsGroupWithinPlayerLimits(ownerId, coreType);
                    if (!isWithinFactionLimits || !isWithinPlayerLimits)
                    {
                        var cube = MainCoreComponent?.CoreBlock as MyCubeBlock;
                        cube?.ChangeOwner(_lastOwnerId, MyOwnershipShareModeEnum.Faction);

                        GridsPerFactionManager.RemoveGridGroup(newOwningFaction, coreType);
                        GridsPerPlayerManager.RemoveGridGroup(ownerId, coreType);

                        GridsPerFactionManager.AddGridGroup(oldOwningFaction, coreType);
                        GridsPerPlayerManager.AddGridGroup(_lastOwnerId, coreType);

                        ownerId = _lastOwnerId;
                    }
                    SaveOwnerIdToMainCore(ownerId);
                }

                _lastOwnerId = ownerId;
                return ownerId;
            }
        }
        
        private long GetSavedOwnerIdFromMainCore()
        {
            var storage = MainCoreComponent?.CoreBlock?.Storage;
            if (storage == null || !storage.ContainsKey(Session.CoreLastOwnerStorageGUID)) return 0;

            long ownerId;
            return long.TryParse(storage[Session.CoreLastOwnerStorageGUID], out ownerId) ? ownerId : 0;
        }

        private void SaveOwnerIdToMainCore(long ownerId)
        {
            var coreBlock = MainCoreComponent?.CoreBlock;
            if (coreBlock == null) return;
            if (coreBlock.Storage == null) coreBlock.Storage = new MyModStorageComponent();
            coreBlock.Storage[Session.CoreLastOwnerStorageGUID] = ownerId.ToString();
        }

        internal bool IsIgnoredGroup()
        {
            if (IsIgnoredNpcGroup()) return true;
            if (OwnerId == 0) return true;
            var player = MyAPIGateway.Players.TryGetIdentityId(OwnerId);
            if (player != null && player.PromoteLevel == MyPromoteLevel.Admin &&
                MyAPIGateway.Session.IsUserIgnorePCULimit(player.SteamUserId)) return true;

            var faction = OwningFaction;
            if (faction == null) return false;
            if (faction.IsEveryoneNpc()) return true;
            return Session.Config.IgnoredFactionTags != null &&
                   Session.Config.IgnoredFactionTags.Contains(faction.Tag);
        }

        private long GetRepresentativeGridId()
        {
            var main = MainCoreComponent?.GridComponent?.Grid;
            var grid = main ?? GridDictionary.Keys.FirstOrDefault();
            return grid?.EntityId ?? 0;
        }
    }
}
