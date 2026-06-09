using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void SetPhysicalLinkedGroups(IMyGridGroupData physicalGroup, bool isSpeedClusterRepresentative)
        {
            lock (_connectedGroupsLock)
            {
                _trackedPhysicalGroup = physicalGroup;
                SpeedClusterPhysicalGroup = physicalGroup;
                IsSpeedClusterRepresentative = isSpeedClusterRepresentative;
            }
        }

        internal void ClearPhysicalLinkedGroups(IMyGridGroupData physicalGroup = null)
        {
            lock (_connectedGroupsLock)
            {
                if (physicalGroup != null && !ReferenceEquals(_trackedPhysicalGroup, physicalGroup))
                    return;

                _trackedPhysicalGroup = null;
                SpeedClusterPhysicalGroup = null;
                IsSpeedClusterRepresentative = false;
            }
        }
    }
}
