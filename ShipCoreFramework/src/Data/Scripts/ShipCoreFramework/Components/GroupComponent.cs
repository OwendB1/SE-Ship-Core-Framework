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

        internal Dictionary<IMyCubeBlock, CoreComponent> CoreDictionary
        {
            get
            {
                var result = new Dictionary<IMyCubeBlock, CoreComponent>();
                foreach (var gridComponent in GridDictionary.Values)
                {
                    foreach (var kvp in gridComponent.CoreDictionary)
                        result[kvp.Key] = kvp.Value;
                }

                return result;
            }
        }

        private bool TryGetGridComponent(MyCubeGrid grid, out GridComponent component)
        {
            return GridDictionary.TryGetValue(grid, out component);
        }

        private int GridCount => GridDictionary.Count;

        private void ClearGridDictionary()
        {
            GridDictionary.Clear();
        }

        internal string GetGroupKey()
        {
            return MyGroup != null ? MyGroup.GetHashCode().ToString() : GetRepresentativeGridId().ToString();
        }
    }
}
