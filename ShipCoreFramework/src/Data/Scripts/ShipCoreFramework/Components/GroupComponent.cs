using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using MyCubeGrid = Sandbox.Game.Entities.MyCubeGrid;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal ShipCore ShipCore => Session.Config.GetShipCoreByTypeId(MainCoreComponent?.SubtypeId ?? string.Empty);

        internal IMyFaction OwningFaction => MyAPIGateway.Session.Factions.TryGetPlayerFaction(OwnerId);

        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;

        internal readonly ConcurrentDictionary<MyCubeGrid, GridComponent> GridDictionary =
            new ConcurrentDictionary<MyCubeGrid, GridComponent>();

        internal Dictionary<IMyCubeBlock, CoreComponent> CoreDictionary =>
            Utils.Flatten(GridDictionary.Values, component => component.CoreDictionary);

        private bool TryGetGridComponent(MyCubeGrid grid, out GridComponent component)
        {
            return GridDictionary.TryGetValue(grid, out component);
        }

        private int GridCount => GridDictionary.Count;

        private void ClearGridDictionary()
        {
            GridDictionary.Clear();
        }

        internal string GetThreadWorkKey()
        {
            return MyGroup != null ? MyGroup.GetHashCode().ToString() : GetRepresentativeGridId().ToString();
        }
    }
}
