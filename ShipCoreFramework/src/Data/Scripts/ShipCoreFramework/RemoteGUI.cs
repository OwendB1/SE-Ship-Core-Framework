using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.Network;

namespace ShipCoreFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class RemoteGUI : MySessionComponentBase, IMyEventProxy
    {
        private static readonly string[] ControlsToHideIfNotMainRemote = { "SetGridClassLargeStatic", "SetGridClassLargeMobile", "SetGridClassSmall" };
        private readonly List<IMyTerminalControl> _remoteControls = new List<IMyTerminalControl>();
        private readonly List<IMyTerminalAction> _remoteActions = new List<IMyTerminalAction>();
        public override void BeforeStart()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
        }
        private void BoostButtonWriter(IMyTerminalBlock block, StringBuilder sb)
        {
            var gridLogic = block.CubeGrid.GetMainGridLogic();
            if (gridLogic != null)
            {
                sb.Append(gridLogic.EnableBoost ? $"Go: {(int)Math.Round(gridLogic.BoostDuration/60.0f)}" : (gridLogic.BoostCoolDown>0? $"Wait: {(int)Math.Round(gridLogic.BoostCoolDown/60.0f)}" : "Ready"));
            }
            else
            {
                sb.Append("Boost: N/A");
            }
        }
        private static bool BoostButtonAvailability(IMyTerminalBlock obj)
        {
            var gridLogic = obj.GetMainGridLogic();
            if (gridLogic == null)
            {
                return false;
            }

            if (!(gridLogic.Modifiers.MaxBoost > 1))
            {
                return false;
            }

            return gridLogic.BoostCoolDown != null;
        }
        private IMyTerminalAction GetBoostButton(string name, Func<IMyTerminalBlock, bool> isEnabled)
        {
            var boostButton = MyAPIGateway.TerminalControls.CreateAction<IMyRemoteControl>(name);
            boostButton.Enabled = isEnabled;
            boostButton.Action = BoostButtonClicked;
            boostButton.Icon=Path.Combine(ModContext.ModPath, "Textures", "BoostButton_Sad_Static.png");
            boostButton.Writer = BoostButtonWriter;
            boostButton.Name = new StringBuilder("Boost");
            return boostButton;
        }

        private static void BoostButtonClicked(IMyTerminalBlock block)
        {
            var gridLogic = block.GetMainGridLogic();
            if (gridLogic == null)
            {
                Utils.Log("gridnotfound");
                return;
            }

            if (gridLogic.EnableBoost == null)
            {
                Utils.Log("BoostDataNotFound");
                return;
            }

            if (gridLogic.BoostCoolDown > 0)
            {
                gridLogic.EnableBoost=false;
                Utils.ShowNotification("Booster On Cooldown!",block.CubeGrid,600);
                return;
            }

            gridLogic.EnableBoost= !gridLogic.EnableBoost;
            Utils.ShowNotification(gridLogic.EnableBoost ? "Booster Engaged!" : "Booster Disengaged!",block.CubeGrid,600);
        }
        protected override void UnloadData()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
        }

        public void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (!(block is IMyRemoteControl)) return;
            if (controls.Any(control => _remoteControls.Contains(control))) return;
            controls.AddRange(_remoteControls);
            foreach (var control in controls.Where(control => ControlsToHideIfNotMainRemote.Contains(control.Id)))
                control.Enabled = TerminalChainedDelegate.Create(control.Visible, VisibleIfIsMainOwner);
        }
        public void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> controls)
        {
            if (!(block is IMyRemoteControl)) return;
            if (controls.Any(control => _remoteActions.Contains(control))) return;
            controls.AddRange(_remoteActions);
        }
        private static bool VisibleIfIsMainOwner(IMyTerminalBlock block)
        {
            var remote = block as IMyRemoteControl;
            if (remote.OwnerId == Utils.GetGridOwner(block.CubeGrid))
            {
                return true;
            }
            return MyAPIGateway.Session.Factions.TryGetPlayerFaction(remote.OwnerId) ==
                   MyAPIGateway.Session.Factions.TryGetPlayerFaction(Utils.GetGridOwner(block.CubeGrid));
        }
        
    }
}