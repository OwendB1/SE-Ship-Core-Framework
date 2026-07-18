using System.Collections.Concurrent;
using NexusModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static NexusAPI _myNexusApi;
        private const int MaxMassCacheRefreshesPerTick = 4;
        private const int MaxMassCacheGroupsCheckedPerTick = 32;
        private bool _startedNexus;
        private int _massCacheRefreshCursor;

        internal static readonly ConcurrentDictionary<IMyGridGroupData, PhysicalSpeedCluster> PhysicalSpeedClusterDict =
            new ConcurrentDictionary<IMyGridGroupData, PhysicalSpeedCluster>();
    }
}
