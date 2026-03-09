using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;

namespace ShipCoreFramework
{
    public static class CoreTerminalControls
    {
        private const string IdPrefix = "ShipCoreFramework_CoreTerminalControls_";
        private static bool _done;
        
        public static void RegisterOnce()
        {
            if (Session.LocalPlayer == null) return;
            if(_done)
                return;
            _done = true;
            
            
            CreateControls();
            CreateActions();
        }

        private static void CreateControls()
        {
            var checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>(IdPrefix + "IsMainCoreCheckbox");
            checkbox.Title = MyStringId.GetOrCompute("Main Core");
            checkbox.Tooltip = MyStringId.GetOrCompute("Mark this core as the main core for the grid.");
            checkbox.SupportsMultipleBlocks = false;

            checkbox.Getter = b => {
                var group = b.GetGroupComponent();
                CoreComponent cc;
                return group != null
                       && group.CoreDictionary.TryGetValue((MyCubeBlock)b, out cc)
                       && cc.IsMainCore;
            };

            checkbox.Enabled = TerminalChainedDelegate.Create(checkbox.Enabled, b => {
                var group = b.GetGroupComponent();
                if (group == null) return false;
                CoreComponent cc;
                return group.CoreDictionary.TryGetValue((MyCubeBlock)b, out cc) && !cc.IsMainCore;
            });

            checkbox.Setter = (b, val) =>
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

            MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(checkbox);
        }
        
        private static void CreateActions()
        {
            var boost = MyAPIGateway.TerminalControls.CreateAction<IMyBeacon>("ShipCore_ActivateBoost");
            boost.Name = new StringBuilder("Activate Boost");
            boost.Icon = @"Textures\BoostButton_Sad_Static.png";
            boost.ValidForGroups = false;
            
            boost.Enabled = delegate(IMyTerminalBlock b)
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp != null) return groupComp.MainCoreComponent?.IsMainCore ?? false;
                Utils.ShowChatMessage("Could not sync box, main grid group match was not found??");
                return false;
            };
            
            boost.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowChatMessage("Could not trigger boost, main grid group match was not found??");
                    return;
                }
                Session.Networking.SendToServer(new PacketAction{ActionData = new ButtonAction {CubegridEntityId = b.CubeGrid.EntityId, IsBoost = true }});
            };
            MyAPIGateway.TerminalControls.AddAction<IMyBeacon>(boost);

            var defense = MyAPIGateway.TerminalControls.CreateAction<IMyBeacon>("ShipCore_ActivateDefense");
            defense.Name = new StringBuilder("Activate Defense");
            defense.Icon = @"Textures\BoostButton_Sad_Static.png";
            defense.ValidForGroups = false;
            
            defense.Enabled = delegate(IMyTerminalBlock b)
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp != null) return groupComp.MainCoreComponent?.IsMainCore ?? false;
                Utils.ShowChatMessage("Could not sync box, main grid group match was not found??");
                return false;
            };
            
            defense.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowChatMessage("Could not trigger defense, main grid group match was not found??");
                    return;
                }
                Session.Networking.SendToServer(new PacketAction{ActionData = new ButtonAction {CubegridEntityId = b.CubeGrid.EntityId, IsBoost = false }});
            };
            MyAPIGateway.TerminalControls.AddAction<IMyBeacon>(defense);
        }
    }
}
