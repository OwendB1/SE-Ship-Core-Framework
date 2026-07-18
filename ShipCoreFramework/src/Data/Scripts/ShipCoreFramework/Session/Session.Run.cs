using System;
using System.Collections.Generic;
using NexusModAPI;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation, 0)]
    public partial class Session : MySessionComponentBase
    {
        private static readonly string[] AmmoDefinitionIds =
        {
            "Missile", "LargeCalibreShell", "MediumCalibreShell", "LargeCaliber", "AutocannonShell",
            "LargeRailgunSlug", "SmallRailgunSlug", "SmallCaliber", "PistolCaliber", "Shrapnel"
        };
        public override void BeforeStart()
        {
            ModAPI.Initialize();
            if (Config.SelectedNoCore == null) return;
            MyAPIGateway.GridGroups.OnGridGroupCreated += GridGroupsOnOnGridGroupCreated;
            MyAPIGateway.GridGroups.OnGridGroupDestroyed += GridGroupsOnOnGridGroupDestroyed;
            MyCubeGrid.OnBlocksChangeFinishedGlobally += MyCubeGridOnBlocksChangeFinishedGlobally;
            
            var mechanicalGroups = new List<IMyGridGroupData>();
            var physicalGroups = new List<IMyGridGroupData>();
            MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Mechanical, mechanicalGroups);
            MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Physical, physicalGroups);
            Utils.Log("BeforeStart: found " + mechanicalGroups.Count + " mechanical groups and " +
                      physicalGroups.Count + " physical groups for initial scan.", 1);

            IsInitialGroupScan = true;
            try
            {
                MyAPIGateway.Parallel.ForEach(mechanicalGroups, mechanicalGroup =>
                {
                    GridGroupsOnOnGridGroupCreated(mechanicalGroup);
                });

                MyAPIGateway.Parallel.ForEach(physicalGroups, physicalGroup =>
                {
                    GridGroupsOnOnGridGroupCreated(physicalGroup);
                });
            }
            finally
            {
                IsInitialGroupScan = false;
            }
        }
        
        public override void LoadData()
        {
            GameThreadId = Environment.CurrentManagedThreadId;
            IsShuttingDown = false;
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = (MpActive && MyAPIGateway.Multiplayer.IsServer) || !MpActive;
            IsClient = (MpActive && !MyAPIGateway.Utilities.IsDedicated) || !MpActive;

            Networking.Register();
            Config.LoadConfig(IsServer);
            if (IsClient)
            {
                try
                {
                    ApplyHighResolutionLcdDefinitions();
                }
                catch (Exception e)
                {
                    Utils.Log("High-resolution LCD setup skipped: " + e.Message, 1);
                }
            }

            Utils.Log("LoadData: MpActive=" + MpActive + ", IsServer=" + IsServer +
                      ", IsClient=" + IsClient + ", Dedicated=" + MyAPIGateway.Utilities.IsDedicated + ".", 1);
            _myNexusApi = new NexusAPI(OnNexusEnabled);

            if (IsServer)
            {
                ApplyConfigToDefinitions();
            }
            else if (MpActive)
            {
                Networking.SendToServer(new PacketRequestConfig(), onlyToServer: true);
            }
            else
            {
                ApplyConfigToDefinitions();
            }
            
            MyAPIGateway.Session.OnSessionReady += SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
            MyAPIGateway.Session.Factions.FactionCreated += FactionCreated;
            MyAPIGateway.Session.Factions.FactionEdited += FactionEdited;
            MyAPIGateway.Utilities.MessageEnteredSender += Commands.OnChatCommand;
            if(IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
                Utils.Log("Ship Cores: Awaiting Commands From Clients", 1);
            }
            if (IsServer) Config.SaveConfig();
        }

        protected override void UnloadData()
        {
            Utils.Log("UnloadData: shutting down Ship Core Framework session.", 1);
            IsShuttingDown = true;
            MyAPIGateway.Session.OnSessionReady -= SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged -= FactionStateChanged;
            MyAPIGateway.Session.Factions.FactionCreated -= FactionCreated;
            MyAPIGateway.Session.Factions.FactionEdited -= FactionEdited;
            MyAPIGateway.Utilities.MessageEnteredSender -= Commands.OnChatCommand;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
            MyExplosions.OnExplosion -= CubeGridModifiers.HandleLightningExplosions;
            
            try //Because this throws a NRE in keen code if you alt-F4
            {
                MyAPIGateway.GridGroups.OnGridGroupCreated -= GridGroupsOnOnGridGroupCreated;
                MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupsOnOnGridGroupDestroyed;
                MyCubeGrid.OnBlocksChangeFinishedGlobally -= MyCubeGridOnBlocksChangeFinishedGlobally;
            }
            catch { /**/ }

            UntrackAllPhysicalGridGroups();

            RevertAmmoSpeedAdjustments();
            RevertHighResolutionLcdDefinitions();
            CoreTerminalControls.Unregister();
            
            LimitsNexusSync.Stop();
            _myNexusApi?.Unload();
            _myNexusApi = null;

            ModAPI.Close();
            RuntimeStateStore.Clear();
            ResetRuntimeStateSync();

            PerFactionManager.Reset();
            PerPlayerManager.Reset();
            PerManifestGroupManager.Reset();
            if (IsServer) Config.SaveConfig();
            Networking?.Unregister();
            Networking = null;
            
            foreach (var kvp in GroupDict)
            {
                kvp.Value.Clean();
            }
            GroupDict.Clear();
            GameThreadId = 0;
            Utils.Log("UnloadData: Ship Core Framework session unloaded.", 1);
        }
        
        public override void UpdateAfterSimulation()
        {
            if (Config.SelectedNoCore == null)
            {
                Utils.ShowNotification("There is no No Core currently selected. Make sure to select one and reload the world!!", 0, 20000, true);
                return;
            }
            if(!HasStarted) HasStarted = true;
            CoreTerminalControls.RegisterOnce();

            _tick++;
            CurrentTick = _tick;
            if (IsClient)
                RunClientSimulationTick();

            if (!IsServer) return;
            RunServerSimulationTick();
        }

        internal static void ApplyConfigToDefinitions()
        {
            if (MyDefinitionManager.Static?.EnvironmentDefinition != null)
            {
                MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = Config.MaxPossibleSpeedMetersPerSecond;
                MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = Config.MaxPossibleSpeedMetersPerSecond;
            }

            var newDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
            var delta = newDifferential - AppliedSpeedDifferential;
            if (Math.Abs(delta) < 0.001f)
                return;

            foreach (var ammoId in AmmoDefinitionIds)
            {
                try
                {
                    var ammoDefinition = MyDefinitionManager.Static?.GetAmmoDefinition(
                        new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                    if (ammoDefinition != null)
                        ammoDefinition.DesiredSpeed += delta;
                }
                catch
                {
                    // Ignore missing ammo definitions.
                }
            }

            AppliedSpeedDifferential = newDifferential;
        }

        private static void RevertAmmoSpeedAdjustments()
        {
            var delta = -AppliedSpeedDifferential;
            if (Math.Abs(delta) < 0.001f)
                return;

            foreach (var ammoId in AmmoDefinitionIds)
            {
                try
                {
                    var ammoDefinition = MyDefinitionManager.Static.GetAmmoDefinition(
                        new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                    if (ammoDefinition != null)
                        ammoDefinition.DesiredSpeed += delta;
                }
                catch
                {
                    // Ignore missing ammo definitions.
                }
            }

            AppliedSpeedDifferential = 0f;
        }
        internal static void RefreshGroupsAfterConfigChanged()
        {
            var groups = new List<GroupComponent>(GroupDict.Values);
            foreach (var group in groups)
            {
                if (group == null) continue;
                group.OnConfigChanged();
            }
        }
    }
}
