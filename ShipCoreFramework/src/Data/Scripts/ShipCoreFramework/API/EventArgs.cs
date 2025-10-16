using System;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
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