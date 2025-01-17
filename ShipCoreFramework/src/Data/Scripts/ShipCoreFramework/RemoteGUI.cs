using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Network;
using VRage.Utils;

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
            _remoteControls.Add(GetCombobox("SetGridClassLargeStatic", SetComboboxContentLargeStatic,
                remote => remote.CubeGrid.IsStatic && remote.CubeGrid.GridSizeEnum == MyCubeSize.Large));
            _remoteControls.Add(GetCombobox("SetGridClassLargeMobile", SetComboboxContentLargeGridMobile,
                remote => !remote.CubeGrid.IsStatic && remote.CubeGrid.GridSizeEnum == MyCubeSize.Large));
            _remoteControls.Add(GetCombobox("SetGridClassSmall", SetComboboxContentSmall,
                remote => !remote.CubeGrid.IsStatic && remote.CubeGrid.GridSizeEnum == MyCubeSize.Small));
            _remoteActions.Add(GetBoostButton("BoostButton", BoostButtonAvailability));
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
        private static IMyTerminalControlCombobox GetCombobox(string name,
            Action<List<MyTerminalControlComboBoxItem>> setComboboxContent, Func<IMyTerminalBlock, bool> isVisible)
        {
            var combobox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyRemoteControl>(name);
            combobox.Visible = isVisible;
            combobox.Enabled = isVisible;
            combobox.Title = MyStringId.GetOrCompute("Grid class");
            combobox.Tooltip = MyStringId.GetOrCompute("Select your desired grid class");
            combobox.SupportsMultipleBlocks = false;
            combobox.Getter = GetGridClass;
            combobox.Setter = SetGridClass;
            combobox.ComboBoxContent = setComboboxContent;
            return combobox;
        }

        private static void SetComboboxContentLargeStatic(List<MyTerminalControlComboBoxItem> list)
        {
            list.AddRange(from gridLimit in ModSessionManager.Config.ShipCores
                          where gridLimit.LargeGridStatic
                          select new MyTerminalControlComboBoxItem { Key = gridLimit.Id, Value = MyStringId.GetOrCompute(gridLimit.SimpleName) });
        }

        private static void SetComboboxContentLargeGridMobile(List<MyTerminalControlComboBoxItem> list)
        {
            list.AddRange(from gridLimit in ModSessionManager.Config.ShipCores
                          where gridLimit.LargeGridMobile
                          select new MyTerminalControlComboBoxItem { Key = gridLimit.Id, Value = MyStringId.GetOrCompute(gridLimit.SimpleName) });
        }

        private static void SetComboboxContentSmall(List<MyTerminalControlComboBoxItem> list)
        {
            list.AddRange(from gridLimit in ModSessionManager.Config.ShipCores
                          where gridLimit.SmallGrid
                          select new MyTerminalControlComboBoxItem { Key = gridLimit.Id, Value = MyStringId.GetOrCompute(gridLimit.SimpleName) });
        }

        private static long GetGridClass(IMyTerminalBlock block)
        {
            var cubeGridLogic = block.GetMainGridLogic();
            return cubeGridLogic?.GridClassId ?? 0;
        }

        private static void SetGridClass(IMyTerminalBlock block, long key)
        {
            var cubeGridLogic = block.GetMainGridLogic();
            if (cubeGridLogic != null)
            {
                Utils.Log(
                    $"RemoteGUI::SetGridClass: Sending change grid class message, entityId = {block.CubeGrid.EntityId}, grid class id = {key}",
                    2);
                cubeGridLogic.GridClassId = key;
                if (!Constants.IsServer)
                    ModSessionManager.Comms.ChangeGridClass(cubeGridLogic.Grid.EntityId, key);
            }
            else
            {
                Utils.Log($"RemoteGUI::SetGridClass: Unable to set GridClassId, GetGridLogic is returning null on {block.EntityId}", 3);
            }
        }
    }
}