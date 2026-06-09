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
        internal readonly ConcurrentDictionary<BlockLimit, LimitBucket> Limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();
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
            Grid = (MyCubeGrid)grid;
            _groupData = groupData;

            Grid.OnMarkForClose += GridMarkedForClose;
            Grid.OnBlockAdded += BlockAddedEvent;
            Grid.OnBlockRemoved += BlockRemoved;

            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            var coreBlocks = blocks.Where(Utils.IsCoreBlock).ToList();
            foreach (var coreBlock in coreBlocks) BlockAddedInternal(coreBlock);

            var otherBlocks = blocks.Where(block => !Utils.IsCoreBlock(block)).ToList();
            foreach (var otherBlock in otherBlocks) BlockAddedInternal(otherBlock);
        }

        private void GridMarkedForClose(IngameIMyEntity entity)
        {
            if (entity != Grid) return;
            LimitsNexusSync.NotifyLocalGridClose();
        }
    }
}
