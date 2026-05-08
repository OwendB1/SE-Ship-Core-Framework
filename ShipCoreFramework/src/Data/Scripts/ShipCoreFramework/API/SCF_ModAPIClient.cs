using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
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
    /// - Validates API compatibility by major version.
    /// - Exposes strongly-typed helper methods that internally call the method factory.
    /// - Subscribes to framework events (byte[] payloads) and deserializes them.
    /// </summary>
    public class ShipCoreFrameworkClient
    {
        /// <summary>
        /// True if the API payload has been received and the provider major version is compatible.
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

        // ===== Optional resolved events (convenience) =====
        //
        // These provide best-effort resolution of the involved grid(s) and logical group.
        // Resolution can fail (e.g. entity not streamed in on the client), so parameters may be null.

        public event Action<CoreActivatedEventArgs, IMyCubeGrid, IMyGridGroupData> CoreActivatedResolved;
        public event Action<CoreDeactivatedEventArgs, IMyCubeGrid, IMyGridGroupData> CoreDeactivatedResolved;
        public event Action<LimitsRecalculatedEventArgs, IMyCubeGrid, IMyGridGroupData> LimitsRecalculatedResolved;
        public event Action<LimitsEnforcedEventArgs, IMyCubeGrid, IMyGridGroupData> LimitsEnforcedResolved;
        public event Action<BoostEventArgs, IMyCubeGrid, IMyGridGroupData> BoostActivatedResolved;
        public event Action<BoostEventArgs, IMyCubeGrid, IMyGridGroupData> BoostDeactivatedResolved;
        public event Action<ActiveDefenseEventArgs, IMyCubeGrid, IMyGridGroupData> ActiveDefenseActivatedResolved;
        public event Action<ActiveDefenseEventArgs, IMyCubeGrid, IMyGridGroupData> ActiveDefenseDeactivatedResolved;
        public event Action<GridGroupEventArgs, IMyCubeGrid, IMyCubeGrid, IMyGridGroupData> GridAddedToGroupResolved;
        public event Action<GridGroupEventArgs, IMyCubeGrid, IMyCubeGrid, IMyGridGroupData> GridRemovedFromGroupResolved;

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

                if (!ApiConstants.IsApiCompatible(ProviderApiVersion))
                {
                    IsReady = false;
                    _factory = null;

                    MyLog.Default.WriteLine(
                        $"[SCF] API major version mismatch. Provider={ApiConstants.FormatApiVersion(ProviderApiVersion)}, Consumer={ApiConstants.FormatApiVersion(ApiConstants.API_VERSION)}"
                    );
                    return;
                }

                _factory = payload.Item2;
                IsReady = _factory != null;

                if (ProviderApiVersion == ApiConstants.API_VERSION)
                {
                    MyLog.Default.WriteLine($"[SCF] API connected. Version={ApiConstants.FormatApiVersion(ProviderApiVersion)}");
                }
                else
                {
                    MyLog.Default.WriteLine(
                        $"[SCF] API connected with compatible minor version difference. Provider={ApiConstants.FormatApiVersion(ProviderApiVersion)}, Consumer={ApiConstants.FormatApiVersion(ApiConstants.API_VERSION)}"
                    );
                }
            }
            catch (Exception e)
            {
                IsReady = false;
                ProviderApiVersion = 0;
                _factory = null;
                MyLog.Default.WriteLine($"[SCF] Failed to read API payload: {e}");
            }
        }

        // ===== Resolution helpers =====

        public static IMyCubeGrid ResolveGrid(long cubeGridEntityId)
        {
            if (cubeGridEntityId == 0) return null;
            IMyEntity ent;
            if (!MyAPIGateway.Entities.TryGetEntityById(cubeGridEntityId, out ent)) return null;
            return ent as IMyCubeGrid;
        }

        public static IMyGridGroupData ResolveLogicalGroup(IMyCubeGrid anyGridInGroup)
        {
            if (anyGridInGroup == null) return null;
            return MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, anyGridInGroup);
        }

        private static long GetEntityId(IMyCubeGrid grid) => grid?.EntityId ?? 0L;

        // ===== Public API helpers (strongly typed) =====

        /// <summary>
        /// Gets the active ShipCore configuration for a grid (deserialized DTO).
        /// </summary>
        public ShipCoreData GetGridCore(IMyCubeGrid grid)
        {
            return GetGridCore(GetEntityId(grid));
        }

        /// <summary>
        /// Gets the active ShipCore configuration for a grid (deserialized DTO) by entityId.
        /// </summary>
        public ShipCoreData GetGridCore(long cubeGridEntityId)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetGridCore_Binary, cubeGridEntityId);
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
            return GetBlockLimitsStatus(GetEntityId(grid));
        }

        /// <summary>
        /// Gets block limit status (deserialized dictionary) by entityId.
        /// </summary>
        public Dictionary<string, LimitStatusData> GetBlockLimitsStatus(long cubeGridEntityId)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetBlockLimitsStatus_Binary, cubeGridEntityId);
            if (bytes == null) return new Dictionary<string, LimitStatusData>();

            var dict = MyAPIGateway.Utilities.SerializeFromBinary<Dictionary<string, LimitStatusData>>(bytes);
            return dict ?? new Dictionary<string, LimitStatusData>();
        }

        /// <summary>
        /// Checks if adding blocks would violate limits.
        /// </summary>
        public bool IsBlockAllowed(IMyCubeGrid grid, string typeId, string subtypeId, int count)
        {
            return IsBlockAllowed(GetEntityId(grid), typeId, subtypeId, count);
        }

        /// <summary>
        /// Checks if adding blocks would violate limits by entityId.
        /// </summary>
        public bool IsBlockAllowed(long cubeGridEntityId, string typeId, string subtypeId, int count)
        {
            var args = MyTuple.Create(cubeGridEntityId, typeId, subtypeId, count);
            var result = Invoke(ApiMethodId.IsBlockAllowed, args);
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Gets current grid modifiers (deserialized DTO).
        /// </summary>
        public GridModifiersData GetGridModifiers(IMyCubeGrid grid)
        {
            return GetGridModifiers(GetEntityId(grid));
        }

        /// <summary>
        /// Gets current grid modifiers (deserialized DTO) by entityId.
        /// </summary>
        public GridModifiersData GetGridModifiers(long cubeGridEntityId)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetGridModifiers_Binary, cubeGridEntityId);
            return bytes == null ? null : MyAPIGateway.Utilities.SerializeFromBinary<GridModifiersData>(bytes);
        }

        /// <summary>
        /// Gets the maximum speed for a grid based on its core.
        /// </summary>
        public float GetMaxSpeed(IMyCubeGrid grid)
        {
            return GetMaxSpeed(GetEntityId(grid));
        }

        /// <summary>
        /// Gets the maximum speed for a grid based on its core by entityId.
        /// </summary>
        public float GetMaxSpeed(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.GetMaxSpeed, cubeGridEntityId);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Checks if boost is active for a grid.
        /// </summary>
        public bool IsBoostActive(IMyCubeGrid grid)
        {
            return IsBoostActive(GetEntityId(grid));
        }

        /// <summary>
        /// Checks if boost is active for a grid by entityId.
        /// </summary>
        public bool IsBoostActive(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.IsBoostActive, cubeGridEntityId);
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
            var result = Invoke(ApiMethodId.GetGridCore_SubtypeId, GetEntityId(grid));
            return result as string ?? string.Empty;
        }
        
        /// <summary>
        /// Gets SpeedModifiers for the grid's active core (deserialized DTO).
        /// </summary>
        public SpeedModifiersData GetSpeedModifiers(IMyCubeGrid grid) => GetSpeedModifiers(GetEntityId(grid));

        public SpeedModifiersData GetSpeedModifiers(long cubeGridEntityId)
        {
            var bytes = (byte[])Invoke(ApiMethodId.GetSpeedModifiers_Binary, cubeGridEntityId);
            return bytes == null ? null : MyAPIGateway.Utilities.SerializeFromBinary<SpeedModifiersData>(bytes);
        }

        /// <summary>
        /// Gets BoostResistance for the grid's active core.
        /// </summary>
        public float GetBoostResistance(IMyCubeGrid grid) => GetBoostResistance(GetEntityId(grid));

        public float GetBoostResistance(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.GetBoostResistance, cubeGridEntityId);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Gets base max speed in m/s (no boost applied).
        /// </summary>
        public float GetBaseMaxSpeed(IMyCubeGrid grid) => GetBaseMaxSpeed(GetEntityId(grid));

        public float GetBaseMaxSpeed(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.GetBaseMaxSpeed, cubeGridEntityId);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Gets max boost multiplier (core SpeedModifiers.MaxBoost).
        /// </summary>
        public float GetMaxBoostMultiplier(IMyCubeGrid grid) => GetMaxBoostMultiplier(GetEntityId(grid));

        public float GetMaxBoostMultiplier(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.GetMaxBoostMultiplier, cubeGridEntityId);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Gets boost duration in seconds.
        /// </summary>
        public float GetBoostDuration(IMyCubeGrid grid) => GetBoostDuration(GetEntityId(grid));

        public float GetBoostDuration(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.GetBoostDuration, cubeGridEntityId);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Gets boost cooldown in seconds.
        /// </summary>
        public float GetBoostCooldown(IMyCubeGrid grid) => GetBoostCooldown(GetEntityId(grid));

        public float GetBoostCooldown(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.GetBoostCooldown, cubeGridEntityId);
            return result as float? ?? 0f;
        }

        /// <summary>
        /// Enables/disables friction-based speed limiting for a logical grid group.
        /// </summary>
        public bool SetFrictionEnabledForGroup(IMyCubeGrid grid, bool enabled) =>
            SetFrictionEnabledForGroup(GetEntityId(grid), enabled);

        public bool SetFrictionEnabledForGroup(long cubeGridEntityId, bool enabled)
        {
            var result = Invoke(ApiMethodId.SetFrictionEnabledForGroup, MyTuple.Create(cubeGridEntityId, enabled));
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Gets whether friction-based speed limiting is enabled for a logical grid group.
        /// </summary>
        public bool GetFrictionEnabledForGroup(IMyCubeGrid grid) => GetFrictionEnabledForGroup(GetEntityId(grid));

        public bool GetFrictionEnabledForGroup(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.GetFrictionEnabledForGroup, cubeGridEntityId);
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Sets the maximum friction deceleration override (m/s^2) for a logical grid group.
        /// </summary>
        public bool SetFrictionMaximumDecelerationForGroup(IMyCubeGrid grid, float deceleration) =>
            SetFrictionMaximumDecelerationForGroup(GetEntityId(grid), deceleration);

        public bool SetFrictionMaximumDecelerationForGroup(long cubeGridEntityId, float deceleration)
        {
            var result = Invoke(ApiMethodId.SetFrictionMaximumDecelerationForGroup, MyTuple.Create(cubeGridEntityId, deceleration));
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Clears the maximum friction deceleration override for a logical grid group.
        /// </summary>
        public bool ClearFrictionMaximumDecelerationForGroup(IMyCubeGrid grid) =>
            ClearFrictionMaximumDecelerationForGroup(GetEntityId(grid));

        public bool ClearFrictionMaximumDecelerationForGroup(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.ClearFrictionMaximumDecelerationForGroup, cubeGridEntityId);
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Gets the maximum friction deceleration override (m/s^2) for a logical grid group, or -1 if none.
        /// </summary>
        public float GetFrictionMaximumDecelerationForGroup(IMyCubeGrid grid) =>
            GetFrictionMaximumDecelerationForGroup(GetEntityId(grid));

        public float GetFrictionMaximumDecelerationForGroup(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.GetFrictionMaximumDecelerationForGroup, cubeGridEntityId);
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

        public bool SetFrictionMinimumSpeedAbsoluteForGroup(IMyCubeGrid grid, float speedMetersPerSecond, out string error) =>
            SetFrictionMinimumSpeedAbsoluteForGroup(GetEntityId(grid), speedMetersPerSecond, out error);

        public bool SetFrictionMinimumSpeedAbsoluteForGroup(long cubeGridEntityId, float speedMetersPerSecond, out string error)
        {
            error = null;
            var result = Invoke(ApiMethodId.SetFrictionMinimumSpeedAbsoluteForGroup, MyTuple.Create(cubeGridEntityId, speedMetersPerSecond));
            if (result is MyTuple<bool, string>)
            {
                var t = (MyTuple<bool, string>)result;
                error = t.Item2;
                return t.Item1;
            }
            error = "Invalid response.";
            return false;
        }

        public bool SetFrictionMaximumSpeedAbsoluteForGroup(IMyCubeGrid grid, float speedMetersPerSecond, out string error) =>
            SetFrictionMaximumSpeedAbsoluteForGroup(GetEntityId(grid), speedMetersPerSecond, out error);

        public bool SetFrictionMaximumSpeedAbsoluteForGroup(long cubeGridEntityId, float speedMetersPerSecond, out string error)
        {
            error = null;
            var result = Invoke(ApiMethodId.SetFrictionMaximumSpeedAbsoluteForGroup, MyTuple.Create(cubeGridEntityId, speedMetersPerSecond));
            if (result is MyTuple<bool, string>)
            {
                var t = (MyTuple<bool, string>)result;
                error = t.Item2;
                return t.Item1;
            }
            error = "Invalid response.";
            return false;
        }

        public bool TryGetFrictionMinimumSpeedAbsoluteForGroup(IMyCubeGrid grid, out float speedMetersPerSecond, out string error) =>
            TryGetFrictionMinimumSpeedAbsoluteForGroup(GetEntityId(grid), out speedMetersPerSecond, out error);

        public bool TryGetFrictionMinimumSpeedAbsoluteForGroup(long cubeGridEntityId, out float speedMetersPerSecond, out string error)
        {
            speedMetersPerSecond = -1f;
            error = null;

            var result = Invoke(ApiMethodId.GetFrictionMinimumSpeedAbsoluteForGroup, cubeGridEntityId);
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

        public bool TryGetFrictionMaximumSpeedAbsoluteForGroup(IMyCubeGrid grid, out float speedMetersPerSecond, out string error) =>
            TryGetFrictionMaximumSpeedAbsoluteForGroup(GetEntityId(grid), out speedMetersPerSecond, out error);

        public bool TryGetFrictionMaximumSpeedAbsoluteForGroup(long cubeGridEntityId, out float speedMetersPerSecond, out string error)
        {
            speedMetersPerSecond = -1f;
            error = null;

            var result = Invoke(ApiMethodId.GetFrictionMaximumSpeedAbsoluteForGroup, cubeGridEntityId);
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

        public bool SetFrictionMinimumSpeedModifierForGroup(IMyCubeGrid grid, float modifier, out string error) =>
            SetFrictionMinimumSpeedModifierForGroup(GetEntityId(grid), modifier, out error);

        public bool SetFrictionMinimumSpeedModifierForGroup(long cubeGridEntityId, float modifier, out string error)
        {
            error = null;
            var result = Invoke(ApiMethodId.SetFrictionMinimumSpeedModifierForGroup, MyTuple.Create(cubeGridEntityId, modifier));
            if (result is MyTuple<bool, string>)
            {
                var t = (MyTuple<bool, string>)result;
                error = t.Item2;
                return t.Item1;
            }
            error = "Invalid response.";
            return false;
        }

        public bool SetFrictionMaximumSpeedModifierForGroup(IMyCubeGrid grid, float modifier, out string error) =>
            SetFrictionMaximumSpeedModifierForGroup(GetEntityId(grid), modifier, out error);

        public bool SetFrictionMaximumSpeedModifierForGroup(long cubeGridEntityId, float modifier, out string error)
        {
            error = null;
            var result = Invoke(ApiMethodId.SetFrictionMaximumSpeedModifierForGroup, MyTuple.Create(cubeGridEntityId, modifier));
            if (result is MyTuple<bool, string>)
            {
                var t = (MyTuple<bool, string>)result;
                error = t.Item2;
                return t.Item1;
            }
            error = "Invalid response.";
            return false;
        }

        public bool TryGetFrictionMinimumSpeedModifierForGroup(IMyCubeGrid grid, out float modifier, out string error) =>
            TryGetFrictionMinimumSpeedModifierForGroup(GetEntityId(grid), out modifier, out error);

        public bool TryGetFrictionMinimumSpeedModifierForGroup(long cubeGridEntityId, out float modifier, out string error)
        {
            modifier = -1f;
            error = null;

            var result = Invoke(ApiMethodId.GetFrictionMinimumSpeedModifierForGroup, cubeGridEntityId);
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

        public bool TryGetFrictionMaximumSpeedModifierForGroup(IMyCubeGrid grid, out float modifier, out string error) =>
            TryGetFrictionMaximumSpeedModifierForGroup(GetEntityId(grid), out modifier, out error);

        public bool TryGetFrictionMaximumSpeedModifierForGroup(long cubeGridEntityId, out float modifier, out string error)
        {
            modifier = -1f;
            error = null;

            var result = Invoke(ApiMethodId.GetFrictionMaximumSpeedModifierForGroup, cubeGridEntityId);
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

        /// <summary>
        /// Gets whether the logical grid group has been deactivated.
        /// </summary>
        public bool IsGroupDeactivated(IMyCubeGrid grid) => IsGroupDeactivated(GetEntityId(grid));

        public bool IsGroupDeactivated(long cubeGridEntityId)
        {
            var result = Invoke(ApiMethodId.IsGroupDeactivated, cubeGridEntityId);
            return result is bool && (bool)result;
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
            if (e == null) return;
            CoreActivated?.Invoke(e);

            if (CoreActivatedResolved == null) return;
            
            var grid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(grid);
            CoreActivatedResolved(e, grid, group);
        }

        private void OnCoreDeactivated(object obj)
        {
            var e = Deserialize<CoreDeactivatedEventArgs>(obj);
            if (e == null) return;
            CoreDeactivated?.Invoke(e);

            if (CoreDeactivatedResolved == null) return;
            
            var grid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(grid);
            CoreDeactivatedResolved(e, grid, group);
        }

        private void OnLimitsRecalculated(object obj)
        {
            var e = Deserialize<LimitsRecalculatedEventArgs>(obj);
            if (e == null) return;
            LimitsRecalculated?.Invoke(e);

            if (LimitsRecalculatedResolved == null) return;
            
            var groupGrid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(groupGrid);
            LimitsRecalculatedResolved(e, groupGrid, group);
        }

        private void OnLimitsEnforced(object obj)
        {
            var e = Deserialize<LimitsEnforcedEventArgs>(obj);
            if (e == null) return;
            LimitsEnforced?.Invoke(e);

            if (LimitsEnforcedResolved == null) return;
            
            var groupGrid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(groupGrid);
            LimitsEnforcedResolved(e, groupGrid, group);
        }

        private void OnBoostActivated(object obj)
        {
            var e = Deserialize<BoostEventArgs>(obj);
            if (e == null) return;
            BoostActivated?.Invoke(e);

            if (BoostActivatedResolved == null) return;
            
            var grid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(grid);
            BoostActivatedResolved(e, grid, group);
        }

        private void OnBoostDeactivated(object obj)
        {
            var e = Deserialize<BoostEventArgs>(obj);
            if (e == null) return;
            BoostDeactivated?.Invoke(e);

            if (BoostDeactivatedResolved == null) return;
            
            var grid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(grid);
            BoostDeactivatedResolved(e, grid, group);
        }

        private void OnActiveDefenseActivated(object obj)
        {
            var e = Deserialize<ActiveDefenseEventArgs>(obj);
            if (e == null) return;
            ActiveDefenseActivated?.Invoke(e);

            if (ActiveDefenseActivatedResolved == null) return;
            
            var grid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(grid);
            ActiveDefenseActivatedResolved(e, grid, group);
        }

        private void OnActiveDefenseDeactivated(object obj)
        {
            var e = Deserialize<ActiveDefenseEventArgs>(obj);
            if (e == null) return;
            ActiveDefenseDeactivated?.Invoke(e);

            if (ActiveDefenseDeactivatedResolved == null) return;
            
            var grid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(grid);
            ActiveDefenseDeactivatedResolved(e, grid, group);
        }

        private void OnGridAddedToGroup(object obj)
        {
            var e = Deserialize<GridGroupEventArgs>(obj);
            if (e == null) return;
            GridAddedToGroup?.Invoke(e);

            if (GridAddedToGroupResolved == null) return;
            
            var grid = ResolveGrid(e.GroupGridId);
            var groupGrid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(groupGrid ?? grid);
            GridAddedToGroupResolved(e, grid, groupGrid, group);
        }

        private void OnGridRemovedFromGroup(object obj)
        {
            var e = Deserialize<GridGroupEventArgs>(obj);
            if (e == null) return;
            GridRemovedFromGroup?.Invoke(e);

            if (GridRemovedFromGroupResolved == null) return;
            
            var grid = ResolveGrid(e.GroupGridId);
            var groupGrid = ResolveGrid(e.GroupGridId);
            var group = ResolveLogicalGroup(groupGrid ?? grid);
            GridRemovedFromGroupResolved(e, grid, groupGrid, group);
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
