using System.Collections.Generic;
using NexusModAPI;
using ParallelTasks;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation, 0)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            Networking.Register();
            MyAPIGateway.GridGroups.OnGridGroupCreated += GridGroupsOnOnGridGroupCreated;
            MyAPIGateway.GridGroups.OnGridGroupDestroyed += GridGroupsOnOnGridGroupDestroyed;
            
            var groupStartList = new List<IMyGridGroupData>();
            MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Logical, groupStartList);
            foreach(var group in groupStartList) GridGroupsOnOnGridGroupCreated(group);
            TickScheduler.Schedule(() =>
            {
                MyAPIGateway.Parallel.ForEach(GroupDict, kvp =>
                {
                    kvp.Value.ApplyModifiers(kvp.Value.Modifiers);
                });
            }, 10);
        }
        
        public override void LoadData()
        {
            MPActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = (MPActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            IsClient = (MPActive && !MyAPIGateway.Utilities.IsDedicated) || !MPActive;
            
            Config.LoadConfig();
            _myNexusApi = new NexusAPI(OnNexusEnabled);
            MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = Config.MaxPossibleSpeedMetersPerSecond;
            MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = Config.MaxPossibleSpeedMetersPerSecond;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
            var ammoDefinitions = new List<string>
            {
                "Missile", "LargeCalibreShell", "MediumCalibreShell", "LargeCaliber", "AutocannonShell",
                "LargeRailgunSlug", "SmallRailgunSlug", "SmallCaliber", "PistolCaliber"
            };
            
            foreach (var ammoId in ammoDefinitions)
            {
                var ammoDefinition =
                    MyDefinitionManager.Static.GetAmmoDefinition(
                        new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                if (ammoDefinition != null)
                    ammoDefinition.DesiredSpeed += speedDifferential;
                else
                    Utils.Log($"AmmoType: {ammoId} was not successfully adjusted to match maxspeed");
            }
            
            MyAPIGateway.Session.OnSessionReady += SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
            MyAPIGateway.Utilities.MessageEnteredSender += Commands.OnChatCommand;
            if(IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
                Utils.Log("Ship Cores: Awaiting Commands From Clients");
            }
            Config.SaveConfig();
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Session.OnSessionReady -= SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged -= FactionStateChanged;
            MyAPIGateway.Utilities.MessageEnteredSender -= Commands.OnChatCommand;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
            if(IsServer)
            {
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
                
                try //Because this throws a NRE in keen code if you alt-F4
                {
                    MyAPIGateway.GridGroups.OnGridGroupCreated -= GridGroupsOnOnGridGroupCreated;
                    MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupsOnOnGridGroupDestroyed;
                }
                catch { /**/ }
            }
            var ammoDefinitions = new List<string>
            {
                "Missile", "LargeCalibreShell", "MediumCalibreShell", "LargeCaliber", "AutocannonShell",
                "LargeRailgunSlug", "SmallRailgunSlug", "SmallCaliber", "PistolCaliber", "Flare", "FireworkBlue",
                "FireworkGreen", "FireworkRed", "FireworkPink", "FireworkYellow", "FireworkRainbow", "Shrapnel"
            };
            foreach (var ammoId in ammoDefinitions)
                try
                {
                    var ammoDefinition =
                        MyDefinitionManager.Static.GetAmmoDefinition(
                            new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                    if (ammoDefinition != null)
                        ammoDefinition.DesiredSpeed -= speedDifferential;
                    else
                        Utils.Log($"AmmoType: {ammoId} was not sucessfully adjusted to match maxspeed");
                }
                catch
                {
                    Utils.Log($"Vanilla AmmoType {ammoId} is missing.");
                }
            
            LimitsNexusSync.Stop();
            _myNexusApi?.Unload();
            _myNexusApi = null;
            
            GridsPerFactionManager.Reset();
            GridsPerPlayerManager.Reset();
            Config.SaveConfig();
            Networking?.Unregister();
            Networking = null;
            GroupDict.Clear();
        }
        
        private void OnNexusEnabled()
        {
            if (_started) return;
            if (!IsServer) return;
            _started = true;
            LimitsNexusSync.Start(_myNexusApi);
            LimitsNexusSync.BroadcastSnapshot();
        }
        
        public override void UpdateAfterSimulation()
        {
            TickScheduler.Update1();
            Utils.ProcessUiQueue();
            CoreTerminalControls.RegisterOnce(); 
            MyAPIGateway.Parallel.StartBackground(() =>
            {
                MyAPIGateway.Parallel.ForEach(GroupDict, kvp =>
                {
                    SpeedEnforcement.EnforceSpeedLimit(kvp.Value);
                });
            });
        }
    }
}