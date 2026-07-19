using System.Collections.Generic;
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
            _massCacheRefreshCursor = 0;
            _myNexusApi = new NexusAPI(OnNexusEnabled);
            ApplyConfigToDefinitions();

            MyAPIGateway.Session.OnSessionReady += SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
            MyAPIGateway.Session.Factions.FactionCreated += FactionCreated;
            MyAPIGateway.Session.Factions.FactionEdited += FactionEdited;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
            Utils.Log("Ship Cores: Awaiting Commands From Clients", 1);
            Config.SaveConfig();
        }

        private static void NotifyMissingNoCore()
        {
            Utils.ShowNotification(
                "There is no No Core currently selected. Make sure to select one and reload the world!!",
                0, 20000, true);
        }

        private void UnloadServerData()
        {
            MyAPIGateway.Session.OnSessionReady -= SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged -= FactionStateChanged;
            MyAPIGateway.Session.Factions.FactionCreated -= FactionCreated;
            MyAPIGateway.Session.Factions.FactionEdited -= FactionEdited;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(CommandsSyncId, Commands.ServerMessageHandler);
            MyExplosions.OnExplosion -= CubeGridModifiers.HandleLightningExplosions;

            UntrackAllPhysicalGridGroups();
            LimitsNexusSync.Stop();
            if (_myNexusApi != null)
                _myNexusApi.Unload();
            _myNexusApi = null;
            _startedNexus = false;

            ResetRuntimeStateSync();
            PerFactionManager.Reset();
            PerPlayerManager.Reset();
            PerManifestGroupManager.Reset();
            Config.SaveConfig();
        }
    }
}
