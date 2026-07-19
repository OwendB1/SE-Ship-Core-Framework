using System.Text;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        private bool InitClientObserver()
        {
            CubeGridModifiers.RegisterUpgradeModuleLink(CoreBlock);
            AttachBlockEvents();
            return true;
        }

        private void RefreshCoreCustomInfo()
        {
            if (CoreBlock == null || CoreBlock.MarkedForClose || CoreBlock.Closed) return;
            CoreBlock.RefreshCustomInfo();
        }

        internal void ApplyAuthoritativeMainState(bool value)
        {
            _isMainCore = value;
            if (CoreBlock == null || CoreBlock.MarkedForClose || CoreBlock.Closed) return;
            if (Session.IsGameThread)
            {
                RefreshCoreCustomInfo();
                return;
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(delegate
            {
                if (CoreBlock != null && !CoreBlock.MarkedForClose && !CoreBlock.Closed && !Session.IsShuttingDown)
                    RefreshCoreCustomInfo();
            });
        }

        private static void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder myText)
        {
            var groupComponent = block.GetGroupComponent();
            if (groupComponent == null) Utils.Log($"Group component is null for {block.CustomName}", 3);
            var shipCore = groupComponent?.ShipCore;
            if (shipCore == null) return;
            myText.Append(Commands.GetCoreInfo(block.CubeGrid, shipCore, groupComponent));
        }

        private void ObserveCoreDestroyed()
        {
            DetachBlockEvents();
            if (ReferenceEquals(_groupComponent.MainCoreComponent, this))
                _groupComponent.MainCoreComponent = null;
        }
    }
}
