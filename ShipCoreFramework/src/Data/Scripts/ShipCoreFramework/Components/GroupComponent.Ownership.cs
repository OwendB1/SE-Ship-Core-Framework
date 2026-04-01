using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class GroupComponent
    {
        internal long OwnerId
        {
            get
            {
                long ownerId;
                if (MainCoreComponent != null)
                {
                    ownerId = MainCoreComponent.CoreBlock.OwnerId;
                    ownerId = ownerId == 0 ? MainCoreComponent.CoreBlock.SlimBlock.BuiltBy : ownerId;
                }
                else
                {
                    ownerId = this.GetMajorityOwnerId();
                }

                if (_lastOwnerId != 0 && ownerId != 0 && _lastOwnerId != ownerId)
                {
                    var relation = MyIDModule.GetRelationPlayerPlayer(_lastOwnerId, ownerId);
                    if (relation != MyRelationsBetweenPlayers.Allies)
                    {
                        if (MainCoreComponent == null)
                        {
                            _lastOwnerId = ownerId;
                        }
                        else
                        {
                            Utils.ShowChatMessage($"Not changing ownership to {ownerId} because it's not an ally!");
                            var cube = MainCoreComponent?.CoreBlock as MyCubeBlock;
                            cube?.ChangeOwner(_lastOwnerId, MyOwnershipShareModeEnum.Faction);
                        }
                        return _lastOwnerId;
                    }

                    Utils.Log($"OwnerId: Changed from {_lastOwnerId} to {ownerId}", 2);
                    var newOwningFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
                    var oldOwningFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(_lastOwnerId);

                    var coreType = ShipCore.SubtypeId;
                    GridsPerFactionManager.RemoveGridGroup(oldOwningFaction, coreType);
                    GridsPerPlayerManager.RemoveGridGroup(_lastOwnerId, coreType);

                    GridsPerFactionManager.AddGridGroup(newOwningFaction, coreType);
                    GridsPerPlayerManager.AddGridGroup(ownerId, coreType);

                    var isWithinFactionLimits =
                        GridsPerFactionManager.IsGroupWithinFactionLimits(newOwningFaction, ownerId, coreType);
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
                }

                _lastOwnerId = ownerId;
                return ownerId;
            }
        }

        internal bool IsIgnoredGroup()
        {
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
