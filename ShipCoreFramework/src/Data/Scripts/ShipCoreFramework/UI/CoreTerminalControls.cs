using System.Linq;
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
            if(_done)
                return;
            _done = true;
            
            CreateControls();
            CreateActions();
        }

        private static void CreateControls()
        {
            if (Session.LocalPlayer == null) return;

            var checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>(IdPrefix + "IsMainCoreCheckbox");
            checkbox.Title = MyStringId.GetOrCompute("Main Core");
            checkbox.Tooltip = MyStringId.GetOrCompute("Mark this core as the main core for the grid.");
            checkbox.SupportsMultipleBlocks = false;

            checkbox.Getter = b => b.GetGroupComponent()?.MainCoreComponent?.IsMainCore ?? false;

            checkbox.Setter = delegate(IMyTerminalBlock b, bool val)
            {
                if (!val) return; // Unchecking not supported

                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowMessage("Could not sync box, main grid group match was not found??");
                    return;
                }
                
                CoreComponent coreComp;
                var getCoreComp = groupComp.CoreDictionary.TryGetValue((MyCubeBlock)b, out coreComp);
                if (!getCoreComp)
                {
                    Utils.ShowMessage("Could not sync box, main core component match was not found??");
                    return;
                }
                
                foreach (var kvp in groupComp.CoreDictionary.Where(kvp => kvp.Value.IsMainCore))
                {
                    kvp.Value.IsMainCore = false;
                    kvp.Value.CoreBlock.RefreshCustomInfo();
                }

                coreComp.IsMainCore = true;
                b.RefreshCustomInfo();
            };

            checkbox.Enabled = delegate(IMyTerminalBlock b)
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowMessage("Could not sync box, main grid group match was not found??");
                    return false;
                }
                CoreComponent coreComp;
                var succeed = groupComp.CoreDictionary.TryGetValue((MyCubeBlock)b, out coreComp);
                return succeed && coreComp.IsMainCore;
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
                if (groupComp != null) return groupComp.MainCoreComponent.IsMainCore;
                Utils.ShowMessage("Could not sync box, main grid group match was not found??");
                return false;
            };
            
            boost.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowMessage("Could not trigger boost, main grid group match was not found??");
                    return;
                }
                Session.Networking.SendToServer(new PacketAction{actionData = new ButtonAction {Group = groupComp.MyGroup, IsBoost = true }});
            };
            MyAPIGateway.TerminalControls.AddAction<IMyBeacon>(boost);

            var defense = MyAPIGateway.TerminalControls.CreateAction<IMyBeacon>("ShipCore_ActivateDefense");
            defense.Name = new StringBuilder("Activate Defense");
            defense.Icon = @"Textures\BoostButton_Sad_Static.png";
            defense.ValidForGroups = false;
            
            defense.Enabled = delegate(IMyTerminalBlock b)
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp != null) return groupComp.MainCoreComponent.IsMainCore;
                Utils.ShowMessage("Could not sync box, main grid group match was not found??");
                return false;
            };
            
            defense.Action = b =>
            {
                var groupComp = b.GetGroupComponent();
                if (groupComp == null)
                {
                    Utils.ShowMessage("Could not trigger defense, main grid group match was not found??");
                    return;
                }
                Session.Networking.SendToServer(new PacketAction{actionData = new ButtonAction {Group = groupComp.MyGroup, IsBoost = false }});
            };
            MyAPIGateway.TerminalControls.AddAction<IMyBeacon>(defense);
        }
    }
}