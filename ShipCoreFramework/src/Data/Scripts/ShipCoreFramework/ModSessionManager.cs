using ProtoBuf;
// System
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
// Sandbox
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI;
using Sandbox.Definitions;
// VRage
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI.Network;
using VRage.Sync;


namespace ShipCoreFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ModSessionManager : MySessionComponentBase
    {
        public static readonly ModConfig Config = new ModConfig();
        public static Dictionary<long, ShipCoreLogic> ShipCoreLogics = new Dictionary<long, ShipCoreLogic>();
        
        public override void LoadData()
        {
            Config.LoadConfig();
            
            MyAPIGateway.Session.OnSessionReady += HookDamageHandler;
           //MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
        }
        /*
        private void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId, long factionId, long playerId)
        {
            if (action != MyFactionStateChange.FactionMemberKick && action != MyFactionStateChange.FactionMemberLeave) return;
            //Utils.Log($"FactionStateChanged: {action} from {fromFactionId} to {toFactionId} for faction {factionId} and player {playerId}");
            var factionGridLogics = ShipCoreLogics.Where(x => x.Value.OwningFaction?.FactionId == factionId).ToList();
            foreach (var gridLogic in factionGridLogics.Where(gridLogic => gridLogic.Value.OwningFaction.Members.Count < gridLogic.Value.ShipCore.MinPlayers))
            {
                gridLogic.Value.IsDisabled = true;
            }
        }*/

        protected override void UnloadData()
        {
            MyAPIGateway.Session.OnSessionReady -= HookDamageHandler;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond-100.0f;
            var ammoDefinitions = new List<string>{"Missile","LargeCalibreShell","MediumCalibreShell","LargeCaliber","AutocannonShell","LargeRailgunSlug","SmallRailgunSlug","SmallCaliber","PistolCaliber","Flare","FireworkBlue","FireworkGreen","FireworkRed","FireworkPink","FireworkYellow","FireworkRainbow","Shrapnel"};
            foreach(var ammoId in ammoDefinitions)
            {
                try{
                    var ammoDefinition = MyDefinitionManager.Static.GetAmmoDefinition(new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                    if (ammoDefinition != null){ammoDefinition.DesiredSpeed -= speedDifferential;}else{}//{Utils.Log($"AmmoType: {ammoId} was not sucessfully adjusted to match maxspeed");}
                }catch{}//{Utils.Log($"Vanilla AmmoType {ammoId} is missing.");}
            }

            ShipCoreLogics.Clear();
            //GridsPerFactionClassManager.Reset();
            //GridsPerPlayerClassManager.Reset();
            
            Config.SaveConfig();
        }
        

        private void HookDamageHandler()
        {
           // MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(99, CubeGridModifiers.GridClassDamageHandler);
        }
    }
}