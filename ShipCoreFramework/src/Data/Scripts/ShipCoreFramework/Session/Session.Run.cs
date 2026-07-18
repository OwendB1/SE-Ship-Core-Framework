using System;
using System.Collections.Generic;
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
        public override void BeforeStart()
        {
            ModAPI.Initialize();
            if (Config.SelectedNoCore == null) return;
            MyAPIGateway.GridGroups.OnGridGroupCreated += GridGroupsOnOnGridGroupCreated;
            MyAPIGateway.GridGroups.OnGridGroupDestroyed += GridGroupsOnOnGridGroupDestroyed;
            MyCubeGrid.OnBlocksChangeFinishedGlobally += MyCubeGridOnBlocksChangeFinishedGlobally;
            
            var initialGroups = new List<IMyGridGroupData>();
            MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Mechanical, initialGroups);
            if (IsServer)
                AppendInitialPhysicalGroups(initialGroups);
            Utils.Log("BeforeStart: found " + initialGroups.Count + " grid groups for initial scan.", 1);

            IsInitialGroupScan = true;
            try
            {
                MyAPIGateway.Parallel.ForEach(initialGroups, group =>
                {
                    GridGroupsOnOnGridGroupCreated(group);
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
            _tick = 0;
            CurrentTick = 0;
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = (MpActive && MyAPIGateway.Multiplayer.IsServer) || !MpActive;
            IsClient = (MpActive && !MyAPIGateway.Utilities.IsDedicated) || !MpActive;

            if (Networking == null)
                Networking = new Networking(32124);
            Networking.Register();
            Config.LoadConfig(IsServer);
            if (IsClient)
                LoadClientData();
            if (IsServer)
                LoadServerData();

            Utils.Log("LoadData: MpActive=" + MpActive + ", IsServer=" + IsServer +
                      ", IsClient=" + IsClient + ", Dedicated=" + MyAPIGateway.Utilities.IsDedicated + ".", 1);
            if (IsClient && !IsServer && MpActive)
            {
                Networking.SendToServer(new PacketRequestConfig(), onlyToServer: true);
            }
        }

        protected override void UnloadData()
        {
            Utils.Log("UnloadData: shutting down Ship Core Framework session.", 1);
            IsShuttingDown = true;
            
            try //Because this throws a NRE in keen code if you alt-F4
            {
                MyAPIGateway.GridGroups.OnGridGroupCreated -= GridGroupsOnOnGridGroupCreated;
                MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupsOnOnGridGroupDestroyed;
                MyCubeGrid.OnBlocksChangeFinishedGlobally -= MyCubeGridOnBlocksChangeFinishedGlobally;
            }
            catch { /**/ }

            if (IsClient)
                UnloadClientData();
            if (IsServer)
                UnloadServerData();

            RevertAmmoSpeedAdjustments();
            ModAPI.Close();
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
                if (IsServer)
                    NotifyMissingNoCore();
                return;
            }

            _tick++;
            CurrentTick = _tick;
            if (IsClient)
                RunClientSimulationTick();

            if (!IsServer) return;
            if (!HasStarted) HasStarted = true;
            RunServerSimulationTick();
        }
    }
}
