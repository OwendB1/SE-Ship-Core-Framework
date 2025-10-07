using System;
using System.Collections.Generic;
using NexusModAPI;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

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
            MyAPIGateway.Parallel.ForEach(groupStartList, GridGroupsOnOnGridGroupCreated);
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
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = (MpActive && MyAPIGateway.Multiplayer.IsServer) || !MpActive;
            IsClient = (MpActive && !MyAPIGateway.Utilities.IsDedicated) || !MpActive;
            
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
            IsShuttingDown = true;
            MyAPIGateway.Session.OnSessionReady -= SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged -= FactionStateChanged;
            MyAPIGateway.Utilities.MessageEnteredSender -= Commands.OnChatCommand;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
            
            try //Because this throws a NRE in keen code if you alt-F4
            {
                MyAPIGateway.GridGroups.OnGridGroupCreated -= GridGroupsOnOnGridGroupCreated;
                MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupsOnOnGridGroupDestroyed;
            }
            catch { /**/ }
            
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
            
            foreach (var kvp in GroupDict)
            {
                kvp.Value.Clean();
            }
            GroupDict.Clear();
        }
        
        private void OnNexusEnabled()
        {
            if (_startedNexus) return;
            if (!IsServer) return;
            _startedNexus = true;
            LimitsNexusSync.Start(_myNexusApi);
            LimitsNexusSync.BroadcastSnapshot();
        }

        public override void Draw()
        {
            var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            const double edgeProximity = 20000.0;

            var zones = Config.NoFlyZones;
            if (zones == null || zones.Count == 0) return;

            foreach (var z in zones)
            {
                var dist = Vector3D.Distance(z.Position, camPos);
                if (Math.Abs(dist - z.Radius) > edgeProximity) continue;

                var world = MatrixD.CreateTranslation(z.Position);
                var color = z.ForceOff ? new Color(1f, 0.2f, 0.2f, 0.05f) : new Color(0.2f, 0.6f, 1f, 0.05f);
                var edge = z.ForceOff ? new Color(1f, 0.1f, 0.1f, 1f) : new Color(0.3f, 0.7f, 1f, 1f);

                MySimpleObjectDraw.DrawTransparentSphere(ref world, (float)z.Radius, ref color, MySimpleObjectRasterizer.Solid,24, MatSphere, null, 1f);
                MySimpleObjectDraw.DrawTransparentSphere(ref world, (float)z.Radius, ref edge, MySimpleObjectRasterizer.Wireframe,64, null, MatLine, 0.75f);
            }

        }

        public override void UpdateAfterSimulation()
        {
            TickScheduler.Update1();
            if(!HasStarted) HasStarted = true;
            CoreTerminalControls.RegisterOnce();

            _tick++;
            var runNfz = _tick % 10 == 0;
            var doPunish = _tick % 60 == 0;

            MyAPIGateway.Parallel.StartBackground(() =>
            {
                MyAPIGateway.Parallel.ForEach(GroupDict, kvp =>
                {
                    kvp.Value.RunBoostTimerTick();
                    kvp.Value.RunActiveDefenseTimerTick();
                    SpeedEnforcement.EnforceSpeedLimit(kvp.Value);
                    
                    if (runNfz) NoFlyZones.EnforceNoFlyZones(kvp.Value, doPunish);
                });
            });
        }
    }
}