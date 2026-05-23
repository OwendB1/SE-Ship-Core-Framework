using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void SetPhysicalLinkedGroups(IMyGridGroupData physicalGroup, IEnumerable<IMyGridGroupData> linkedGroups)
        {
            lock (_connectedGroupsLock)
            {
                _trackedPhysicalGroup = physicalGroup;
                _connectedPhysicalGroups.Clear();

                if (linkedGroups == null) return;

                foreach (var linkedGroup in linkedGroups.Where(linkedGroup => linkedGroup != null && !ReferenceEquals(linkedGroup, MyGroup)))
                    _connectedPhysicalGroups.Add(linkedGroup);
            }
        }

        internal void ClearPhysicalLinkedGroups(IMyGridGroupData physicalGroup = null)
        {
            lock (_connectedGroupsLock)
            {
                if (physicalGroup != null && !ReferenceEquals(_trackedPhysicalGroup, physicalGroup))
                    return;

                _trackedPhysicalGroup = null;
                _connectedPhysicalGroups.Clear();
            }
        }

        internal List<IMyGridGroupData> GetConnectedPhysicalGroupDataSnapshot()
        {
            lock (_connectedGroupsLock)
                return _connectedPhysicalGroups.Where(otherGroupData => otherGroupData != null).ToList();
        }
    }
}
