namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void InvalidateSpeedStateCache()
        {
            LastSpeedStateUpdateTick = -1;
        }
    }
}
