using System.Collections.Generic;
using System.Threading;
using NexusModAPI;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static void AppendInitialPhysicalGroups(List<IMyGridGroupData> groups)
        {
            var physicalGroups = new List<IMyGridGroupData>();
            MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Physical, physicalGroups);
            groups.AddRange(physicalGroups);
        }

        private void LoadServerData()
        {
            HasStarted = false;
            _startedNexus = false;
            _serverRuntimeDataLoaded = false;
            Interlocked.Exchange(ref _serverSimulationBatchRunning, 0);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);

            if (Config.SelectedNoCore != null)
            {
                _serverRuntimeDataLoaded = true;
                _myNexusApi = new NexusAPI(OnNexusEnabled);
                ApplyConfigToDefinitions();

                MyAPIGateway.Session.OnSessionReady += SessionReady;
                MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
                MyAPIGateway.Session.Factions.FactionCreated += FactionCreated;
                MyAPIGateway.Session.Factions.FactionEdited += FactionEdited;
            }
            Utils.Log("Ship Cores: Awaiting Commands From Clients", 1);
            Config.SaveConfig();
        }

        private void UnloadServerData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);

            if (_serverRuntimeDataLoaded)
            {
                MyAPIGateway.Session.OnSessionReady -= SessionReady;
                MyAPIGateway.Session.Factions.FactionStateChanged -= FactionStateChanged;
                MyAPIGateway.Session.Factions.FactionCreated -= FactionCreated;
                MyAPIGateway.Session.Factions.FactionEdited -= FactionEdited;
                MyExplosions.OnExplosion -= CubeGridModifiers.HandleLightningExplosions;

                UntrackAllPhysicalGridGroups();
                LimitsNexusSync.Stop();
                if (_myNexusApi != null)
                    _myNexusApi.Unload();

                ResetRuntimeStateSync();
                PerFactionManager.Reset();
                PerPlayerManager.Reset();
                PerManifestGroupManager.Reset();
            }

            _myNexusApi = null;
            _startedNexus = false;
            _serverRuntimeDataLoaded = false;
            Config.SaveConfig();
        }
    }
}
