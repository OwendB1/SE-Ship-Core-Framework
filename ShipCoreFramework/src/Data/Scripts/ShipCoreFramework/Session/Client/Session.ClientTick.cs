namespace ShipCoreFramework
{
    public partial class Session
    {
        private void RunClientSimulationTick()
        {
            CoreTerminalControls.RegisterOnce();
            CoreTypeLCDScript.RunFrameScrollUpdate();
            NotificationInstance.RunCountdownTick();
        }
    }
}
