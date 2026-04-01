using System;
using ProtoBuf;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

namespace ShipCoreFramework
{
    /// <summary>
    /// Ship Core Framework API Data Structures.
    /// Other mods can copy this file to get all API-related data types, constants and method IDs.
    /// </summary>
    public static class ApiConstants
    {
        /// <summary>
        /// Unique identifier for Ship Core Framework API messages.
        /// Other mods should use this ID to register a message handler.
        /// Example:
        /// MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.API_ID, OnApiReceived);
        /// </summary>
        public const long API_ID = 3217652398L;

        /// <summary>
        /// API Major version.
        /// Increment when you make breaking changes to the API contract.
        /// </summary>
        public const int API_MAJOR = 3;

        /// <summary>
        /// API Minor version.
        /// Increment when you add functionality in a backwards compatible way,
        /// but you still want consumers to update if you require exact matches.
        /// </summary>
        public const int API_MINOR = 2;

        /// <summary>
        /// Encoded API version (Major.Minor) packed into a single int.
        /// Consumers must match this exactly.
        /// </summary>
        public const int API_VERSION = (API_MAJOR << 8) | API_MINOR;

        // Event IDs - Other mods can register handlers for these to receive event notifications.
        // NOTE: If you want cross-assembly safe event payloads, send byte[] and deserialize on the client side.
        public const long EVENT_CORE_ACTIVATED = 3217652399L;
        public const long EVENT_CORE_DEACTIVATED = 3217652400L;
        public const long EVENT_LIMITS_RECALCULATED = 3217652401L;
        public const long EVENT_LIMITS_ENFORCED = 3217652402L;
        public const long EVENT_BOOST_ACTIVATED = 3217652403L;
        public const long EVENT_BOOST_DEACTIVATED = 3217652404L;
        public const long EVENT_ACTIVE_DEFENSE_ACTIVATED = 3217652405L;
        public const long EVENT_ACTIVE_DEFENSE_DEACTIVATED = 3217652406L;
        public const long EVENT_GRID_ADDED_TO_GROUP = 3217652407L;
        public const long EVENT_GRID_REMOVED_FROM_GROUP = 3217652408L;
    }

    /// <summary>
    /// Integer method IDs for the API method factory.
    /// Consumers should prefer these IDs over string keys.
    /// </summary>
    public static class ApiMethodId
    {
        /// <summary>
        /// Returns <see cref="ApiConstants.API_VERSION"/> as int.
        /// Signature: object -> int (arg ignored).
        /// </summary>
        public const int GetApiVersion = 0;

        /// <summary>
        /// Gets the active ShipCore configuration for a grid (serialized).
        /// Signature: IMyCubeGrid -> byte[] (ShipCoreData).
        /// </summary>
        public const int GetGridCore_Binary = 10;

        /// <summary>
        /// Gets a specific ShipCore configuration by its SubtypeId (serialized).
        /// Signature: string -> byte[] (ShipCoreData).
        /// </summary>
        public const int GetCoreBySubtypeId_Binary = 11;

        /// <summary>
        /// Gets all available ShipCore configurations loaded by the framework (serialized).
        /// Signature: object -> byte[] (List&lt;ShipCoreData&gt;), arg ignored.
        /// </summary>
        public const int GetAllCoreConfigs_Binary = 12;

        /// <summary>
        /// Gets block limit status for a grid (serialized).
        /// Signature: IMyCubeGrid -> byte[] (Dictionary&lt;string, LimitStatusData&gt;).
        /// </summary>
        public const int GetBlockLimitsStatus_Binary = 13;

        /// <summary>
        /// Checks if adding blocks would violate limits.
        /// Signature: object -> bool (expects MyTuple&lt;IMyCubeGrid, string, string, int&gt;).
        /// </summary>
        public const int IsBlockAllowed = 14;

        /// <summary>
        /// Gets current grid modifiers (serialized).
        /// Signature: IMyCubeGrid -> byte[] (GridModifiersData).
        /// </summary>
        public const int GetGridModifiers_Binary = 15;

        /// <summary>
        /// Gets the maximum speed allowed for a grid based on its core.
        /// Signature: IMyCubeGrid -> float.
        /// </summary>
        public const int GetMaxSpeed = 16;

        /// <summary>
        /// Checks if boost is currently active for a grid.
        /// Signature: IMyCubeGrid -> bool.
        /// </summary>
        public const int IsBoostActive = 17;

        /// <summary>
        /// Gets the currently selected NoCore configuration (serialized).
        /// Signature: object -> byte[] (ShipCoreData), arg ignored.
        /// </summary>
        public const int GetNoCoreConfig_Binary = 18;
        
        /// <summary>
        /// Gets the SpeedModifiers for the grid's active core (serialized).
        /// Signature: IMyCubeGrid -> byte[] (SpeedModifiersData).
        /// </summary>
        public const int GetSpeedModifiers_Binary = 19;
        

        /// <summary>
        /// Gets BoostResistance from the grid's active core SpeedModifiers.
        /// Signature: IMyCubeGrid -> float.
        /// </summary>
        public const int GetBoostResistance = 20;

        /// <summary>
        /// Gets base max speed in m/s (without boost), based on core SpeedModifiers.
        /// Signature: IMyCubeGrid -> float.
        /// </summary>
        public const int GetBaseMaxSpeed = 21;

        /// <summary>
        /// Gets max boost multiplier (core SpeedModifiers.MaxBoost).
        /// Signature: IMyCubeGrid -> float.
        /// </summary>
        public const int GetMaxBoostMultiplier = 22;

        /// <summary>
        /// Gets boost duration in seconds (core SpeedModifiers.BoostDuration).
        /// Signature: IMyCubeGrid -> float.
        /// </summary>
        public const int GetBoostDuration = 23;

        /// <summary>
        /// Gets boost cooldown in seconds (core SpeedModifiers.BoostCoolDown).
        /// Signature: IMyCubeGrid -> float.
        /// </summary>
        public const int GetBoostCooldown = 24;

        /// <summary>
        /// Enables/disables friction-based speed limiting for a logical grid group.
        /// Signature: object -> bool (expects MyTuple&lt;IMyCubeGrid, bool&gt;).
        /// </summary>
        public const int SetFrictionEnabledForGroup = 25;

        /// <summary>
        /// Gets whether friction-based speed limiting is enabled for a logical grid group.
        /// Signature: IMyCubeGrid -> bool.
        /// </summary>
        public const int GetFrictionEnabledForGroup = 26;

        /// <summary>
        /// Sets the maximum friction deceleration (m/s^2) override for a logical grid group.
        /// Use a value &gt;= 0 to override the core/config value.
        /// Signature: object -> bool (expects MyTuple&lt;IMyCubeGrid, float&gt;).
        /// </summary>
        public const int SetFrictionMaximumDecelerationForGroup = 27;

        /// <summary>
        /// Clears the maximum friction deceleration override for a logical grid group.
        /// Signature: IMyCubeGrid -> bool.
        /// </summary>
        public const int ClearFrictionMaximumDecelerationForGroup = 28;

        /// <summary>
        /// Gets the maximum friction deceleration override for a logical grid group.
        /// Returns -1 if no override is set.
        /// Signature: IMyCubeGrid -> float.
        /// </summary>
        public const int GetFrictionMaximumDecelerationForGroup = 29;

        /// <summary>
        /// Gets the current world setting for how friction speeds are interpreted.
        /// 0 = Modifier, 1 = Absolute.
        /// Signature: object -> int (arg ignored).
        /// </summary>
        public const int GetFrictionSpeedValueMode = 30;

        /// <summary>
        /// Sets the minimum friction speed override (absolute m/s) for a logical grid group.
        /// Use a value &lt; 0 to clear the override.
        /// Signature: object -> object (expects MyTuple&lt;IMyCubeGrid, float&gt;, returns MyTuple&lt;bool, string&gt;).
        /// </summary>
        public const int SetFrictionMinimumSpeedAbsoluteForGroup = 31;

        /// <summary>
        /// Sets the maximum friction speed override (absolute m/s) for a logical grid group.
        /// Use a value &lt; 0 to clear the override.
        /// Signature: object -> object (expects MyTuple&lt;IMyCubeGrid, float&gt;, returns MyTuple&lt;bool, string&gt;).
        /// </summary>
        public const int SetFrictionMaximumSpeedAbsoluteForGroup = 32;

        /// <summary>
        /// Gets the minimum friction speed override (absolute m/s) for a logical grid group.
        /// Returns -1 if no override is set.
        /// Signature: IMyCubeGrid -> object (returns MyTuple&lt;float, string&gt;).
        /// </summary>
        public const int GetFrictionMinimumSpeedAbsoluteForGroup = 33;

        /// <summary>
        /// Gets the maximum friction speed override (absolute m/s) for a logical grid group.
        /// Returns -1 if no override is set.
        /// Signature: IMyCubeGrid -> object (returns MyTuple&lt;float, string&gt;).
        /// </summary>
        public const int GetFrictionMaximumSpeedAbsoluteForGroup = 34;

        /// <summary>
        /// Sets the minimum friction speed override (modifier) for a logical grid group.
        /// Use a value &lt; 0 to clear the override.
        /// Signature: object -> object (expects MyTuple&lt;IMyCubeGrid, float&gt;, returns MyTuple&lt;bool, string&gt;).
        /// </summary>
        public const int SetFrictionMinimumSpeedModifierForGroup = 35;

        /// <summary>
        /// Sets the maximum friction speed override (modifier) for a logical grid group.
        /// Use a value &lt; 0 to clear the override.
        /// Signature: object -> object (expects MyTuple&lt;IMyCubeGrid, float&gt;, returns MyTuple&lt;bool, string&gt;).
        /// </summary>
        public const int SetFrictionMaximumSpeedModifierForGroup = 36;

        /// <summary>
        /// Gets the minimum friction speed override (modifier) for a logical grid group.
        /// Returns -1 if no override is set.
        /// Signature: IMyCubeGrid -> object (returns MyTuple&lt;float, string&gt;).
        /// </summary>
        public const int GetFrictionMinimumSpeedModifierForGroup = 37;

        /// <summary>
        /// Gets the maximum friction speed override (modifier) for a logical grid group.
        /// Returns -1 if no override is set.
        /// Signature: IMyCubeGrid -> object (returns MyTuple&lt;float, string&gt;).
        /// </summary>
        public const int GetFrictionMaximumSpeedModifierForGroup = 38;

        // Optional: Field getters for "no parsing" access (primitives only).
        // These can be handy if a consumer only needs a single field and wants to avoid deserializing a full DTO.
        public const int GetGridCore_SubtypeId = 100;   // IMyCubeGrid -> string
        public const int GetGridCore_UniqueName = 101;  // IMyCubeGrid -> string
        public const int GetGridCore_MaxBlocks = 102;   // IMyCubeGrid -> int
    }

    // ===== Data Structures (DTOs) =====
    //
    // IMPORTANT:
    // These DTOs are intended to be serialized to byte[] by the provider and deserialized by the consumer.
    // The consumer must not attempt to cast provider DTO instances directly.
    //
    // ProtoBuf attributes are used because Space Engineers commonly supports protobuf-net serialization.
    // Fields remain public for simplicity.

    /// <summary>
    /// Ship Core configuration data.
    /// This is a lightweight DTO containing essential core information.
    /// </summary>
    [ProtoContract]
    public class ShipCoreData
    {
        [ProtoMember(1)] public string SubtypeId;
        [ProtoMember(2)] public string UniqueName;
        [ProtoMember(3)] public bool ForceBroadCast;
        [ProtoMember(4)] public float ForceBroadCastRange;
        [ProtoMember(5)] public MobilityTypeData MobilityTypeData;
        [ProtoMember(6)] public int MaxBlocks;
        [ProtoMember(7)] public float MaxMass;
        [ProtoMember(8)] public int MaxPCU;
        [ProtoMember(9)] public int MaxPerFaction;
        [ProtoMember(10)] public int MaxPerPlayer;
        [ProtoMember(11)] public int MinPlayers;
        [ProtoMember(12)] public GridModifiersData Modifiers;
        [ProtoMember(13)] public GridDefenseModifiersData PassiveDefenseModifiers;
        [ProtoMember(14)] public bool SpeedBoostEnabled;
        [ProtoMember(15)] public bool EnableActiveDefenseModifiers;
        [ProtoMember(16)] public GridDefenseModifiersData ActiveDefenseModifiers;
        [ProtoMember(17)] public bool DynamicBoostEnabled;
        [ProtoMember(18)] public SpeedModifiersData SpeedModifiers;
        [ProtoMember(19)] public int MinBlocks;
        [ProtoMember(20)] public int MaxPlayers;
    }

    /// <summary>
    /// Grid modifiers data (performance multipliers).
    /// </summary>
    [ProtoContract]
    public class GridModifiersData
    {
        [ProtoMember(1)] public float AssemblerSpeed;
        [ProtoMember(2)] public float DrillHarvestMultiplier;
        [ProtoMember(3)] public float GyroEfficiency;
        [ProtoMember(4)] public float GyroForce;
        [ProtoMember(5)] public float PowerProducersOutput;
        [ProtoMember(6)] public float RefineEfficiency;
        [ProtoMember(7)] public float RefineSpeed;
        [ProtoMember(8)] public float ThrusterEfficiency;
        [ProtoMember(9)] public float ThrusterForce;
    }
    
    /// <summary>
    /// Speed modifiers data (movement/boost tuning).
    /// </summary>
    [ProtoContract]
    public class SpeedModifiersData
    {
        [ProtoMember(1)] public float MaxSpeed;
        [ProtoMember(2)] public float MaxBoost;
        [ProtoMember(3)] public float BoostDuration;
        [ProtoMember(4)] public float BoostCoolDown;

        // Legacy field kept for backwards compatibility (previously BoostResistance).
        [ProtoMember(5)] public float BoostResistance;

        // Friction tuning
        [ProtoMember(6)] public float MinimumFrictionSpeedAbsolute;
        [ProtoMember(7)] public float MaximumFrictionSpeedAbsolute;
        [ProtoMember(8)] public float MaximumFrictionDeceleration;
        [ProtoMember(9)] public float MinimumFrictionSpeedModifier;
        [ProtoMember(10)] public float MaximumFrictionSpeedModifier;
    }

    /// <summary>
    /// Defense modifiers data (damage reduction multipliers).
    /// </summary>
    [ProtoContract]
    public class GridDefenseModifiersData
    {
        [ProtoMember(1)] public float Bullet;
        [ProtoMember(2)] public float PostShield;
        [ProtoMember(3)] public float Duration;
        [ProtoMember(4)] public float Cooldown;
        [ProtoMember(5)] public float Rocket;
        [ProtoMember(6)] public float Explosion;
        [ProtoMember(7)] public float Environment;
        [ProtoMember(8)] public float Energy;
        [ProtoMember(9)] public float Kinetic;
    }

    /// <summary>
    /// Status information for a block limit.
    /// </summary>
    [ProtoContract]
    public class LimitStatusData
    {
        [ProtoMember(1)] public string Name;
        [ProtoMember(2)] public double Current;
        [ProtoMember(3)] public double Max;
        [ProtoMember(4)] public bool IsOverLimit;

        public override string ToString()
        {
            return $"{Name}: {Current:F1}/{Max:F1} {(IsOverLimit ? "[OVER LIMIT]" : "")}";
        }
    }

    /// <summary>
    /// Mobility type data for a grid core.
    /// </summary>
    public enum MobilityTypeData
    {
        Static = 0,
        Mobile = 1,
        Both = 2
    }

    public enum FrictionSpeedValueModeData
    {
        Modifier = 0,
        Absolute = 1
    }

    // ===== Event Argument Classes =====
    //
    // NOTE:
    // If you keep sending these directly as objects across assemblies, consumers will hit the same type identity issue.
    // If you want events to be cross-assembly safe, the recommended approach is to send byte[] payloads and deserialize.

    /// <summary>
    /// Event arguments for CoreActivated event.
    /// Fired when a core becomes the main/active core for a grid group.
    /// </summary>
    [ProtoContract]
    public class CoreActivatedEventArgs
    {
        [ProtoMember(1)] public long GroupGridId;
        [ProtoMember(2)] public string CoreSubtypeId;
        [ProtoMember(3)] public string CoreName;
        [ProtoMember(4)] public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for CoreDeactivated event.
    /// Fired when all cores are destroyed or the group loses its main core.
    /// </summary>
    [ProtoContract]
    public class CoreDeactivatedEventArgs
    {
        [ProtoMember(1)] public long GroupGridId;
        [ProtoMember(2)] public string PreviousCoreSubtypeId;
        [ProtoMember(3)] public string PreviousCoreName;
        [ProtoMember(4)] public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for LimitsRecalculated event.
    /// Fired when block limits are recalculated for a grid group.
    /// </summary>
    [ProtoContract]
    public class LimitsRecalculatedEventArgs
    {
        [ProtoMember(1)] public long GroupGridId;
        [ProtoMember(2)] public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for LimitsEnforced event.
    /// Fired when block limit enforcement runs and blocks are punished.
    /// </summary>
    [ProtoContract]
    public class LimitsEnforcedEventArgs
    {
        [ProtoMember(1)] public long GroupGridId;
        [ProtoMember(2)] public int BlocksPunished;
        [ProtoMember(3)] public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for Boost activation/deactivation events.
    /// </summary>
    [ProtoContract]
    public class BoostEventArgs
    {
        [ProtoMember(1)] public long GroupGridId;
        [ProtoMember(2)] public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for Active Defense activation/deactivation events.
    /// </summary>
    [ProtoContract]
    public class ActiveDefenseEventArgs
    {
        [ProtoMember(1)] public long GroupGridId;
        [ProtoMember(2)] public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for grid group membership changes.
    /// </summary>
    [ProtoContract]
    public class GridGroupEventArgs
    {
        [ProtoMember(1)] public long GridId;
        [ProtoMember(2)] public long GroupGridId;
        [ProtoMember(3)] public DateTime Timestamp;
    }
}
