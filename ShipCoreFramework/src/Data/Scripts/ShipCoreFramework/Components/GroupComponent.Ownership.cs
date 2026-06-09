using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
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
                    PerFactionManager.RemoveGridGroup(oldOwningFaction, coreType);
                    PerPlayerManager.RemoveGridGroup(_lastOwnerId, coreType);

                    PerFactionManager.AddGridGroup(newOwningFaction, coreType);
                    PerPlayerManager.AddGridGroup(ownerId, coreType);

                    var isWithinFactionLimits = PerFactionManager.IsGroupWithinFactionLimits(newOwningFaction, ownerId, coreType);
                    var isWithinPlayerLimits = PerPlayerManager.IsGroupWithinPlayerLimits(ownerId, coreType);
                    if (!isWithinFactionLimits || !isWithinPlayerLimits)
                    {
                        var cube = MainCoreComponent?.CoreBlock as MyCubeBlock;
                        cube?.ChangeOwner(_lastOwnerId, MyOwnershipShareModeEnum.Faction);

                        PerFactionManager.RemoveGridGroup(newOwningFaction, coreType);
                        PerPlayerManager.RemoveGridGroup(ownerId, coreType);

                        PerFactionManager.AddGridGroup(oldOwningFaction, coreType);
                        PerPlayerManager.AddGridGroup(_lastOwnerId, coreType);

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
            if (IsIgnoredByAiOrFactionTag()) return true;
            if (OwnerId == 0) return true;
            var player = MyAPIGateway.Players.TryGetIdentityId(OwnerId);
            if (player != null && player.PromoteLevel == MyPromoteLevel.Admin &&
                MyAPIGateway.Session.IsUserIgnorePCULimit(player.SteamUserId)) return true;

            return false;
        }

        internal bool IsIgnoredByAiOrFactionTag()
        {
            if (IsIgnoredNpcGroup()) return true;

            var faction = OwningFaction;
            if (faction == null) return false;
            if (faction.IsEveryoneNpc()) return true;
            return Session.Config.IgnoredFactionTags != null &&
                   Session.Config.IgnoredFactionTags.Contains(faction.Tag);
        }

        internal long GetRepresentativeGridId()
        {
            var main = MainCoreComponent?.GridComponent?.Grid;
            var grid = main ?? GridDictionary.Keys.FirstOrDefault();
            return grid?.EntityId ?? 0;
        }
    }
}
