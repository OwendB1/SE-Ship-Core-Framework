using ProtoBuf;
using Sandbox.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    [ProtoContract]
    public class ModConfig
    {
        [ProtoMember(1)] public bool DebugMode;
        [ProtoMember(2)] public float MaxPossibleSpeedMetersPerSecond;
        [ProtoMember(3)] public bool IncludeAiFactions;
        [ProtoMember(4)] public List<string> IgnoreFactionTags;
        [ProtoMember(5)] public List<Zones> NoFlyZones;
        [ProtoMember(6)] public string NoCoreSimpleName = "";
        
        [ProtoIgnore] public readonly List<BlockGroups> BlockGroups = new List<BlockGroups>();
        [ProtoIgnore] public readonly List<ShipCore> ShipCores = new List<ShipCore>();
        [ProtoIgnore] private readonly List<ShipCore> _noCoreConfigs = new List<ShipCore>();
        [ProtoIgnore] public ShipCore DefaultNoCore;
        
        [ProtoIgnore] private const string GlobalConfigFileName = "ShipCoreConfig_World.xml";
        [ProtoIgnore] private const string CoreManifestFileName = "ShipCoreConfig_Manifest.xml";   
        [ProtoIgnore] private const string BlockGroupsFileName = "ShipCoreConfig_Groups.xml";
        [ProtoIgnore] private const string DefaultNoCoreFileName = "ShipCoreConfig_No_Core.xml";
        
        public ShipCore GetShipCoreBySubtype(string coreSubtypeId)
        {
            var shipCore = ShipCores.FirstOrDefault(core => core.SubtypeId == coreSubtypeId);
            if (shipCore == null) Utils.Log($"Unknown core {coreSubtypeId}, using default core");
            return shipCore ?? DefaultNoCore;
        }
        
        public void SaveConfig()
        {
            try
            {
                var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig));
                globalConfigWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                globalConfigWriter.Close();
                
                var blockGroupsWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(BlockGroupsFileName, typeof(BlockGroups[]));
                blockGroupsWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                blockGroupsWriter.Close();
                
                var defaultNoCoreWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(DefaultNoCoreFileName, typeof(ShipCore));
                defaultNoCoreWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                defaultNoCoreWriter.Close();
            }
            catch (Exception e)
            {
                Utils.Log($"Failed to save configs, reason {e.Message}", 3);
            }
        }

        public ModConfig LoadConfig()
        {
            var globalSettings = new ModConfig();
            try
            {
                //Get World Settings
                if(MyAPIGateway.Utilities.FileExistsInWorldStorage(GlobalConfigFileName,typeof(ModConfig)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
                    {
                        var text = reader.ReadToEnd();
                        var newGlobalSettings = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(text);
                        if (newGlobalSettings == null)
                            throw new Exception($"Failed to load world config.");
                        globalSettings=newGlobalSettings;
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
                    if(MyAPIGateway.Utilities.FileExistsInModLocation(BlockGroupsFileName, mod))
                    {
                        using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(BlockGroupsFileName, mod))
                        {
                            var text = reader.ReadToEnd();
                            var newBlockGroups = MyAPIGateway.Utilities.SerializeFromXML<BlockGroups[]>(text);
                                
                            if (newBlockGroups == null)
                                throw new Exception($"Failed to load block groups from Mod: {mod.FriendlyName}");
                            globalSettings.BlockGroups.AddRange(newBlockGroups);
                        }
                    }
                    
                    //Add default Core to list
                    if(MyAPIGateway.Utilities.FileExistsInModLocation(DefaultNoCoreFileName, mod))
                    {
                        using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(DefaultNoCoreFileName, mod))
                        {
                            var text = reader.ReadToEnd();
                            var newNoCore = MyAPIGateway.Utilities.SerializeFromXML<ShipCore>(text);
                            
                            if (newNoCore == null)
                                throw new Exception($"Failed to load no-core from Mod: {mod.FriendlyName}");
                            _noCoreConfigs.Add(newNoCore);
                        }
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
                        {
                            using (var textReader = MyAPIGateway.Utilities.ReadFileInModLocation(shipCoreFilename, mod))
                            {
                                var modText = textReader.ReadToEnd();
                                var newShipCore = MyAPIGateway.Utilities.SerializeFromXML<ShipCore>(modText);
                                
                                if(newShipCore == null)
                                    throw new Exception($"Failed to load ship core from file {shipCoreFilename} in Mod: {mod.FriendlyName}");
                                
                                globalSettings.ShipCores.Add(newShipCore);
                            }
                        }
                    }
                }

                ThrowErrorIfDuplicates(_noCoreConfigs, core => core.UniqueName);
                ThrowErrorIfDuplicates(ShipCores, core => core.UniqueName);
                ThrowErrorIfDuplicates(BlockGroups, groups => groups.Name);

                if (_noCoreConfigs.Count == 0)
                {
                    Utils.Log($"Could not find any no-core configs, setting no-core config to use pre-generated internal one!!", 1);
                    DefaultNoCore = DefaultNoCoreConfig.ShipCore;
                }
                else
                {
                    var chosenNoCore = _noCoreConfigs.FirstOrDefault(core => core.UniqueName == NoCoreSimpleName);
                    if (chosenNoCore != null)
                    {
                        DefaultNoCore = chosenNoCore;
                    } else
                    {
                        var exceptionMessage =
                            $"No no-core config found for simple name: \"{NoCoreSimpleName}\", please make sure to define the preferred no core! The following cores can be chosen: \n\n";
                        exceptionMessage = _noCoreConfigs.Aggregate(exceptionMessage, (current, noCore) => current + $"- {noCore.UniqueName}\n");
                        throw new Exception(exceptionMessage);
                    }
                }
            }
            catch (Exception e)
            {
                //Set Defaults
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
    
    [ProtoContract]
	public class Zones {
        [ProtoMember(1)]
		public int Id;
        [ProtoMember(2)] 
        public Vector3D Position;
        [ProtoMember(3)]
        public double Radius;
        [ProtoMember(4)]
        public List<string> AllowedCoresSubtype = new List<string>();

    }
    
    [ProtoContract]
    public class CoreManifest
    {
        [ProtoMember(1)] public List<string> ShipCoreFilenames;
    }
    
    [ProtoContract]
    public class ShipCore
    {
        [ProtoMember(1)]
        public string SubtypeId = string.Empty;
        [ProtoMember(2)]
        public string UniqueName = string.Empty;
        [ProtoMember(3)]
        public bool ForceBroadCast = false;
        [ProtoMember(4)]
        public float ForceBroadCastRange = 0;
        [ProtoMember(5)]
        public bool LargeGridStatic = false;
        [ProtoMember(6)]
        public bool LargeGridMobile = false;
        [ProtoMember(7)]
        public bool SmallGrid = false;
        [ProtoMember(8)]
        public int MaxBlocks = -1;
        [ProtoMember(9)]
        public float MaxMass = -1;
        [ProtoMember(10)]
        public int MaxPCU = -1;
        [ProtoMember(11)]
        public int MaxPerFaction = -1;
        [ProtoMember(12)]
        public int MaxPerPlayer = -1;
        [ProtoMember(13)]
        public int MinBlocks = -1;
        [ProtoMember(14)]
        public int MinPlayers = -1;
        [ProtoMember(15)]
        public GridModifiers Modifiers = new GridModifiers();
        [ProtoMember(16)]
        public GridDefenseModifiers PassiveDefenseModifiers = new GridDefenseModifiers();
        [ProtoMember(17)]
        public bool SpeedBoostEnabled = false;
        [ProtoMember(18)]
        public bool EnableActiveDefenseModifiers = false;
        [ProtoMember(19)]
        public GridDefenseModifiers ActiveDefenseModifiers = new GridDefenseModifiers();
        [ProtoMember(20)] 
        public bool EnableReloadModifier = false;
        [ProtoMember(21)] 
        public float ReloadModifier = 1f;
        [ProtoMember(22)]
        public BlockLimit[] BlockLimits = Array.Empty<BlockLimit>();
    }
    
    [ProtoContract]
    public class GridModifiers
    {
        [ProtoMember(1)]
        public float AssemblerSpeed = 1;
        [ProtoMember(2)]
        public float DrillHarvestMultiplier = 1;
        [ProtoMember(3)]
        public float GyroEfficiency = 1;
        [ProtoMember(4)]
        public float GyroForce = 1;
        [ProtoMember(5)]
        public float PowerProducersOutput = 1;
        [ProtoMember(6)]
        public float RefineEfficiency = 1;
        [ProtoMember(7)]
        public float RefineSpeed = 1;
        [ProtoMember(8)]
        public float ThrusterEfficiency = 1;
        [ProtoMember(9)]
        public float ThrusterForce = 1;
        [ProtoMember(10)]
        public float MaxSpeed = 100.0f;
        [ProtoMember(13)]
        public float MaxBoost = 1.2f;
        [ProtoMember(14)]
        public float BoostDuration = 10f; 
        [ProtoMember(15)]
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
        public string Name;
        public float Value;

        public ModifierNameValue(string name, float value)
        {
            Name = name;
            Value = value;
        }
    }

    [ProtoContract]
    public class BlockLimit
    {
        [ProtoMember(1)] 
        public string Name = string.Empty;

        [ProtoMember(2)] 
        public string[] BlockGroups = Array.Empty<string>();

        [ProtoMember(3)] 
        public float MaxCount = 0;

        [ProtoMember(4)] 
        public bool TurnedOffByNoFlyZone = false;
        
        [ProtoMember(5)] 
        public PunishmentType PunishmentType = PunishmentType.ShutOff;
    }

    [ProtoContract]
    public class BlockGroups
    {
        [ProtoMember(1)]
        public string Name = string.Empty;
        [ProtoMember(2)]
        public List<BlockType> BlockTypes = new List<BlockType>();
    }
    
    [ProtoContract]
    public class BlockType
    {
        [ProtoMember(1)] 
        public string TypeId;

        [ProtoMember(2)] 
        public string SubtypeId;

        [ProtoMember(3)] 
        public float CountWeight;

        public BlockType()
        {
        }

        public BlockType(string typeId, string subtypeId = "", float countWeight = 1)
        {
            TypeId = typeId;
            SubtypeId = subtypeId;
            CountWeight = countWeight;
        }
    }

    [ProtoContract]
    public class GridDefenseModifiers
    {
        [ProtoMember(1)]
        public float Bullet = 1f;
        [ProtoMember(2)]
        public float Rocket = 1f;
        [ProtoMember(3)]
        public float Explosion = 1f;
        [ProtoMember(4)]
        public float Environment = 1f;
        [ProtoMember(5)]
        public float Energy = 1f;
        [ProtoMember(6)]
        public float Kinetic = 1f;
        [ProtoMember(7)]
        public float Duration = 0f;
        [ProtoMember(8)]
        public float Cooldown = 0f;
    }

    [ProtoContract]
    public enum PunishmentType
    {
        ShutOff,
        Damage,
        Delete,
        Explode
    }
}