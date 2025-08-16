#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Utils;
#endregion

namespace ShipCoreFramework
{
    [XmlRoot("ModConfig")]
    public class ModConfig
    {
        [XmlIgnoreAttribute] private const string GlobalConfigFileName = "ShipCoreConfig_World.xml";
        [XmlIgnoreAttribute] private const string CoreManifestFileName = "ShipCoreConfig_Manifest.xml";
        [XmlIgnoreAttribute] private const string BlockGroupsFileName = "ShipCoreConfig_Groups.xml";
        [XmlIgnoreAttribute] private const string DefaultNoCoreFileName = "ShipCoreConfig_No_Core.xml";
        [XmlIgnoreAttribute] public readonly List<ShipCore> NoCoreConfigs = new List<ShipCore>();
        [XmlIgnoreAttribute] public readonly List<BlockGroup> BlockGroups = new List<BlockGroup>();
        [XmlIgnoreAttribute] public readonly List<ShipCore> ShipCores = new List<ShipCore>();
        [XmlElement("DebugMode")] public bool DebugMode = true;
        [XmlElement("CombatLogging")] public bool CombatLogging = true;
        [XmlElement("LOG_LEVEL")]public  int LogLevel = 0; //messages with logPriority >= this will get logged, less than will be ignored
        [XmlElement("CLIENT_OUTPUT_LOG_LEVEL")]public  int ClientOutputLogLevel = 3; //messages with logPriority >= this will get output to clients
        [XmlIgnoreAttribute] public ShipCore DefaultNoCore = DefaultNoCoreConfig.ShipCore;

        [XmlElement("MaxPossibleSpeedMetersPerSecond")]
        public float MaxPossibleSpeedMetersPerSecond = 300;

        [XmlElement("IncludeAiFactions")] public bool IncludeAiFactions;
        [XmlElement("IgnoreFactionTags")] public List<string> IgnoreFactionTags = new List<string>();


        [XmlIgnoreAttribute] public string NoCoreSimpleName = "NoCoreGrids";
        [XmlElement("NoFlyZones")] public List<Zones> NoFlyZones = new List<Zones>();


        public ShipCore GetShipCoreByTypeId(string coreTypeId)
        {
            if (coreTypeId == string.Empty) return DefaultNoCore;
            var shipCore = ShipCores.FirstOrDefault(core => core.SubtypeId == coreTypeId);
            return shipCore ?? DefaultNoCore;
        }

        public bool IsValidCoreType(string coreTypeName)
        {
            return ShipCores.Any(core => core.SubtypeId == coreTypeName);
        }

        public void SaveConfig(bool showInChat = false)
        {
            try
            {
                var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig));
                globalConfigWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                globalConfigWriter.Close();
                Utils.Log($"Save Config: Saved {GlobalConfigFileName}", showInChat ? 3 : 0);

                var blockGroupsWriter = 
                    MyAPIGateway.Utilities.WriteFileInWorldStorage(BlockGroupsFileName, typeof(BlockGroup[]));
                blockGroupsWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this.BlockGroups));
                blockGroupsWriter.Close();
                Utils.Log($"Save Config: Saved {BlockGroupsFileName}", showInChat ? 3 : 0);

                var defaultNoCoreWriter =
                    MyAPIGateway.Utilities.WriteFileInWorldStorage(DefaultNoCoreFileName, typeof(ShipCore));
                defaultNoCoreWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this.DefaultNoCore));
                defaultNoCoreWriter.Close();
                Utils.Log($"Save Config: Saved {DefaultNoCoreFileName}", showInChat ? 3 : 0);

                var manifestWriter =
                    MyAPIGateway.Utilities.WriteFileInWorldStorage(CoreManifestFileName, typeof(CoreManifest));
                manifestWriter.Write(MyAPIGateway.Utilities.SerializeToXML(new List<string>(){"Example Core.xml"}));
                manifestWriter.Close();

                var shipcoreWriter =
                    MyAPIGateway.Utilities.WriteFileInWorldStorage("Example Core.xml", typeof(ShipCore));
                shipcoreWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this.ShipCores[0]));
                shipcoreWriter.Close();

                //Utils.Log($"Save Config: Saved {CoreManifestFileName}", showInChat ? 3 : 0);
            }
            catch (Exception e)
            {
                //Utils.Log($"Failed to save configs, reason {e.Message}", 3);
                Utils.Log($"Save Error: {e}");
                var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage("Error.txt", typeof(ModConfig));
                globalConfigWriter.Write(e);
                globalConfigWriter.Close();
            }
        }

        public ModConfig LoadConfig()
        {
            var globalSettings = new ModConfig();
            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(DefaultNoCoreFileName, typeof(ShipCore)))
                {
                    Utils.ShowNotification("No NoCore is selected for this world. Admin: use /core select <name> to choose one.", 0);
                }
                
                //Get World Settings
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
                {
                    using (var reader =
                           MyAPIGateway.Utilities.ReadFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
                    {
                        var text = reader.ReadToEnd();
                        var newGlobalSettings = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(text);
                        if (newGlobalSettings == null)
                            throw new Exception("Failed to load world config.");
                        globalSettings = newGlobalSettings;
                    }
                }
                else
                {
                    //Write Global Settings using predefined values
                    var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig));
                    globalConfigWriter.Write(MyAPIGateway.Utilities.SerializeToXML(globalSettings));
                    globalConfigWriter.Close();
                }

                //Run Though Mods
                foreach (var mod in MyAPIGateway.Session.Mods)
                {
                    //Add Custom BlockGroups
                    if (MyAPIGateway.Utilities.FileExistsInModLocation(BlockGroupsFileName, mod))
                        using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(BlockGroupsFileName, mod))
                        {
                            var text = reader.ReadToEnd();
                            var newBlockGroups = MyAPIGateway.Utilities.SerializeFromXML<List<BlockGroup>>(text);

                            if (newBlockGroups == null)
                                throw new Exception($"Failed to load block groups from Mod: {mod.FriendlyName}");
                            globalSettings.BlockGroups.AddRange(newBlockGroups);
                            Utils.Log($"Loaded Groups From: {mod.FriendlyName}", 0, "Ship Core Config");
                            
                        }

                    //Add default Core to list
                    if (MyAPIGateway.Utilities.FileExistsInModLocation(DefaultNoCoreFileName, mod))
                        using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(DefaultNoCoreFileName, mod))
                        {
                            var text = reader.ReadToEnd();
                            var newNoCore = MyAPIGateway.Utilities.SerializeFromXML<ShipCore>(text);

                            if (newNoCore == null)
                                throw new Exception($"Failed to load no-core from Mod: {mod.FriendlyName}");
                            globalSettings.NoCoreConfigs.Add(newNoCore);
                            Utils.Log($"Loaded No-Core Config From: {mod.FriendlyName}", 0, "Ship Core Config");
                        }

                    if (!MyAPIGateway.Utilities.FileExistsInModLocation(CoreManifestFileName, mod)) continue;

                    //Check the Core Manifest to get all cores in the mod
                    using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(CoreManifestFileName, mod))
                    {
                        var text = reader.ReadToEnd();
                        var coreManifest = MyAPIGateway.Utilities.SerializeFromXML<CoreManifest>(text);
                        if (coreManifest == null)
                            throw new Exception($"Failed to Load Classes from Mod: {mod.FriendlyName}");

                        //Go get ship cores
                        foreach (var shipCoreFilename in coreManifest.ShipCoreFilenames)
                            using (var textReader = MyAPIGateway.Utilities.ReadFileInModLocation(shipCoreFilename, mod))
                            {
                                var modText = textReader.ReadToEnd();
                                var newShipCore = MyAPIGateway.Utilities.SerializeFromXML<ShipCore>(modText);

                                if (newShipCore == null){throw new Exception($"Failed to load ship core from file {shipCoreFilename} in Mod: {mod.FriendlyName}");}
                                globalSettings.ShipCores.Add(newShipCore);
                                Utils.Log($"Loaded Core {newShipCore.UniqueName} From: {mod.FriendlyName}", 0, "Ship Core Config");
                            }
                    }
                }

                ThrowErrorIfDuplicates(NoCoreConfigs, core => core.UniqueName);
                ThrowErrorIfDuplicates(ShipCores, core => core.UniqueName);
                ThrowErrorIfDuplicates(BlockGroups, groups => groups.Name);
                Utils.Log($"NoCoreConfigs.Count = {globalSettings.NoCoreConfigs.Count}", 0, "Ship Core Config");
                if (globalSettings.NoCoreConfigs.Count == 0)
                {
                    //Utils.Log($"Could not find any no-core configs, setting no-core config to use pre-generated internal one!!", 1);
                    globalSettings.DefaultNoCore = DefaultNoCoreConfig.ShipCore;
                }
                Utils.Log($"BlockGroups.Count = {globalSettings.BlockGroups.Count}", 0, "Ship Core Config");
                if(globalSettings.BlockGroups.Count == 0)
                {
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.VanillaSmallGridFixedWeapons);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.VanillaSmallGridTurretWeapons);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.VanillaLargeGridFixedWeapons);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.VanillaLargeGridTurretWeapons);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Drills);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Welders);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Grinders);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.SafeZone);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.ProgrammableBlocks);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Assemblers);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Refineries);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.LargeHydrogenTanks);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.SmallHydrogenTanks);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.SmallCargoContainers);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.MediumCargoContainers);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.LargeCargoContainers);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Gyros);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Collectors);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.ConveyorJunctions);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.VanillaRailguns);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.VanillaArtillery);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.VanillaBrawl);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.VanillaPDC);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Turrets);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.StaticWeaponry);
                    globalSettings.BlockGroups.Add(DefaultGridClassConfig.Production);
                    //globalSettings.BlockGroups.Add(DefaultGridClassConfig.Weaponry);
                    
                }
                if(globalSettings.ShipCores.Count==0)
                {
                    globalSettings.ShipCores.Add(new ShipCore
                        {
                            UniqueName = "Example Grid Class",
                            SubtypeId = "Example_Core",
                            LargeGridStatic = true,
                            LargeGridMobile = true,
                            SmallGrid = true,
                            MaxBlocks = 50000,
                            MaxPerFaction=10,
                            MaxPerPlayer=15,
                            //MaxPCU=,
                            //MaxMass=,
                            Modifiers = new GridModifiers{
                                ThrusterForce = 1.5f,
                                ThrusterEfficiency = 1.5f,
                                GyroForce = 1.5f,
                                GyroEfficiency = 1.5f,
                                RefineEfficiency = 1.5f,
                                RefineSpeed = 1.5f,
                                AssemblerSpeed = 1.5f,
                                PowerProducersOutput = 1.5f,
                                DrillHarvestMultiplier = 1.5f,
                                MaxSpeed = 0.6f,
                            },
                            PassiveDefenseModifiers = new GridDefenseModifiers
                            {
                                Bullet = 0.9f,
                                Energy = 0.9f,
                                Kinetic = 0.9f,
                                Duration = 0.9f,
                                Cooldown = 0.9f,
                                Rocket = 0.9f,
                                Explosion = 0.9f,
                                Environment = 0.9f,
                            },
                            ActiveDefenseModifiers = new GridDefenseModifiers
                            {
                                Bullet = 0.9f,
                                Energy = 0.9f,
                                Kinetic = 0.9f,
                                Duration = 0.9f,
                                Cooldown = 0.9f,
                                Rocket = 0.9f,
                                Explosion = 0.9f,
                                Environment = 0.9f,
                            },
                            BlockLimits = new BlockLimit[]
                            {
                                /*new BlockLimit
                                {
                                    Name = "Example: Weapons",
                                    BlockGroupsShortHand = new string[]{"Weaponry",},
                                    MaxCount = 10f,
                                    TurnedOffByNoFlyZone = true,
                                    PunishmentType = PunishmentType.Delete,
                                    AllowedDirections =new List<DirectionType> {DirectionType.Forward,DirectionType.Backward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right},
                                },*/
                                new BlockLimit
                                {
                                    Name = "Example: Drills",
                                    BlockGroupsShortHand = new string[]{"Drills",},
                                    MaxCount = 10f,
                                    TurnedOffByNoFlyZone = true,
                                    PunishmentType = PunishmentType.Delete,
                                    AllowedDirections =new List<DirectionType> {DirectionType.Forward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right},
                                },
                            }
                        }
                    );
                }

                foreach (var limit in globalSettings.ShipCores.SelectMany(core => core.BlockLimits))
                {
                    foreach(var shorthand in limit.BlockGroupsShortHand)
                    {
                        foreach (var group in globalSettings.BlockGroups.Where(group => group.Name == shorthand))
                        {
                            limit.BlockGroups.Add(group);
                            Utils.Log($"{group.Name} Count: {limit.BlockGroups.Count}",0, "Ship Core Config groups");
                        }
                    }
                }

                foreach(var limit in DefaultNoCore.BlockLimits)
                {
                    foreach(var shorthand in limit.BlockGroupsShortHand)
                    {
                        foreach (var group in globalSettings.BlockGroups.Where(group => group.Name == shorthand))
                        {
                            limit.BlockGroups.Add(group);
                            Utils.Log($"{group.Name} Count: {limit.BlockGroups.Count}",0, "Ship Core Config groups");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Utils.Log($"Load Error: {e.Message}", 0, "Ship Core Config");
            }
            return globalSettings;
        }

        private static void ThrowErrorIfDuplicates<T, TKey>(List<T> list, Func<T, TKey> selector)
        {
            var dupeList = list.GroupBy(selector)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            
            if (dupeList.Any())
            {
                throw new Exception($"Found duplicates f0r {selector.Method.Name}: {string.Join("\n- ", dupeList)}");
            }
        }
    }
    
    [XmlRoot("Zones")]
	public class Zones {
        [XmlElement("ID")]
		public int Id;
        [XmlElement("Position")]
        public Vector3D Position;
        [XmlElement("Radius")]
        public double Radius;
        [XmlElement("AllowedCoresSubtype")]
        public List<string> AllowedCoresSubtype = new List<string>();
        [XmlElement("OverideBlockLimitsForceShutOff")]
        public bool ForceOff = false;

    }
    
    [XmlRoot("CoreManifest")]
    public class CoreManifest
    {
        [XmlElement("ShipCoreFilenames")] public List<string> ShipCoreFilenames;
    }
    
    [XmlRoot("ShipCore")]
    public class ShipCore
    {
        [XmlElement("SubtypeId")]
        public string SubtypeId = string.Empty;
        [XmlElement("UniqueName")]
        public string UniqueName = string.Empty;
        [XmlElement("ForceBroadCast")]
        public bool ForceBroadCast = false;
        [XmlElement("ForceBroadCastRange")]
        public float ForceBroadCastRange = 0;
        [XmlElement("LargeGridStatic")]
        public bool LargeGridStatic = false;
        [XmlElement("LargeGridMobile")]
        public bool LargeGridMobile = false;
        [XmlElement("SmallGrid")]
        public bool SmallGrid = false;
        [XmlElement("MaxBlocks")]
        public int MaxBlocks = -1;
        [XmlElement("MaxMass")]
        public float MaxMass = -1;
        [XmlElement("MaxPCU")]
        public int MaxPCU = -1;
        [XmlElement("MaxPerFaction")]
        public int MaxPerFaction = -1;
        [XmlElement("MaxPerPlayer")]
        public int MaxPerPlayer = -1;
        [XmlElement("MinBlocks")]
        public int MinBlocks = -1;
        [XmlElement("MinPlayers")]
        public int MinPlayers = -1;
        [XmlElement("Modifiers")]
        public GridModifiers Modifiers = new GridModifiers();
        [XmlElement("PassiveDefenseModifiers")]
        public GridDefenseModifiers PassiveDefenseModifiers = new GridDefenseModifiers();
        [XmlElement("SpeedBoostEnabled")]
        public bool SpeedBoostEnabled = false;
        [XmlElement("EnableActiveDefenseModifiers")]
        public bool EnableActiveDefenseModifiers = false;
        [XmlElement("ActiveDefenseModifiers")]
        public GridDefenseModifiers ActiveDefenseModifiers = new GridDefenseModifiers();
        [XmlElement("EnableReloadModifier")]
        public bool EnableReloadModifier = false;
        [XmlElement("ReloadModifier")]
        public float ReloadModifier = 1f;
        [XmlElement("BlockLimits")]
        public BlockLimit[] BlockLimits = Array.Empty<BlockLimit>();
    }
    
    [XmlRoot("GridModifiers")]
    public class GridModifiers
    {
        [XmlElement("AssemblerSpeed")]
        public float AssemblerSpeed = 1;
        [XmlElement("DrillHarvestMultiplier")]
        public float DrillHarvestMultiplier = 1;
        [XmlElement("GyroEfficiency")]
        public float GyroEfficiency = 1;
        [XmlElement("GyroForce")]
        public float GyroForce = 1;
        [XmlElement("PowerProducersOutput")]
        public float PowerProducersOutput = 1;
        [XmlElement("RefineEfficiency")]
        public float RefineEfficiency = 1;
        [XmlElement("RefineSpeed")]
        public float RefineSpeed = 1;
        [XmlElement("ThrusterEfficiency")]
        public float ThrusterEfficiency = 1;
        [XmlElement("ThrusterForce")]
        public float ThrusterForce = 1;
        [XmlElement("MaxSpeed")]
        public float MaxSpeed = 0.3f;
        [XmlElement("MaxBoost")]
        public float MaxBoost = 1.2f;
        [XmlElement("BoostDuration")]
        public float BoostDuration = 10f; 
        [XmlElement("BoostCoolDown")]
        public float BoostCoolDown = 60f; 
        
        public override string ToString()
        {
            return
                $"<GridModifiers ThrusterForce={ThrusterForce} " +
                $"ThrusterEfficiency={ThrusterEfficiency} " +
                $"GyroForce={GyroForce} " +
                $"GyroEfficiency={GyroEfficiency} " +
                $"RefineEfficiency={RefineEfficiency} " +
                $"RefineSpeed={RefineSpeed} " +
                $"AssemblerSpeed={AssemblerSpeed} " +
                $"PowerProducersOutput={PowerProducersOutput} " +
                $"DrillHarvestMultiplier={DrillHarvestMultiplier} />";
        }

        public List<ModifierNameValue> GetModifierValues()
        {
            return new List<ModifierNameValue>
            {
                new ModifierNameValue("Thruster force", ThrusterForce),
                new ModifierNameValue("Thruster efficiency", ThrusterEfficiency),
                new ModifierNameValue("Gyro force", GyroForce),
                new ModifierNameValue("Gyro efficiency", GyroEfficiency),
                new ModifierNameValue("Refinery efficiency", RefineEfficiency),
                new ModifierNameValue("Refinery speed", RefineSpeed),
                new ModifierNameValue("Assembler speed", AssemblerSpeed),
                new ModifierNameValue("Power output", PowerProducersOutput),
                new ModifierNameValue("Drill harvest", DrillHarvestMultiplier),
                new ModifierNameValue("Max speed", MaxSpeed),
                new ModifierNameValue("Max boost", MaxBoost),
                new ModifierNameValue("Boost duration", BoostDuration),
                new ModifierNameValue("Boost cooldown", BoostCoolDown)
            };
        }
    }

    public struct ModifierNameValue
    {
        public readonly string Name;
        public readonly float Value;

        public ModifierNameValue(string name, float value)
        {
            Name = name;
            Value = value;
        }
    }

    [XmlRoot("BlockLimit")]
    public class BlockLimit
    {
        [XmlElement("Name")] public string Name = string.Empty;

        [XmlElement("BlockGroups")] public string[] BlockGroupsShortHand = Array.Empty<string>();

        [XmlIgnore]  public List<BlockGroup> BlockGroups = new List<BlockGroup>();

        [XmlElement("MaxCount")] public float MaxCount;

        [XmlElement("TurnedOffByNoFlyZone")] public bool TurnedOffByNoFlyZone;

        [XmlElement("PunishmentType")] public PunishmentType PunishmentType = PunishmentType.ShutOff;

        [XmlElement("AllowedDirections")] public List<DirectionType> AllowedDirections = new List<DirectionType>{DirectionType.Forward,DirectionType.Backward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right};
    }

    [XmlRoot("BlockGroup")]
    public class BlockGroup
    {
        [XmlElement("Name")] public string Name = string.Empty;

        [XmlElement("BlockTypes")] public List<BlockType> BlockTypes = new List<BlockType>();
    }

    [XmlRoot("BlockType")]
    public class BlockType
    {
        [XmlElement("TypeId")] public string TypeId;

        [XmlElement("SubtypeId")] public string SubtypeId;

        [XmlElement("CountWeight")] public float CountWeight;


        public BlockType()
        {
            TypeId = string.Empty;
            SubtypeId = string.Empty;
            CountWeight = 1;
        }

        public BlockType(string typeId, string subtypeId = "", float countWeight = 1)
        {
            TypeId = typeId;
            SubtypeId = subtypeId;
            CountWeight = countWeight;
        }
    }

    [XmlRoot("GridDefenseModifiers")]
    public class GridDefenseModifiers
    {
        [XmlElement("Bullet")] public float Bullet = 1f;

        [XmlElement("Energy")] public float Energy = 1f;

        [XmlElement("Kinetic")] public float Kinetic = 1f;

        [XmlElement("Duration")] public float Duration;

        [XmlElement("Cooldown")] public float Cooldown;

        [XmlElement("Rocket")] public float Rocket = 1f;

        [XmlElement("Explosion")] public float Explosion = 1f;

        [XmlElement("Environment")] public float Environment = 1f;
    }

    [XmlRoot("PunishmentType")]
    public enum PunishmentType
    {
        ShutOff,
        Damage,
        Delete,
        Explode
    }

    [XmlRoot("DirectionType")]
    public enum DirectionType
    {
        Forward,
        Backward,
        Up,
        Down,
        Left,
        Right,
    }
}