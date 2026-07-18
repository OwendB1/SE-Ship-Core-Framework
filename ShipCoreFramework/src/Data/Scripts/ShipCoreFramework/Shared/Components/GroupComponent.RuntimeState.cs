namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal int GetCurrentPlayerCoreCount()
        {
            return Session.IsServer
                ? GetAuthoritativePlayerCoreCount()
                : _runtimePlayerCoreCount;
        }

        internal int GetCurrentFactionCoreCount()
        {
            return Session.IsServer
                ? GetAuthoritativeFactionCoreCount()
                : _runtimeFactionCoreCount;
        }

        internal int GetCurrentManifestCoreCount(string name)
        {
            return Session.IsServer
                ? GetAuthoritativeManifestCoreCount(name)
                : GetRuntimeManifestCoreCount(name);
        }

        private int GetRuntimeManifestCoreCount(string name)
        {
            int count;
            return !string.IsNullOrEmpty(name) && _runtimeManifestCounts.TryGetValue(name, out count) ? count : 0;
        }
    }
}
