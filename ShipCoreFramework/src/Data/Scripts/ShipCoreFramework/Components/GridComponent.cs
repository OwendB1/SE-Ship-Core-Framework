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
        internal readonly Dictionary<BlockLimit, LimitBucket> Limits = new Dictionary<BlockLimit, LimitBucket>();

        internal readonly Dictionary<IMyCubeBlock, CoreComponent> CoreDictionary =
            new Dictionary<IMyCubeBlock, CoreComponent>();

        internal readonly Dictionary<IMyCubeBlock, BeaconComponent> BeaconDictionary =
            new Dictionary<IMyCubeBlock, BeaconComponent>();

        private readonly Dictionary<IMyCubeBlock, UpgradeModuleComponent> _upgradeModuleDictionary =
            new Dictionary<IMyCubeBlock, UpgradeModuleComponent>();

        private readonly HashSet<long> _trackedConnectorIds = new HashSet<long>();

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
