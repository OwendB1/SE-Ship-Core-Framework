using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal static class PerPlayerManager
    {
        internal struct PlayerChange
        {
            internal long PlayerId;
            internal string CoreType;
            internal int Count;
        }

        private static readonly ConcurrentDictionary<CoreCountKey, int> PerPlayerCounts =
            new ConcurrentDictionary<CoreCountKey, int>();

        private static bool _suppressEvents;

        private static ModConfig Config => Session.Config;

        internal static bool IsGroupWithinPlayerLimits(long ownerId, string coreType, bool notify = true)
        {
            if (!Config.IsValidCoreType(coreType))
            {
                if (notify)
                    Utils.ShowChatMessage($"PerPlayerManager::IsGridWithinPlayerLimits: Unknown core type id {coreType}", playerEntityId: ownerId);
                return false;
            }

            var maxAllowedGrids = Config.GetShipCoreByTypeId(coreType).MaxPerPlayer;
            if (maxAllowedGrids < 0) return true;

            var currentCount = GetCurrentCount(ownerId, coreType);
            if (currentCount <= maxAllowedGrids) return true;

            if (notify)
                Utils.ShowChatMessage($"Player limit reached, you already have {currentCount - 1}/{maxAllowedGrids} {coreType} built!", playerEntityId: ownerId);
            return false;
        }

        internal static int GetCurrentCount(long ownerId, string coreType)
        {
            if (ownerId <= 0 || string.IsNullOrWhiteSpace(coreType))
                return 0;

            int localCount;
            PerPlayerCounts.TryGetValue(new CoreCountKey(ownerId, coreType), out localCount);
            return localCount + LimitsNexusSync.GetRemotePlayerCount(ownerId, coreType);
        }

        internal static void AddGridGroup(long ownerId, string coreType)
        {
            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate { AddGridGroup(ownerId, coreType); });
                return;
            }

            Utils.Log($"PerPlayerManager::AddCubeGrid: Adding grid for player {ownerId} with core type {coreType}", 1);
            if (ownerId <= 0 || string.IsNullOrWhiteSpace(coreType)) return;

            var key = new CoreCountKey(ownerId, coreType);
            var count = PerPlayerCounts.AddOrUpdate(key, 1, delegate(CoreCountKey k, int value) { return value + 1; });
            Utils.Log($"PerPlayerManager::AddCubeGrid: Player {ownerId} now has {count} grids with {coreType}", 1);
            if (!_suppressEvents) LimitsNexusSync.BroadcastPlayerChange(new PlayerChange { PlayerId = ownerId, CoreType = coreType, Count = count });
        }

        internal static void RemoveGridGroup(long ownerId, string coreType)
        {
            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate { RemoveGridGroup(ownerId, coreType); });
                return;
            }

            if (ownerId <= 0 || string.IsNullOrWhiteSpace(coreType)) return;

            var key = new CoreCountKey(ownerId, coreType);
            int previous;
            PerPlayerCounts.TryGetValue(key, out previous);
            if (previous <= 0) return;

            var count = PerPlayerCounts.AddOrUpdate(key, 0,
                delegate(CoreCountKey k, int value) { return value <= 0 ? 0 : value - 1; });
            if (!_suppressEvents) LimitsNexusSync.BroadcastPlayerChange(new PlayerChange { PlayerId = ownerId, CoreType = coreType, Count = count });
        }

        internal static void Reset()
        {
            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(Reset);
                return;
            }

            PerPlayerCounts.Clear();
        }

        internal static CoreCountEntry[] GetLocalCountsSnapshot()
        {
            var snapshot = PerPlayerCounts.ToArray();
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

        internal static Dictionary<string, int> GetPlayerCountsSnapshot(long ownerId)
        {
            var result = new Dictionary<string, int>();
            foreach (var entry in GetLocalCountsSnapshot())
            {
                if (entry.OwnerId != ownerId) continue;
                result[entry.CoreType] = entry.Count;
            }

            return result;
        }

        internal static void BeginExternalUpdate() { _suppressEvents = true; }
        internal static void EndExternalUpdate() { _suppressEvents = false; }
    }
}
