using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using IngameIMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        internal MyCubeGrid Grid;
        private IMyGridGroupData _groupData;
        private readonly object _blocksLock = new object();
        private readonly List<IMySlimBlock> _blocks = new List<IMySlimBlock>();
        private ConcurrentDictionary<BlockLimit, LimitBucket> _limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();
        internal ConcurrentDictionary<BlockLimit, LimitBucket> Limits { get { return _limits; } }
        internal int BlockCount
        {
            get
            {
                lock (_blocksLock)
                    return _blocks.Count;
            }
        }

        internal readonly ConcurrentDictionary<IMyCubeBlock, CoreComponent> CoreDictionary =
            new ConcurrentDictionary<IMyCubeBlock, CoreComponent>();

        internal readonly ConcurrentDictionary<IMyCubeBlock, BeaconComponent> BeaconDictionary =
            new ConcurrentDictionary<IMyCubeBlock, BeaconComponent>();

        private readonly ConcurrentDictionary<IMyCubeBlock, UpgradeModuleComponent> _upgradeModuleDictionary =
            new ConcurrentDictionary<IMyCubeBlock, UpgradeModuleComponent>();

        private readonly ConcurrentDictionary<long, byte> _trackedConnectorIds = new ConcurrentDictionary<long, byte>();

        private GroupComponent GroupComponent
        {
            get
            {
                if (_groupData == null) return null;
                GroupComponent groupComponent;
                return Session.GroupDict.TryGetValue(_groupData, out groupComponent) ? groupComponent : null;
            }
        }
        
        internal void Init(IMyCubeGrid grid, IMyGridGroupData groupData)
        {
            Init(grid, groupData, true);
        }

        internal void Init(IMyCubeGrid grid, IMyGridGroupData groupData, bool processBlocks)
        {
            Grid = (MyCubeGrid)grid;
            _groupData = groupData;

            Grid.OnMarkForClose += GridMarkedForClose;
            Grid.OnBlockAdded += BlockAddedEvent;
            Grid.OnBlockRemoved += BlockRemoved;

            if (!processBlocks) return;

            InitializeCoreBlocks();
            InitializeNonCoreBlocks();
        }

        internal void InitializeCoreBlocks()
        {
            var blocks = new List<IMySlimBlock>();
            ((IMyCubeGrid)Grid).GetBlocks(blocks);

            var coreBlocks = blocks.Where(Utils.IsCoreBlock).ToList();
            foreach (var coreBlock in coreBlocks) BlockAddedInternal(coreBlock);
        }

        internal void InitializeNonCoreBlocks()
        {
            var blocks = new List<IMySlimBlock>();
            ((IMyCubeGrid)Grid).GetBlocks(blocks);

            var otherBlocks = blocks.Where(block => !Utils.IsCoreBlock(block)).ToList();
            foreach (var otherBlock in otherBlocks) BlockAddedInternal(otherBlock);
        }

        internal void PublishLimitsSnapshot(ConcurrentDictionary<BlockLimit, LimitBucket> limits)
        {
            System.Threading.Interlocked.Exchange(ref _limits,
                limits ?? new ConcurrentDictionary<BlockLimit, LimitBucket>());
        }

        private void GridMarkedForClose(IngameIMyEntity entity)
        {
            if (entity != Grid) return;
            LimitsNexusSync.NotifyLocalGridClose();
        }
    }
}
