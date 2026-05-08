using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRageMath;

namespace ShipCoreFramework
{
    [XmlRoot("ModConfig")]
    public partial class ModConfig
    {
        [XmlIgnore] private const string GlobalConfigFileName = "ShipCoreConfig_World.xml";
        [XmlIgnore] private const string CoreManifestFileName = @"Data\ShipCoreConfig_Manifest.xml";
        [XmlIgnore] private const string BlockGroupsFileName = @"Data\ShipCoreConfig_Groups.xml";
        [XmlIgnore] private const string DefaultNoCoreFileName = @"Data\ShipCoreConfig_No_Core.xml";
        [XmlIgnore] private const string LegacyIgnoreAiKey = "ShipCore.IgnoreAiV1";
        [XmlIgnore] private const string LegacyIgnoredFactionsKey = "ShipCore.IgnoredFactionsV1";
        [XmlIgnore] private const string LegacySelectedNoCoreKey = "ShipCore.SelectedNoCoreBlobV1";
        [XmlIgnore] private static readonly string[] DefaultIgnoredFactionTagValues =
        {
            "SPRT", "ADMIN", "FMCA", "BORG", "TERA"
        };
        [XmlIgnore] public readonly List<ShipCore> NoCoreConfigs = new List<ShipCore>();
        [XmlIgnore] public readonly List<BlockGroup> BlockGroups = new List<BlockGroup>();
        [XmlIgnore] public readonly List<ShipCore> ShipCores = new List<ShipCore>();
        [XmlIgnore] public readonly List<ManifestCoreGroup> ManifestCoreGroups = new List<ManifestCoreGroup>();
        [XmlIgnore] public readonly List<UpgradeModuleConfig> UpgradeModules = new List<UpgradeModuleConfig>();

        [XmlElement("IgnoreAiFactions")]
        public bool IgnoreAiFactions;

        [XmlArray("IgnoredFactionTags")]
        [XmlArrayItem("Tag")]
        public List<string> IgnoredFactionTags = new List<string>();

        [XmlElement("SelectedNoCoreUniqueName")]
        public string SelectedNoCoreUniqueName = string.Empty;

        [XmlIgnore] public ShipCore SelectedNoCore;

        [XmlElement("DebugMode")] public bool DebugMode;
        [XmlElement("CombatLogging")] public bool CombatLogging = true;
        [XmlElement("LOG_LEVEL")] public int LogLevel = 2;
        [XmlElement("CLIENT_OUTPUT_LOG_LEVEL")] public int ClientOutputLogLevel = 2;
        [XmlElement("MaxPossibleSpeedMetersPerSecond")] public float MaxPossibleSpeedMetersPerSecond = 300;
        [XmlElement("MassTypeMode")] public MassTypeMode MassTypeMode = MassTypeMode.Dry;
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
        [XmlArray("ManifestGroups")]
        [XmlArrayItem("Group")]
        public List<ManifestCoreGroup> ManifestGroups = new List<ManifestCoreGroup>();
        [XmlElement("ShipCore")]
        public List<ManifestShipCoreEntry> ShipCores = new List<ManifestShipCoreEntry>();
        [XmlElement("UpgradeModule")]
        public List<ManifestUpgradeModuleEntry> UpgradeModules = new List<ManifestUpgradeModuleEntry>();
    }

    [XmlRoot("ManifestGroup")]
    public class ManifestCoreGroup
    {
        [XmlElement("Name")]
        public string Name = string.Empty;

        [XmlElement("MaxCount")]
        public int MaxCount = -1;

        [XmlIgnore]
        internal string ConfigSource = string.Empty;

        [XmlIgnore]
        internal string ConfigFile = string.Empty;

        [XmlIgnore]
        public readonly HashSet<string> CoreSubtypeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    [XmlRoot("ManifestShipCore")]
    public class ManifestShipCoreEntry
    {
        [XmlElement("Filename")]
        public string Filename = string.Empty;

        [XmlElement("Group")]
        public List<string> Groups = new List<string>();

        [XmlElement("BlacklistedCoreSubtypeId")]
        public List<string> BlacklistedCoreSubtypeIds = new List<string>();
    }

    [XmlRoot("ManifestUpgradeModule")]
    public class ManifestUpgradeModuleEntry
    {
        [XmlElement("Filename")]
        public string Filename = string.Empty;
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

        [XmlElement("FactionPlayersNeededPerCore")]
        public int FactionPlayersNeededPerCore = -1;

        [XmlElement("MaxPerPlayer")]
        public int MaxPerPlayer = -1;

        [XmlElement("MinPlayers")]
        public int MinPlayers = -1;

        [XmlElement("MaxPlayers")]
        public int MaxPlayers = -1;

        [XmlIgnore]
        public readonly List<string> ManifestGroupNames = new List<string>();

        [XmlIgnore]
        public readonly HashSet<string> ConnectorBlacklistCoreSubtypeIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [XmlElement("AllowedUpgradeModules")]
        public UpgradeModuleAllowance[] AllowedUpgradeModules = Array.Empty<UpgradeModuleAllowance>();

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
        public BlockLimit[] BlockLimits = Array.Empty<BlockLimit>();

        [XmlIgnore]
        internal string ConfigSource = string.Empty;

        [XmlIgnore]
        internal string ConfigFile = string.Empty;

        public bool IsUpgradeModuleAllowed(string moduleSubtypeId)
        {
            return !string.IsNullOrWhiteSpace(moduleSubtypeId) && 
                   AllowedUpgradeModuleCounts.ContainsKey(moduleSubtypeId);
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

        public bool IsConnectorBlacklistedCore(string coreSubtypeId)
        {
            return !string.IsNullOrWhiteSpace(coreSubtypeId) &&
                   ConnectorBlacklistCoreSubtypeIds.Contains(coreSubtypeId);
        }

        internal void NormalizeAllowedUpgradeModules(string source, string coreFileOrKey)
        {
            AllowedUpgradeModuleCounts.Clear();
            if (AllowedUpgradeModules == null)
            {
                AllowedUpgradeModules = Array.Empty<UpgradeModuleAllowance>();
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
        public UpgradeStatModifier[] Modifiers = Array.Empty<UpgradeStatModifier>();

        [XmlElement("BlockLimitModifiers")]
        public BlockLimitModifier[] BlockLimitModifiers = Array.Empty<BlockLimitModifier>();

        [XmlIgnore]
        internal string ConfigSource = string.Empty;

        [XmlIgnore]
        internal string ConfigFile = string.Empty;
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
        public string[] BlockGroupsShortHand = Array.Empty<string>();

        [XmlIgnore]
        public List<BlockGroup> BlockGroups = new List<BlockGroup>();

        [XmlElement("MaxCount")]
        public float MaxCount;

        [XmlElement("CrossConnectorPunishment")]
        public bool CrossConnectorPunishment;

        [XmlElement("PunishByNoFlyZone")]
        public bool PunishByNoFlyZone;

        [XmlElement("IsCriticalLimit")]
        public bool IsCriticalLimit;

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
                    if (blockType.Matches(key))
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

        [XmlIgnore]
        internal string ConfigSource = string.Empty;

        [XmlIgnore]
        internal string ConfigFile = string.Empty;
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

        internal bool Matches(BlockKey key)
        {
            return Matches(key.TypeId, key.SubtypeId);
        }

        internal bool Matches(string typeId, string subtypeId)
        {
            var configuredTypeId = (TypeId ?? string.Empty).Trim();
            if (!string.Equals(configuredTypeId, (typeId ?? string.Empty).Trim(), StringComparison.Ordinal))
                return false;

            var configuredSubtypeId = (SubtypeId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(configuredSubtypeId))
                return true;

            return string.Equals(configuredSubtypeId, "any", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(configuredSubtypeId, (subtypeId ?? string.Empty).Trim(), StringComparison.Ordinal);
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

    [XmlRoot("MassTypeMode")]
    public enum MassTypeMode
    {
        Dry = 0,
        Wet = 1
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
