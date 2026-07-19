namespace ShipCoreFramework
{
    public static partial class ModAPI
    {
        private static void RefreshAuthoritativeSpeedState(GroupComponent groupComponent)
        {
            SpeedEnforcement.RefreshSpeedState(groupComponent);
        }

        private static int GetAuthoritativeManifestGroupCount(string name)
        {
            return PerManifestGroupManager.GetCurrentCount(name);
        }
    }
}
