using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static IMyEntity _terminalGuardEntity;
        private static IMyCubeGrid _terminalGuardGrid;

        private void RunClientSimulationTick()
        {
            CoreTerminalControls.RegisterOnce();
            UpdateTerminalGridCloseGuard();
            CoreTypeLCDScript.RunFrameScrollUpdate();
            NotificationInstance.RunCountdownTick();
        }

        private static void UpdateTerminalGridCloseGuard()
        {
            IMyEntity interactedEntity = MyAPIGateway.Gui.InteractedEntity;
            if (ReferenceEquals(interactedEntity, _terminalGuardEntity)) return;

            ResetTerminalGridCloseGuard();

            IMyCubeBlock interactedBlock = interactedEntity as IMyCubeBlock;
            IMyCubeGrid interactedGrid = interactedBlock == null
                ? interactedEntity as IMyCubeGrid
                : interactedBlock.CubeGrid;
            if (interactedGrid == null) return;

            _terminalGuardEntity = interactedEntity;
            _terminalGuardGrid = interactedGrid;
            _terminalGuardGrid.OnMarkForClose += TerminalGridMarkedForClose;
        }

        private static void TerminalGridMarkedForClose(IMyEntity entity)
        {
            if (!ReferenceEquals(entity, _terminalGuardGrid)) return;

            bool terminalStillUsesTrackedEntity =
                ReferenceEquals(MyAPIGateway.Gui.InteractedEntity, _terminalGuardEntity);
            ResetTerminalGridCloseGuard();

            if (terminalStillUsesTrackedEntity)
                MyAPIGateway.Gui.ChangeInteractedEntity(null, false);
        }

        private static void ResetTerminalGridCloseGuard()
        {
            if (_terminalGuardGrid != null)
                _terminalGuardGrid.OnMarkForClose -= TerminalGridMarkedForClose;

            _terminalGuardEntity = null;
            _terminalGuardGrid = null;
        }
    }
}
