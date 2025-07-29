#region

using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

#endregion

namespace ShipCoreFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ModSessionManager : MySessionComponentBase
    {
        public static ModConfig Config = new ModConfig();

        public override void LoadData()
        {
            MyAPIGateway.Utilities.MessageEntered += Commands.OnChatCommand;
            Config = Config.LoadConfig();
            Config.SaveConfig(true);
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
            
            MyAPIGateway.Session.OnSessionReady += HookDamageHandler;
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
        }

        private void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId,
            long factionId, long playerId)
        {
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
                gridLogic.ActiveNoCore = false;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Session.OnSessionReady -= HookDamageHandler;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
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
            
            GridsPerFactionClassManager.Reset();
            GridsPerPlayerClassManager.Reset();

            Config.SaveConfig();
        }

        private void HookDamageHandler()
        {
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(99, CubeGridModifiers.GridClassDamageHandler);
        }
    }
}