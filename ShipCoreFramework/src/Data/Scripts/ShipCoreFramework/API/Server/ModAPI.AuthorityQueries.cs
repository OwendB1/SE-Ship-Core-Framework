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

        private static float GetAuthoritativeGroupMass(long gridId)
        {
            GroupComponent groupComponent;
            return TryGetGroupComponent(gridId, out groupComponent) ? groupComponent.GroupMass : 0f;
        }
    }
}
