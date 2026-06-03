using System;
using System.Collections.Generic;
using System.Linq;

namespace ShipCoreFramework
{
    internal static class PerManifestGroupManager
    {
        internal struct ManifestGroupChange
        {
            internal string GroupName;
            internal int Count;
        }

        internal static readonly Dictionary<string, int> PerManifestGroup =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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

            int count;
            return (PerManifestGroup.TryGetValue(groupName, out count) ? count : 0) +
                   LimitsNexusSync.GetRemoteManifestGroupCount(groupName);
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
            var core = Config.GetShipCoreByTypeId(coreType);
            if (!HasManifestGroupLimit(core))
                return;

            foreach (var group in GetManifestGroups(core))
            {
                if (!PerManifestGroup.ContainsKey(group.Name))
                    PerManifestGroup[group.Name] = 0;

                PerManifestGroup[group.Name]++;
                if (_suppressEvents)
                    continue;

                LimitsNexusSync.BroadcastManifestGroupChange(new ManifestGroupChange
                {
                    GroupName = group.Name,
                    Count = PerManifestGroup[group.Name]
                });
            }
        }

        internal static void RemoveGridGroup(string coreType)
        {
            var core = Config.GetShipCoreByTypeId(coreType);
            if (!HasManifestGroupLimit(core))
                return;

            foreach (var group in GetManifestGroups(core))
            {
                int value;
                if (!PerManifestGroup.TryGetValue(group.Name, out value) || value <= 0)
                    continue;

                PerManifestGroup[group.Name]--;
                if (_suppressEvents)
                    continue;

                LimitsNexusSync.BroadcastManifestGroupChange(new ManifestGroupChange
                {
                    GroupName = group.Name,
                    Count = PerManifestGroup[group.Name]
                });
            }
        }

        internal static void Reset()
        {
            foreach (var key in PerManifestGroup.Keys.ToList())
                PerManifestGroup[key] = 0;

            foreach (var group in Config.ManifestCoreGroups.Where(group => group != null && !string.IsNullOrWhiteSpace(group.Name)))
                if (!PerManifestGroup.ContainsKey(group.Name))
                    PerManifestGroup[group.Name] = 0;
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
