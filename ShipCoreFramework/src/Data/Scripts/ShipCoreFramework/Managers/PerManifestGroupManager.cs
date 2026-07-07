using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal static class PerManifestGroupManager
    {
        internal struct ManifestGroupChange
        {
            internal string GroupName;
            internal int Count;
        }

        internal struct ManifestGroupCountEntry
        {
            internal string GroupName;
            internal int Count;
        }

        private static readonly ConcurrentDictionary<string, int> PerManifestGroupCounts =
            new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static bool _suppressEvents;

        private static ModConfig Config => Session.Config;

        internal static bool HasManifestGroupLimit(ShipCore core)
        {
            return GetManifestGroups(core).Any();
        }

        internal static int GetCurrentCount(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return 0;

            int localCount;
            PerManifestGroupCounts.TryGetValue(groupName, out localCount);
            return localCount + LimitsNexusSync.GetRemoteManifestGroupCount(groupName);
        }

        internal static bool IsGroupWithinManifestLimits(string coreType, long ownerId)
        {
            if (!Config.IsValidCoreType(coreType))
            {
                Utils.ShowChatMessage($"PerManifestGroupManager::IsGroupWithinManifestLimits: Unknown core type id {coreType}", playerEntityId: ownerId);
                return false;
            }

            var core = Config.GetShipCoreByTypeId(coreType);
            if (!HasManifestGroupLimit(core))
                return true;

            foreach (var group in GetManifestGroups(core))
            {
                var currentCount = GetCurrentCount(group.Name);
                if (currentCount <= group.MaxCount)
                    continue;

                Utils.ShowChatMessage(
                    $"Manifest group limit reached, you already have {currentCount - 1}/{group.MaxCount} cores built in {group.Name}.",
                    playerEntityId: ownerId);
                return false;
            }

            return true;
        }

        internal static void AddGridGroup(string coreType)
        {
            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate { AddGridGroup(coreType); });
                return;
            }

            var core = Config.GetShipCoreByTypeId(coreType);
            if (!HasManifestGroupLimit(core))
                return;

            foreach (var group in GetManifestGroups(core))
            {
                var count = PerManifestGroupCounts.AddOrUpdate(group.Name, 1,
                    delegate(string key, int value) { return value + 1; });
                if (_suppressEvents)
                    continue;

                LimitsNexusSync.BroadcastManifestGroupChange(new ManifestGroupChange
                {
                    GroupName = group.Name,
                    Count = count
                });
            }
        }

        internal static void RemoveGridGroup(string coreType)
        {
            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate { RemoveGridGroup(coreType); });
                return;
            }

            var core = Config.GetShipCoreByTypeId(coreType);
            if (!HasManifestGroupLimit(core))
                return;

            foreach (var group in GetManifestGroups(core))
            {
                int previous;
                PerManifestGroupCounts.TryGetValue(group.Name, out previous);
                if (previous <= 0)
                    continue;

                var count = PerManifestGroupCounts.AddOrUpdate(group.Name, 0,
                    delegate(string key, int value) { return value <= 0 ? 0 : value - 1; });
                if (_suppressEvents)
                    continue;

                LimitsNexusSync.BroadcastManifestGroupChange(new ManifestGroupChange
                {
                    GroupName = group.Name,
                    Count = count
                });
            }
        }

        internal static void Reset()
        {
            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(Reset);
                return;
            }

            PerManifestGroupCounts.Clear();

            foreach (var group in Config.ManifestCoreGroups.Where(group => group != null && !string.IsNullOrWhiteSpace(group.Name)))
                PerManifestGroupCounts[group.Name] = 0;
        }

        internal static ManifestGroupCountEntry[] GetLocalCountsSnapshot()
        {
            var snapshot = PerManifestGroupCounts.ToArray();
            var result = new ManifestGroupCountEntry[snapshot.Length];
            for (var i = 0; i < snapshot.Length; i++)
            {
                result[i] = new ManifestGroupCountEntry
                {
                    GroupName = snapshot[i].Key,
                    Count = snapshot[i].Value
                };
            }

            return result;
        }

        internal static void BeginExternalUpdate()
        {
            _suppressEvents = true;
        }

        internal static void EndExternalUpdate()
        {
            _suppressEvents = false;
        }

        internal static IEnumerable<ManifestCoreGroup> GetManifestGroups(ShipCore core)
        {
            if (core == null)
                yield break;

            foreach (var groupName in core.ManifestGroupNames)
            {
                var group = Config.GetManifestGroupByName(groupName);
                if (group != null)
                    yield return group;
            }
        }
    }
}
