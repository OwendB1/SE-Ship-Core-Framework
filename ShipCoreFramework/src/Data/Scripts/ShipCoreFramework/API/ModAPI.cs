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
        /// Called internally by Session component during BeforeStart.
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
                        var dto = GetGridCore(arg as long? ?? 0);
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
                        var dict = GetBlockLimitsStatus(arg as long? ?? 0);
                        return MyAPIGateway.Utilities.SerializeToBinary(dict);
                    };

                case ApiMethodId.IsBlockAllowed:
                    return arg =>
                    {
                        // Expect MyTuple<MyCubeGrid, string, string, int>
                        var t = (MyTuple<long, string, string, int>)arg;
                        return IsBlockAllowed(t.Item1, t.Item2, t.Item3, t.Item4);
                    };

                case ApiMethodId.GetGridModifiers_Binary:
                    return arg =>
                    {
                        var gridId = arg as long? ?? 0;
                        var dto = GetGridModifiers(gridId);
                        return MyAPIGateway.Utilities.SerializeToBinary(dto);
                    };

                case ApiMethodId.GetMaxSpeed:
                    return arg => GetMaxSpeed(arg as long? ?? 0);

                case ApiMethodId.IsBoostActive:
                    return arg => IsBoostActive(arg as long? ?? 0);

                case ApiMethodId.GetNoCoreConfig_Binary:
                    return _ =>
                    {
                        var dto = GetNoCoreConfig();
                        return MyAPIGateway.Utilities.SerializeToBinary(dto);
                    };
                
                case ApiMethodId.GetSpeedModifiers_Binary:
                    return arg =>
                    {
                        var dto = GetSpeedModifiers(arg as long? ?? 0);
                        return MyAPIGateway.Utilities.SerializeToBinary(dto);
                    };

                case ApiMethodId.GetBoostResistance:
                    return arg => GetBoostResistance(arg as long? ?? 0);

                case ApiMethodId.GetBaseMaxSpeed:
                    return arg => GetBaseMaxSpeed(arg as long? ?? 0);

                case ApiMethodId.GetMaxBoostMultiplier:
                    return arg => GetMaxBoostMultiplier(arg as long? ?? 0);

                case ApiMethodId.GetBoostDuration:
                    return arg => GetBoostDuration(arg as long? ?? 0);

                case ApiMethodId.GetBoostCooldown:
                    return arg => GetBoostCooldown(arg as long? ?? 0);

                case ApiMethodId.SetFrictionEnabledForGroup:
                    return arg =>
                    {
                        var t = (MyTuple<long, bool>)arg;
                        return SetFrictionEnabledForGroup(t.Item1, t.Item2);
                    };

                case ApiMethodId.GetFrictionEnabledForGroup:
                    return arg => GetFrictionEnabledForGroup(arg as long? ?? 0);

                case ApiMethodId.SetFrictionMaximumDecelerationForGroup:
                    return arg =>
                    {
                        var t = (MyTuple<long, float>)arg;
                        return SetFrictionMaximumDecelerationForGroup(t.Item1, t.Item2);
                    };

                case ApiMethodId.ClearFrictionMaximumDecelerationForGroup:
                    return arg => ClearFrictionMaximumDecelerationForGroup(arg as long? ?? 0);

                case ApiMethodId.GetFrictionMaximumDecelerationForGroup:
                    return arg => GetFrictionMaximumDecelerationForGroup(arg as long? ?? 0);

                case ApiMethodId.GetFrictionSpeedValueMode:
                    return _ => (int)Session.Config.FrictionSpeedValueMode;

                case ApiMethodId.SetFrictionMinimumSpeedAbsoluteForGroup:
                    return arg =>
                    {
                        var t = (MyTuple<long, float>)arg;
                        return SetFrictionMinimumSpeedAbsoluteForGroup(t.Item1, t.Item2);
                    };

                case ApiMethodId.SetFrictionMaximumSpeedAbsoluteForGroup:
                    return arg =>
                    {
                        var t = (MyTuple<long, float>)arg;
                        return SetFrictionMaximumSpeedAbsoluteForGroup(t.Item1, t.Item2);
                    };

                case ApiMethodId.GetFrictionMinimumSpeedAbsoluteForGroup:
                    return arg => GetFrictionMinimumSpeedAbsoluteForGroup(arg as long? ?? 0);

                case ApiMethodId.GetFrictionMaximumSpeedAbsoluteForGroup:
                    return arg => GetFrictionMaximumSpeedAbsoluteForGroup(arg as long? ?? 0);

                case ApiMethodId.SetFrictionMinimumSpeedModifierForGroup:
                    return arg =>
                    {
                        var t = (MyTuple<long, float>)arg;
                        return SetFrictionMinimumSpeedModifierForGroup(t.Item1, t.Item2);
                    };

                case ApiMethodId.SetFrictionMaximumSpeedModifierForGroup:
                    return arg =>
                    {
                        var t = (MyTuple<long, float>)arg;
                        return SetFrictionMaximumSpeedModifierForGroup(t.Item1, t.Item2);
                    };

                case ApiMethodId.GetFrictionMinimumSpeedModifierForGroup:
                    return arg => GetFrictionMinimumSpeedModifierForGroup(arg as long? ?? 0);

                case ApiMethodId.GetFrictionMaximumSpeedModifierForGroup:
                    return arg => GetFrictionMaximumSpeedModifierForGroup(arg as long? ?? 0);

                case ApiMethodId.IsGroupDeactivated:
                    return arg => IsGroupDeactivated(arg as long? ?? 0);

                case ApiMethodId.GetFullConfig_Binary:
                    return _ => MyAPIGateway.Utilities.SerializeToBinary(GetFullConfig());

                // Optional primitive getters:
                case ApiMethodId.GetGridCore_SubtypeId:
                    return arg =>
                    {
                        var dto = GetGridCore(arg as long? ?? 0);
                        return dto?.SubtypeId ?? string.Empty;
                    };

                case ApiMethodId.GetGridCore_UniqueName:
                    return arg =>
                    {
                        var dto = GetGridCore(arg as long? ?? 0);
                        return dto?.UniqueName ?? string.Empty;
                    };

                case ApiMethodId.GetGridCore_MaxBlocks:
                    return arg =>
                    {
                        var dto = GetGridCore(arg as long? ?? 0);
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
        internal static void BroadcastCoreActivated(long groupGridId, string coreSubtypeId, string coreName)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new CoreActivatedEventArgs
                {
                    GroupGridId = groupGridId,
                    CoreSubtypeId = coreSubtypeId,
                    CoreName = coreName,
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_CORE_ACTIVATED, payload);

                Utils.Log($"ModAPI Event: CoreActivated for grid Entity ID: {groupGridId}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastCoreActivated: Exception - {ex}", 3);
            }
        }

        /// <summary>
        /// Broadcasts the CoreDeactivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastCoreDeactivated(long groupGridId, string previousCoreSubtypeId, string previousCoreName)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new CoreDeactivatedEventArgs
                {
                    GroupGridId = groupGridId,
                    PreviousCoreSubtypeId = previousCoreSubtypeId,
                    PreviousCoreName = previousCoreName,
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_CORE_DEACTIVATED, payload);

                Utils.Log($"ModAPI Event: CoreDeactivated for grid Entity ID: {groupGridId}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastCoreDeactivated: Exception - {ex}", 3);
            }
        }

        /// <summary>
        /// Broadcasts the LimitsRecalculated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastLimitsRecalculated(long groupGridId)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new LimitsRecalculatedEventArgs
                {
                    GroupGridId = groupGridId,
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
        internal static void BroadcastLimitsEnforced(long groupGridId, int blocksPunished)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new LimitsEnforcedEventArgs
                {
                    GroupGridId = groupGridId,
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
        internal static void BroadcastBoostActivated(long groupGridId)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new BoostEventArgs
                {
                    GroupGridId = groupGridId,
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_BOOST_ACTIVATED, payload);

                Utils.Log($"ModAPI Event: BoostActivated for grid Entity ID: {groupGridId}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastBoostActivated: Exception - {ex}", 3);
            }
        }

        /// <summary>
        /// Broadcasts the BoostDeactivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastBoostDeactivated(long groupGridId)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new BoostEventArgs
                {
                    GroupGridId = groupGridId,
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_BOOST_DEACTIVATED, payload);

                Utils.Log($"ModAPI Event: BoostDeactivated for grid Entity ID: {groupGridId}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastBoostDeactivated: Exception - {ex}", 3);
            }
        }

        /// <summary>
        /// Broadcasts the ActiveDefenseActivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastActiveDefenseActivated(long groupGridId)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new ActiveDefenseEventArgs
                {
                    GroupGridId = groupGridId,
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_ACTIVE_DEFENSE_ACTIVATED, payload);

                Utils.Log($"ModAPI Event: ActiveDefenseActivated for grid Entity ID: {groupGridId}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastActiveDefenseActivated: Exception - {ex}", 3);
            }
        }

        /// <summary>
        /// Broadcasts the ActiveDefenseDeactivated event to all subscribed mods.
        /// </summary>
        internal static void BroadcastActiveDefenseDeactivated(long groupGridId)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new ActiveDefenseEventArgs
                {
                    GroupGridId = groupGridId,
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_ACTIVE_DEFENSE_DEACTIVATED, payload);

                Utils.Log($"ModAPI Event: ActiveDefenseDeactivated for grid Entity ID: {groupGridId}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastActiveDefenseDeactivated: Exception - {ex}", 3);
            }
        }

        /// <summary>
        /// Broadcasts the GridAddedToGroup event to all subscribed mods.
        /// </summary>
        internal static void BroadcastGridAddedToGroup(long gridId)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new GridGroupEventArgs
                {
                    GridId = gridId,
                    GroupGridId = gridId,
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_GRID_ADDED_TO_GROUP, payload);

                Utils.Log($"ModAPI Event: GridAddedToGroup Entity ID: {gridId}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastGridAddedToGroup: Exception - {ex}", 3);
            }
        }

        /// <summary>
        /// Broadcasts the GridRemovedFromGroup event to all subscribed mods.
        /// </summary>
        internal static void BroadcastGridRemovedFromGroup(long gridId, long groupGridId)
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new GridGroupEventArgs
                {
                    GridId = gridId,
                    GroupGridId = groupGridId,
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_GRID_REMOVED_FROM_GROUP, payload);

                Utils.Log($"ModAPI Event: GridRemovedFromGroup Entity ID: {gridId}", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastGridRemovedFromGroup: Exception - {ex}", 3);
            }
        }

        /// <summary>
        /// Broadcasts the effective mod config after client receives synced world settings from server.
        /// </summary>
        internal static void BroadcastConfigReceived()
        {
            if (!_isInitialized) return;

            try
            {
                var eventData = new ConfigReceivedEventArgs
                {
                    Config = ConvertToModConfigData(Session.Config),
                    Timestamp = DateTime.UtcNow
                };

                var payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_CONFIG_RECEIVED, payload);

                Utils.Log("ModAPI Event: ConfigReceived", 1);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.BroadcastConfigReceived: Exception - {ex}", 3);
            }
        }
        
        /// <summary>
        /// Gets the speed modifiers for a grid's active core.
        /// </summary>
        public static SpeedModifiersData GetSpeedModifiers(long gridId)
        {
            try
            {
                GroupComponent groupComponent;
                if (!TryGetGroupComponent(gridId, out groupComponent)) return ConvertToSpeedModifiersData(null);

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
        /// Gets BoostResistance from the grid's active core speed modifiers.
        /// NOTE: This is a legacy value; it maps to MaximumFrictionDeceleration for newer configs.
        /// </summary>
        public static float GetBoostResistance(long gridId)
        {
            var s = GetSpeedModifiers(gridId);
            return s?.BoostResistance ?? 0f;
        }

        /// <summary>
        /// Enables/disables friction-based speed limiting for a logical grid group.
        /// Friction cores are enabled by default; this is a runtime override.
        /// </summary>
        public static bool SetFrictionEnabledForGroup(long gridId, bool enabled)
        {
            if (!Session.IsServer) return false;

            try
            {
                GroupComponent groupComponent;
                if (!TryGetGroupComponent(gridId, out groupComponent)) return false;

                groupComponent.SetFrictionEnforcementEnabled(enabled);
                return true;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.SetFrictionEnabledForGroup: Exception - {ex}");
                return false;
            }
        }

        /// <summary>
        /// Gets whether friction-based speed limiting is currently active for a logical grid group.
        /// </summary>
        public static bool GetFrictionEnabledForGroup(long gridId)
        {
            try
            {
                GroupComponent groupComponent;
                return TryGetGroupComponent(gridId, out groupComponent) && groupComponent.GetFrictionEnforcementEnabled();
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
        public static bool SetFrictionMaximumDecelerationForGroup(long gridId, float deceleration)
        {
            if (!Session.IsServer) return false;
            if (deceleration < 0f) return false;

            try
            {
                GroupComponent groupComponent;
                if (!TryGetGroupComponent(gridId, out groupComponent)) return false;

                groupComponent.SetFrictionMaximumDecelerationOverride(deceleration);
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
        public static bool ClearFrictionMaximumDecelerationForGroup(long gridId)
        {
            if (!Session.IsServer) return false;

            try
            {
                GroupComponent groupComponent;
                if (!TryGetGroupComponent(gridId, out groupComponent)) return false;

                groupComponent.SetFrictionMaximumDecelerationOverride(-1f);
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
        public static float GetFrictionMaximumDecelerationForGroup(long gridId)
        {
            try
            {
                GroupComponent groupComponent;
                return TryGetGroupComponent(gridId, out groupComponent) ? groupComponent.GetFrictionMaximumDecelerationOverride() : -1f;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetFrictionMaximumDecelerationForGroup: Exception - {ex}");
                return -1f;
            }
        }

        public static MyTuple<bool, string> SetFrictionMinimumSpeedAbsoluteForGroup(long gridId, float speedMetersPerSecond)
        {
            if (!Session.IsServer)
                return MyTuple.Create(false, "Runtime friction overrides are server-authoritative.");

            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Absolute)
                return MyTuple.Create(false, "World config uses modifier-based friction speeds; use SetFrictionMinimumSpeedModifierForGroup.");

            GroupComponent groupComponent;
            if (!TryGetGroupComponent(gridId, out groupComponent))
                return MyTuple.Create(false, "Could not resolve logical grid group for the provided grid.");

            if (speedMetersPerSecond < 0f)
            {
                groupComponent.SetMinimumFrictionSpeedAbsoluteOverride(-1f);
                return MyTuple.Create(true, string.Empty);
            }

            groupComponent.SetMinimumFrictionSpeedAbsoluteOverride(speedMetersPerSecond);
            return MyTuple.Create(true, string.Empty);
        }

        public static MyTuple<bool, string> SetFrictionMaximumSpeedAbsoluteForGroup(long gridId, float speedMetersPerSecond)
        {
            if (!Session.IsServer)
                return MyTuple.Create(false, "Runtime friction overrides are server-authoritative.");

            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Absolute)
                return MyTuple.Create(false, "World config uses modifier-based friction speeds; use SetFrictionMaximumSpeedModifierForGroup.");

            GroupComponent groupComponent;
            if (!TryGetGroupComponent(gridId, out groupComponent))
                return MyTuple.Create(false, "Could not resolve logical grid group for the provided grid.");

            if (speedMetersPerSecond < 0f)
            {
                groupComponent.SetMaximumFrictionSpeedAbsoluteOverride(-1f);
                return MyTuple.Create(true, string.Empty);
            }

            groupComponent.SetMaximumFrictionSpeedAbsoluteOverride(speedMetersPerSecond);
            return MyTuple.Create(true, string.Empty);
        }

        public static MyTuple<float, string> GetFrictionMinimumSpeedAbsoluteForGroup(long gridId)
        {
            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Absolute)
                return MyTuple.Create(-1f, "World config uses modifier-based friction speeds; use GetFrictionMinimumSpeedModifierForGroup.");

            GroupComponent groupComponent;
            return !TryGetGroupComponent(gridId, out groupComponent) ? MyTuple.Create(-1f, "Could not resolve logical grid group for the provided grid.") : MyTuple.Create(groupComponent.GetMinimumFrictionSpeedAbsoluteOverride(), string.Empty);
        }

        public static MyTuple<float, string> GetFrictionMaximumSpeedAbsoluteForGroup(long gridId)
        {
            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Absolute)
                return MyTuple.Create(-1f, "World config uses modifier-based friction speeds; use GetFrictionMaximumSpeedModifierForGroup.");

            GroupComponent groupComponent;
            return !TryGetGroupComponent(gridId, out groupComponent) ? MyTuple.Create(-1f, "Could not resolve logical grid group for the provided grid.") : MyTuple.Create(groupComponent.GetMaximumFrictionSpeedAbsoluteOverride(), string.Empty);
        }

        public static MyTuple<bool, string> SetFrictionMinimumSpeedModifierForGroup(long gridId, float modifier)
        {
            if (!Session.IsServer)
                return MyTuple.Create(false, "Runtime friction overrides are server-authoritative.");

            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Modifier)
                return MyTuple.Create(false, "World config uses absolute friction speeds; use SetFrictionMinimumSpeedAbsoluteForGroup.");

            GroupComponent groupComponent;
            if (!TryGetGroupComponent(gridId, out groupComponent))
                return MyTuple.Create(false, "Could not resolve logical grid group for the provided grid.");

            if (modifier < 0f)
            {
                groupComponent.SetMinimumFrictionSpeedModifierOverride(-1f);
                return MyTuple.Create(true, string.Empty);
            }

            groupComponent.SetMinimumFrictionSpeedModifierOverride(modifier);
            return MyTuple.Create(true, string.Empty);
        }

        public static MyTuple<bool, string> SetFrictionMaximumSpeedModifierForGroup(long gridId, float modifier)
        {
            if (!Session.IsServer)
                return MyTuple.Create(false, "Runtime friction overrides are server-authoritative.");

            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Modifier)
                return MyTuple.Create(false, "World config uses absolute friction speeds; use SetFrictionMaximumSpeedAbsoluteForGroup.");

            GroupComponent groupComponent;
            if (!TryGetGroupComponent(gridId, out groupComponent))
                return MyTuple.Create(false, "Could not resolve logical grid group for the provided grid.");

            if (modifier < 0f)
            {
                groupComponent.SetMaximumFrictionSpeedModifierOverride(-1f);
                return MyTuple.Create(true, string.Empty);
            }

            groupComponent.SetMaximumFrictionSpeedModifierOverride(modifier);
            return MyTuple.Create(true, string.Empty);
        }

        public static MyTuple<float, string> GetFrictionMinimumSpeedModifierForGroup(long gridId)
        {
            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Modifier)
                return MyTuple.Create(-1f, "World config uses absolute friction speeds; use GetFrictionMinimumSpeedAbsoluteForGroup.");

            GroupComponent groupComponent;
            return !TryGetGroupComponent(gridId, out groupComponent) ? MyTuple.Create(-1f, "Could not resolve logical grid group for the provided grid.") : MyTuple.Create(groupComponent.GetMinimumFrictionSpeedModifierOverride(), string.Empty);
        }

        public static MyTuple<float, string> GetFrictionMaximumSpeedModifierForGroup(long gridId)
        {
            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Modifier)
                return MyTuple.Create(-1f, "World config uses absolute friction speeds; use GetFrictionMaximumSpeedAbsoluteForGroup.");

            GroupComponent groupComponent;
            return !TryGetGroupComponent(gridId, out groupComponent) ? MyTuple.Create(-1f, "Could not resolve logical grid group for the provided grid.") : MyTuple.Create(groupComponent.GetMaximumFrictionSpeedModifierOverride(), string.Empty);
        }

        public static bool IsGroupDeactivated(long gridId)
        {
            try
            {
                GroupComponent groupComponent;
                return TryGetGroupComponent(gridId, out groupComponent) && groupComponent.Deactivated;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.IsGroupDeactivated: Exception - {ex}");
                return false;
            }
        }

        private static bool TryGetGroupComponent(long gridId, out GroupComponent groupComponent)
        {
            groupComponent = null;

            try
            {
                if (Utils.TryFindByGridId(gridId, out groupComponent))
                    return true;

                if (!Session.IsGameThread)
                    return false;

                var grid = MyAPIGateway.Entities.GetEntityById(gridId) as MyCubeGrid;
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
                return groupData != null && Session.GroupDict.TryGetValue(groupData, out groupComponent);
            }
            catch
            {
                groupComponent = null;
                return false;
            }
        }

        /// <summary>
        /// Gets base max speed in m/s without boost applied.
        /// </summary>
        public static float GetBaseMaxSpeed(long gridId)
        {
            var grid = MyAPIGateway.Entities.GetEntityById(gridId) as MyCubeGrid;
            if (grid == null) return 100f;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
                if (groupData == null) return 100f;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return 100f;

                SpeedEnforcement.RefreshSpeedState(groupComponent);
                return groupComponent.BaseSpeedLimitMetersPerSecond;
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
        public static float GetMaxBoostMultiplier(long gridId)
        {
            var s = GetSpeedModifiers(gridId);
            return s?.MaxBoost ?? 0f;
        }

        /// <summary>
        /// Gets boost duration in seconds for the grid's active core.
        /// </summary>
        public static float GetBoostDuration(long gridId)
        {
            var s = GetSpeedModifiers(gridId);
            return s?.BoostDuration ?? 0f;
        }

        /// <summary>
        /// Gets boost cooldown in seconds for the grid's active core.
        /// </summary>
        public static float GetBoostCooldown(long gridId)
        {
            var s = GetSpeedModifiers(gridId);
            return s?.BoostCoolDown ?? 0f;
        }

        // ===== Helper Methods =====

        private static ShipCoreData ConvertToShipCoreData(ShipCore core, bool isDeactivated = false)
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
                        MinimumFrictionSpeedAbsolute = 0f,
                        MaximumFrictionSpeedAbsolute = 0f,
                        MaximumFrictionDeceleration = 0f,
                        MinimumFrictionSpeedModifier = 0f,
                        MaximumFrictionSpeedModifier = 0f,
                        FrictionCurve = Array.Empty<FrictionCurveSegmentData>(),
                        CruiseFrictionMultiplier = 1f,
                        CruiseAccelerationThreshold = 0.05f,
                        AtmosphericFriction = null
                    },
                    ManifestGroupNames = Array.Empty<string>(),
                    ConnectorBlacklistCoreSubtypeIds = Array.Empty<string>(),
                    CoreSelectionPriority = 0,
                    CrossConnectorPunishmentWhitelisted = false,
                    MinFactionRank = FactionRankData.None,
                    SpeedOverrideMode = SpeedOverrideModeData.OnlyIfHeavier,
                    PowerOverclockMultiplier = 1f,
                    PowerOverclockDuration = 10f,
                    PowerOverclockCooldown = 60f,
                    MaxBackupCores = -1,
                    AllowedUpgradeModules = Array.Empty<UpgradeModuleAllowanceData>(),
                    SpeedLimitTypeData = SpeedLimitTypeData.Normal,
                    BlockLimits = Array.Empty<BlockLimitData>(),
                    ManifestGroupName = string.Empty,
                    ManifestGroupMaxCount = -1,
                    ManifestGroupCurrentCount = 0,
                    ManifestGroups = Array.Empty<ManifestGroupLimitData>(),
                    IsDeactivated = isDeactivated
                };
            }

            var manifestGroups = PerManifestGroupManager.GetManifestGroups(core)
                .Select(group => new ManifestGroupLimitData
                {
                    Name = group.Name,
                    MaxCount = group.MaxCount,
                    CurrentCount = PerManifestGroupManager.GetCurrentCount(group.Name)
                })
                .ToArray();

            var primaryManifestGroup = manifestGroups.FirstOrDefault();

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
                MinBlocks = core.MinBlocks,
                MaxPlayers = core.MaxPlayers,
                FactionPlayersNeededPerCore = core.FactionPlayersNeededPerCore,
                ManifestGroupNames = core.ManifestGroupNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ConnectorBlacklistCoreSubtypeIds = core.ConnectorBlacklistCoreSubtypeIds
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                CoreSelectionPriority = core.CoreSelectionPriority,
                CrossConnectorPunishmentWhitelisted = core.CrossConnectorPunishmentWhitelisted,
                MinFactionRank = (FactionRankData)(int)core.MinFactionRank,
                MaxBackupCores = core.MaxBackupCores,
                AllowedUpgradeModules = (core.AllowedUpgradeModules ?? Array.Empty<UpgradeModuleAllowance>())
                    .Where(allowance => allowance != null)
                    .Select(ConvertToUpgradeModuleAllowanceData)
                    .ToArray(),
                SpeedLimitTypeData = (SpeedLimitTypeData)(int)core.SpeedLimitType,
                BlockLimits = (core.BlockLimits ?? Array.Empty<BlockLimit>())
                    .Where(limit => limit != null)
                    .Select(ConvertToBlockLimitData)
                    .ToArray(),
                ManifestGroupName = primaryManifestGroup?.Name ?? string.Empty,
                ManifestGroupMaxCount = primaryManifestGroup?.MaxCount ?? -1,
                ManifestGroupCurrentCount = primaryManifestGroup?.CurrentCount ?? 0,
                ManifestGroups = manifestGroups,
                Modifiers = ConvertToGridModifiersData(core.Modifiers),
                PassiveDefenseModifiers = ConvertToDefenseModifiersData(core.PassiveDefenseModifiers),
                SpeedBoostEnabled = core.SpeedBoostEnabled,
                SpeedOverrideMode = (SpeedOverrideModeData)(int)core.SpeedOverrideMode,
                SpeedOverridePriority = core.SpeedOverridePriority,
                EnableActiveDefenseModifiers = core.EnableActiveDefenseModifiers,
                ActiveDefenseModifiers = ConvertToDefenseModifiersData(core.ActiveDefenseModifiers),
                PowerOverclockEnabled = core.PowerOverclockEnabled,
                PowerOverclockMultiplier = core.PowerOverclockMultiplier,
                PowerOverclockDuration = core.PowerOverclockDuration,
                PowerOverclockCooldown = core.PowerOverclockCooldown,
                PowerOverclockDamagePerSecond = core.PowerOverclockDamagePerSecond,
                DynamicBoostEnabled = false,
                SpeedModifiers = ConvertToSpeedModifiersData(core.SpeedModifiers),
                IsDeactivated = isDeactivated
            };
        }

        private static ModConfigData ConvertToModConfigData(ModConfig config)
        {
            if (config == null)
            {
                return new ModConfigData
                {
                    BlockDirectionalPlacementOnSubgrids = true,
                    NoCoreGraceSeconds = 30,
                    MinimumBlocksGraceSeconds = 30,
                    IgnoredFactionTags = Array.Empty<string>(),
                    NoFlyZones = Array.Empty<NoFlyZoneData>(),
                    NoCoreConfigs = Array.Empty<ShipCoreData>(),
                    ShipCores = Array.Empty<ShipCoreData>(),
                    ManifestCoreGroups = Array.Empty<ManifestCoreGroupData>(),
                    UpgradeModules = Array.Empty<UpgradeModuleConfigData>(),
                    BlockGroups = Array.Empty<BlockGroupData>(),
                    SelectedNoCore = ConvertToShipCoreData(null)
                };
            }

            return new ModConfigData
            {
                IgnoreAiFactions = config.IgnoreAiFactions,
                IgnoredFactionTags = (config.IgnoredFactionTags ?? new List<string>())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToArray(),
                SelectedNoCoreUniqueName = config.SelectedNoCoreUniqueName ?? string.Empty,
                DebugMode = config.DebugMode,
                CombatLogging = config.CombatLogging,
                LogLevel = config.LogLevel,
                ClientOutputLogLevel = config.ClientOutputLogLevel,
                MaxPossibleSpeedMetersPerSecond = config.MaxPossibleSpeedMetersPerSecond,
                MassTypeMode = (MassTypeModeData)(int)config.MassTypeMode,
                FrictionSpeedValueMode = (FrictionSpeedValueModeData)(int)config.FrictionSpeedValueMode,
                BlockDirectionalPlacementOnSubgrids = config.BlockDirectionalPlacementOnSubgrids,
                AllowUnattachedUpgradeModules = config.AllowUnattachedUpgradeModules,
                NoCoreGraceSeconds = config.NoCoreGraceSeconds,
                MinimumBlocksGraceSeconds = config.MinimumBlocksGraceSeconds,
                NoFlyZones = config.NoFlyZones
                    .Where(zone => zone != null)
                    .Select(ConvertToNoFlyZoneData)
                    .ToArray(),
                NoCoreConfigs = config.NoCoreConfigs
                    .Where(core => core != null)
                    .Select(core => ConvertToShipCoreData(core))
                    .ToArray(),
                ShipCores = config.ShipCores
                    .Where(core => core != null)
                    .Select(core => ConvertToShipCoreData(core))
                    .ToArray(),
                ManifestCoreGroups = config.ManifestCoreGroups
                    .Where(group => group != null)
                    .Select(ConvertToManifestCoreGroupData)
                    .ToArray(),
                UpgradeModules = config.UpgradeModules
                    .Where(module => module != null)
                    .Select(ConvertToUpgradeModuleConfigData)
                    .ToArray(),
                BlockGroups = config.BlockGroups
                    .Where(group => group != null)
                    .Select(ConvertToBlockGroupData)
                    .ToArray(),
                SelectedNoCore = ConvertToShipCoreData(config.SelectedNoCore)
            };
        }

        private static ManifestCoreGroupData ConvertToManifestCoreGroupData(ManifestCoreGroup group)
        {
            if (group == null)
            {
                return new ManifestCoreGroupData
                {
                    Name = string.Empty,
                    CoreSubtypeIds = Array.Empty<string>()
                };
            }

            return new ManifestCoreGroupData
            {
                Name = group.Name ?? string.Empty,
                MaxCount = group.MaxCount,
                CoreSubtypeIds = group.CoreSubtypeIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
        }

        private static NoFlyZoneData ConvertToNoFlyZoneData(Zones zone)
        {
            if (zone == null)
            {
                return new NoFlyZoneData
                {
                    AllowedCoresSubtype = Array.Empty<string>(),
                    Position = new Vector3DData()
                };
            }

            return new NoFlyZoneData
            {
                Id = zone.Id,
                Position = new Vector3DData
                {
                    X = zone.Position.X,
                    Y = zone.Position.Y,
                    Z = zone.Position.Z
                },
                Radius = zone.Radius,
                AllowedCoresSubtype = (zone.AllowedCoresSubtype ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToArray(),
                ForceOff = zone.ForceOff
            };
        }

        private static UpgradeModuleAllowanceData ConvertToUpgradeModuleAllowanceData(UpgradeModuleAllowance allowance)
        {
            if (allowance == null)
            {
                return new UpgradeModuleAllowanceData
                {
                    UniqueName = string.Empty,
                    TypeId = string.Empty,
                    SubtypeId = string.Empty
                };
            }

            return new UpgradeModuleAllowanceData
            {
                UniqueName = allowance.UniqueName ?? string.Empty,
                TypeId = allowance.TypeId ?? string.Empty,
                SubtypeId = allowance.SubtypeId ?? string.Empty,
                MaxCount = allowance.MaxCount
            };
        }

        private static UpgradeModuleConfigData ConvertToUpgradeModuleConfigData(UpgradeModuleConfig module)
        {
            if (module == null)
            {
                return new UpgradeModuleConfigData
                {
                    TypeId = string.Empty,
                    SubtypeId = string.Empty,
                    UniqueName = string.Empty,
                    Modifiers = Array.Empty<UpgradeStatModifierData>(),
                    BlockLimitModifiers = Array.Empty<BlockLimitModifierData>(),
                    CapacityModifiers = Array.Empty<CapacityModifierData>()
                };
            }

            return new UpgradeModuleConfigData
            {
                TypeId = module.TypeId ?? string.Empty,
                SubtypeId = module.SubtypeId ?? string.Empty,
                UniqueName = module.UniqueName ?? string.Empty,
                Modifiers = (module.Modifiers ?? Array.Empty<UpgradeStatModifier>())
                    .Where(modifier => modifier != null)
                    .Select(ConvertToUpgradeStatModifierData)
                    .ToArray(),
                BlockLimitModifiers = (module.BlockLimitModifiers ?? Array.Empty<BlockLimitModifier>())
                    .Where(modifier => modifier != null)
                    .Select(ConvertToBlockLimitModifierData)
                    .ToArray(),
                CapacityModifiers = (module.CapacityModifiers ?? Array.Empty<CapacityModifier>())
                    .Where(modifier => modifier != null)
                    .Select(ConvertToCapacityModifierData)
                    .ToArray()
            };
        }

        private static UpgradeStatModifierData ConvertToUpgradeStatModifierData(UpgradeStatModifier modifier)
        {
            if (modifier == null)
            {
                return new UpgradeStatModifierData
                {
                    Stat = string.Empty
                };
            }

            return new UpgradeStatModifierData
            {
                Stat = modifier.Stat ?? string.Empty,
                Value = modifier.Value,
                ModifierType = (UpgradeModifierOperationData)(int)modifier.ModifierType
            };
        }

        private static BlockLimitModifierData ConvertToBlockLimitModifierData(BlockLimitModifier modifier)
        {
            if (modifier == null)
            {
                return new BlockLimitModifierData
                {
                    BlockLimitName = string.Empty
                };
            }

            return new BlockLimitModifierData
            {
                BlockLimitName = modifier.BlockLimitName ?? string.Empty,
                Value = modifier.Value,
                ModifierType = (UpgradeModifierOperationData)(int)modifier.ModifierType
            };
        }

        private static CapacityModifierData ConvertToCapacityModifierData(CapacityModifier modifier)
        {
            if (modifier == null)
            {
                return new CapacityModifierData
                {
                    Stat = string.Empty
                };
            }

            return new CapacityModifierData
            {
                Stat = modifier.Stat ?? string.Empty,
                Value = modifier.Value,
                ModifierType = (UpgradeModifierOperationData)(int)modifier.ModifierType
            };
        }

        private static BlockLimitData ConvertToBlockLimitData(BlockLimit limit)
        {
            if (limit == null)
            {
                return new BlockLimitData
                {
                    Name = string.Empty,
                    BlockGroupNames = Array.Empty<string>(),
                    AllowedDirections = Array.Empty<DirectionTypeData>()
                };
            }

            return new BlockLimitData
            {
                Name = limit.Name ?? string.Empty,
                BlockGroupNames = (limit.BlockGroupsShortHand ?? Array.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToArray(),
                MaxCount = limit.MaxCount,
                CrossConnectorPunishment = limit.CrossConnectorPunishment,
                PunishByNoFlyZone = limit.PunishByNoFlyZone,
                PunishmentType = (PunishmentTypeData)(int)limit.PunishmentType,
                AllowedDirections = (limit.AllowedDirections ?? new List<DirectionType>())
                    .Select(direction => (DirectionTypeData)(int)direction)
                    .ToArray(),
                IsCriticalLimit = limit.IsCriticalLimit
            };
        }

        private static BlockGroupData ConvertToBlockGroupData(BlockGroup group)
        {
            if (group == null)
            {
                return new BlockGroupData
                {
                    Name = string.Empty,
                    BlockTypes = Array.Empty<BlockTypeData>()
                };
            }

            return new BlockGroupData
            {
                Name = group.Name ?? string.Empty,
                BlockTypes = (group.BlockTypes ?? new List<BlockType>())
                    .Where(blockType => blockType != null)
                    .Select(ConvertToBlockTypeData)
                    .ToArray()
            };
        }

        private static BlockTypeData ConvertToBlockTypeData(BlockType blockType)
        {
            if (blockType == null)
            {
                return new BlockTypeData
                {
                    TypeId = string.Empty,
                    SubtypeId = string.Empty
                };
            }

            return new BlockTypeData
            {
                TypeId = blockType.TypeId ?? string.Empty,
                SubtypeId = blockType.SubtypeId ?? string.Empty,
                CountWeight = blockType.CountWeight,
                PrimaryDirection = (DirectionTypeData)(int)blockType.PrimaryDirection
            };
        }

        internal static GridModifiersData ConvertToGridModifiersData(GridModifiers modifiers)
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
        
        internal static SpeedModifiersData ConvertToSpeedModifiersData(SpeedModifiers modifiers)
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
                    MinimumFrictionSpeedAbsolute = 0f,
                    MaximumFrictionSpeedAbsolute = 0f,
                    MaximumFrictionDeceleration = 0f,
                    MinimumFrictionSpeedModifier = 0f,
                    MaximumFrictionSpeedModifier = 0f,
                    FrictionCurve = Array.Empty<FrictionCurveSegmentData>(),
                    CruiseFrictionMultiplier = 1f,
                    CruiseAccelerationThreshold = 0.05f,
                    AtmosphericFriction = null
                };
            }

            return new SpeedModifiersData
            {
                MaxSpeed = modifiers.MaxSpeed,
                MaxBoost = modifiers.MaxBoost,
                BoostDuration = modifiers.BoostDuration,
                BoostCoolDown = modifiers.BoostCoolDown,
                BoostResistance = modifiers.MaximumFrictionDeceleration,
                MinimumFrictionSpeedAbsolute = modifiers.MinimumFrictionSpeedAbsolute,
                MaximumFrictionSpeedAbsolute = modifiers.MaximumFrictionSpeedAbsolute,
                MaximumFrictionDeceleration = modifiers.MaximumFrictionDeceleration,
                MinimumFrictionSpeedModifier = modifiers.MinimumFrictionSpeedModifier,
                MaximumFrictionSpeedModifier = modifiers.MaximumFrictionSpeedModifier,
                FrictionCurve = ConvertToFrictionCurveData(modifiers.FrictionCurve),
                CruiseFrictionMultiplier = modifiers.CruiseFrictionMultiplier,
                CruiseAccelerationThreshold = modifiers.CruiseAccelerationThreshold,
                AtmosphericFriction = ConvertToAtmosphericFrictionData(modifiers.AtmosphericFriction)
            };
        }

        private static FrictionCurveSegmentData[] ConvertToFrictionCurveData(FrictionCurve curve)
        {
            if (curve == null || curve.Segments == null || curve.Segments.Length == 0)
                return Array.Empty<FrictionCurveSegmentData>();

            var segments = new List<FrictionCurveSegmentData>();
            for (var i = 0; i < curve.Segments.Length; i++)
            {
                var segment = curve.Segments[i];
                if (segment == null) continue;

                segments.Add(new FrictionCurveSegmentData
                {
                    StartSpeed = segment.StartSpeed,
                    EndSpeed = segment.EndSpeed,
                    StartDeceleration = segment.StartDeceleration,
                    EndDeceleration = segment.EndDeceleration
                });
            }

            return segments.ToArray();
        }

        private static AtmosphericFrictionData ConvertToAtmosphericFrictionData(AtmosphericFrictionSettings settings)
        {
            if (settings == null) return null;

            return new AtmosphericFrictionData
            {
                Enabled = settings.Enabled,
                FrictionCurve = ConvertToFrictionCurveData(settings.FrictionCurve),
                CruiseFrictionMultiplier = settings.CruiseFrictionMultiplier,
                CruiseAccelerationThreshold = settings.CruiseAccelerationThreshold,
                AirDensityThreshold = settings.AirDensityThreshold
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

        public static ShipCoreData GetGridCore(long gridId)
        {
            var grid = MyAPIGateway.Entities.GetEntityById(gridId) as MyCubeGrid;
            if (grid == null) return ConvertToShipCoreData(Session.Config.SelectedNoCore);

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
                if (groupData == null) return ConvertToShipCoreData(Session.Config.SelectedNoCore);

                GroupComponent groupComponent;
                return !Session.GroupDict.TryGetValue(groupData, out groupComponent) ? 
                    ConvertToShipCoreData(Session.Config.SelectedNoCore) : 
                    ConvertToShipCoreData(groupComponent.ShipCore ?? Session.Config.SelectedNoCore, groupComponent.Deactivated);
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
                return Session.Config.ShipCores.Select(core => ConvertToShipCoreData(core)).ToList();
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetAllCoreConfigs: Exception - {ex}");
                return new List<ShipCoreData>();
            }
        }

        /// <summary>
        /// Gets a snapshot of the full effective framework configuration.
        /// </summary>
        public static ModConfigData GetFullConfig()
        {
            try
            {
                return ConvertToModConfigData(Session.Config);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetFullConfig: Exception - {ex}");
                return null;
            }
        }

        public static Dictionary<string, LimitStatusData> GetBlockLimitsStatus(long gridId)
        {
            var result = new Dictionary<string, LimitStatusData>();
            var grid = MyAPIGateway.Entities.GetEntityById(gridId) as MyCubeGrid;
            if (grid == null) return result;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
                if (groupData == null) return result;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return result;

                var shipCore = groupComponent.ShipCore ?? Session.Config.SelectedNoCore;
                var configuredLimits = shipCore?.BlockLimits ?? Array.Empty<BlockLimit>();
                foreach (var configuredLimit in configuredLimits)
                {
                    if (configuredLimit == null || string.IsNullOrWhiteSpace(configuredLimit.Name)) continue;

                    var max = groupComponent.GetEffectiveMaxCount(configuredLimit);
                    result[configuredLimit.Name] = new LimitStatusData
                    {
                        Name = configuredLimit.Name,
                        Current = 0d,
                        Max = max,
                        IsOverLimit = false
                    };
                }

                foreach (var kvp in groupComponent.Limits)
                {
                    var limit = kvp.Key;
                    var bucket = kvp.Value;
                    if (limit == null || bucket == null || string.IsNullOrWhiteSpace(limit.Name)) continue;

                    double totalWeight;
                    lock (bucket.BucketLock)
                    {
                        totalWeight = bucket.TotalWeight;
                    }

                    var max = groupComponent.GetEffectiveMaxCount(limit);
                    result[limit.Name] = new LimitStatusData
                    {
                        Name = limit.Name,
                        Current = totalWeight,
                        Max = max,
                        IsOverLimit = totalWeight > max
                    };
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetBlockLimitsStatus: Exception - {ex}");
            }

            return result;
        }

        public static bool IsBlockAllowed(long gridId, string typeId, string subtypeId, int count)
        {
            var grid = MyAPIGateway.Entities.GetEntityById(gridId) as MyCubeGrid;
            if (grid == null || string.IsNullOrEmpty(typeId)) return true;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
                if (groupData == null) return true;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return true;

                // Per-block-type limits only (this API's contract), evaluated against the
                // upgrade-module-adjusted effective max instead of raw MaxCount. Allocation-free.
                var blockKey = new BlockKey(typeId, subtypeId ?? string.Empty);
                return !LimitEvaluation.WouldExceedCountLimits(groupComponent, blockKey, count);
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.IsBlockAllowed: Exception - {ex}");
                return true;
            }
        }

        public static GridModifiersData GetGridModifiers(long gridId)
        {
            var grid = MyAPIGateway.Entities.GetEntityById(gridId) as MyCubeGrid;
            if (grid == null) return ConvertToGridModifiersData(null);
            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
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

        public static float GetMaxSpeed(long gridId)
        {
            var grid = MyAPIGateway.Entities.GetEntityById(gridId) as MyCubeGrid;
            if (grid == null) return 100f;

            try
            {
                var groupData = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
                if (groupData == null) return 100f;

                GroupComponent groupComponent;
                if (!Session.GroupDict.TryGetValue(groupData, out groupComponent))
                    return 100f;

                SpeedEnforcement.RefreshSpeedState(groupComponent);
                return groupComponent.EffectiveSpeedLimitMetersPerSecond;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.GetMaxSpeed: Exception - {ex}");
                return 100f;
            }
        }

        public static bool IsBoostActive(long gridId)
        {
            try
            {
                GroupComponent groupComponent;
                if (!TryGetGroupComponent(gridId, out groupComponent)) return false;

                SpeedEnforcement.RefreshSpeedState(groupComponent);
                lock (groupComponent.SpeedStateLock)
                {
                    return groupComponent.EffectiveBoostEnabled;
                }
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
