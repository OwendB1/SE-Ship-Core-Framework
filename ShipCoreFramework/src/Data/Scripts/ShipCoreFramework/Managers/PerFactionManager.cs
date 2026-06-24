using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal static class PerFactionManager
    {
        internal struct FactionChange
        {
            internal long FactionId;
            internal string CoreType;
            internal int Count;
        }

        private static readonly GameThreadWriteDictionary<CoreCountKey, int> PerFactionCounts =
            new GameThreadWriteDictionary<CoreCountKey, int>(null, ThreadWork.CountsCategory, "faction-counts");

        private static bool _suppressEvents;

        private static ModConfig Config => Session.Config;

        internal static int GetFactionMemberCount(IMyFaction owningFaction)
        {
            if (owningFaction == null)
                return 0;

            return owningFaction.Members.Count(member => ShouldCountTowardsPlayerLimits(member.Key));
        }

        internal static int GetFactionPlayerCount(IMyFaction owningFaction, long ownerId)
        {
            if (owningFaction != null)
                return GetFactionMemberCount(owningFaction);

            return ShouldCountTowardsPlayerLimits(ownerId) ? 1 : 0;
        }

        internal static FactionRank GetFactionRank(IMyFaction owningFaction, long ownerId)
        {
            if (owningFaction == null || ownerId <= 0)
                return FactionRank.None;

            foreach (KeyValuePair<long, MyFactionMember> member in owningFaction.Members)
            {
                if (member.Key != ownerId)
                    continue;

                if (member.Value.IsFounder || owningFaction.IsFounder(ownerId))
                    return FactionRank.Founder;

                if (member.Value.IsLeader || owningFaction.IsLeader(ownerId))
                    return FactionRank.Leader;

                return FactionRank.Member;
            }

            return FactionRank.None;
        }

        internal static bool TryGetMinFactionRankViolation(ShipCore core, IMyFaction owningFaction, long ownerId, out string reason)
        {
            reason = string.Empty;
            if (core == null || core.MinFactionRank == FactionRank.None)
                return false;

            FactionRank ownerRank = GetFactionRank(owningFaction, ownerId);
            if (ownerRank >= core.MinFactionRank)
                return false;

            string coreName = string.IsNullOrWhiteSpace(core.UniqueName) ? core.SubtypeId : core.UniqueName;
            reason = "Core " + coreName + " requires faction rank " + core.MinFactionRank + " or higher.";
            if (ownerRank == FactionRank.None)
                reason += " Owner is not a faction member.";

            return true;
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
                Utils.Log($"PerFactionManager::IsGridWithinFactionLimits: Unknown core type id {coreType}", 3);
                return false;
            }

            var core = Config.GetShipCoreByTypeId(coreType);
            var minNeededPlayers = core.MinPlayers;
            var maxAllowedPlayers = core.MaxPlayers;
            var requiresFaction = core.FactionPlayersNeededPerCore > 0;
            var factionMemberCount = GetFactionMemberCount(owningFaction);

            if (factionId == -1 && (minNeededPlayers > 0 || requiresFaction))
            {
                Utils.ShowChatMessage($"Player is not in Faction [OwningPlayer:{ownerId}] and therefore cannot build faction limited core: {coreType}", playerEntityId: ownerId);
                return false;
            }

            var playerCount = GetFactionPlayerCount(owningFaction, ownerId);
            var maxAllowedGrids = GetEffectiveFactionCoreLimit(core, playerCount);
            var playerScaledLimit = GetPlayerScaledFactionCoreLimit(core, playerCount);

            if (factionMemberCount < minNeededPlayers)
            {
                Utils.ShowChatMessage($"{factionMemberCount}/{minNeededPlayers} players needed to build: {coreType}", playerEntityId: ownerId);
                return false;
            }

            if (maxAllowedPlayers > 0 && playerCount > maxAllowedPlayers)
            {
                Utils.ShowChatMessage(
                    $"{playerCount}/{maxAllowedPlayers} players exceeds the allowed faction size for: {coreType}", playerEntityId: ownerId);
                return false;
            }

            if (maxAllowedGrids < 0) return true;

            var currentCount = GetCurrentCount(factionId, coreType);
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

        internal static int GetCurrentCount(long factionId, string coreType)
        {
            if (factionId <= 0 || string.IsNullOrWhiteSpace(coreType))
                return 0;

            var localCount = PerFactionCounts.GetOrDefault(new CoreCountKey(factionId, coreType), 0);
            return localCount + LimitsNexusSync.GetRemoteFactionCount(factionId, coreType);
        }

        internal static void AddGridGroup(IMyFaction owningFaction, string coreType)
        {
            if (owningFaction == null) return;
            AddGridGroup(owningFaction.FactionId, coreType);
        }

        internal static void AddGridGroup(long factionId, string coreType)
        {
            if (!Session.IsGameThread)
            {
                ThreadWork.Enqueue(ThreadWork.CountsCategory, string.Empty,
                    "Add faction core count", delegate { AddGridGroup(factionId, coreType); });
                return;
            }

            Utils.Log($"PerFactionManager::AddCubeGrid: Adding grid for faction {factionId} with core type {coreType}", 1);
            if (factionId <= 0 || string.IsNullOrWhiteSpace(coreType)) return;

            var key = new CoreCountKey(factionId, coreType);
            var count = PerFactionCounts.AddOrUpdate(key, 1, delegate(CoreCountKey k, int value) { return value + 1; });
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = coreType, Count = count });
        }

        internal static void RemoveGridGroup(IMyFaction owningFaction, string coreType)
        {
            if (owningFaction == null) return;
            RemoveGridGroup(owningFaction.FactionId, coreType);
        }

        internal static void RemoveGridGroup(long factionId, string coreType)
        {
            if (!Session.IsGameThread)
            {
                ThreadWork.Enqueue(ThreadWork.CountsCategory, string.Empty,
                    "Remove faction core count", delegate { RemoveGridGroup(factionId, coreType); });
                return;
            }

            if (factionId <= 0 || string.IsNullOrWhiteSpace(coreType)) return;

            var key = new CoreCountKey(factionId, coreType);
            var previous = PerFactionCounts.GetOrDefault(key, 0);
            if (previous <= 0) return;

            var count = PerFactionCounts.AddOrUpdate(key, 0,
                delegate(CoreCountKey k, int value) { return value <= 0 ? 0 : value - 1; });
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = coreType, Count = count });
        }

        internal static void RemoveFaction(long factionId)
        {
            if (!Session.IsGameThread)
            {
                ThreadWork.Enqueue(ThreadWork.CountsCategory, "faction-remove:" + factionId,
                    "Remove faction counts", delegate { RemoveFaction(factionId); });
                return;
            }

            if (factionId <= 0) return;

            foreach (var entry in GetFactionCountsSnapshot(factionId).ToList())
            {
                if (entry.Value <= 0) continue;
                PerFactionCounts.Set(new CoreCountKey(factionId, entry.Key), 0);
                if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = entry.Key, Count = 0 });
            }
        }

        internal static void Reset()
        {
            if (!Session.IsGameThread)
            {
                ThreadWork.Enqueue(ThreadWork.CountsCategory, "faction-reset", "Reset faction core counts", Reset);
                return;
            }

            PerFactionCounts.Clear();
        }

        internal static CoreCountEntry[] GetLocalCountsSnapshot()
        {
            var snapshot = PerFactionCounts.ToArraySnapshot();
            var result = new CoreCountEntry[snapshot.Length];
            for (var i = 0; i < snapshot.Length; i++)
            {
                result[i] = new CoreCountEntry
                {
                    OwnerId = snapshot[i].Key.OwnerId,
                    CoreType = snapshot[i].Key.CoreType,
                    Count = snapshot[i].Value
                };
            }

            return result;
        }

        internal static Dictionary<string, int> GetFactionCountsSnapshot(long factionId)
        {
            var result = new Dictionary<string, int>();
            foreach (var entry in GetLocalCountsSnapshot())
            {
                if (entry.OwnerId != factionId) continue;
                result[entry.CoreType] = entry.Count;
            }

            return result;
        }

        internal static void BeginExternalUpdate() { _suppressEvents = true; }
        internal static void EndExternalUpdate() { _suppressEvents = false; }
    }
}
