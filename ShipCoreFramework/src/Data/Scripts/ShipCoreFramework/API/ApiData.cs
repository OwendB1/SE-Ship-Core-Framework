using System;
using VRage.Game.ModAPI;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

namespace ShipCoreFramework
{
    /// <summary>
    /// Ship Core Framework API Data Structures.
    /// Other mods can copy this file to get all API-related data types.
    /// </summary>
    public static class ApiConstants
    {
        /// <summary>
        /// Unique identifier for Ship Core Framework API messages.
        /// Other mods should use this ID to register a message handler.
        /// Example: MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.API_ID, OnApiReceived);
        /// </summary>
        public const long API_ID = 3217652398L;
        
        
        private const int API_MAJOR = 1;
        private const int API_MINOR = 1;
        
        /// <summary>
        /// Versioning of the API. This allows mods to check for API compatibility and prevent mismatched versions.
        /// </summary>
        public const int API_VERSION = (API_MAJOR << 8) | API_MINOR;

        // Event IDs - Other mods can register handlers for these to receive event notifications
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

    // ===== Data Structures =====

    /// <summary>
    /// Ship Core configuration data.
    /// This is a lightweight struct containing essential core information.
    /// </summary>
    public struct ShipCoreData
    {
        public string SubtypeId;
        public string UniqueName;
        public bool ForceBroadCast;
        public float ForceBroadCastRange;
        public MobilityTypeData MobilityTypeData;
        public int MaxBlocks;
        public float MaxMass;
        public int MaxPCU;
        public int MaxPerFaction;
        public int MaxPerPlayer;
        public int MinPlayers;
        public GridModifiersData Modifiers;
        public GridDefenseModifiersData PassiveDefenseModifiers;
        public bool SpeedBoostEnabled;
        public bool EnableActiveDefenseModifiers;
        public GridDefenseModifiersData ActiveDefenseModifiers;
    }

    /// <summary>
    /// Grid modifiers data (performance multipliers).
    /// </summary>
    public struct GridModifiersData
    {
        public float AssemblerSpeed;
        public float DrillHarvestMultiplier;
        public float GyroEfficiency;
        public float GyroForce;
        public float PowerProducersOutput;
        public float RefineEfficiency;
        public float RefineSpeed;
        public float ThrusterEfficiency;
        public float ThrusterForce;
        public float MaxSpeed;
        public float MaxBoost;
        public float BoostDuration;
        public float BoostCoolDown;
    }

    /// <summary>
    /// Defense modifiers data (damage reduction multipliers).
    /// </summary>
    public struct GridDefenseModifiersData
    {
        public float Bullet;
        public float PostShield;
        public float Duration;
        public float Cooldown;
        public float Rocket;
        public float Explosion;
        public float Environment;
        public float Energy;
        public float Kinetic;
    }

    /// <summary>
    /// Status information for a block limit.
    /// </summary>
    public struct LimitStatusData
    {
        public string Name;
        public double Current;
        public double Max;
        public bool IsOverLimit;

        public override string ToString()
        {
            return string.Format("{0}: {1:F1}/{2:F1} {3}", Name, Current, Max, IsOverLimit ? "[OVER LIMIT]" : "");
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

    // ===== Event Argument Classes =====

    /// <summary>
    /// Event arguments for CoreActivated event.
    /// Fired when a core becomes the main/active core for a grid group.
    /// </summary>
    public class CoreActivatedEventArgs
    {
        public IMyCubeGrid Grid;
        public string CoreSubtypeId;
        public string CoreName;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for CoreDeactivated event.
    /// Fired when all cores are destroyed or the group loses its main core.
    /// </summary>
    public class CoreDeactivatedEventArgs
    {
        public IMyCubeGrid Grid;
        public string PreviousCoreSubtypeId;
        public string PreviousCoreName;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for LimitsRecalculated event.
    /// Fired when block limits are recalculated for a grid group.
    /// </summary>
    public class LimitsRecalculatedEventArgs
    {
        public IMyGridGroupData GroupData;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for LimitsEnforced event.
    /// Fired when block limit enforcement runs and blocks are punished.
    /// </summary>
    public class LimitsEnforcedEventArgs
    {
        public IMyGridGroupData GroupData;
        public int BlocksPunished;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for Boost activation/deactivation events.
    /// </summary>
    public class BoostEventArgs
    {
        public IMyCubeGrid Grid;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for Active Defense activation/deactivation events.
    /// </summary>
    public class ActiveDefenseEventArgs
    {
        public IMyCubeGrid Grid;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Event arguments for grid group membership changes.
    /// </summary>
    public class GridGroupEventArgs
    {
        public IMyCubeGrid Grid;
        public IMyGridGroupData GroupData;
        public DateTime Timestamp;
    }
}