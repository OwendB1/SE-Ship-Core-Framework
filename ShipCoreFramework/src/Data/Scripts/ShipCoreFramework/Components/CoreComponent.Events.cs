using System.Linq;
using System.Text;
using VRage.Game.ModAPI;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
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
            var targetGrid = block.CubeGrid;
            var groupKvp = Session.GroupDict.FirstOrDefault(gk => gk.Value.GridDictionary.Any(kvp => kvp.Key == targetGrid));
            if (groupKvp.Value == null || targetGrid == null) return;
            var shipCore = groupKvp.Value.ShipCore;
            if (shipCore == null) return;
            myText.Append(Commands.GetCoreInfo(targetGrid, shipCore, groupKvp));
        }

        internal void CoreDestroyed()
        {
            var grid = CoreBlock.CubeGrid;
            Utils.ShowNotification(
                IsMainCore
                    ? $"Main core of grid {grid.CustomName} was destroyed!"
                    : $"A backup core of grid {grid.CustomName} was destroyed!",
                0, 5000, true);

            CoreBlock.OnUpgradeValuesChanged -= OnUpgradeValuesChanged;
            CoreBlock.AppendingCustomInfo -= AppendingCustomInfo;
            CoreBlock.IsWorkingChanged -= OnIsWorkingChanged;

            _groupComponent.CoreRemoved(this);
        }
    }
}
