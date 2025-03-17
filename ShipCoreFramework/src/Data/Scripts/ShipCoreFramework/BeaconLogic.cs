using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ShipCoreFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false)]
    public class BeaconLogic : MyGameLogicComponent
    {
        private IMyBeacon _beacon;
        private ShipCoreLogic GridLogic => _beacon?.GetMainGridLogic();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            // the base methods are usually empty, except for OnAddedToContainer()'s, which has some sync stuff making it required to be called.
            base.Init(objectBuilder);

            _beacon = (IMyBeacon)Entity;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (_beacon.CubeGrid?.Physics == null)
                return; // ignore ghost/projected grids
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            UpdateBeacon();
            /*
            try // only for non-critical code
            {
                // ideally you want to refresh this only when necessary but this is a good compromise to only refresh it if player is in the terminal.
                // this check still doesn't mean you're looking at even the same grid's terminal as this block, for that there's other ways to check it if needed.
                //TODO only if correct grid/block?
                //"one way is to hook MyAPIGateway.TerminalControls.CustomControlGetter and store the block or entityId of that, and that is your last selected block (gets triggered per selected block including for multiple selected)"

                if (MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.ControlPanel) return;
                //TODO only run this if grid check results actually change
               // _beacon.RefreshCustomInfo();
                _//beacon.SetDetailedInfoDirty();
            }
            catch (Exception e)
            {
                Utils.LogException(e);
            }*/
        }

        public void UpdateBeacon()
        {
            var gridClass =GridLogic?.ShipCore; //<this was returning null, either because Beacon = null, or GetGridLogic isn't working

            if (gridClass == null) return;

            if (!gridClass.ForceBroadCast) return;
            _beacon.Enabled = true;
            _beacon.Radius = gridClass.ForceBroadCastRange;
            if(!_beacon.HudText.Contains(gridClass.UniqueName)){_beacon.HudText = $"{_beacon.CubeGrid.DisplayName} : {gridClass.UniqueName}";}

            /*if(primaryOwnerId != -1)
            {
                Beacon.own
                Beacon.OwnerId = primaryOwnerId;
            }*/
        }

    }
}