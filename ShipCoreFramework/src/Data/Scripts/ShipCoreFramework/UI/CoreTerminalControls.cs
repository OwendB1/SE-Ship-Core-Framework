using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
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
                var group = b.GetGroupComponent();
                CoreComponent cc;
                var cubeBlock = b as MyCubeBlock;
                return group != null
                       && cubeBlock != null
                       && group.CoreDictionary.TryGetValue(cubeBlock, out cc)
                       && cc.IsMainCore;
            };

            _mainCoreCheckbox.Enabled = b => {
                var group = b.GetGroupComponent();
                if (group == null) return false;
                CoreComponent cc;
                var cubeBlock = b as MyCubeBlock;
                return cubeBlock != null && group.CoreDictionary.TryGetValue(cubeBlock, out cc) && !cc.IsMainCore;
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

                var currentCoreOwningFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(b.OwnerId);
                if (currentCoreOwningFaction == null)
                {
                    if (groupComp.OwnerId != b.OwnerId)
                    {
                        Utils.ShowChatMessage("Cannot transfer main core because its owner is not the same as the main core. Destroy main core first!");
                        return;
                    }
                } else if (currentCoreOwningFaction.FactionId != groupComp.OwningFaction.FactionId)
                {
                    Utils.ShowChatMessage("Owning faction of this core is unequal to current main core. Destroy main core first!");
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
                var groupComp = b.GetGroupComponent();
                if (groupComp != null) return groupComp.MainCoreComponent?.IsMainCore ?? false;
                Utils.ShowChatMessage("Could not sync box, main grid group match was not found??");
                return false;
            };
            
            _boostAction.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                Utils.ShowChatMessage("Test: " + b.CustomName);
                if (groupComp == null)
                {
                    Utils.ShowChatMessage("Could not trigger boost, main grid group match was not found??");
                    return;
                }
                
                Session.Networking.SendToServer(new PacketAction{ActionData = new ButtonAction {CubegridEntityId = b.CubeGrid.EntityId, IsBoost = true }});
            };

            _defenseAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("ShipCore_ActivateDefense");
            _defenseAction.Name = new StringBuilder("Activate Defense");
            _defenseAction.Icon = @"Textures\BoostButton_Sad_Static.png";
            _defenseAction.ValidForGroups = false;
            
            _defenseAction.Enabled = delegate(IMyTerminalBlock b)
            {
                if (!Utils.IsCoreBlock(b as IMyFunctionalBlock)) return false;
                var groupComp = b.GetGroupComponent();
                if (groupComp != null) return groupComp.MainCoreComponent?.IsMainCore ?? false;
                Utils.ShowChatMessage("Could not sync box, main grid group match was not found??");
                return false;
            };
            
            _defenseAction.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowChatMessage("Could not trigger defense, main grid group match was not found??");
                    return;
                }
                Session.Networking.SendToServer(new PacketAction{ActionData = new ButtonAction {CubegridEntityId = b.CubeGrid.EntityId, IsBoost = false }});
            };
        }

        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (controls == null || _mainCoreCheckbox == null) return;

            ToggleMember(controls, _mainCoreCheckbox, Utils.IsCoreBlock(block as IMyFunctionalBlock));
        }

        private static void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (actions == null || _boostAction == null || _defenseAction == null) return;

            var isCoreBlock = Utils.IsCoreBlock(block as IMyFunctionalBlock);
            ToggleMember(actions, _boostAction, isCoreBlock);
            ToggleMember(actions, _defenseAction, isCoreBlock);
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
    }
}
