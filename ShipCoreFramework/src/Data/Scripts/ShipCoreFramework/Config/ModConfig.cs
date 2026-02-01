#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Sandbox.ModAPI;
using VRageMath;

#endregion

namespace ShipCoreFramework
{
    [XmlRoot("ModConfig")]
    public class ModConfig
    {
        [XmlIgnore] private const string IgnoreAiKey = "ShipCore.IgnoreAiV1";
        [XmlIgnore] private const string IgnoredFactionsKey = "ShipCore.IgnoredFactionsV1";
        [XmlIgnore] private const string SelectedNoCoreKey = "ShipCore.SelectedNoCoreBlobV1";
        [XmlIgnore] private const string GlobalConfigFileName = "ShipCoreConfig_World.xml";
        [XmlIgnore] private const string CoreManifestFileName = @"Data\ShipCoreConfig_Manifest.xml";
        [XmlIgnore] private const string BlockGroupsFileName = @"Data\ShipCoreConfig_Groups.xml";
        [XmlIgnore] private const string DefaultNoCoreFileName = @"Data\ShipCoreConfig_No_Core.xml";
        [XmlIgnore] public readonly List<ShipCore> NoCoreConfigs = new List<ShipCore>();
        [XmlIgnore] public readonly List<BlockGroup> BlockGroups = new List<BlockGroup>();
        [XmlIgnore] public readonly List<ShipCore> ShipCores = new List<ShipCore>();
        [XmlIgnore] public List<string> IgnoredFactionTags = new List<string>();
        [XmlIgnore] public ShipCore SelectedNoCore;
        [XmlIgnore] public bool IgnoreAiFactions;
        
        [XmlElement("DebugMode")] public bool DebugMode;
        [XmlElement("CombatLogging")] public bool CombatLogging = true;
        [XmlElement("LOG_LEVEL")]public int LogLevel = 2; //messages with logPriority >= this will get logged, less than will be ignored
        [XmlElement("CLIENT_OUTPUT_LOG_LEVEL")]public int ClientOutputLogLevel = 2; //messages with logPriority >= this will get output to clients

        [XmlElement("MaxPossibleSpeedMetersPerSecond")] public float MaxPossibleSpeedMetersPerSecond = 300;
        [XmlElement("NoFlyZones")] public List<Zones> NoFlyZones = new List<Zones>();
        
        public ShipCore GetShipCoreByTypeId(string coreTypeId)
        {
            if (coreTypeId == string.Empty) return SelectedNoCore;
            var shipCore = ShipCores.FirstOrDefault(core => core.SubtypeId == coreTypeId);
            return shipCore ?? SelectedNoCore;
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
                Utils.SaveToSandbox(IgnoreAiKey, IgnoreAiFactions);
                Utils.Log($"Stored Data In World Config: Saved {IgnoreAiKey}", showInChat ? 3 : 0);
                Utils.SaveToSandbox(IgnoredFactionsKey, IgnoredFactionTags);
                Utils.Log($"Stored Data In World Config: : Saved {IgnoredFactionsKey}", showInChat ? 3 : 0);
                Utils.SaveToSandbox(SelectedNoCoreKey, SelectedNoCore);
                Utils.Log($"Stored Data In World Config: : Saved {SelectedNoCoreKey}", showInChat ? 3 : 0);
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
        
        public void LoadConfig()
        {
            //Get World Settings
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
            {
                using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
                {
                    var text = reader.ReadToEnd();
                    var import = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(text);
                    if (import == null) throw new Exception("Failed to load world config.");

                    DebugMode = !Session.IsClient && import.DebugMode;
                    CombatLogging = import.CombatLogging;
                    LogLevel = import.LogLevel;
                    ClientOutputLogLevel = import.ClientOutputLogLevel;

                    if (import.MaxPossibleSpeedMetersPerSecond <= 0 || import.MaxPossibleSpeedMetersPerSecond > 10000)
                    {
                        Utils.Log("MaxPossibleSpeedMetersPerSecond validation failed - using default 300", 0, "Config Validation");
                        MaxPossibleSpeedMetersPerSecond = 300;
                    }
                    else
                    {
                        MaxPossibleSpeedMetersPerSecond = import.MaxPossibleSpeedMetersPerSecond;
                    }

                    NoFlyZones = import.NoFlyZones;
                }
            }
            else
            {
                //Write Global Settings using predefined values
                var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig));
                globalConfigWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                globalConfigWriter.Close();
            }

            IgnoreAiFactions = Utils.LoadFromSandbox<bool>(IgnoreAiKey);
            IgnoredFactionTags = Utils.LoadFromSandbox<List<string>>(IgnoredFactionsKey) ?? new List<string>{"SPRT","ADMIN","FMCA", "BORG", "TERA"};
            SelectedNoCore = Utils.LoadFromSandbox<ShipCore>(SelectedNoCoreKey);
            
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
                        BlockGroups.AddRange(newBlockGroups);
                        Utils.Log($"Loaded Groups From: {mod.FriendlyName}", 1, "Ship Core Config");
                    }

                //Add default Core to list
                if (MyAPIGateway.Utilities.FileExistsInModLocation(DefaultNoCoreFileName, mod))
                    using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(DefaultNoCoreFileName, mod))
                    {
                        var text = reader.ReadToEnd();
                        var newNoCore = MyAPIGateway.Utilities.SerializeFromXML<ShipCore>(text);

                        if (newNoCore == null)
                            throw new Exception($"Failed to load no-core from Mod: {mod.FriendlyName}");
                        NoCoreConfigs.Add(newNoCore);
                        Utils.Log($"Loaded No-Core Config From: {mod.FriendlyName}", 1, "Ship Core Config");
                    }

                if (!MyAPIGateway.Utilities.FileExistsInModLocation(CoreManifestFileName, mod)) continue;
                Utils.Log($"Found Manifest in: {mod.FriendlyName}", 1, "Ship Core Config");
                //Check the Core Manifest to get all cores in the mod
                using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(CoreManifestFileName, mod))
                {
                    var text = reader.ReadToEnd();
                    var coreManifest = MyAPIGateway.Utilities.SerializeFromXML<CoreManifest>(text);
                    if (coreManifest == null)
                        throw new Exception($"Failed to Load Classes from Mod: {mod.FriendlyName}");

                    //Go get ship cores
                    foreach (var shipCoreFilename in coreManifest.ShipCoreFilenames.Where(shipCoreFilename => MyAPIGateway.Utilities.FileExistsInModLocation(shipCoreFilename, mod)))
                        using (var textReader = MyAPIGateway.Utilities.ReadFileInModLocation(shipCoreFilename, mod))
                        {
                            var modText = textReader.ReadToEnd();
                            var newShipCore = MyAPIGateway.Utilities.SerializeFromXML<ShipCore>(modText);

                            if (newShipCore == null){throw new Exception($"Failed to load ship core from file {shipCoreFilename} in Mod: {mod.FriendlyName}");}
                            ShipCores.Add(newShipCore);
                            Utils.Log($"Loaded Core {newShipCore.UniqueName} From: {mod.FriendlyName}", 1, "Ship Core Config");
                        }
                }
            }

            ThrowErrorIfDuplicates(NoCoreConfigs, core => core.UniqueName);
            ThrowErrorIfDuplicates(ShipCores, core => core.UniqueName);
            ThrowErrorIfDuplicates(BlockGroups, groups => groups.Name);
            Utils.Log($"NoCoreConfigs.Count = {NoCoreConfigs.Count}", 1, "Ship Core Config");
            Utils.Log($"BlockGroups.Count = {BlockGroups.Count}", 1, "Ship Core Config");

            foreach (var limit in ShipCores.SelectMany(core => core.BlockLimits))
            {
                foreach(var shorthand in limit.BlockGroupsShortHand)
                {
                    foreach (var group in BlockGroups.Where(group => group.Name == shorthand))
                    {
                        limit.BlockGroups.Add(group);
                        Utils.Log($"{group.Name} Count: {limit.BlockGroups.Count}",0, "Ship Core Config groups");
                    }
                }
            }
            //Select the default config instead of returning
            if (SelectedNoCore == null) SelectedNoCore=DefaultNoCoreConfig.ShipCore;
            foreach(var limit in SelectedNoCore.BlockLimits)
            {
                foreach(var shorthand in limit.BlockGroupsShortHand)
                {
                    foreach (var group in BlockGroups.Where(group => group.Name == shorthand))
                    {
                        limit.BlockGroups.Add(group);
                        Utils.Log($"{group.Name} Count: {limit.BlockGroups.Count}",0, "Ship Core Config groups");
                    }
                }
            }
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
        public bool ForceOff;
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
        [XmlElement("MaxBackupCores")]
        public int MaxBackupCores = -1;
        [XmlElement("ForceBroadCast")]
        public bool ForceBroadCast;
        [XmlElement("ForceBroadCastRange")]
        public float ForceBroadCastRange;
        [XmlElement("MobilityType")]
        public MobilityType MobilityType = MobilityType.Both;
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
        [XmlElement("MinPlayers")]
        public int MinPlayers = -1;
        [XmlElement("Modifiers")]
        public GridModifiers Modifiers = new GridModifiers();
        [XmlElement("PassiveDefenseModifiers")]
        public GridDefenseModifiers PassiveDefenseModifiers = new GridDefenseModifiers();
        [XmlElement("SpeedBoostEnabled")]
        public bool SpeedBoostEnabled;
        [XmlElement("SpeedLimitType")]
        public SpeedLimitType SpeedLimitType;
        [XmlElement("SpeedModifiers")]
        public SpeedModifiers SpeedModifiers = new SpeedModifiers();
        [XmlElement("EnableActiveDefenseModifiers")]
        public bool EnableActiveDefenseModifiers;
        [XmlElement("ActiveDefenseModifiers")]
        public GridDefenseModifiers ActiveDefenseModifiers = new GridDefenseModifiers();
        [XmlElement("BlockLimits")]
        public BlockLimit[] BlockLimits = Array.Empty<BlockLimit>();
    }
    
    [XmlRoot("SpeedModifiers")]
    public class SpeedModifiers
    {
        [XmlElement("MaxSpeed")]
        public float MaxSpeed = 0.3f;
        [XmlElement("MaxBoost")]
        public float MaxBoost = 0.5f;
        [XmlElement("BoostDuration")]
        public float BoostDuration = 10f; 
        [XmlElement("BoostCoolDown")]
        public float BoostCoolDown = 60f;      
        [XmlElement("MinimumFrictionSpeed")]
        public float MinimumFrictionSpeed = 100f;
        [XmlElement("MaximumFrictionSpeed")]
        public float MaximumFrictionSpeed = 290f;
        [XmlElement("MaximumFrictionDeceleration")]
        public float MaximumFrictionDeceleration= 1f;  
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
                new ModifierNameValue("Drill harvest", DrillHarvestMultiplier)
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
        [XmlElement("Name")]
        public string Name = string.Empty;
        [XmlElement("BlockGroups")]
        public string[] BlockGroupsShortHand = Array.Empty<string>();
        [XmlIgnore]
        public List<BlockGroup> BlockGroups = new List<BlockGroup>();
        [XmlElement("MaxCount")]
        public float MaxCount;
        [XmlElement("PunishByNoFlyZone")]
        public bool PunishByNoFlyZone;
        [XmlElement("PunishmentType")]
        public PunishmentType PunishmentType = PunishmentType.ShutOff;
        [XmlElement("AllowedDirections")]
        public List<DirectionType> AllowedDirections;

        internal double GetWeight(BlockKey key)
        {
            if (BlockGroups == null) return 0d;

            foreach (var group in BlockGroups)
            {
                if (group?.BlockTypes == null) continue;

                foreach (var blockType in group.BlockTypes)
                {
                    if (blockType == null) continue;

                    if (blockType.TypeId != key.TypeId) continue;
                    if (blockType.SubtypeId == key.SubtypeId || blockType.SubtypeId == "any")
                    {
                        return blockType.CountWeight;
                    }
                }
            }
            return 0d;
        }
    }

    [XmlRoot("BlockGroup")]
    public class BlockGroup
    {
        [XmlElement("Name")] 
        public string Name = string.Empty;
        [XmlElement("BlockTypes")] 
        public List<BlockType> BlockTypes = new List<BlockType>();
    }

    [XmlRoot("BlockType")]
    public class BlockType
    {
        [XmlElement("TypeId")] 
        public string TypeId;
        [XmlElement("SubtypeId")] 
        public string SubtypeId;
        [XmlElement("CountWeight")] 
        public float CountWeight;
        
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
        [XmlElement("Bullet")] 
        public float Bullet = 1f;
        [XmlElement("PostShield")] 
        public float PostShield = 1f;
        [XmlElement("Duration")] 
        public float Duration;
        [XmlElement("Cooldown")] 
        public float Cooldown;
        [XmlElement("Rocket")] 
        public float Rocket = 1f;
        [XmlElement("Explosion")] 
        public float Explosion = 1f;
        [XmlElement("Environment")] 
        public float Environment = 1f;
        [XmlElement("Energy")] 
        public float Energy = 1f;
        [XmlElement("Kinetic")] 
        public float Kinetic = 1f;
    }
    
    [XmlRoot("MobilityType")]
    public enum MobilityType
    {
        Static = 0, 
        Mobile = 1, 
        Both = 2
    }
    
    [XmlRoot("SpeedLimitType")]
    public enum SpeedLimitType
    {
        Normal = 0, 
        Friction = 1
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