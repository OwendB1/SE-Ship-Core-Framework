using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRageMath;

namespace ShipCoreFramework
{
    [XmlRoot("ModConfig")]
    public partial class ModConfig
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
        [XmlIgnore] public readonly List<UpgradeModuleConfig> UpgradeModules = new List<UpgradeModuleConfig>();
        [XmlIgnore] public List<string> IgnoredFactionTags = new List<string>();
        [XmlIgnore] public ShipCore SelectedNoCore;
        [XmlIgnore] public bool IgnoreAiFactions;

        [XmlElement("DebugMode")] public bool DebugMode;
        [XmlElement("CombatLogging")] public bool CombatLogging = true;
        [XmlElement("LOG_LEVEL")] public int LogLevel = 2;
        [XmlElement("CLIENT_OUTPUT_LOG_LEVEL")] public int ClientOutputLogLevel = 2;
        [XmlElement("MaxPossibleSpeedMetersPerSecond")] public float MaxPossibleSpeedMetersPerSecond = 300;
        [XmlElement("FrictionSpeedValueMode")] public FrictionSpeedValueMode FrictionSpeedValueMode = FrictionSpeedValueMode.Modifier;
        [XmlElement("NoFlyZones")] public List<Zones> NoFlyZones = new List<Zones>();
    }

    [XmlRoot("Zones")]
    public class Zones
    {
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
        [XmlElement("UpgradeModuleFilenames")] public List<string> UpgradeModuleFilenames = new List<string>();
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

        [XmlElement("MinBlocks")]
        public int MinBlocks = -1;

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

        [XmlElement("AllowedUpgradeModules")]
        public UpgradeModuleAllowance[] AllowedUpgradeModules = new UpgradeModuleAllowance[0];

        [XmlIgnore]
        public readonly Dictionary<string, int> AllowedUpgradeModuleCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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
        public BlockLimit[] BlockLimits = new BlockLimit[0];

        public bool IsUpgradeModuleAllowed(string moduleSubtypeId)
        {
            if (string.IsNullOrWhiteSpace(moduleSubtypeId)) return false;
            return AllowedUpgradeModuleCounts.ContainsKey(moduleSubtypeId);
        }

        public bool TryGetAllowedUpgradeModuleCount(string moduleSubtypeId, out int maxCount)
        {
            if (string.IsNullOrWhiteSpace(moduleSubtypeId))
            {
                maxCount = 0;
                return false;
            }

            return AllowedUpgradeModuleCounts.TryGetValue(moduleSubtypeId, out maxCount);
        }

        internal void NormalizeAllowedUpgradeModules(string source, string coreFileOrKey)
        {
            AllowedUpgradeModuleCounts.Clear();
            if (AllowedUpgradeModules == null)
            {
                AllowedUpgradeModules = new UpgradeModuleAllowance[0];
                return;
            }

            foreach (var allowance in AllowedUpgradeModules)
            {
                if (allowance == null || string.IsNullOrWhiteSpace(allowance.SubtypeId)) continue;

                if (AllowedUpgradeModuleCounts.ContainsKey(allowance.SubtypeId))
                    throw new Exception(
                        $"ShipCore '{UniqueName}' from {source} ({coreFileOrKey}) has duplicate AllowedUpgradeModules entry for '{allowance.SubtypeId}'.");

                AllowedUpgradeModuleCounts[allowance.SubtypeId] = allowance.MaxCount;
            }
        }
    }

    [XmlRoot("AllowedUpgradeModules")]
    public class UpgradeModuleAllowance
    {
        [XmlElement("SubtypeId")]
        public string SubtypeId = string.Empty;

        [XmlElement("MaxCount")]
        public int MaxCount;
    }

    [XmlRoot("UpgradeModule")]
    public class UpgradeModuleConfig
    {
        [XmlElement("SubtypeId")]
        public string SubtypeId = string.Empty;

        [XmlElement("UniqueName")]
        public string UniqueName = string.Empty;

        [XmlElement("Modifiers")]
        public UpgradeStatModifier[] Modifiers = new UpgradeStatModifier[0];

        [XmlElement("BlockLimitModifiers")]
        public BlockLimitModifier[] BlockLimitModifiers = new BlockLimitModifier[0];
    }

    [XmlRoot("Modifiers")]
    public class UpgradeStatModifier
    {
        [XmlElement("Stat")]
        public string Stat = string.Empty;

        [XmlElement("Value")]
        public float Value;

        [XmlElement("ModifierType")]
        public UpgradeModifierOperation ModifierType = UpgradeModifierOperation.Multiplicative;
    }

    [XmlRoot("BlockLimitModifiers")]
    public class BlockLimitModifier
    {
        [XmlElement("BlockLimitName")]
        public string BlockLimitName = string.Empty;

        [XmlElement("Value")]
        public float Value;

        [XmlElement("ModifierType")]
        public UpgradeModifierOperation ModifierType = UpgradeModifierOperation.Additive;
    }

    [XmlRoot("UpgradeModifierOperation")]
    public enum UpgradeModifierOperation
    {
        Additive = 0,
        Multiplicative = 1
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

        [XmlElement("MinimumFrictionSpeedAbsolute")]
        public float MinimumFrictionSpeedAbsolute = 100f;

        [XmlElement("MaximumFrictionSpeedAbsolute")]
        public float MaximumFrictionSpeedAbsolute = 290f;

        [XmlElement("MinimumFrictionSpeedModifier")]
        public float MinimumFrictionSpeedModifier = 0.3f;

        [XmlElement("MaximumFrictionSpeedModifier")]
        public float MaximumFrictionSpeedModifier = 0.8f;

        [XmlElement("MaximumFrictionDeceleration")]
        public float MaximumFrictionDeceleration = 1f;
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
        public string[] BlockGroupsShortHand = new string[0];

        [XmlIgnore]
        public List<BlockGroup> BlockGroups = new List<BlockGroup>();

        [XmlElement("MaxCount")]
        public float MaxCount;

        [XmlElement("CrossConnectorPunishment")]
        public bool CrossConnectorPunishment;

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
                        return blockType.CountWeight;
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

    [XmlRoot("FrictionSpeedValueMode")]
    public enum FrictionSpeedValueMode
    {
        Modifier = 0,
        Absolute = 1
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
        Right
    }
}
