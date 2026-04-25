using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal static class GridsPerFactionManager
    {
        internal struct FactionChange
        {
            public long FactionId;
            public string CoreType;
            public int Count;
        }

        public static readonly Dictionary<long, Dictionary<string, int>> PerFaction = new Dictionary<long, Dictionary<string, int>>();
        private static bool _suppressEvents;

        private static ModConfig Config => Session.Config;

        internal static int GetFactionPlayerCount(IMyFaction owningFaction, long ownerId)
        {
            if (owningFaction != null)
                return owningFaction.Members.Count(member => ShouldCountTowardsPlayerLimits(member.Key));

            return ShouldCountTowardsPlayerLimits(ownerId) ? 1 : 0;
        }

        private static bool ShouldCountTowardsPlayerLimits(long identityId)
        {
            if (identityId <= 0)
                return false;

            if (Config.DebugMode)
                return true;

            return MyAPIGateway.Players != null && MyAPIGateway.Players.TryGetSteamId(identityId) != 0;
        }

        internal static bool HasFactionCoreLimit(ShipCore core)
        {
            return core != null && (core.MaxPerFaction >= 0 || core.FactionPlayersNeededPerCore > 0);
        }

        internal static int GetPlayerScaledFactionCoreLimit(ShipCore core, int playerCount)
        {
            if (core == null || core.FactionPlayersNeededPerCore <= 0)
                return -1;

            if (playerCount <= 0)
                return 0;

            return playerCount / core.FactionPlayersNeededPerCore;
        }

        internal static int GetEffectiveFactionCoreLimit(ShipCore core, int playerCount)
        {
            if (core == null)
                return -1;

            var fixedLimit = core.MaxPerFaction;
            var playerScaledLimit = GetPlayerScaledFactionCoreLimit(core, playerCount);

            if (playerScaledLimit < 0)
                return fixedLimit;

            if (fixedLimit < 0)
                return playerScaledLimit;

            return Math.Min(fixedLimit, playerScaledLimit);
        }

        internal static bool IsGroupWithinFactionLimits(IMyFaction owningFaction, long ownerId, string coreType)
        {
            var factionId = owningFaction?.FactionId ?? -1;
            if (!Config.IsValidCoreType(coreType))
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: Unknown core type id {coreType}", 3);
                return false;
            }

            var core = Config.GetShipCoreByTypeId(coreType);
            var minNeededPlayers = core.MinPlayers;
            var maxAllowedPlayers = core.MaxPlayers;
            var requiresFaction = core.FactionPlayersNeededPerCore > 0;

            if (factionId == -1 && (minNeededPlayers > 1 || requiresFaction))
            {
                Utils.ShowChatMessage($"Player is not in Faction [OwningPlayer:{ownerId}] and therefore cannot build faction limited core: {coreType}", playerEntityId: ownerId);
                return false;
            }

            var playerCount = GetFactionPlayerCount(owningFaction, ownerId);
            var maxAllowedGrids = GetEffectiveFactionCoreLimit(core, playerCount);
            var playerScaledLimit = GetPlayerScaledFactionCoreLimit(core, playerCount);

            if (playerCount < minNeededPlayers)
            {
                Utils.ShowChatMessage($"{playerCount}/{minNeededPlayers} players needed to build: {coreType}", playerEntityId: ownerId);
                return false;
            }

            if (maxAllowedPlayers > 0 && playerCount > maxAllowedPlayers)
            {
                Utils.ShowChatMessage(
                    $"{playerCount}/{maxAllowedPlayers} players exceeds the allowed faction size for: {coreType}", playerEntityId: ownerId);
                return false;
            }

            if (maxAllowedGrids < 0) return true;
            if (!PerFaction.ContainsKey(factionId) || !PerFaction[factionId].ContainsKey(coreType)) return true;
            var currentCount = PerFaction[factionId][coreType];
            if (currentCount <= maxAllowedGrids) return true;
            if (playerScaledLimit == 0)
            {
                Utils.ShowChatMessage($"{playerCount}/{core.FactionPlayersNeededPerCore} faction players needed per {coreType}.", playerEntityId: ownerId);
                return false;
            }

            var message = $"Faction limit reached, you already have {currentCount - 1}/{maxAllowedGrids} {coreType} built!";
            if (playerScaledLimit >= 0)
                message += $" Player-scaled cap: {playerCount} players -> {playerScaledLimit} allowed (1 per {core.FactionPlayersNeededPerCore} players).";

            Utils.ShowChatMessage(message, playerEntityId: ownerId);
            return false;
        }

        internal static void AddGridGroup(IMyFaction owningFaction, string coreType)
        {
            Utils.Log($"GridsPerFactionClass::AddCubeGrid: Adding grid for faction {owningFaction?.FactionId} with core type {coreType}", 1);
            if (owningFaction == null) return;

            Dictionary<string, int> perGroup;
            if (!PerFaction.TryGetValue(owningFaction.FactionId, out perGroup))
            {
                perGroup = GetDefaultFactionGridsSet();
                PerFaction[owningFaction.FactionId] = perGroup;
            }

            if (!perGroup.ContainsKey(coreType))
            {
                Utils.Log($"GridsPerFactionClass::AddCubeGrid: Missing entry for core type {coreType} in faction {owningFaction.FactionId}", 1);
                perGroup[coreType] = 0;
            }

            perGroup[coreType]++;
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = owningFaction.FactionId, CoreType = coreType, Count = perGroup[coreType] });
        }

        internal static void RemoveGridGroup(IMyFaction owningFaction, string coreType)
        {
            if (owningFaction == null) return;

            RemoveGridGroup(owningFaction.FactionId, coreType);
        }

        internal static void RemoveGridGroup(long factionId, string coreType)
        {
            if (factionId <= 0) return;

            Dictionary<string, int> perGroup;
            if (!PerFaction.TryGetValue(factionId, out perGroup)) return;

            int value;
            if (!perGroup.TryGetValue(coreType, out value)) return;
            if (value <= 0) return;

            perGroup[coreType]--;
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = coreType, Count = perGroup[coreType] });
        }

        internal static void RemoveFaction(long factionId)
        {
            if (factionId <= 0) return;

            Dictionary<string, int> perGroup;
            if (!PerFaction.TryGetValue(factionId, out perGroup)) return;

            foreach (var coreType in perGroup.Keys.ToList())
            {
                var value = perGroup[coreType];
                if (value <= 0) continue;

                perGroup[coreType] = 0;
                if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = coreType, Count = 0 });
            }
        }

        internal static void Reset()
        {
            foreach (var factionEntry in PerFaction.Values)
            {
                foreach (var key in factionEntry.Keys.ToList())
                {
                    factionEntry[key] = 0;
                }
            }
        }

        internal static void BeginExternalUpdate() { _suppressEvents = true; }
        internal static void EndExternalUpdate() { _suppressEvents = false; }

        private static Dictionary<string, int> GetDefaultFactionGridsSet()
        {
            var set = new Dictionary<string, int>();
            foreach (var core in Config.ShipCores) set[core.SubtypeId] = 0;
            return set;
        }
    }
}
