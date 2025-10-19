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
    ///
    /// IMPORTANT: To use this API, copy ApiData.cs to your mod project.
    /// It contains all necessary data structures and constants.
    /// </summary>
    public static class ModAPI
    {
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
                    { "GetGridCore", new Func<IMyCubeGrid, ShipCoreData>(GetGridCore) },
                    { "GetCoreBySubtypeId", new Func<string, ShipCoreData>(GetCoreBySubtypeId) },
                    { "GetAllCoreConfigs", new Func<List<ShipCoreData>>(GetAllCoreConfigs) },
                    { "GetBlockLimitsStatus", new Func<IMyCubeGrid, Dictionary<string, LimitStatusData>>(GetBlockLimitsStatus) },
                    { "IsBlockAllowed", new Func<IMyCubeGrid, string, string, int, bool>(IsBlockAllowed) },
                    { "GetGridModifiers", new Func<IMyCubeGrid, GridModifiersData>(GetGridModifiers) },
                    { "GetMaxSpeed", new Func<IMyCubeGrid, float>(GetMaxSpeed) },
                    { "IsBoostActive", new Func<IMyCubeGrid, bool>(IsBoostActive) },
                    { "GetNoCoreConfig", new Func<ShipCoreData>(GetNoCoreConfig) }
                };

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.API_ID, apiDictionary);
                Utils.Log("ModAPI: Successfully broadcast API dictionary to other mods", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI: Failed to initialize API - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_CORE_ACTIVATED, eventData);
                Utils.Log($"ModAPI Event: CoreActivated for grid {grid?.DisplayName ?? "Unknown"}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastCoreActivated: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_CORE_DEACTIVATED, eventData);
                Utils.Log($"ModAPI Event: CoreDeactivated for grid {grid?.DisplayName ?? "Unknown"}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastCoreDeactivated: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_LIMITS_RECALCULATED, eventData);
                Utils.Log("ModAPI Event: LimitsRecalculated for group", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastLimitsRecalculated: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_LIMITS_ENFORCED, eventData);
                Utils.Log($"ModAPI Event: LimitsEnforced, punished {blocksPunished} blocks", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastLimitsEnforced: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_BOOST_ACTIVATED, eventData);
                Utils.Log($"ModAPI Event: BoostActivated for grid {grid?.DisplayName ?? "Unknown"}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastBoostActivated: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_BOOST_DEACTIVATED, eventData);
                Utils.Log($"ModAPI Event: BoostDeactivated for grid {grid?.DisplayName ?? "Unknown"}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastBoostDeactivated: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_ACTIVE_DEFENSE_ACTIVATED, eventData);
                Utils.Log($"ModAPI Event: ActiveDefenseActivated for grid {grid?.DisplayName ?? "Unknown"}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastActiveDefenseActivated: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_ACTIVE_DEFENSE_DEACTIVATED, eventData);
                Utils.Log($"ModAPI Event: ActiveDefenseDeactivated for grid {grid?.DisplayName ?? "Unknown"}", 3);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastActiveDefenseDeactivated: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_GRID_ADDED_TO_GROUP, eventData);
                Utils.Log($"ModAPI Event: GridAddedToGroup {grid?.DisplayName ?? "Unknown"}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastGridAddedToGroup: Exception - {ex}", 3);
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

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_GRID_REMOVED_FROM_GROUP, eventData);
                Utils.Log($"ModAPI Event: GridRemovedFromGroup {grid?.DisplayName ?? "Unknown"}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastGridRemovedFromGroup: Exception - {ex}", 3);
            }
        }

        // ===== Helper Methods =====

        private static ShipCoreData ConvertToShipCoreData(ShipCore core)
        {
            if (core == null)
            {
                return new ShipCoreData
                {
                    SubtypeId = string.Empty,
                    UniqueName = "NoCore",
                    Modifiers = new GridModifiersData { AssemblerSpeed = 1, DrillHarvestMultiplier = 1, GyroEfficiency = 1, GyroForce = 1, PowerProducersOutput = 1, RefineEfficiency = 1, RefineSpeed = 1, ThrusterEfficiency = 1, ThrusterForce = 1, MaxSpeed = 0.3f, MaxBoost = 1.2f, BoostDuration = 10f, BoostCoolDown = 60f }
                };
            }

            return new ShipCoreData
            {
                SubtypeId = core.SubtypeId,
                UniqueName = core.UniqueName,
                ForceBroadCast = core.ForceBroadCast,
                ForceBroadCastRange = core.ForceBroadCastRange,
                LargeGridStatic = core.LargeGridStatic,
                LargeGridMobile = core.LargeGridMobile,
                MaxBlocks = core.MaxBlocks,
                MaxMass = core.MaxMass,
                MaxPCU = core.MaxPCU,
                MaxPerFaction = core.MaxPerFaction,
                MaxPerPlayer = core.MaxPerPlayer,
                MinPlayers = core.MinPlayers,
                Modifiers = ConvertToGridModifiersData(core.Modifiers),
                PassiveDefenseModifiers = ConvertToDefenseModifiersData(core.PassiveDefenseModifiers),
                SpeedBoostEnabled = core.SpeedBoostEnabled,
                EnableActiveDefenseModifiers = core.EnableActiveDefenseModifiers,
                ActiveDefenseModifiers = ConvertToDefenseModifiersData(core.ActiveDefenseModifiers)
            };
        }

        private static GridModifiersData ConvertToGridModifiersData(GridModifiers modifiers)
        {
            if (modifiers == null)
            {
                return new GridModifiersData
                {
                    AssemblerSpeed = 1,
                    DrillHarvestMultiplier = 1,
                    GyroEfficiency = 1,
                    GyroForce = 1,
                    PowerProducersOutput = 1,
                    RefineEfficiency = 1,
                    RefineSpeed = 1,
                    ThrusterEfficiency = 1,
                    ThrusterForce = 1,
                    MaxSpeed = 0.3f,
                    MaxBoost = 1.2f,
                    BoostDuration = 10f,
                    BoostCoolDown = 60f
                };
            }

            return new GridModifiersData
            {
                AssemblerSpeed = modifiers.AssemblerSpeed,
                DrillHarvestMultiplier = modifiers.DrillHarvestMultiplier,
                GyroEfficiency = modifiers.GyroEfficiency,
                GyroForce = modifiers.GyroForce,
                PowerProducersOutput = modifiers.PowerProducersOutput,
                RefineEfficiency = modifiers.RefineEfficiency,
                RefineSpeed = modifiers.RefineSpeed,
                ThrusterEfficiency = modifiers.ThrusterEfficiency,
                ThrusterForce = modifiers.ThrusterForce,
                MaxSpeed = modifiers.MaxSpeed,
                MaxBoost = modifiers.MaxBoost,
                BoostDuration = modifiers.BoostDuration,
                BoostCoolDown = modifiers.BoostCoolDown
            };
        }

        private static GridDefenseModifiersData ConvertToDefenseModifiersData(GridDefenseModifiers modifiers)
        {
            if (modifiers == null)
            {
                return new GridDefenseModifiersData
                {
                    Bullet = 1f,
                    PostShield = 1f,
                    Duration = 0f,
                    Cooldown = 0f,
                    Rocket = 1f,
                    Explosion = 1f,
                    Environment = 1f,
                    Energy = 1f,
                    Kinetic = 1f
                };
            }

            return new GridDefenseModifiersData
            {
                Bullet = modifiers.Bullet,
                PostShield = modifiers.PostShield,
                Duration = modifiers.Duration,
                Cooldown = modifiers.Cooldown,
                Rocket = modifiers.Rocket,
                Explosion = modifiers.Explosion,
                Environment = modifiers.Environment,
                Energy = modifiers.Energy,
                Kinetic = modifiers.Kinetic
            };
        }

        // ===== Public API Methods =====

        /// <summary>
        /// Gets the active ShipCore configuration for a grid.
        /// Returns the NoCore config if the grid has no active core.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <returns>The active ShipCore config data, or NoCore if none exists</returns>
        public static ShipCoreData GetGridCore(IMyCubeGrid grid)
        {
            if (grid == null) return ConvertToShipCoreData(Session.Config.SelectedNoCore);

            try
            {
                var myCubeGrid = grid as MyCubeGrid;
                if (myCubeGrid == null) return ConvertToShipCoreData(Session.Config.SelectedNoCore);


                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return ConvertToShipCoreData(Session.Config.SelectedNoCore);

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return ConvertToShipCoreData(Session.Config.SelectedNoCore);

                return ConvertToShipCoreData(groupComponent.ShipCore ?? Session.Config.SelectedNoCore);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetGridCore: Exception - {ex}");
                return ConvertToShipCoreData(Session.Config.SelectedNoCore);
            }
        }

        /// <summary>
        /// Gets a specific ShipCore configuration by its SubtypeId.
        /// </summary>
        /// <param name="subtypeId">The SubtypeId of the core beacon block</param>
        /// <returns>The ShipCore config data, or NoCore if not found</returns>
        public static ShipCoreData GetCoreBySubtypeId(string subtypeId)
        {
            if (string.IsNullOrEmpty(subtypeId)) return ConvertToShipCoreData(Session.Config.SelectedNoCore);

            try
            {
                return ConvertToShipCoreData(Session.Config.GetShipCoreByTypeId(subtypeId));
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetCoreBySubtypeId: Exception - {ex}");
                return ConvertToShipCoreData(Session.Config.SelectedNoCore);
            }
        }

        /// <summary>
        /// Gets all available ShipCore configurations loaded by the framework.
        /// </summary>
        /// <returns>List of all ShipCore config data</returns>
        public static List<ShipCoreData> GetAllCoreConfigs()
        {
            try
            {
                var result = new List<ShipCoreData>();
                foreach (var core in Session.Config.ShipCores)
                {
                    result.Add(ConvertToShipCoreData(core));
                }
                return result;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetAllCoreConfigs: Exception - {ex}");
                return new List<ShipCoreData>();
            }
        }

        /// <summary>
        /// Gets the current block limit status for a grid.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <returns>Dictionary mapping limit names to their current status</returns>
        public static Dictionary<string, LimitStatusData> GetBlockLimitsStatus(IMyCubeGrid grid)
        {
            var result = new Dictionary<string, LimitStatusData>();
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

                    result[limit.Name] = new LimitStatusData
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
        /// <returns>GridModifiers data for the grid, or default modifiers if no core</returns>
        public static GridModifiersData GetGridModifiers(IMyCubeGrid grid)
        {
            if (grid == null) return ConvertToGridModifiersData(null);

            try
            {
                var groupData =  MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return ConvertToGridModifiersData(null);

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return ConvertToGridModifiersData(null);

                return ConvertToGridModifiersData(groupComponent.Modifiers);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetGridModifiers: Exception - {ex}");
                return ConvertToGridModifiersData(null);
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
                var groupData =  MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return 100f;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return 100f;

                var core = groupComponent.ShipCore;
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
                var groupData =  MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return false;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return false;

                return groupComponent.BoostEnabled;
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
        /// <returns>The active NoCore ShipCore config data</returns>
        public static ShipCoreData GetNoCoreConfig()
        {
            try
            {
                return ConvertToShipCoreData(Session.Config.SelectedNoCore ?? new ShipCore());
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetNoCoreConfig: Exception - {ex}");
                return ConvertToShipCoreData(null);
            }
        }
    }
}