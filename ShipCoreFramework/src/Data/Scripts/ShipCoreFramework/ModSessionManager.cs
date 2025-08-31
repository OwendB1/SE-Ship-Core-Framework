#region

using System.Collections.Generic;
using System.Linq;
using NexusModAPI;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

#endregion

namespace ShipCoreFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation, 0)]
    public class ModSessionManager : MySessionComponentBase
    {
        public static ModConfig Config = new ModConfig();
        private static NexusAPI _myNexusApi;
        private bool _started;

        public override void LoadData()
        {
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
                    Utils.Log($"AmmoType: {ammoId} was not sucessfully adjusted to match maxspeed");
            }
            
            MyAPIGateway.Session.OnSessionReady += SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
            MyAPIGateway.Utilities.MessageEnteredSender += Commands.OnChatCommand;
            if(Constants.IsMultiplayer && Constants.IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(Constants.CommandsSyncId, Commands.ServerMessageHandler);
                Utils.Log($"Ship Cores: Awaiting Commands From Clients");
            }
            Config.SaveConfig();
        }

        private void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId,
            long factionId, long playerId)
        {
            if (Config.SelectedNoCore == null) return;
            if (action != MyFactionStateChange.FactionMemberKick &&
                action != MyFactionStateChange.FactionMemberLeave) return;
            Utils.Log(
                $"FactionStateChanged: {action} from {fromFactionId} to {toFactionId} for faction {factionId} and player {playerId}");
            
            var gridEntities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(gridEntities, entity => entity is IMyCubeGrid);
            var physicalGrids = gridEntities.Cast<IMyCubeGrid>().Where(grid => grid.Physics != null).ToList();
            
            var factionGridLogics = physicalGrids
                .Select(x => x.GameLogic.GetAs<GridLogic>())
                .Where(logic => logic?.OwningFaction?.FactionId == factionId)
                .ToList();
                
            foreach (var gridLogic in factionGridLogics.Where(gridLogic =>
                         gridLogic.OwningFaction.Members.Count < gridLogic.ShipCore.MinPlayers))
                gridLogic.ResetCore();
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Session.OnSessionReady -= SessionReady;
            MyAPIGateway.Session.Factions.FactionStateChanged -= FactionStateChanged;
            MyAPIGateway.Utilities.MessageEnteredSender -= Commands.OnChatCommand;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
            if(Constants.IsMultiplayer && Constants.IsServer)
            {
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(Constants.CommandsSyncId, Commands.ServerMessageHandler);
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
        }
        
        private void OnNexusEnabled()
        {
            if (_started) return;
            if (!MyAPIGateway.Multiplayer.IsServer) return;
            _started = true;
            LimitsNexusSync.Start(_myNexusApi);
            LimitsNexusSync.BroadcastSnapshot();
        }

        private void SessionReady()
        {
            if (Config.SelectedNoCore == null) return;
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(99, CubeGridModifiers.GridClassDamageHandler);
        }
        
        public override void UpdateAfterSimulation()
        {
            Utils.ProcessUiQueue();
        }
    }
}