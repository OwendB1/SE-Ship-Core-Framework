using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace ShipCoreFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ModSessionManager : MySessionComponentBase
    {
        public static ModConfig Config;
        public static Dictionary<long, CubeGridLogic> CubeGridLogics = new Dictionary<long, CubeGridLogic>();
        public readonly Queue<IMyCubeGrid> ToBeInitialized = new Queue<IMyCubeGrid>();
        internal static Comms Comms;

        public override void LoadData()
        {
            Comms = new Comms(Settings.COMMS_MESSAGE_ID);
            if (Constants.IsServer)
            {
                var config = ModConfig.LoadConfig() ?? DefaultGridClassConfig.DefaultModConfig;
                LoadConfig(config);
                ModConfig.SaveConfig(Config, Constants.ConfigFilename);
            }
            else
            {
                Comms.RequestConfig();
            }

            //Utils.Log("Mod Path: "+ModPath);
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += EntityRemoved;
            MyAPIGateway.Session.OnSessionReady += HookDamageHandler;
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionStateChanged;
        }

        public static void LoadConfig(ModConfig config)
        {
            Config = config;
            Config.UpdateGridClassesDictionary();
            MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = Config.MaxPossibleSpeedMetersPerSecond;
            MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = Config.MaxPossibleSpeedMetersPerSecond;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
            var ammoDefinitions = new List<string> { "Missile","LargeCalibreShell","MediumCalibreShell","LargeCaliber","AutocannonShell","LargeRailgunSlug","SmallRailgunSlug","SmallCaliber","PistolCaliber" };
            foreach(var ammoId in ammoDefinitions)
            {
                var ammoDefinition = MyDefinitionManager.Static.GetAmmoDefinition(new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                if (ammoDefinition != null)
                {
                    ammoDefinition.DesiredSpeed += speedDifferential;
                }
                else
                {
                    Utils.Log($"AmmoType: {ammoId} was not sucessfully adjusted to match maxspeed");
                }
            }
        }

        private void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId, long factionId, long playerId)
        {
            if (action != MyFactionStateChange.FactionMemberKick && action != MyFactionStateChange.FactionMemberLeave) return;
            Utils.Log($"FactionStateChanged: {action} from {fromFactionId} to {toFactionId} for faction {factionId} and player {playerId}");
            var factionGridLogics = CubeGridLogics.Where(x => x.Value.OwningFaction?.FactionId == factionId).ToList();
            foreach (var gridLogic in factionGridLogics.Where(gridLogic => gridLogic.Value.OwningFaction.Members.Count < gridLogic.Value.GridClass.MinPlayers))
            {
                gridLogic.Value.GridClassId = DefaultGridClassConfig.DefaultGridClassDefinition.Id;
            }
        }

        protected override void UnloadData()
        {
            if (Constants.IsServer)
                ModConfig.SaveConfig(Config, Constants.ConfigFilename);
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
            MyAPIGateway.Session.OnSessionReady -= HookDamageHandler;
            var speedDifferential = Config.MaxPossibleSpeedMetersPerSecond-100.0f;
            var ammoDefinitions = new List<string>{"Missile","LargeCalibreShell","MediumCalibreShell","LargeCaliber","AutocannonShell","LargeRailgunSlug","SmallRailgunSlug","SmallCaliber","PistolCaliber","Flare","FireworkBlue","FireworkGreen","FireworkRed","FireworkPink","FireworkYellow","FireworkRainbow","Shrapnel"};
            foreach(var ammoId in ammoDefinitions)
            {
                try{
                    var ammoDefinition = MyDefinitionManager.Static.GetAmmoDefinition(new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                    if (ammoDefinition != null){ammoDefinition.DesiredSpeed -= speedDifferential;}else{Utils.Log($"AmmoType: {ammoId} was not sucessfully adjusted to match maxspeed");}
                }catch{Utils.Log($"Vanilla AmmoType {ammoId} is missing.");}
            }
            //foreach (var logic in CubeGridLogics)
            //{
            //    logic.Value.GridMarkedForClose();
            //}
            ToBeInitialized.Clear();
            CubeGridLogics.Clear();
            GridsPerFactionClassManager.Reset();
            GridsPerPlayerClassManager.Reset();
            Comms.Discard();
            //Instance = null;
        }

        private void EntityAdded(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null) return;
            Utils.Log($"EntityAdded: {grid.DisplayName}");
            ToBeInitialized.Enqueue(grid);
        }

        private void EntityRemoved(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null) return;
            if (!CubeGridLogics.ContainsKey(grid.EntityId)) return;
            try
            {
                CubeGridLogics[grid.EntityId].RemoveGridLogic();
            }
            catch
            {
                Utils.Log($"Cubegrid was not accessible in list due to silly witchcraft shenanigans:{grid.EntityId}");
            }
        }

        private void HookDamageHandler()
        {
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(99, CubeGridModifiers.GridClassDamageHandler);
        }

        public override void UpdateAfterSimulation()
        {
            if (Config == null) return;
            if (ToBeInitialized.Count < 1) return;
            if (MyAPIGateway.Session.GameplayFrameCounter < 1) return;

            var target = ToBeInitialized.Dequeue();
            if (target.Physics == null) return;

            var logic = new CubeGridLogic();
            logic.Initialize(target);
        }
    }
}