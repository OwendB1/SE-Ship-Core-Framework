using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global

namespace ShipCoreFramework
{
    /// <summary>
    /// Ship Core Framework external API for other mods to interact with the system.
    ///
    /// This implementation avoids cross-assembly type identity issues by:
    /// - Broadcasting a method factory (Func&lt;int, Func&lt;object, object&gt;&gt;) via MyTuple.
    /// - Returning primitives directly (int, float, bool, string).
    /// - Returning custom DTOs as byte[] (SerializeToBinary) so consumers can deserialize locally.
    ///
    /// IMPORTANT:
    /// Other mods should copy ApiData.cs and use the provided client wrapper (see below).
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
                var apiPayload = MyTuple.Create(
                    ApiConstants.API_VERSION,
                    new Func<int, Func<object, object>>(MethodFactory)
                );

                MyAPIGateway.Utilities.SendModMessage(ApiConstants.API_ID, apiPayload);
                Utils.Log("ModAPI: Successfully broadcast API factory payload to other mods", 1);
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

        /// <summary>
        /// Produces an API method delegate for the requested method ID.
        /// The returned delegate uses only object input/output to avoid cross-assembly issues.
        /// </summary>
        private static Func<object, object> MethodFactory(int methodId)
        {
            switch (methodId)
            {
                case ApiMethodId.GetApiVersion:
                    return _ => ApiConstants.API_VERSION;

                case ApiMethodId.GetGridCore_Binary:
                    return arg =>
                    {
                        var grid = arg as IMyCubeGrid;
                        var dto = GetGridCore(grid);
                        return MyAPIGateway.Utilities.SerializeToBinary(dto);
                    };

                case ApiMethodId.GetCoreBySubtypeId_Binary:
                    return arg =>
                    {
                        var subtypeId = arg as string;
                        var dto = GetCoreBySubtypeId(subtypeId);
                        return MyAPIGateway.Utilities.SerializeToBinary(dto);
                    };

                case ApiMethodId.GetAllCoreConfigs_Binary:
                    return _ =>
                    {
                        var list = GetAllCoreConfigs();
                        return MyAPIGateway.Utilities.SerializeToBinary(list);
                    };

                case ApiMethodId.GetBlockLimitsStatus_Binary:
                    return arg =>
                    {
                        var grid = arg as IMyCubeGrid;
                        var dict = GetBlockLimitsStatus(grid);
                        return MyAPIGateway.Utilities.SerializeToBinary(dict);
                    };

                case ApiMethodId.IsBlockAllowed:
                    return arg =>
                    {
                        // Expect MyTuple<IMyCubeGrid, string, string, int>
                        var t = (MyTuple<IMyCubeGrid, string, string, int>)arg;
                        return IsBlockAllowed(t.Item1, t.Item2, t.Item3, t.Item4);
                    };

                case ApiMethodId.GetGridModifiers_Binary:
                    return arg =>
                    {
                        var grid = arg as IMyCubeGrid;
                        var dto = GetGridModifiers(grid);
                        return MyAPIGateway.Utilities.SerializeToBinary(dto);
                    };

                case ApiMethodId.GetMaxSpeed:
                    return arg => GetMaxSpeed(arg as IMyCubeGrid);

                case ApiMethodId.IsBoostActive:
                    return arg => IsBoostActive(arg as IMyCubeGrid);

                case ApiMethodId.GetNoCoreConfig_Binary:
                    return _ =>
                    {
                        var dto = GetNoCoreConfig();
                        return MyAPIGateway.Utilities.SerializeToBinary(dto);
                    };
                
                case ApiMethodId.GetSpeedModifiers_Binary:
                    return arg =>
                    {
                        var grid = arg as IMyCubeGrid;
                        var dto = GetSpeedModifiers(grid);
                        return MyAPIGateway.Utilities.SerializeToBinary(dto);
                    };

                case ApiMethodId.IsDynamicBoostEnabled:
                    return arg => IsDynamicBoostEnabled(arg as IMyCubeGrid);

                case ApiMethodId.GetBoostResistance:
                    return arg => GetBoostResistance(arg as IMyCubeGrid);

                case ApiMethodId.GetBaseMaxSpeed:
                    return arg => GetBaseMaxSpeed(arg as IMyCubeGrid);

                case ApiMethodId.GetMaxBoostMultiplier:
                    return arg => GetMaxBoostMultiplier(arg as IMyCubeGrid);

                case ApiMethodId.GetBoostDuration:
                    return arg => GetBoostDuration(arg as IMyCubeGrid);

                case ApiMethodId.GetBoostCooldown:
                    return arg => GetBoostCooldown(arg as IMyCubeGrid);

                case ApiMethodId.SetFrictionEnabledForGroup:
                    return arg =>
                    {
                        var t = (MyTuple<IMyGridGroupData, bool>)arg;
                        return SetFrictionEnabledForGroup(t.Item1, t.Item2);
                    };

                case ApiMethodId.GetFrictionEnabledForGroup:
                    return arg => GetFrictionEnabledForGroup(arg as IMyGridGroupData);

                case ApiMethodId.SetFrictionMaximumDecelerationForGroup:
                    return arg =>
                    {
                        var t = (MyTuple<IMyGridGroupData, float>)arg;
                        return SetFrictionMaximumDecelerationForGroup(t.Item1, t.Item2);
                    };

                case ApiMethodId.ClearFrictionMaximumDecelerationForGroup:
                    return arg => ClearFrictionMaximumDecelerationForGroup(arg as IMyGridGroupData);

                case ApiMethodId.GetFrictionMaximumDecelerationForGroup:
                    return arg => GetFrictionMaximumDecelerationForGroup(arg as IMyGridGroupData);

                // Optional primitive getters:
                case ApiMethodId.GetGridCore_SubtypeId:
                    return arg =>
                    {
                        var grid = arg as IMyCubeGrid;
                        var dto = GetGridCore(grid);
                        return dto != null && dto.SubtypeId != null ? dto.SubtypeId : string.Empty;
                    };

                case ApiMethodId.GetGridCore_UniqueName:
                    return arg =>
                    {
                        var grid = arg as IMyCubeGrid;
                        var dto = GetGridCore(grid);
                        return dto != null && dto.UniqueName != null ? dto.UniqueName : string.Empty;
                    };

                case ApiMethodId.GetGridCore_MaxBlocks:
                    return arg =>
                    {
                        var grid = arg as IMyCubeGrid;
                        var dto = GetGridCore(grid);
                        return dto?.MaxBlocks ?? 0;
                    };

                default:
                    return null;
            }
        }

        // ===== Event Broadcasting Methods =====
        //
        // IMPORTANT:
        // Events must be cross-assembly safe.
        // Do NOT send custom event arg objects directly; other mods cannot cast them.
        // Instead, serialize to byte[] and let consumers deserialize locally.

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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_CORE_ACTIVATED, payload);

                Utils.Log($"ModAPI Event: CoreActivated for grid {(grid != null ? grid.DisplayName : "Unknown")}", 1);
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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_CORE_DEACTIVATED, payload);

                Utils.Log($"ModAPI Event: CoreDeactivated for grid {(grid != null ? grid.DisplayName : "Unknown")}", 1);
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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_LIMITS_RECALCULATED, payload);

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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_LIMITS_ENFORCED, payload);

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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_BOOST_ACTIVATED, payload);

                Utils.Log($"ModAPI Event: BoostActivated for grid {(grid != null ? grid.DisplayName : "Unknown")}", 1);
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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_BOOST_DEACTIVATED, payload);

                Utils.Log($"ModAPI Event: BoostDeactivated for grid {(grid != null ? grid.DisplayName : "Unknown")}", 1);
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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_ACTIVE_DEFENSE_ACTIVATED, payload);

                Utils.Log($"ModAPI Event: ActiveDefenseActivated for grid {(grid != null ? grid.DisplayName : "Unknown")}", 1);
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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_ACTIVE_DEFENSE_DEACTIVATED, payload);

                Utils.Log($"ModAPI Event: ActiveDefenseDeactivated for grid {(grid != null ? grid.DisplayName : "Unknown")}", 1);
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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_GRID_ADDED_TO_GROUP, payload);

                Utils.Log($"ModAPI Event: GridAddedToGroup {(grid != null ? grid.DisplayName : "Unknown")}", 1);
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

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_GRID_REMOVED_FROM_GROUP, payload);

                Utils.Log($"ModAPI Event: GridRemovedFromGroup {(grid != null ? grid.DisplayName : "Unknown")}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastGridRemovedFromGroup: Exception - {ex}", 3);
            }
        }
        
        /// <summary>
        /// Gets the speed modifiers for a grid's active core.
        /// </summary>
        public static SpeedModifiersData GetSpeedModifiers(IMyCubeGrid grid)
        {
            if (grid == null) return ConvertToSpeedModifiersData(null);

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return ConvertToSpeedModifiersData(null);

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent)) return ConvertToSpeedModifiersData(null);

                var core = groupComponent.ShipCore;
                return ConvertToSpeedModifiersData(core?.SpeedModifiers);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetSpeedModifiers: Exception - {ex}");
                return ConvertToSpeedModifiersData(null);
            }
        }

        /// <summary>
        /// Returns true if Dynamic Boost is enabled for the grid's active core.
        /// NOTE: Dynamic boost has been deprecated; this currently always returns false.
        /// </summary>
        public static bool IsDynamicBoostEnabled(IMyCubeGrid grid)
        {
            return false;
        }

        /// <summary>
        /// Gets BoostResistance from the grid's active core speed modifiers.
        /// NOTE: This is a legacy value; it maps to MaximumFrictionDeceleration for newer configs.
        /// </summary>
        public static float GetBoostResistance(IMyCubeGrid grid)
        {
            var s = GetSpeedModifiers(grid);
            return s?.BoostResistance ?? 0f;
        }

        /// <summary>
        /// Enables/disables friction-based speed limiting for a logical grid group.
        /// </summary>
        public static bool SetFrictionEnabledForGroup(IMyGridGroupData groupData, bool enabled)
        {
            if (groupData == null) return false;

            try
            {
                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return false;

                groupComponent.FrictionEnforcementEnabled = enabled;
                return true;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.SetFrictionEnabledForGroup: Exception - {ex}");
                return false;
            }
        }

        /// <summary>
        /// Gets whether friction-based speed limiting is enabled for a logical grid group.
        /// </summary>
        public static bool GetFrictionEnabledForGroup(IMyGridGroupData groupData)
        {
            if (groupData == null) return false;

            try
            {
                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return false;

                return groupComponent.FrictionEnforcementEnabled;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetFrictionEnabledForGroup: Exception - {ex}");
                return false;
            }
        }

        /// <summary>
        /// Sets the maximum friction deceleration override (m/s^2) for a logical grid group.
        /// </summary>
        public static bool SetFrictionMaximumDecelerationForGroup(IMyGridGroupData groupData, float deceleration)
        {
            if (groupData == null) return false;
            if (deceleration < 0f) return false;

            try
            {
                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return false;

                groupComponent.FrictionMaximumDecelerationOverride = deceleration;
                return true;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.SetFrictionMaximumDecelerationForGroup: Exception - {ex}");
                return false;
            }
        }

        /// <summary>
        /// Clears the maximum friction deceleration override for a logical grid group.
        /// </summary>
        public static bool ClearFrictionMaximumDecelerationForGroup(IMyGridGroupData groupData)
        {
            if (groupData == null) return false;

            try
            {
                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return false;

                groupComponent.FrictionMaximumDecelerationOverride = -1f;
                return true;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.ClearFrictionMaximumDecelerationForGroup: Exception - {ex}");
                return false;
            }
        }

        /// <summary>
        /// Gets the maximum friction deceleration override for a logical grid group (or -1 if none).
        /// </summary>
        public static float GetFrictionMaximumDecelerationForGroup(IMyGridGroupData groupData)
        {
            if (groupData == null) return -1f;

            try
            {
                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return -1f;

                return groupComponent.FrictionMaximumDecelerationOverride;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetFrictionMaximumDecelerationForGroup: Exception - {ex}");
                return -1f;
            }
        }

        /// <summary>
        /// Gets base max speed in m/s without boost applied.
        /// </summary>
        public static float GetBaseMaxSpeed(IMyCubeGrid grid)
        {
            if (grid == null) return 100f;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return 100f;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return 100f;

                var core = groupComponent.ShipCore;
                if (core == null || core.SpeedModifiers == null)
                    return 100f;

                return core.SpeedModifiers.MaxSpeed * Session.Config.MaxPossibleSpeedMetersPerSecond;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetBaseMaxSpeed: Exception - {ex}");
                return 100f;
            }
        }

        /// <summary>
        /// Gets max boost multiplier for the grid's active core.
        /// </summary>
        public static float GetMaxBoostMultiplier(IMyCubeGrid grid)
        {
            var s = GetSpeedModifiers(grid);
            return s?.MaxBoost ?? 0f;
        }

        /// <summary>
        /// Gets boost duration in seconds for the grid's active core.
        /// </summary>
        public static float GetBoostDuration(IMyCubeGrid grid)
        {
            var s = GetSpeedModifiers(grid);
            return s?.BoostDuration ?? 0f;
        }

        /// <summary>
        /// Gets boost cooldown in seconds for the grid's active core.
        /// </summary>
        public static float GetBoostCooldown(IMyCubeGrid grid)
        {
            var s = GetSpeedModifiers(grid);
            return s?.BoostCoolDown ?? 0f;
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
                    Modifiers = new GridModifiersData
                    {
                        AssemblerSpeed = 1,
                        DrillHarvestMultiplier = 1,
                        GyroEfficiency = 1,
                        GyroForce = 1,
                        PowerProducersOutput = 1,
                        RefineEfficiency = 1,
                        RefineSpeed = 1,
                        ThrusterEfficiency = 1,
                        ThrusterForce = 1
                    },
                    SpeedModifiers = new SpeedModifiersData
                    {
                        MaxSpeed = 0.0f,
                        MaxBoost = 0.0f,
                        BoostDuration = 10f,
                        BoostCoolDown = 60f,
                        BoostResistance = 0f,
                        MinimumFrictionSpeed = 0f,
                        MaximumFrictionSpeed = 0f,
                        MaximumFrictionDeceleration = 0f
                    }
                };
            }

            return new ShipCoreData
            {
                SubtypeId = core.SubtypeId,
                UniqueName = core.UniqueName,
                ForceBroadCast = core.ForceBroadCast,
                ForceBroadCastRange = core.ForceBroadCastRange,
                MobilityTypeData = (MobilityTypeData)(int)core.MobilityType,
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
                ActiveDefenseModifiers = ConvertToDefenseModifiersData(core.ActiveDefenseModifiers),
                DynamicBoostEnabled = false,
                SpeedModifiers = ConvertToSpeedModifiersData(core.SpeedModifiers),
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
                    ThrusterForce = 1
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
            };
        }
        
        private static SpeedModifiersData ConvertToSpeedModifiersData(SpeedModifiers modifiers)
        {
            if (modifiers == null)
            {
                return new SpeedModifiersData
                {
                    MaxSpeed = 0.0f,
                    MaxBoost = 0.0f,
                    BoostDuration = 10f,
                    BoostCoolDown = 60f,
                    BoostResistance = 0f,
                    MinimumFrictionSpeed = 0f,
                    MaximumFrictionSpeed = 0f,
                    MaximumFrictionDeceleration = 0f
                };
            }

            return new SpeedModifiersData
            {
                MaxSpeed = modifiers.MaxSpeed,
                MaxBoost = modifiers.MaxBoost,
                BoostDuration = modifiers.BoostDuration,
                BoostCoolDown = modifiers.BoostCoolDown,
                BoostResistance = modifiers.MaximumFrictionDeceleration,
                MinimumFrictionSpeed = modifiers.MinimumFrictionSpeed,
                MaximumFrictionSpeed = modifiers.MaximumFrictionSpeed,
                MaximumFrictionDeceleration = modifiers.MaximumFrictionDeceleration
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
        //
        // NOTE:
        // These methods are still useful internally, and the factory wraps them.
        // Consumers should NOT call them directly across assemblies.

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

        public static List<ShipCoreData> GetAllCoreConfigs()
        {
            try
            {
                return Session.Config.ShipCores.Select(ConvertToShipCoreData).ToList();
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetAllCoreConfigs: Exception - {ex}");
                return new List<ShipCoreData>();
            }
        }

        public static Dictionary<string, LimitStatusData> GetBlockLimitsStatus(IMyCubeGrid grid)
        {
            var result = new Dictionary<string, LimitStatusData>();
            if (grid == null) return result;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
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

        public static bool IsBlockAllowed(IMyCubeGrid grid, string typeId, string subtypeId, int count)
        {
            if (grid == null || string.IsNullOrEmpty(typeId)) return true;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
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

        public static GridModifiersData GetGridModifiers(IMyCubeGrid grid)
        {
            if (grid == null) return ConvertToGridModifiersData(null);
            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return ConvertToGridModifiersData(null);

                GroupComponent groupComponent;
                return ConvertToGridModifiersData(!Session.GroupDict.TryGetValue(groupData, out groupComponent) ? null : groupComponent.Modifiers);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetGridModifiers: Exception - {ex}");
                return ConvertToGridModifiersData(null);
            }
        }

        public static float GetMaxSpeed(IMyCubeGrid grid)
        {
            if (grid == null) return 100f;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
                if (groupData == null) return 100f;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return 100f;
                
                var core = groupComponent.ShipCore;
                if (core?.SpeedModifiers == null) return 100f;

                var baseSpeed = core.SpeedModifiers.MaxSpeed * Session.Config.MaxPossibleSpeedMetersPerSecond;

                if (groupComponent.BoostEnabled) return baseSpeed * core.SpeedModifiers.MaxBoost;
                return baseSpeed;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetMaxSpeed: Exception - {ex}");
                return 100f;
            }
        }

        public static bool IsBoostActive(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Logical, grid);
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
