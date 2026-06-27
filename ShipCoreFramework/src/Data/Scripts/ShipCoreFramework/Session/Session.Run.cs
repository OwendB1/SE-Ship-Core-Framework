using System;
using System.Collections.Generic;
using NexusModAPI;
using Sandbox.Definitions;
using Sandbox.Game;
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
            
            var mechanicalGroups = new List<IMyGridGroupData>();
            var physicalGroups = new List<IMyGridGroupData>();
            MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Mechanical, mechanicalGroups);
            MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Physical, physicalGroups);

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
            MyAPIGateway.Utilities.MessageEnteredSender += Commands.OnChatCommand;
            if(IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
                Utils.Log("Ship Cores: Awaiting Commands From Clients");
            }
            if (IsServer) Config.SaveConfig();
        }

        protected override void UnloadData()
        {
            IsShuttingDown = true;
            ThreadWork.CancelAll("Session unload");
            MyAPIGateway.Session.OnSessionReady -= SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged -= FactionStateChanged;
            MyAPIGateway.Utilities.MessageEnteredSender -= Commands.OnChatCommand;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
            MyExplosions.OnExplosion -= CubeGridModifiers.HandleLightningExplosions;
            
            try //Because this throws a NRE in keen code if you alt-F4
            {
                MyAPIGateway.GridGroups.OnGridGroupCreated -= GridGroupsOnOnGridGroupCreated;
                MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupsOnOnGridGroupDestroyed;
            }
            catch { /**/ }

            UntrackAllPhysicalGridGroups();

            RevertAmmoSpeedAdjustments();
            CoreTerminalControls.Unregister();
            
            LimitsNexusSync.Stop();
            _myNexusApi?.Unload();
            _myNexusApi = null;

            ModAPI.Close();

            PerFactionManager.Reset();
            PerPlayerManager.Reset();
            PerManifestGroupManager.Reset();
            if (IsServer) Config.SaveConfig();
            Networking?.Unregister();
            Networking = null;
            ThreadWork.Clear();
            
            foreach (var kvp in GroupDict)
            {
                kvp.Value.Clean();
            }
            GroupDict.Clear();
            GameThreadId = 0;
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
            if (Config.SelectedNoCore == null)
            {
                Utils.ShowNotification("There is no No Core currently selected. Make sure to select one and reload the world!!", 0, 20000, true);
                return;
            }
            if(!HasStarted) HasStarted = true;
            CoreTerminalControls.RegisterOnce();

            _tick++;
            CurrentTick = _tick;
            ThreadWork.FlushPendingWrites(ThreadWork.CountsCategory);
            ThreadWork.FlushPendingWrites(null, MaxQueuedStateWorkPerTick);
            foreach (KeyValuePair<IMyGridGroupData, GroupComponent> kvp in GroupDict)
            {
                GroupComponent group = kvp.Value;
                if (group != null)
                    group.RefreshGameThreadStateCache();
            }

            RefreshMassCacheBatch();
            if (IsServer) LimitsNexusSync.RunPeriodicSnapshotTick();
            bool runNfz = _tick % 10 == 0;
            bool doPunish = _tick % 60 == 0;

            if (doPunish)
            {
                foreach (KeyValuePair<IMyGridGroupData, GroupComponent> kvp in GroupDict)
                {
                    GroupComponent group = kvp.Value;
                    if (group != null)
                        group.RefreshPunishmentState();
                }
            }

            MyAPIGateway.Parallel.StartBackground(() =>
            {
                var speedBatch = SpeedEnforcement.CreateBatch();
                MyAPIGateway.Parallel.ForEach(GroupDict, kvp =>
                {
                    kvp.Value.UpdateDeactivationState();
                    kvp.Value.RunBoostTimerTick();
                    kvp.Value.RunActiveDefenseTimerTick();
                    kvp.Value.RunLimitedBlockPunishmentTick();
                    kvp.Value.RunExternalLimitValidationTick();
                    SpeedEnforcement.EnforceSpeedLimit(kvp.Value, speedBatch);
                    if (runNfz) NoFlyZoneEnforcement.EnforceNoFlyZones(kvp.Value, doPunish);
                });

                SpeedEnforcement.DispatchBatch(speedBatch);
            });
        }

        private void RefreshMassCacheBatch()
        {
            int index = 0;
            int checkedGroups = 0;
            int refreshedGroups = 0;
            bool sawAnyGroup = false;
            bool stoppedEarly = false;

            foreach (KeyValuePair<IMyGridGroupData, GroupComponent> kvp in GroupDict)
            {
                sawAnyGroup = true;
                if (index < _massCacheRefreshCursor)
                {
                    index++;
                    continue;
                }

                index++;
                checkedGroups++;
                GroupComponent group = kvp.Value;
                if (group != null && group.RefreshScheduledMassCache())
                    refreshedGroups++;

                if (checkedGroups >= MaxMassCacheGroupsCheckedPerTick ||
                    refreshedGroups >= MaxMassCacheRefreshesPerTick)
                {
                    stoppedEarly = true;
                    break;
                }
            }

            if (!sawAnyGroup || checkedGroups == 0)
            {
                _massCacheRefreshCursor = 0;
                return;
            }

            _massCacheRefreshCursor = stoppedEarly ? index : 0;
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

        internal static void BroadcastConfigToClients()
        {
            if (!IsServer || !MpActive)
                return;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            var packet = new PacketSendConfig(MyAPIGateway.Utilities.SerializeToXML(Config));

            foreach (var p in players)
                Networking.SendToPlayer(packet, p.SteamUserId);
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
