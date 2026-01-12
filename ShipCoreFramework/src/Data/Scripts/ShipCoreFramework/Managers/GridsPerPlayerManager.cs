using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal static class GridsPerPlayerManager
    {
        internal struct PlayerChange
        {
            internal long PlayerId;
            internal string CoreType;
            internal int Delta;
        }

        public static readonly Dictionary<long, Dictionary<string, int>> PerPlayer = new Dictionary<long, Dictionary<string, int>>();
        private static bool _suppressEvents;

        private static ModConfig Config => Session.Config;

        internal static bool WillGroupBeWithinPlayerLimits(GroupComponent group, string newCoreType)
        {
            if (!IsApplicableGroup(group)) return true;
            var playerId = group.OwnerId;

            if (!Config.IsValidCoreType(newCoreType))
            {
                Utils.ShowChatMessage($"GridsPerPlayerClass::IsGridWithinPlayerLimits: Unknown core type id {newCoreType}");
                return false;
            }

            if (PerPlayer.ContainsKey(playerId) && PerPlayer[playerId].ContainsKey(newCoreType))
            {
                var maxAllowedGrids = Config.GetShipCoreByTypeId(newCoreType).MaxPerPlayer;
                if (maxAllowedGrids < 0) return true;
                var currentCount = PerPlayer[playerId][newCoreType];
                if (currentCount + 1 <= maxAllowedGrids) return true;
                Utils.ShowChatMessage("Per player limit of this core has been hit!");
                return false;
            }

            Utils.Log("GridsPerPlayerClass::IsGridWithinPlayerLimits: Player or class not found in player limits data", 2);
            return true;
        }

        internal static void AddGridGroup(GroupComponent group)
        {
            if (!IsApplicableGroup(group)) return;
            var playerId = group.OwnerId;
            var coreType = group.ShipCore.SubtypeId;

            Dictionary<string, int> perGridClass;
            if (!PerPlayer.TryGetValue(playerId, out perGridClass))
            {
                perGridClass = GetDefaultPlayerGridsSet();
                PerPlayer[playerId] = perGridClass;
            }

            if (!perGridClass.ContainsKey(coreType))
            {
                Utils.Log($"GridsPerPlayerClass::AddCubeGrid: Missing entry for core type {coreType} for player {playerId}", 2);
                perGridClass[coreType] = 0;
            }

            perGridClass[coreType]++;
            Utils.Log($"GridsPerPlayerClass::AddCubeGrid: Player {playerId} now has {perGridClass[coreType]} grids with {coreType}", 1);
            if (!_suppressEvents) LimitsNexusSync.BroadcastPlayerChange(new PlayerChange { PlayerId = playerId, CoreType = coreType, Delta = 1 });
        }

        internal static void RemoveGridGroup(GroupComponent group)
        {
            var playerId = group.OwnerId;
            var coreType = group.ShipCore.SubtypeId;
            
            Dictionary<string, int> perGridClass;
            if (!PerPlayer.TryGetValue(playerId, out perGridClass)) return;
            if (!perGridClass.ContainsKey(coreType)) return;
            if (perGridClass[coreType] <= 0) return;

            perGridClass[coreType]--;
            if (!_suppressEvents) LimitsNexusSync.BroadcastPlayerChange(new PlayerChange { PlayerId = playerId, CoreType = coreType, Delta = -1 });
        }

        internal static void Reset()
        {
            foreach (var classesEntry in PerPlayer.Values)
            {
                foreach (var key in classesEntry.Keys.ToList())
                {
                    classesEntry[key] = 0;
                }
            }
        }

        internal static void BeginExternalUpdate() { _suppressEvents = true; }
        internal static void EndExternalUpdate() { _suppressEvents = false; }

        private static bool IsApplicableGroup(GroupComponent group)
        {
            var player = MyAPIGateway.Players.TryGetIdentityId(group.OwnerId);
            if (player == null)
            {
                Utils.Log("Player is null!!");
                return false;
            }
            if (player.PromoteLevel == MyPromoteLevel.Admin) return false;

            // Check if AI/bot and we're ignoring AI
            if (player.IsBot && Config.IgnoreAiFactions)
                return false;

            // Check if faction tag is in ignored list
            var faction = group.OwningFaction;
            if (faction != null && Config.IgnoredFactionTags != null && Config.IgnoredFactionTags.Contains(faction.Tag))
                return false;

            return true;
        }

        private static Dictionary<string, int> GetDefaultPlayerGridsSet()
        {
            var set = new Dictionary<string, int>();
            foreach (var core in Config.ShipCores) set[core.SubtypeId] = 0;
            return set;
        }
    }
}