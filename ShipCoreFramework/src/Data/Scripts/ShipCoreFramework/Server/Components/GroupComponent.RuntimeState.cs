namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private int GetAuthoritativePlayerCoreCount()
        {
            return PerPlayerManager.GetCurrentCount(OwnerId, ShipCore.SubtypeId);
        }

        private int GetAuthoritativeFactionCoreCount()
        {
            var factionId = OwningFaction?.FactionId ?? -1;
            return PerFactionManager.GetCurrentCount(factionId, ShipCore.SubtypeId);
        }

        private int GetAuthoritativeFactionPlayerCount()
        {
            return PerFactionManager.GetFactionPlayerCount(OwningFaction, OwnerId);
        }

        private int GetAuthoritativeEffectiveFactionCoreLimit()
        {
            return PerFactionManager.GetEffectiveFactionCoreLimit(ShipCore,
                GetAuthoritativeFactionPlayerCount());
        }

        private static int GetAuthoritativeManifestCoreCount(string name)
        {
            return PerManifestGroupManager.GetCurrentCount(name);
        }
    }
}
