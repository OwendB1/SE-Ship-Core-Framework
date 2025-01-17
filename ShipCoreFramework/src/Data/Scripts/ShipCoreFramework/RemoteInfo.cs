using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ShipCoreFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), false)]
    public class RemoteControlInfo : MyGameLogicComponent
    {
        private IMyRemoteControl _remote;
        private CubeGridLogic GridLogic => _remote?.GetMainGridLogic();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            // the base methods are usually empty, except for OnAddedToContainer()'s, which has some sync stuff making it required to be called.
            base.Init(objectBuilder);
            if (!Constants.IsClient) return;

            _remote = (IMyRemoteControl)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (!Constants.IsClient) return;

            if (_remote.CubeGrid?.Physics == null)
                return; // ignore ghost/projected grids

            _remote.AppendingCustomInfo += AppendingCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }
        public override void UpdateAfterSimulation()
        {
            if (GridLogic == null || _remote.CubeGrid == null || _remote.CubeGrid.IsStatic || !_remote.IsUnderControl){return;}
            GridLogic.EnforceSpeedLimit(_remote.CubeGrid);
        }
        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            try
            {
                if (MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.ControlPanel) return;
                //TODO only run this if grid check results actually change
                _remote.RefreshCustomInfo();
                _remote.SetDetailedInfoDirty();
            }
            catch (Exception e)
            {
                Utils.LogException(e);
            }
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            var gridLogic = block.GetMainGridLogic();
            if (gridLogic == null) return;

            var gridClass = gridLogic.ShipCore;
            var concreteGrid = _remote.CubeGrid as MyCubeGrid;
            if (gridClass == null || concreteGrid == null) return;

            var infoBuilder = new StringBuilder();
            infoBuilder.Append($"\nClass: {gridClass.SimpleName} \n\n");

            FormatRangeCheckResult("Blocks", infoBuilder, gridClass.MinBlocks, gridClass.MaxBlocks, concreteGrid.BlocksCount);
            FormatMaxCheckResult("Mass", infoBuilder, gridClass.MaxMass, concreteGrid.Mass);
            FormatMaxCheckResult("PCU", infoBuilder, gridClass.MaxPCU, concreteGrid.BlocksPCU);

            if (gridClass.BlockLimits != null)
                foreach (var blockLimit in gridClass.BlockLimits)
                {
                    if (gridLogic.Blocks == null) continue;
                    var countWeight = gridLogic.BlocksPerLimit[blockLimit].Sum(l => l.Value);
                    FormatBlockLimitCheckResult(infoBuilder, blockLimit, countWeight);
                }
                    

            infoBuilder.Append("\nApplied Modifiers: \n\n");

            foreach (var modifierValue in GridLogic.Modifiers.GetModifierValues())
                infoBuilder.Append($"{modifierValue.Name}: {modifierValue.Value}\n");

            sb.Append(infoBuilder);
            
        }

        private static void FormatBlockLimitCheckResult(StringBuilder sb, BlockLimit blockLimit, double countWeight)
        {
            sb.Append($"{blockLimit.Name}: {countWeight}/{blockLimit.MaxCount}{(countWeight <= blockLimit.MaxCount ? "\n" : " (fail)\n")}");
        }

        private static void FormatMaxCheckResult(string name, StringBuilder sb, float max,float value)
        {
            if (max > 0)
                sb.Append($"{name}: {value}/{max}{(value <= max ? "\n" : " (fail)\n")}");
        }

        private static void FormatMaxCheckResult(string name, StringBuilder sb, int max, int value)
        {
            if (max > 0)
                sb.Append($"{name}: {value}/{max}{(value <= max ? "\n" : " (fail)\n")}");
        }

        private static void FormatRangeCheckResult(string name, StringBuilder sb, int min, int max, int value)
        {
            if (min < 1 && max < 1) return;
            var passed = value <= max && value >= min;
            var range = min > 0 && max > 0
                ? $"{min} - {max}"
                : min > 0
                    ? $">= {min}"
                    : $"<= {max}";

            sb.Append($"{name}: {value}/{range}{(passed ? "\n" : " (fail)\n")}");
        }
    }
}