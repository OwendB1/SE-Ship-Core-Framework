namespace ShipCoreFramework
{
    public static partial class ModAPI
    {
        private static int GetReplicatedManifestGroupCount(string name)
        {
            int count;
            return RuntimeStateStore.TryGetManifestCount(name, out count) ? count : 0;
        }
    }
}
