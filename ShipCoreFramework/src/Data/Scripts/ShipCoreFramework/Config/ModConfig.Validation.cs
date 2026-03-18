using System;
using System.Collections.Generic;
using System.Linq;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        private static void ThrowErrorIfDuplicates<T, TKey>(List<T> list, Func<T, TKey> selector)
        {
            var dupeList = list.GroupBy(selector)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (dupeList.Any())
                throw new Exception($"Found duplicates f0r {selector.Method.Name}: {string.Join("\n- ", dupeList)}");
        }

        private static void NormalizeShipCoreBlockLimits(ShipCore core, string source, string coreFileOrKey)
        {
            if (core == null) return;

            core.NormalizeAllowedUpgradeModules(source, coreFileOrKey);

            if (core.BlockLimits == null)
            {
                core.BlockLimits = Array.Empty<BlockLimit>();
                Utils.Log($"Config warning: ShipCore '{core.UniqueName}' from {source} ({coreFileOrKey}) had no <BlockLimits>; treating as none.", 2, "Config Validation");
                return;
            }

            foreach (var limit in core.BlockLimits)
            {
                if (limit == null) continue;

                if (limit.BlockGroupsShortHand == null)
                {
                    limit.BlockGroupsShortHand = Array.Empty<string>();
                    Utils.Log($"Config warning: ShipCore '{core.UniqueName}' from {source} ({coreFileOrKey}) has a <BlockLimit> with null <BlockGroups>; treating as empty.", 2, "Config Validation");
                }

                if (limit.BlockGroups == null) limit.BlockGroups = new List<BlockGroup>();
            }
        }

        private static void NormalizeBlockGroups(List<BlockGroup> groups, string source)
        {
            if (groups == null) return;

            for (var i = groups.Count - 1; i >= 0; i--)
            {
                var group = groups[i];
                if (group == null)
                {
                    groups.RemoveAt(i);
                    Utils.Log($"Config warning: Null <BlockGroup> entry found in {source}; ignoring.", 2, "Config Validation");
                    continue;
                }

                if (group.Name == null) group.Name = string.Empty;

                if (group.BlockTypes == null)
                {
                    group.BlockTypes = new List<BlockType>();
                    Utils.Log($"Config warning: BlockGroup '{group.Name}' from {source} had no <BlockTypes>; treating as empty.", 2, "Config Validation");
                }
                else
                {
                    group.BlockTypes.RemoveAll(type => type == null);
                }
            }
        }

        private static void NormalizeUpgradeModule(UpgradeModuleConfig module, string source, string moduleFile)
        {
            if (module == null) return;

            if (string.IsNullOrWhiteSpace(module.SubtypeId))
                throw new Exception($"UpgradeModuleConfig from {source} ({moduleFile}) is missing <SubtypeId>.");

            if (string.IsNullOrWhiteSpace(module.UniqueName))
                module.UniqueName = module.SubtypeId;

            if (module.Modifiers == null)
                module.Modifiers = Array.Empty<UpgradeStatModifier>();

            if (module.BlockLimitModifiers == null)
                module.BlockLimitModifiers = Array.Empty<BlockLimitModifier>();

            if (module.Modifiers.Any(modifier => modifier != null && string.IsNullOrWhiteSpace(modifier.Stat)))
                throw new Exception($"UpgradeModuleConfig '{module.SubtypeId}' from {source} ({moduleFile}) has a modifier with no <Stat>.");

            if (module.BlockLimitModifiers.Any(limitModifier => limitModifier != null && string.IsNullOrWhiteSpace(limitModifier.BlockLimitName)))
                throw new Exception($"UpgradeModuleConfig '{module.SubtypeId}' from {source} ({moduleFile}) has a block limit modifier with no <BlockLimitName>.");
        }
    }
}
