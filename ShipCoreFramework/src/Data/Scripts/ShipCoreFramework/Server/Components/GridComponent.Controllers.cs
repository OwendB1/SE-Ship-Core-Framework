using System.Collections.Generic;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        private readonly object _shipControllersLock = new object();
        private readonly List<IMyShipController> _shipControllers = new List<IMyShipController>();

        internal List<IMyShipController> GetShipControllersCopy()
        {
            lock (_shipControllersLock)
                return new List<IMyShipController>(_shipControllers);
        }

        private void TrackShipController(IMyShipController shipController)
        {
            if (shipController == null) return;

            lock (_shipControllersLock)
            {
                if (!_shipControllers.Contains(shipController))
                    _shipControllers.Add(shipController);
            }
        }

        private void UntrackShipController(IMyShipController shipController)
        {
            if (shipController == null) return;

            lock (_shipControllersLock)
                _shipControllers.Remove(shipController);
        }

        private void ShipControllerOnPropertiesChanged(IMyTerminalBlock block)
        {
            var groupComponent = GroupComponent;
            if (groupComponent == null || groupComponent.MainCoreComponent != null) return;

            groupComponent.OnNoCoreDirectionReferencePropertiesChanged();
        }
    }
}
