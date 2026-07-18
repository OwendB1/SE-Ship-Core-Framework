namespace ShipCoreFramework
{
    public partial class Session
    {
        private void RunClientSimulationTick()
        {
            CoreTypeLCDScript.RunFrameScrollUpdate();
            NotificationInstance.RunCountdownTick();
        }
    }
}
