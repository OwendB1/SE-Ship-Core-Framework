using System.Text;
using VRage.Game.ModAPI;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        private void AttachBlockEvents()
        {
            if (CoreBlock == null || _eventsAttached) return;
            if (!Session.IsGameThread)
            {
                var blockId = CoreBlock.EntityId;
                ThreadWork.Enqueue(ThreadWork.StateCategory, "core-events:" + blockId,
                    "Attach core events " + blockId,
                    delegate
                    {
                        return CoreBlock != null &&
                               !CoreBlock.MarkedForClose &&
                               !CoreBlock.Closed &&
                               !Session.IsShuttingDown;
                    },
                    AttachBlockEvents);
                return;
            }

            CoreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            CoreBlock.AppendingCustomInfo += AppendingCustomInfo;
            CoreBlock.IsWorkingChanged += OnIsWorkingChanged;
            _eventsAttached = true;
        }

        private void DetachBlockEvents()
        {
            if (CoreBlock == null || !_eventsAttached) return;

            CoreBlock.OnUpgradeValuesChanged -= OnUpgradeValuesChanged;
            CoreBlock.AppendingCustomInfo -= AppendingCustomInfo;
            CoreBlock.IsWorkingChanged -= OnIsWorkingChanged;
            _eventsAttached = false;
        }

        internal void Clean()
        {
            DetachBlockEvents();
        }

        private void OnIsWorkingChanged(IMyCubeBlock obj)
        {
            _groupComponent.RefreshPunishmentState();
        }

        private void OnUpgradeValuesChanged()
        {
            _groupComponent.OnUpgradeModulesChanged();
        }

        private static void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder myText)
        {
            var groupComponent = block.GetGroupComponent();
            if (groupComponent == null) Utils.Log($"Group component is null for {block.CustomName}",3);
            var shipCore = groupComponent?.ShipCore;
            if (shipCore == null) return;
            myText.Append(Commands.GetCoreInfo(block.CubeGrid, shipCore, groupComponent));
        }

        internal void CoreDestroyed()
        {
            var grid = CoreBlock.CubeGrid;
            Utils.ShowNotification(
                IsMainCore
                    ? $"Main core of grid {grid.CustomName} was destroyed!"
                    : $"A backup core of grid {grid.CustomName} was destroyed!",
                0, 5000, true);

            DetachBlockEvents();
            _groupComponent.CoreRemoved(this);
        }
    }
}
