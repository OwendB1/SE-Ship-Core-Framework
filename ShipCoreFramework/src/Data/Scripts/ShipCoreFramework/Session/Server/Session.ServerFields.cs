using System;
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

        internal static bool HasStarted;
        internal static readonly Guid CoreStateStorageGUID =
            new Guid("a8807ad4-524d-441a-a89a-0671fbfb1dd3");
        internal static readonly ConcurrentDictionary<IMyGridGroupData, PhysicalSpeedCluster> PhysicalSpeedClusterDict =
            new ConcurrentDictionary<IMyGridGroupData, PhysicalSpeedCluster>();
    }
}
