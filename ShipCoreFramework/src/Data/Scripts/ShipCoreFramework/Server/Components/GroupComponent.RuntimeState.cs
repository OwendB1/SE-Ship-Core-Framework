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

        private static int GetAuthoritativeManifestCoreCount(string name)
        {
            return PerManifestGroupManager.GetCurrentCount(name);
        }
    }
}
