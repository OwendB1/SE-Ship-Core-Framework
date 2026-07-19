using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        private void OnIsWorkingChanged(IMyCubeBlock obj)
        {
            if (!Session.IsServer) return;
            _groupComponent.RefreshPunishmentState();
        }

        private void OnUpgradeValuesChanged()
        {
            if (!Session.IsServer) return;
            _groupComponent.OnUpgradeModulesChanged();
        }

        private void DestroyCoreAuthoritative()
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
