#region

using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

#endregion

namespace ShipCoreFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false)]
    public class BeaconLogic : MyGameLogicComponent
    {
        private IMyBeacon _beacon;
        private GridLogic GridLogic => _beacon?.GetMainGridLogic();

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
        }

        private void UpdateBeacon()
        {
              if (GridLogic == null) return;
              var core = GridLogic.ShipCore;
              if (core.ForceBroadCast == false) return;
              _beacon.Enabled = true;
              _beacon.Radius = core.ForceBroadCastRange;
              if(!_beacon.HudText.Contains(core.UniqueName)) _beacon.HudText = $"{_beacon.CubeGrid.DisplayName} : {core.UniqueName}";
        }
    }
}