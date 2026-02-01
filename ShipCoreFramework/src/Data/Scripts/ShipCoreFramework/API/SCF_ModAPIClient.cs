using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.Utils;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global

namespace ShipCoreFramework
{
    /// <summary>
    /// Ship Core Framework API client wrapper for other mods.
    ///
    /// Usage pattern:
    /// - Call Register() in LoadData (or before first use).
    /// - Call Unregister() in UnloadData.
    ///
    /// This wrapper:
    /// - Receives the API payload broadcast by ShipCoreFramework.
    /// - Validates API version exact match.
    /// - Exposes strongly-typed helper methods that internally call the method factory.
    /// - Subscribes to framework events (byte[] payloads) and deserializes them.
    /// </summary>
    public class ShipCoreFrameworkClient
    {
        /// <summary>
        /// True if the API payload has been received and the version matches.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// The provider version received from the framework (ApiConstants.API_VERSION on the provider).
        /// </summary>
        public int ProviderApiVersion { get; private set; }

        private Func<int, Func<object, object>> _factory;

        // ===== Events (consumer-facing) =====

        /// <summary>
        /// Fired when a core becomes active for a grid group.
        /// </summary>
        public event Action<CoreActivatedEventArgs> CoreActivated;

        /// <summary>
        /// Fired when a group loses its active core.
        /// </summary>
        public event Action<CoreDeactivatedEventArgs> CoreDeactivated;

        /// <summary>
        /// Fired when block limits are recalculated.
        /// </summary>
        public event Action<LimitsRecalculatedEventArgs> LimitsRecalculated;

        /// <summary>
        /// Fired when enforcement runs and blocks are punished.
        /// </summary>
        public event Action<LimitsEnforcedEventArgs> LimitsEnforced;

        /// <summary>
        /// Fired when boost becomes active.
        /// </summary>
        public event Action<BoostEventArgs> BoostActivated;

        /// <summary>
        /// Fired when boost ends.
        /// </summary>
        public event Action<BoostEventArgs> BoostDeactivated;

        /// <summary>
        /// Fired when active defense becomes active.
        /// </summary>
        public event Action<ActiveDefenseEventArgs> ActiveDefenseActivated;

        /// <summary>
        /// Fired when active defense ends.
        /// </summary>
        public event Action<ActiveDefenseEventArgs> ActiveDefenseDeactivated;

        /// <summary>
        /// Fired when a grid is added to a logical group.
        /// </summary>
        public event Action<GridGroupEventArgs> GridAddedToGroup;

        /// <summary>
        /// Fired when a grid is removed from a logical group.
        /// </summary>
        public event Action<GridGroupEventArgs> GridRemovedFromGroup;

        // ===== Registration =====

        /// <summary>
        /// Registers message handlers to receive the API payload and all framework events.
        /// Call this once during LoadData.
        /// </summary>
        public void Register()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.API_ID, OnApiPayloadReceived);

            // Event payloads are byte[] (serialized via SerializeToBinary on provider).
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_CORE_ACTIVATED, OnCoreActivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_CORE_DEACTIVATED, OnCoreDeactivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_LIMITS_RECALCULATED, OnLimitsRecalculated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_LIMITS_ENFORCED, OnLimitsEnforced);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_BOOST_ACTIVATED, OnBoostActivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_BOOST_DEACTIVATED, OnBoostDeactivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_ACTIVE_DEFENSE_ACTIVATED, OnActiveDefenseActivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_ACTIVE_DEFENSE_DEACTIVATED, OnActiveDefenseDeactivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_GRID_ADDED_TO_GROUP, OnGridAddedToGroup);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_GRID_REMOVED_FROM_GROUP, OnGridRemovedFromGroup);
        }

        /// <summary>
        /// Unregisters message handlers.
        /// Call this once during UnloadData.
        /// </summary>
        public void Unregister()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.API_ID, OnApiPayloadReceived);

            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_CORE_ACTIVATED, OnCoreActivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_CORE_DEACTIVATED, OnCoreDeactivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_LIMITS_RECALCULATED, OnLimitsRecalculated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_LIMITS_ENFORCED, OnLimitsEnforced);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_BOOST_ACTIVATED, OnBoostActivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_BOOST_DEACTIVATED, OnBoostDeactivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_ACTIVE_DEFENSE_ACTIVATED, OnActiveDefenseActivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_ACTIVE_DEFENSE_DEACTIVATED, OnActiveDefenseDeactivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_GRID_ADDED_TO_GROUP, OnGridAddedToGroup);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_GRID_REMOVED_FROM_GROUP, OnGridRemovedFromGroup);

            IsReady = false;
            ProviderApiVersion = 0;
            _factory = null;
        }

        // ===== API payload =====

        /// <summary>
        /// Receives the API payload from the framework.
        /// Payload format: MyTuple&lt;int, Func&lt;int, Func&lt;object, object&gt;&gt;&gt;
        /// Item1: provider ApiConstants.API_VERSION
        /// Item2: method factory (methodId -> delegate)
        /// </summary>
        private void OnApiPayloadReceived(object obj)
        {
            try
            {
                var payload = (MyTuple<int, Func<int, Func<object, object>>>)obj;

                ProviderApiVersion = payload.Item1;

                if (ProviderApiVersion != ApiConstants.API_VERSION)
                {
                    IsReady = false;
                    _factory = null;

                    // Use your own logging method here.
                    MyLog.Default.WriteLine(
                        $"[SCF] API version mismatch. Provider={ProviderApiVersion:X}, Consumer={ApiConstants.API_VERSION:X}"
                    );
                    return;
                }

                _factory = payload.Item2;
                IsReady = _factory != null;

                MyLog.Default.WriteLine($"[SCF] API connected. Version={ProviderApiVersion:X}");
            }
            catch (Exception e)
            {
                IsReady = false;
                ProviderApiVersion = 0;
                _factory = null;
                MyLog.Default.WriteLine($"[SCF] Failed to read API payload: {e}");
            }
        }

        // ===== Public API helpers (strongly typed) =====

        /// <summary>
        /// Gets the active ShipCore configuration for a grid (deserialized DTO).
        /// </summary>
        public ShipCoreData GetGridCore(IMyCubeGrid grid)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetGridCore_Binary, grid);
            return bytes == null ? null : MyAPIGateway.Utilities.SerializeFromBinary<ShipCoreData>(bytes);
        }

        /// <summary>
        /// Gets a specific ShipCore configuration by its subtypeId (deserialized DTO).
        /// </summary>
        public ShipCoreData GetCoreBySubtypeId(string subtypeId)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetCoreBySubtypeId_Binary, subtypeId);
            return bytes == null ? null : MyAPIGateway.Utilities.SerializeFromBinary<ShipCoreData>(bytes);
        }

        /// <summary>
        /// Gets all available core configs (deserialized DTO list).
        /// </summary>
        public List<ShipCoreData> GetAllCoreConfigs()
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetAllCoreConfigs_Binary, null);
            if (bytes == null) return new List<ShipCoreData>();

            var list = MyAPIGateway.Utilities.SerializeFromBinary<List<ShipCoreData>>(bytes);
            return list ?? new List<ShipCoreData>();
        }

        /// <summary>
        /// Gets block limit status (deserialized dictionary).
        /// </summary>
        public Dictionary<string, LimitStatusData> GetBlockLimitsStatus(IMyCubeGrid grid)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetBlockLimitsStatus_Binary, grid);
            if (bytes == null) return new Dictionary<string, LimitStatusData>();

            var dict = MyAPIGateway.Utilities.SerializeFromBinary<Dictionary<string, LimitStatusData>>(bytes);
            return dict ?? new Dictionary<string, LimitStatusData>();
        }

        /// <summary>
        /// Checks if adding blocks would violate limits.
        /// </summary>
        public bool IsBlockAllowed(IMyCubeGrid grid, string typeId, string subtypeId, int count)
        {
            var args = MyTuple.Create(grid, typeId, subtypeId, count);
            var result = Invoke(ApiMethodId.IsBlockAllowed, args);
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Gets current grid modifiers (deserialized DTO).
        /// </summary>
        public GridModifiersData GetGridModifiers(IMyCubeGrid grid)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetGridModifiers_Binary, grid);
            return bytes == null ? null : MyAPIGateway.Utilities.SerializeFromBinary<GridModifiersData>(bytes);
        }

        /// <summary>
        /// Gets the maximum speed for a grid based on its core.
        /// </summary>
        public float GetMaxSpeed(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetMaxSpeed, grid);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Checks if boost is active for a grid.
        /// </summary>
        public bool IsBoostActive(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.IsBoostActive, grid);
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Gets the currently selected NoCore config (deserialized DTO).
        /// </summary>
        public ShipCoreData GetNoCoreConfig()
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetNoCoreConfig_Binary, null);
            return bytes == null ? null : MyAPIGateway.Utilities.SerializeFromBinary<ShipCoreData>(bytes);
        }

        /// <summary>
        /// Optional primitive getter: grid core subtypeId without DTO deserialization.
        /// </summary>
        public string GetGridCoreSubtypeId(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetGridCore_SubtypeId, grid);
            return result as string ?? string.Empty;
        }
        
        /// <summary>
        /// Gets SpeedModifiers for the grid's active core (deserialized DTO).
        /// </summary>
        public SpeedModifiersData GetSpeedModifiers(IMyCubeGrid grid)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetSpeedModifiers_Binary, grid);
            return bytes == null ? null : MyAPIGateway.Utilities.SerializeFromBinary<SpeedModifiersData>(bytes);
        }

        /// <summary>
        /// Returns true if Dynamic Boost is enabled for the grid's active core.
        /// </summary>
        public bool IsDynamicBoostEnabled(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.IsDynamicBoostEnabled, grid);
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Gets BoostResistance for the grid's active core.
        /// </summary>
        public float GetBoostResistance(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetBoostResistance, grid);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Gets base max speed in m/s (no boost applied).
        /// </summary>
        public float GetBaseMaxSpeed(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetBaseMaxSpeed, grid);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Gets max boost multiplier (core SpeedModifiers.MaxBoost).
        /// </summary>
        public float GetMaxBoostMultiplier(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetMaxBoostMultiplier, grid);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Gets boost duration in seconds.
        /// </summary>
        public float GetBoostDuration(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetBoostDuration, grid);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Gets boost cooldown in seconds.
        /// </summary>
        public float GetBoostCooldown(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetBoostCooldown, grid);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Enables/disables friction-based speed limiting for a logical grid group.
        /// </summary>
        public bool SetFrictionEnabledForGroup(IMyCubeGrid grid, bool enabled)
        {
            var result = Invoke(ApiMethodId.SetFrictionEnabledForGroup, MyTuple.Create(grid, enabled));
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Gets whether friction-based speed limiting is enabled for a logical grid group.
        /// </summary>
        public bool GetFrictionEnabledForGroup(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetFrictionEnabledForGroup, grid);
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Sets the maximum friction deceleration override (m/s^2) for a logical grid group.
        /// </summary>
        public bool SetFrictionMaximumDecelerationForGroup(IMyCubeGrid grid, float deceleration)
        {
            var result = Invoke(ApiMethodId.SetFrictionMaximumDecelerationForGroup, MyTuple.Create(grid, deceleration));
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Clears the maximum friction deceleration override for a logical grid group.
        /// </summary>
        public bool ClearFrictionMaximumDecelerationForGroup(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.ClearFrictionMaximumDecelerationForGroup, grid);
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Gets the maximum friction deceleration override (m/s^2) for a logical grid group, or -1 if none.
        /// </summary>
        public float GetFrictionMaximumDecelerationForGroup(IMyCubeGrid grid)
        {
            var result = Invoke(ApiMethodId.GetFrictionMaximumDecelerationForGroup, grid);
            return result as float? ?? -1f;
        }

        /// <summary>
        /// Gets the current world friction speed value mode (Modifier vs Absolute).
        /// </summary>
        public FrictionSpeedValueModeData GetFrictionSpeedValueMode()
        {
            var result = Invoke(ApiMethodId.GetFrictionSpeedValueMode, null);
            if (result is int)
            {
                return (FrictionSpeedValueModeData)(int)result;
            }
            return FrictionSpeedValueModeData.Modifier;
        }

        public bool SetFrictionMinimumSpeedAbsoluteForGroup(IMyCubeGrid grid, float speedMetersPerSecond, out string error)
        {
            error = null;
            var result = Invoke(ApiMethodId.SetFrictionMinimumSpeedAbsoluteForGroup, MyTuple.Create(grid, speedMetersPerSecond));
            if (result is MyTuple<bool, string>)
            {
                var t = (MyTuple<bool, string>)result;
                error = t.Item2;
                return t.Item1;
            }
            error = "Invalid response.";
            return false;
        }

        public bool SetFrictionMaximumSpeedAbsoluteForGroup(IMyCubeGrid grid, float speedMetersPerSecond, out string error)
        {
            error = null;
            var result = Invoke(ApiMethodId.SetFrictionMaximumSpeedAbsoluteForGroup, MyTuple.Create(grid, speedMetersPerSecond));
            if (result is MyTuple<bool, string>)
            {
                var t = (MyTuple<bool, string>)result;
                error = t.Item2;
                return t.Item1;
            }
            error = "Invalid response.";
            return false;
        }

        public bool TryGetFrictionMinimumSpeedAbsoluteForGroup(IMyCubeGrid grid, out float speedMetersPerSecond, out string error)
        {
            speedMetersPerSecond = -1f;
            error = null;

            var result = Invoke(ApiMethodId.GetFrictionMinimumSpeedAbsoluteForGroup, grid);
            if (result is MyTuple<float, string>)
            {
                var t = (MyTuple<float, string>)result;
                speedMetersPerSecond = t.Item1;
                error = t.Item2;
                return string.IsNullOrEmpty(error);
            }

            error = "Invalid response.";
            return false;
        }

        public bool TryGetFrictionMaximumSpeedAbsoluteForGroup(IMyCubeGrid grid, out float speedMetersPerSecond, out string error)
        {
            speedMetersPerSecond = -1f;
            error = null;

            var result = Invoke(ApiMethodId.GetFrictionMaximumSpeedAbsoluteForGroup, grid);
            if (result is MyTuple<float, string>)
            {
                var t = (MyTuple<float, string>)result;
                speedMetersPerSecond = t.Item1;
                error = t.Item2;
                return string.IsNullOrEmpty(error);
            }

            error = "Invalid response.";
            return false;
        }

        public bool SetFrictionMinimumSpeedModifierForGroup(IMyCubeGrid grid, float modifier, out string error)
        {
            error = null;
            var result = Invoke(ApiMethodId.SetFrictionMinimumSpeedModifierForGroup, MyTuple.Create(grid, modifier));
            if (result is MyTuple<bool, string>)
            {
                var t = (MyTuple<bool, string>)result;
                error = t.Item2;
                return t.Item1;
            }
            error = "Invalid response.";
            return false;
        }

        public bool SetFrictionMaximumSpeedModifierForGroup(IMyCubeGrid grid, float modifier, out string error)
        {
            error = null;
            var result = Invoke(ApiMethodId.SetFrictionMaximumSpeedModifierForGroup, MyTuple.Create(grid, modifier));
            if (result is MyTuple<bool, string>)
            {
                var t = (MyTuple<bool, string>)result;
                error = t.Item2;
                return t.Item1;
            }
            error = "Invalid response.";
            return false;
        }

        public bool TryGetFrictionMinimumSpeedModifierForGroup(IMyCubeGrid grid, out float modifier, out string error)
        {
            modifier = -1f;
            error = null;

            var result = Invoke(ApiMethodId.GetFrictionMinimumSpeedModifierForGroup, grid);
            if (result is MyTuple<float, string>)
            {
                var t = (MyTuple<float, string>)result;
                modifier = t.Item1;
                error = t.Item2;
                return string.IsNullOrEmpty(error);
            }

            error = "Invalid response.";
            return false;
        }

        public bool TryGetFrictionMaximumSpeedModifierForGroup(IMyCubeGrid grid, out float modifier, out string error)
        {
            modifier = -1f;
            error = null;

            var result = Invoke(ApiMethodId.GetFrictionMaximumSpeedModifierForGroup, grid);
            if (result is MyTuple<float, string>)
            {
                var t = (MyTuple<float, string>)result;
                modifier = t.Item1;
                error = t.Item2;
                return string.IsNullOrEmpty(error);
            }

            error = "Invalid response.";
            return false;
        }


        // ===== Internals =====

        /// <summary>
        /// Invokes a methodId on the provider via the method factory.
        /// </summary>
        private object Invoke(int methodId, object arg)
        {
            if (!IsReady || _factory == null)
                return null;

            try
            {
                var method = _factory(methodId);
                return method?.Invoke(arg);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine($"[SCF] API invoke failed. methodId={methodId} ex={e}");
                return null;
            }
        }

        // ===== Event handlers (deserialize byte[] and raise typed events) =====

        private void OnCoreActivated(object obj)
        {
            var e = Deserialize<CoreActivatedEventArgs>(obj);
            if (e != null && CoreActivated != null) CoreActivated(e);
        }

        private void OnCoreDeactivated(object obj)
        {
            var e = Deserialize<CoreDeactivatedEventArgs>(obj);
            if (e != null && CoreDeactivated != null) CoreDeactivated(e);
        }

        private void OnLimitsRecalculated(object obj)
        {
            var e = Deserialize<LimitsRecalculatedEventArgs>(obj);
            if (e != null && LimitsRecalculated != null) LimitsRecalculated(e);
        }

        private void OnLimitsEnforced(object obj)
        {
            var e = Deserialize<LimitsEnforcedEventArgs>(obj);
            if (e != null && LimitsEnforced != null) LimitsEnforced(e);
        }

        private void OnBoostActivated(object obj)
        {
            var e = Deserialize<BoostEventArgs>(obj);
            if (e != null && BoostActivated != null) BoostActivated(e);
        }

        private void OnBoostDeactivated(object obj)
        {
            var e = Deserialize<BoostEventArgs>(obj);
            if (e != null && BoostDeactivated != null) BoostDeactivated(e);
        }

        private void OnActiveDefenseActivated(object obj)
        {
            var e = Deserialize<ActiveDefenseEventArgs>(obj);
            if (e != null && ActiveDefenseActivated != null) ActiveDefenseActivated(e);
        }

        private void OnActiveDefenseDeactivated(object obj)
        {
            var e = Deserialize<ActiveDefenseEventArgs>(obj);
            if (e != null && ActiveDefenseDeactivated != null) ActiveDefenseDeactivated(e);
        }

        private void OnGridAddedToGroup(object obj)
        {
            var e = Deserialize<GridGroupEventArgs>(obj);
            if (e != null && GridAddedToGroup != null) GridAddedToGroup(e);
        }

        private void OnGridRemovedFromGroup(object obj)
        {
            var e = Deserialize<GridGroupEventArgs>(obj);
            if (e != null && GridRemovedFromGroup != null) GridRemovedFromGroup(e);
        }

        /// <summary>
        /// Deserializes a binary event payload into a DTO instance.
        /// </summary>
        private static T Deserialize<T>(object obj) where T : class
        {
            try
            {
                var bytes = obj as byte[];
                if (bytes == null || bytes.Length == 0)
                    return null;

                return MyAPIGateway.Utilities.SerializeFromBinary<T>(bytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
