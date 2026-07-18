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
        private const int HighResolutionLcdTextureSize = 1024;
        private static readonly string[] HighResolutionLcdSubtypes =
        {
            "LargeLCDPanel3x3", "LargeLCDPanel5x3", "LargeLCDPanel5x5"
        };
        private static readonly List<LcdDefinitionTextureState> OriginalLcdTextureStates =
            new List<LcdDefinitionTextureState>();

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
            if (!IsClient) return;
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
            if (IsClient)
            {
                CoreTypeLCDScript.RunFrameScrollUpdate();
                NotificationInstance.RunCountdownTick();
            }

            if (!IsServer) return;

            foreach (var kvp in GroupDict)
            {
                var group = kvp.Value;
                if (group != null)
                {
                    group.RefreshGameThreadStateCache();
                    group.RunMissingCoreRescanTick();
                }
            }

            RefreshMassCacheBatch();
            if (IsServer) LimitsNexusSync.RunPeriodicSnapshotTick();
            var runNfz = _tick % 10 == 0;
            var doPunish = _tick % 60 == 0;

            if (doPunish)
            {
                foreach (var kvp in GroupDict)
                {
                    var group = kvp.Value;
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
                    kvp.Value.RunPowerOverclockTimerTick();
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
            var index = 0;
            var checkedGroups = 0;
            var refreshedGroups = 0;
            var sawAnyGroup = false;
            var stoppedEarly = false;

            foreach (var kvp in GroupDict)
            {
                sawAnyGroup = true;
                if (index < _massCacheRefreshCursor)
                {
                    index++;
                    continue;
                }

                index++;
                checkedGroups++;
                var group = kvp.Value;
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

        private static void ApplyHighResolutionLcdDefinitions()
        {
            if (OriginalLcdTextureStates.Count > 0 || MyDefinitionManager.Static == null)
                return;

            for (var subtypeIndex = 0; subtypeIndex < HighResolutionLcdSubtypes.Length; subtypeIndex++)
            {
                MyCubeBlockDefinition cubeDefinition;
                try
                {
                    var id = new MyDefinitionId(typeof(MyObjectBuilder_TextPanel),
                        HighResolutionLcdSubtypes[subtypeIndex]);
                    if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(id, out cubeDefinition))
                        continue;
                }
                catch (Exception e)
                {
                    Utils.Log("LCD definition lookup failed for " + HighResolutionLcdSubtypes[subtypeIndex] +
                              ": " + e.Message, 1);
                    continue;
                }

                var definition = cubeDefinition as MyTextPanelDefinition;
                if (definition == null || definition.ScreenAreas == null || definition.ScreenAreas.Count == 0)
                    continue;

                var state = new LcdDefinitionTextureState(definition);
                OriginalLcdTextureStates.Add(state);
                definition.TextureResolution = Math.Max(definition.TextureResolution, HighResolutionLcdTextureSize);

                for (var i = 0; i < definition.ScreenAreas.Count; i++)
                {
                    var area = definition.ScreenAreas[i];
                    if (area != null)
                        area.TextureResolution = Math.Max(area.TextureResolution, HighResolutionLcdTextureSize);
                }
            }
        }

        private static void RevertHighResolutionLcdDefinitions()
        {
            for (var stateIndex = 0; stateIndex < OriginalLcdTextureStates.Count; stateIndex++)
            {
                var state = OriginalLcdTextureStates[stateIndex];
                var definition = state.Definition;
                if (definition == null)
                    continue;

                if (definition.TextureResolution == HighResolutionLcdTextureSize)
                    definition.TextureResolution = state.TextureResolution;

                if (definition.ScreenAreas != null)
                {
                    var count = Math.Min(definition.ScreenAreas.Count, state.ScreenAreaTextureResolutions.Length);
                    for (var i = 0; i < count; i++)
                    {
                        var area = definition.ScreenAreas[i];
                        if (area != null && area.TextureResolution == HighResolutionLcdTextureSize)
                            area.TextureResolution = state.ScreenAreaTextureResolutions[i];
                    }
                }
            }

            OriginalLcdTextureStates.Clear();
        }

        private sealed class LcdDefinitionTextureState
        {
            internal readonly MyTextPanelDefinition Definition;
            internal readonly int TextureResolution;
            internal readonly int[] ScreenAreaTextureResolutions;

            internal LcdDefinitionTextureState(MyTextPanelDefinition definition)
            {
                Definition = definition;
                TextureResolution = definition.TextureResolution;
                ScreenAreaTextureResolutions = new int[definition.ScreenAreas.Count];
                for (var i = 0; i < definition.ScreenAreas.Count; i++)
                {
                    var area = definition.ScreenAreas[i];
                    ScreenAreaTextureResolutions[i] = area != null ? area.TextureResolution : 0;
                }
            }
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
