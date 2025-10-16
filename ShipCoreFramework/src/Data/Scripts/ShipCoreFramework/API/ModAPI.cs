using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global

namespace ShipCoreFramework
{
    /// <summary>
    /// Ship Core Framework external API for other mods to interact with the system.
    /// Other mods can receive this API via MyAPIGateway.Utilities.RegisterMessageHandler.
    /// </summary>
    public static class ModAPI
    {
        /// <summary>
        /// Unique identifier for Ship Core Framework API messages.
        /// Other mods should use this ID to register a message handler.
        /// Example: MyAPIGateway.Utilities.RegisterMessageHandler(ModAPI.API_ID, OnApiReceived);
        /// </summary>
        public const long API_ID = 3217652398L; // Unique ID for SCF

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

        private static bool _isInitialized;

        /// <summary>
        /// Initializes the API and broadcasts it to other mods.
        /// Called internally by Session component during LoadData.
        /// </summary>
        internal static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                var apiDictionary = new Dictionary<string, Delegate>
                {
                    { "GetGridCore", new Func<IMyCubeGrid, ShipCore>(GetGridCore) },
                    { "GetCoreBySubtypeId", new Func<string, ShipCore>(GetCoreBySubtypeId) },
                    { "GetAllCoreConfigs", new Func<List<ShipCore>>(GetAllCoreConfigs) },
                    { "GetBlockLimitsStatus", new Func<IMyCubeGrid, Dictionary<string, LimitStatus>>(GetBlockLimitsStatus) },
                    { "IsBlockAllowed", new Func<IMyCubeGrid, string, string, int, bool>(IsBlockAllowed) },
                    { "GetGridModifiers", new Func<IMyCubeGrid, GridModifiers>(GetGridModifiers) },
                    { "GetGroupComponent", new Func<IMyCubeGrid, GroupComponent>(GetGroupComponent) },
                    { "GetMaxSpeed", new Func<IMyCubeGrid, float>(GetMaxSpeed) },
                    { "IsBoostActive", new Func<IMyCubeGrid, bool>(IsBoostActive) },
                    { "GetNoCoreConfig", new Func<ShipCore>(GetNoCoreConfig) }
                };

                MyAPIGateway.Utilities.SendModMessage(API_ID, apiDictionary);
                Utils.Log("ModAPI: Successfully broadcast API dictionary to other mods", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI: Failed to initialize API - {ex}");
            }
        }

        /// <summary>
        /// Closes the API. Called during mod unload.
        /// </summary>
        internal static void Close()
        {
            _isInitialized = false;
        }

        // ===== Event Broadcasting Methods =====

        /// <summary>
        /// Broadcasts the CoreActivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastCoreActivated(IMyCubeGrid grid, string coreSubtypeId, string coreName)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new CoreActivatedEventArgs
                {
                    Grid = grid,
                    CoreSubtypeId = coreSubtypeId,
                    CoreName = coreName,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_CORE_ACTIVATED, eventData);
                Utils.Log($"ModAPI Event: CoreActivated for grid {grid?.DisplayName ?? "Unknown"}", 2);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastCoreActivated: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the CoreDeactivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastCoreDeactivated(IMyCubeGrid grid, string previousCoreSubtypeId, string previousCoreName)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new CoreDeactivatedEventArgs
                {
                    Grid = grid,
                    PreviousCoreSubtypeId = previousCoreSubtypeId,
                    PreviousCoreName = previousCoreName,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_CORE_DEACTIVATED, eventData);
                Utils.Log($"ModAPI Event: CoreDeactivated for grid {grid?.DisplayName ?? "Unknown"}", 2);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastCoreDeactivated: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the LimitsRecalculated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastLimitsRecalculated(IMyGridGroupData groupData)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new LimitsRecalculatedEventArgs
                {
                    GroupData = groupData,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_LIMITS_RECALCULATED, eventData);
                Utils.Log($"ModAPI Event: LimitsRecalculated for group", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastLimitsRecalculated: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the LimitsEnforced event to all subscribed mods.
        /// </summary>
        internal static void BroadcastLimitsEnforced(IMyGridGroupData groupData, int blocksPunished)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new LimitsEnforcedEventArgs
                {
                    GroupData = groupData,
                    BlocksPunished = blocksPunished,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_LIMITS_ENFORCED, eventData);
                Utils.Log($"ModAPI Event: LimitsEnforced, punished {blocksPunished} blocks", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastLimitsEnforced: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the BoostActivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastBoostActivated(IMyCubeGrid grid)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new BoostEventArgs
                {
                    Grid = grid,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_BOOST_ACTIVATED, eventData);
                Utils.Log($"ModAPI Event: BoostActivated for grid {grid?.DisplayName ?? "Unknown"}", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastBoostActivated: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the BoostDeactivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastBoostDeactivated(IMyCubeGrid grid)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new BoostEventArgs
                {
                    Grid = grid,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_BOOST_DEACTIVATED, eventData);
                Utils.Log($"ModAPI Event: BoostDeactivated for grid {grid?.DisplayName ?? "Unknown"}", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastBoostDeactivated: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the ActiveDefenseActivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastActiveDefenseActivated(IMyCubeGrid grid)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new ActiveDefenseEventArgs
                {
                    Grid = grid,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_ACTIVE_DEFENSE_ACTIVATED, eventData);
                Utils.Log($"ModAPI Event: ActiveDefenseActivated for grid {grid?.DisplayName ?? "Unknown"}", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastActiveDefenseActivated: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the ActiveDefenseDeactivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastActiveDefenseDeactivated(IMyCubeGrid grid)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new ActiveDefenseEventArgs
                {
                    Grid = grid,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_ACTIVE_DEFENSE_DEACTIVATED, eventData);
                Utils.Log($"ModAPI Event: ActiveDefenseDeactivated for grid {grid?.DisplayName ?? "Unknown"}", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastActiveDefenseDeactivated: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the GridAddedToGroup event to all subscribed mods.
        /// </summary>
        internal static void BroadcastGridAddedToGroup(IMyCubeGrid grid, IMyGridGroupData groupData)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new GridGroupEventArgs
                {
                    Grid = grid,
                    GroupData = groupData,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_GRID_ADDED_TO_GROUP, eventData);
                Utils.Log($"ModAPI Event: GridAddedToGroup {grid?.DisplayName ?? "Unknown"}", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastGridAddedToGroup: Exception - {ex}");
            }
        }

        /// <summary>
        /// Broadcasts the GridRemovedFromGroup event to all subscribed mods.
        /// </summary>
        internal static void BroadcastGridRemovedFromGroup(IMyCubeGrid grid, IMyGridGroupData groupData)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new GridGroupEventArgs
                {
                    Grid = grid,
                    GroupData = groupData,
                    Timestamp = DateTime.UtcNow
                };

                MyAPIGateway.Utilities.SendModMessage(EVENT_GRID_REMOVED_FROM_GROUP, eventData);
                Utils.Log($"ModAPI Event: GridRemovedFromGroup {grid?.DisplayName ?? "Unknown"}", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastGridRemovedFromGroup: Exception - {ex}");
            }
        }

        // ===== Public API Methods =====

        /// <summary>
        /// Gets the active ShipCore configuration for a grid.
        /// Returns the NoCore config if the grid has no active core.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <returns>The active ShipCore config, or NoCore if none exists</returns>
        public static ShipCore GetGridCore(IMyCubeGrid grid)
        {
            if (grid == null) return Session.Config.SelectedNoCore;

            try
            {
                var myCubeGrid = grid as MyCubeGrid;
                if (myCubeGrid == null) return Session.Config.SelectedNoCore;

                
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return Session.Config.SelectedNoCore;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return Session.Config.SelectedNoCore;

                return groupComponent.ShipCore ?? Session.Config.SelectedNoCore;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetGridCore: Exception - {ex}");
                return Session.Config.SelectedNoCore;
            }
        }

        /// <summary>
        /// Gets a specific ShipCore configuration by its SubtypeId.
        /// </summary>
        /// <param name="subtypeId">The SubtypeId of the core beacon block</param>
        /// <returns>The ShipCore config, or NoCore if not found</returns>
        public static ShipCore GetCoreBySubtypeId(string subtypeId)
        {
            if (string.IsNullOrEmpty(subtypeId)) return Session.Config.SelectedNoCore;

            try
            {
                return Session.Config.GetShipCoreByTypeId(subtypeId);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetCoreBySubtypeId: Exception - {ex}");
                return Session.Config.SelectedNoCore;
            }
        }

        /// <summary>
        /// Gets all available ShipCore configurations loaded by the framework.
        /// </summary>
        /// <returns>List of all ShipCore configs</returns>
        public static List<ShipCore> GetAllCoreConfigs()
        {
            try
            {
                return new List<ShipCore>(Session.Config.ShipCores);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetAllCoreConfigs: Exception - {ex}");
                return new List<ShipCore>();
            }
        }

        /// <summary>
        /// Gets the current block limit status for a grid.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <returns>Dictionary mapping limit names to their current status</returns>
        public static Dictionary<string, LimitStatus> GetBlockLimitsStatus(IMyCubeGrid grid)
        {
            var result = new Dictionary<string, LimitStatus>();
            if (grid == null) return result;

            try
            {
                var groupData =  MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return result;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return result;

                foreach (var kvp in groupComponent.Limits)
                {
                    var limit = kvp.Key;
                    var bucket = kvp.Value;

                    if (limit == null || bucket == null) continue;

                    double totalWeight;
                    lock (bucket.BucketLock)
                    {
                        totalWeight = bucket.TotalWeight;
                    }

                    result[limit.Name] = new LimitStatus
                    {
                        Name = limit.Name,
                        Current = totalWeight,
                        Max = limit.MaxCount,
                        IsOverLimit = totalWeight > limit.MaxCount
                    };
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetBlockLimitsStatus: Exception - {ex}");
            }

            return result;
        }

        /// <summary>
        /// Checks if adding a specific number of blocks would violate limits.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <param name="typeId">Block type ID (e.g., "MyObjectBuilder_Thrust")</param>
        /// <param name="subtypeId">Block subtype ID</param>
        /// <param name="count">Number of blocks to check</param>
        /// <returns>True if the blocks are allowed, false if they would violate limits</returns>
        public static bool IsBlockAllowed(IMyCubeGrid grid, string typeId, string subtypeId, int count)
        {
            if (grid == null || string.IsNullOrEmpty(typeId)) return true;

            try
            {
                var groupData =  MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return true;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return true;

                var blockKey = new BlockKey(typeId, subtypeId ?? string.Empty);

                foreach (var kvp in groupComponent.Limits)
                {
                    var limit = kvp.Key;
                    var bucket = kvp.Value;

                    if (limit == null || bucket == null) continue;

                    var weight = limit.GetWeight(blockKey);
                    if (weight <= 0) continue;

                    double totalWeight;
                    lock (bucket.BucketLock)
                    {
                        totalWeight = bucket.TotalWeight;
                    }

                    if (totalWeight + (weight * count) > limit.MaxCount)
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.IsBlockAllowed: Exception - {ex}");
                return true;
            }
        }

        /// <summary>
        /// Gets the current grid modifiers applied to a grid's core.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <returns>GridModifiers for the grid, or default modifiers if no core</returns>
        public static GridModifiers GetGridModifiers(IMyCubeGrid grid)
        {
            if (grid == null) return new GridModifiers();

            try
            {
                var groupData =  MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return new GridModifiers();

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return new GridModifiers();

                return groupComponent.Modifiers ?? new GridModifiers();
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetGridModifiers: Exception - {ex}");
                return new GridModifiers();
            }
        }

        /// <summary>
        /// Gets the internal GroupComponent for a grid (advanced usage).
        /// WARNING: Direct manipulation of GroupComponent can break the framework.
        /// Use with caution and only for read-only access.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <returns>The GroupComponent, or null if not found</returns>
        public static GroupComponent GetGroupComponent(IMyCubeGrid grid)
        {
            if (grid == null) return null;

            try
            {
                var groupData =  MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return null;

                GroupComponent groupComponent;
                return Session.GroupDict.TryGetValue(groupData, out groupComponent) ? groupComponent : null;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetGroupComponent: Exception - {ex}");
                return null;
            }
        }

        /// <summary>
        /// Gets the maximum speed allowed for a grid based on its core.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <returns>Max speed in m/s, accounting for boost if active</returns>
        public static float GetMaxSpeed(IMyCubeGrid grid)
        {
            if (grid == null) return 100f;

            try
            {
                var groupComponent = GetGroupComponent(grid);
                var core = groupComponent?.ShipCore;
                if (core == null) return 100f;

                var baseSpeed = core.Modifiers.MaxSpeed * Session.Config.MaxPossibleSpeedMetersPerSecond;

                if (groupComponent.BoostEnabled)
                    return baseSpeed * core.Modifiers.MaxBoost;

                return baseSpeed;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetMaxSpeed: Exception - {ex}");
                return 100f;
            }
        }

        /// <summary>
        /// Checks if boost is currently active for a grid.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <returns>True if boost is active, false otherwise</returns>
        public static bool IsBoostActive(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            try
            {
                var groupComponent = GetGroupComponent(grid);
                return groupComponent != null && groupComponent.BoostEnabled;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.IsBoostActive: Exception - {ex}");
                return false;
            }
        }

        /// <summary>
        /// Gets the currently selected NoCore configuration.
        /// This is the config applied to grids without a core beacon.
        /// </summary>
        /// <returns>The active NoCore ShipCore config</returns>
        public static ShipCore GetNoCoreConfig()
        {
            try
            {
                return Session.Config.SelectedNoCore ?? new ShipCore();
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetNoCoreConfig: Exception - {ex}");
                return new ShipCore();
            }
        }
    }

    /// <summary>
    /// Status information for a block limit.
    /// </summary>
    public class LimitStatus
    {
        public string Name;
        public double Current;
        public double Max;
        public bool IsOverLimit;

        public override string ToString()
        {
            return $"{Name}: {Current:F1}/{Max:F1} {(IsOverLimit ? "[OVER LIMIT]" : "")}";
        }
    }
}