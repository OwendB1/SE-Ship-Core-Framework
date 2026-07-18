namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal int GetCurrentPlayerCoreCount()
        {
            return Session.IsServer
                ? PerPlayerManager.GetCurrentCount(OwnerId, ShipCore.SubtypeId)
                : _runtimePlayerCoreCount;
        }

        internal int GetCurrentFactionCoreCount()
        {
            if (!Session.IsServer) return _runtimeFactionCoreCount;
            var faction = OwningFaction;
            return faction == null ? 0 : PerFactionManager.GetCurrentCount(faction.FactionId, ShipCore.SubtypeId);
        }

        internal int GetCurrentManifestCoreCount(string name)
        {
            if (Session.IsServer) return PerManifestGroupManager.GetCurrentCount(name);
            int count;
            return name != null && _runtimeManifestCounts.TryGetValue(name, out count) ? count : 0;
        }
    }
}
