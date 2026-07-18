using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using IngameIMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;
using IMyShipController = Sandbox.ModAPI.IMyShipController;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        internal MyCubeGrid Grid;
        private IMyGridGroupData _groupData;
        private readonly object _blocksLock = new object();
        private readonly object _shipControllersLock = new object();
        private readonly List<IMySlimBlock> _blocks = new List<IMySlimBlock>();
        private readonly List<IMyShipController> _shipControllers = new List<IMyShipController>();
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

        private GroupComponent GroupComponent
        {
            get
            {
                if (_groupData == null) return null;
                GroupComponent groupComponent;
                return Session.GroupDict.TryGetValue(_groupData, out groupComponent) ? groupComponent : null;
            }
        }

        internal void Init(IMyCubeGrid grid, IMyGridGroupData groupData, bool processBlocks = true)
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

        internal bool InitializeCoreBlocks()
        {
            var initializedCore = false;
            var blocks = new List<IMySlimBlock>();
            ((IMyCubeGrid)Grid).GetBlocks(blocks);

            var coreBlocks = blocks.Where(Utils.IsCoreBlock).ToList();
            foreach (var coreBlock in coreBlocks)
                initializedCore |= BlockAddedInternal(coreBlock);

            return initializedCore;
        }

        internal void InitializeNonCoreBlocks()
        {
            var blocks = new List<IMySlimBlock>();
            ((IMyCubeGrid)Grid).GetBlocks(blocks);

            var otherBlocks = blocks.Where(block => !Utils.IsCoreBlock(block)).ToList();
            foreach (var otherBlock in otherBlocks) BlockAddedInternal(otherBlock);
        }

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
            {
                _shipControllers.Remove(shipController);
            }
        }

        private void GridMarkedForClose(IngameIMyEntity entity)
        {
            if (entity != Grid) return;
            if (Session.IsServer) NotifyLocalGridCloseAuthoritative();
        }
    }
}
