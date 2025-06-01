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
        public static ModConfig Config = new ModConfig();
        public static Dictionary<long, ShipCoreLogic> ShipCoreLogics = new Dictionary<long, ShipCoreLogic>();
        public readonly Queue<IMyCubeGrid> ToBeInitialized = new Queue<IMyCubeGrid>();
        
        public override void LoadData()
        {
            /*
            Comms = new Comms(Settings.COMMS_MESSAGE_ID);
            if (Constants.IsServer)
            {
                var config = ModConfig.LoadConfig() ?? DefaultGridClassConfig.DefaultModConfig;
                LoadConfig(config);
                ModConfig.SaveConfig(Config, Constants.ConfigFilename);
            }
            else
            {
                Comms.RequestConfig();
            }*/
            Config = Config.LoadConfig();
            Config.SaveConfig();
            MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = Config.MaxPossibleSpeedMetersPerSecond;
            MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = Config.MaxPossibleSpeedMetersPerSecond;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
            var ammoDefinitions = new List<string> { "Missile","LargeCalibreShell","MediumCalibreShell","LargeCaliber","AutocannonShell","LargeRailgunSlug","SmallRailgunSlug","SmallCaliber","PistolCaliber" };
            foreach(var ammoId in ammoDefinitions)
            {
                var ammoDefinition = MyDefinitionManager.Static.GetAmmoDefinition(new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                if (ammoDefinition != null)
                {
                    ammoDefinition.DesiredSpeed += speedDifferential;
                }
                else
                {
                    Utils.Log($"AmmoType: {ammoId} was not sucessfully adjusted to match maxspeed");
                }
            }
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += EntityRemoved;
            MyAPIGateway.Session.OnSessionReady += HookDamageHandler;
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
        }
        private void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId, long factionId, long playerId)
        {
            if (action != MyFactionStateChange.FactionMemberKick && action != MyFactionStateChange.FactionMemberLeave) return;
            Utils.Log($"FactionStateChanged: {action} from {fromFactionId} to {toFactionId} for faction {factionId} and player {playerId}");
            var factionGridLogics = ShipCoreLogics.Where(x => x.Value.OwningFaction?.FactionId == factionId).ToList();
            foreach (var gridLogic in factionGridLogics.Where(gridLogic => gridLogic.Value.OwningFaction.Members.Count < gridLogic.Value.ShipCore.MinPlayers))
            {
                gridLogic.Value.IsDisabled = true;
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Session.OnSessionReady -= HookDamageHandler;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond-100.0f;
            var ammoDefinitions = new List<string>{"Missile","LargeCalibreShell","MediumCalibreShell","LargeCaliber","AutocannonShell","LargeRailgunSlug","SmallRailgunSlug","SmallCaliber","PistolCaliber","Flare","FireworkBlue","FireworkGreen","FireworkRed","FireworkPink","FireworkYellow","FireworkRainbow","Shrapnel"};
            foreach(var ammoId in ammoDefinitions)
            {
                try{
                    var ammoDefinition = MyDefinitionManager.Static.GetAmmoDefinition(new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                    if (ammoDefinition != null){ammoDefinition.DesiredSpeed -= speedDifferential;}else{Utils.Log($"AmmoType: {ammoId} was not sucessfully adjusted to match maxspeed");}
                }catch{Utils.Log($"Vanilla AmmoType {ammoId} is missing.");}
            }

            ShipCoreLogics.Clear();
            //GridsPerFactionClassManager.Reset();
            //GridsPerPlayerClassManager.Reset();
            
            Config.SaveConfig();
        }

        private void EntityAdded(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null) return;
            Utils.Log($"EntityAdded: {grid.DisplayName}");
            ToBeInitialized.Enqueue(grid);
        }

        private void EntityRemoved(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null) return;
            if (!ShipCoreLogics.ContainsKey(grid.EntityId)) return;
            /*
            try
            {
                ShipCoreLogics[grid.EntityId].RemoveGridLogic();
            }
            catch
            {
                Utils.Log($"Cubegrid was not accessible in list due to silly witchcraft shenanigans:{grid.EntityId}");
            }*/
        }

        private void HookDamageHandler()
        {
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(99, CubeGridModifiers.GridClassDamageHandler);
        }

        public override void UpdateAfterSimulation()
        {
            if (Config == null) return;
            if (ToBeInitialized.Count < 1) return;
            if (MyAPIGateway.Session.GameplayFrameCounter < 1) return;

            var target = ToBeInitialized.Dequeue();
            if (target.Physics == null) return;

            var logic = new ShipCoreLogic();
            //logic.InitOnPhysicsChanged(target);
        }

    }
}