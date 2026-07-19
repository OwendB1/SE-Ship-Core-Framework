using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;

namespace ShipCoreFramework
{
    internal static class CoreTerminalControls
    {
        private const string IdPrefix = "ShipCoreFramework_CoreTerminalControls_";
        private static bool _done;
        private static IMyTerminalControlCheckbox _mainCoreCheckbox;
        private static IMyTerminalAction _boostAction;
        private static IMyTerminalAction _defenseAction;
        private static IMyTerminalAction _powerOverclockAction;
        
        internal static void RegisterOnce()
        {
            if (Session.LocalPlayer == null) return;
            if(_done)
                return;
            _done = true;
            
            CreateControls();
            CreateActions();

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
        }

        internal static void Unregister()
        {
            if (!_done) return;

            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
            _done = false;
        }

        private static void CreateControls()
        {
            _mainCoreCheckbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>(IdPrefix + "IsMainCoreCheckbox");
            _mainCoreCheckbox.Title = MyStringId.GetOrCompute("Main Core");
            _mainCoreCheckbox.Tooltip = MyStringId.GetOrCompute("Mark this core as the main core for the grid.");
            _mainCoreCheckbox.SupportsMultipleBlocks = false;

            _mainCoreCheckbox.Getter = b => {
                return IsMainCoreBlock(b);
            };

            _mainCoreCheckbox.Enabled = b => {
                var group = b.GetGroupComponent();
                if (group == null) return false;
                return !IsMainCoreBlock(b);
            };

            _mainCoreCheckbox.Setter = (b, val) =>
            {
                if (!val) return;
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowChatMessage("Could not set main core: group not found.");
                    return;
                }

                Session.Networking.SendToServer(new PacketSetMainCore
                {
                    ActionData = new SetMainCoreAction
                    {
                        CubegridEntityId = b.CubeGrid.EntityId,
                        BlockEntityId = b.EntityId
                    }
                });
            };
        }
        
        private static void CreateActions()
        {
            _boostAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("ShipCore_ActivateBoost");
            _boostAction.Name = new StringBuilder("Activate Boost");
            _boostAction.Icon = @"Textures\BoostButton_Sad_Static.png";
            _boostAction.ValidForGroups = false;
            
            _boostAction.Enabled = delegate(IMyTerminalBlock b)
            {
                if (!Utils.IsCoreBlock(b as IMyFunctionalBlock)) return false;
                return IsMainCoreBlock(b);
            };
            
            _boostAction.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowChatMessage("Could not trigger boost, main grid group match was not found??");
                    return;
                }
                
                Session.Networking.SendToServer(new PacketAction{ActionData = new ButtonAction {CubegridEntityId = b.CubeGrid.EntityId, Action = CoreActionType.Boost }});
            };

            _defenseAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("ShipCore_ActivateDefense");
            _defenseAction.Name = new StringBuilder("Activate Defense");
            _defenseAction.Icon = @"Textures\BoostButton_Sad_Static.png";
            _defenseAction.ValidForGroups = false;
            
            _defenseAction.Enabled = delegate(IMyTerminalBlock b)
            {
                if (!Utils.IsCoreBlock(b as IMyFunctionalBlock)) return false;
                return IsMainCoreBlock(b);
            };
            
            _defenseAction.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowChatMessage("Could not trigger defense, main grid group match was not found??");
                    return;
                }
                Session.Networking.SendToServer(new PacketAction{ActionData = new ButtonAction {CubegridEntityId = b.CubeGrid.EntityId, Action = CoreActionType.Defense }});
            };

            _powerOverclockAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("ShipCore_ActivatePowerOverclock");
            _powerOverclockAction.Name = new StringBuilder("Activate Power Overclock");
            _powerOverclockAction.Icon = @"Textures\BoostButton_Sad_Static.png";
            _powerOverclockAction.ValidForGroups = false;

            _powerOverclockAction.Enabled = delegate(IMyTerminalBlock b)
            {
                if (!Utils.IsCoreBlock(b as IMyFunctionalBlock)) return false;
                return IsMainCoreBlock(b);
            };

            _powerOverclockAction.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowChatMessage("Could not trigger power overclock, main grid group match was not found??");
                    return;
                }

                Session.Networking.SendToServer(new PacketAction{ActionData = new ButtonAction {CubegridEntityId = b.CubeGrid.EntityId, Action = CoreActionType.PowerOverclock }});
            };
        }

        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (controls == null || _mainCoreCheckbox == null) return;

            ToggleMember(controls, _mainCoreCheckbox, Utils.IsCoreBlock(block as IMyFunctionalBlock));
        }

        private static void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (actions == null || _boostAction == null || _defenseAction == null || _powerOverclockAction == null) return;

            var isCoreBlock = Utils.IsCoreBlock(block as IMyFunctionalBlock);
            ToggleMember(actions, _boostAction, isCoreBlock);
            ToggleMember(actions, _defenseAction, isCoreBlock);
            ToggleMember(actions, _powerOverclockAction, isCoreBlock);
        }

        private static void ToggleMember<T>(List<T> list, T member, bool shouldBePresent)
        {
            var index = list.IndexOf(member);
            if (shouldBePresent)
            {
                if (index < 0)
                    list.Add(member);
            }
            else if (index >= 0)
            {
                list.RemoveAt(index);
            }
        }

        private static bool IsMainCoreBlock(IMyTerminalBlock block)
        {
            if (block?.CubeGrid == null) return false;
            if (!Session.IsServer)
            {
                GroupRuntimeState state;
                return RuntimeStateStore.TryGetByGrid(block.CubeGrid.EntityId, out state) &&
                       state.MainCoreBlockId == block.EntityId;
            }

            var group = block.GetGroupComponent();
            return group?.MainCoreComponent != null &&
                   group.MainCoreComponent.CoreBlock.EntityId == block.EntityId;
        }
    }
}
