using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace ShipCoreFramework
{
    /// <summary>
    /// Shared API v4 consumer implementation. Use ShipCoreFrameworkClientApi on remote clients and
    /// ShipCoreFrameworkServerApi on server processes.
    /// </summary>
    public abstract class ShipCoreFrameworkApiBase
    {
        private readonly long _apiId;
        private readonly ApiProviderRoleData _expectedRole;
        private Func<int, Func<object, object>> _factory;

        public bool ProviderReady { get; private set; }
        public bool ConfigReady { get; private set; }
        public bool RuntimeSnapshotReady { get; private set; }
        public int ProviderApiVersion { get; private set; }
        public ApiProviderRoleData ProviderRole { get; private set; }
        public ApiCapabilityData Capabilities { get; private set; }

        [Obsolete("Use ProviderReady, ConfigReady, and RuntimeSnapshotReady explicitly.")]
        public bool IsReady
        {
            get { return ProviderReady; }
        }

        public event Action<CoreActivatedEventArgs> CoreActivated;
        public event Action<CoreDeactivatedEventArgs> CoreDeactivated;
        public event Action<LimitsRecalculatedEventArgs> LimitsRecalculated;
        public event Action<LimitsEnforcedEventArgs> LimitsEnforced;
        public event Action<BoostEventArgs> BoostActivated;
        public event Action<BoostEventArgs> BoostDeactivated;
        public event Action<ActiveDefenseEventArgs> ActiveDefenseActivated;
        public event Action<ActiveDefenseEventArgs> ActiveDefenseDeactivated;
        public event Action<GridGroupEventArgs> GridAddedToGroup;
        public event Action<GridGroupEventArgs> GridRemovedFromGroup;
        public event Action<ConfigReceivedEventArgs> ConfigReceived;
        public event Action<RuntimeSnapshotReadyEventArgs> RuntimeReady;

        public event Action<CoreActivatedEventArgs, IMyCubeGrid, IMyGridGroupData> CoreActivatedResolved;
        public event Action<CoreDeactivatedEventArgs, IMyCubeGrid, IMyGridGroupData> CoreDeactivatedResolved;
        public event Action<LimitsRecalculatedEventArgs, IMyCubeGrid, IMyGridGroupData> LimitsRecalculatedResolved;
        public event Action<LimitsEnforcedEventArgs, IMyCubeGrid, IMyGridGroupData> LimitsEnforcedResolved;
        public event Action<BoostEventArgs, IMyCubeGrid, IMyGridGroupData> BoostActivatedResolved;
        public event Action<BoostEventArgs, IMyCubeGrid, IMyGridGroupData> BoostDeactivatedResolved;
        public event Action<ActiveDefenseEventArgs, IMyCubeGrid, IMyGridGroupData>
            ActiveDefenseActivatedResolved;
        public event Action<ActiveDefenseEventArgs, IMyCubeGrid, IMyGridGroupData>
            ActiveDefenseDeactivatedResolved;
        public event Action<GridGroupEventArgs, IMyCubeGrid, IMyCubeGrid, IMyGridGroupData>
            GridAddedToGroupResolved;
        public event Action<GridGroupEventArgs, IMyCubeGrid, IMyCubeGrid, IMyGridGroupData>
            GridRemovedFromGroupResolved;

        protected ShipCoreFrameworkApiBase(long apiId, ApiProviderRoleData expectedRole)
        {
            _apiId = apiId;
            _expectedRole = expectedRole;
        }

        public void Register()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(_apiId, OnApiPayloadReceived);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_CORE_ACTIVATED, OnCoreActivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_CORE_DEACTIVATED, OnCoreDeactivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_LIMITS_RECALCULATED,
                OnLimitsRecalculated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_LIMITS_ENFORCED, OnLimitsEnforced);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_BOOST_ACTIVATED, OnBoostActivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_BOOST_DEACTIVATED,
                OnBoostDeactivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_ACTIVE_DEFENSE_ACTIVATED,
                OnActiveDefenseActivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_ACTIVE_DEFENSE_DEACTIVATED,
                OnActiveDefenseDeactivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_GRID_ADDED_TO_GROUP,
                OnGridAddedToGroup);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_GRID_REMOVED_FROM_GROUP,
                OnGridRemovedFromGroup);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_CONFIG_RECEIVED, OnConfigReceived);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_RUNTIME_SNAPSHOT_READY,
                OnRuntimeReady);
        }

        public void Unregister()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(_apiId, OnApiPayloadReceived);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_CORE_ACTIVATED, OnCoreActivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_CORE_DEACTIVATED,
                OnCoreDeactivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_LIMITS_RECALCULATED,
                OnLimitsRecalculated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_LIMITS_ENFORCED,
                OnLimitsEnforced);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_BOOST_ACTIVATED,
                OnBoostActivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_BOOST_DEACTIVATED,
                OnBoostDeactivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_ACTIVE_DEFENSE_ACTIVATED,
                OnActiveDefenseActivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_ACTIVE_DEFENSE_DEACTIVATED,
                OnActiveDefenseDeactivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_GRID_ADDED_TO_GROUP,
                OnGridAddedToGroup);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_GRID_REMOVED_FROM_GROUP,
                OnGridRemovedFromGroup);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_CONFIG_RECEIVED,
                OnConfigReceived);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_RUNTIME_SNAPSHOT_READY,
                OnRuntimeReady);
            Reset();
        }

        public ApiReadResult<ApiReadinessData> RefreshReadiness()
        {
            ApiReadResult<ApiReadinessData> result =
                InvokeBinary<ApiReadinessData>(ApiMethodId.GetReadiness_Binary, null);
            if (result.Success && result.Value != null)
            {
                ProviderRole = result.Value.Role;
                ConfigReady = result.Value.ConfigReady;
                RuntimeSnapshotReady = result.Value.RuntimeSnapshotReady;
            }
            return result;
        }

        public ApiReadResult<bool> TryGetRuntimeStateAvailability(long gridId)
        {
            return InvokePrimitive<bool>(ApiMethodId.GetRuntimeStateAvailability, gridId);
        }

        public ApiReadResult<bool> TryGetRuntimeStateAvailability(IMyCubeGrid grid)
        {
            return TryGetRuntimeStateAvailability(GetEntityId(grid));
        }

        public ApiReadResult<ShipCoreData> TryGetGridCore(IMyCubeGrid grid)
        {
            return TryGetGridCore(GetEntityId(grid));
        }

        public ApiReadResult<ShipCoreData> TryGetGridCore(long gridId)
        {
            return InvokeBinary<ShipCoreData>(ApiMethodId.GetGridCore_Binary, gridId);
        }

        public ApiReadResult<ShipCoreData> TryGetCoreBySubtypeId(string subtypeId)
        {
            return InvokeBinary<ShipCoreData>(ApiMethodId.GetCoreBySubtypeId_Binary, subtypeId);
        }

        public ApiReadResult<List<ShipCoreData>> TryGetAllCoreConfigs()
        {
            return InvokeBinary<List<ShipCoreData>>(ApiMethodId.GetAllCoreConfigs_Binary, null);
        }

        public ApiReadResult<Dictionary<string, LimitStatusData>> TryGetBlockLimitsStatus(IMyCubeGrid grid)
        {
            return TryGetBlockLimitsStatus(GetEntityId(grid));
        }

        public ApiReadResult<Dictionary<string, LimitStatusData>> TryGetBlockLimitsStatus(long gridId)
        {
            return InvokeBinary<Dictionary<string, LimitStatusData>>(
                ApiMethodId.GetBlockLimitsStatus_Binary, gridId);
        }

        public ApiReadResult<bool> TryIsBlockAllowed(IMyCubeGrid grid, string typeId, string subtypeId,
            int count)
        {
            return TryIsBlockAllowed(GetEntityId(grid), typeId, subtypeId, count);
        }

        public ApiReadResult<bool> TryIsBlockAllowed(long gridId, string typeId, string subtypeId, int count)
        {
            return InvokePrimitive<bool>(ApiMethodId.IsBlockAllowed,
                MyTuple.Create(gridId, typeId, subtypeId, count));
        }

        public ApiReadResult<GridModifiersData> TryGetGridModifiers(IMyCubeGrid grid)
        {
            return TryGetGridModifiers(GetEntityId(grid));
        }

        public ApiReadResult<GridModifiersData> TryGetGridModifiers(long gridId)
        {
            return InvokeBinary<GridModifiersData>(ApiMethodId.GetGridModifiers_Binary, gridId);
        }

        public ApiReadResult<float> TryGetMaxSpeed(IMyCubeGrid grid)
        {
            return TryGetMaxSpeed(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetMaxSpeed(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetMaxSpeed, gridId);
        }

        public ApiReadResult<bool> TryIsBoostActive(IMyCubeGrid grid)
        {
            return TryIsBoostActive(GetEntityId(grid));
        }

        public ApiReadResult<bool> TryIsBoostActive(long gridId)
        {
            return InvokePrimitive<bool>(ApiMethodId.IsBoostActive, gridId);
        }

        public ApiReadResult<ShipCoreData> TryGetNoCoreConfig()
        {
            return InvokeBinary<ShipCoreData>(ApiMethodId.GetNoCoreConfig_Binary, null);
        }

        public ApiReadResult<ModConfigData> TryGetFullConfig()
        {
            return InvokeBinary<ModConfigData>(ApiMethodId.GetFullConfig_Binary, null);
        }

        public ApiReadResult<SpeedModifiersData> TryGetSpeedModifiers(IMyCubeGrid grid)
        {
            return TryGetSpeedModifiers(GetEntityId(grid));
        }

        public ApiReadResult<SpeedModifiersData> TryGetSpeedModifiers(long gridId)
        {
            return InvokeBinary<SpeedModifiersData>(ApiMethodId.GetSpeedModifiers_Binary, gridId);
        }

        public ApiReadResult<float> TryGetBoostResistance(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetBoostResistance, gridId);
        }

        public ApiReadResult<float> TryGetBoostResistance(IMyCubeGrid grid)
        {
            return TryGetBoostResistance(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetBaseMaxSpeed(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetBaseMaxSpeed, gridId);
        }

        public ApiReadResult<float> TryGetBaseMaxSpeed(IMyCubeGrid grid)
        {
            return TryGetBaseMaxSpeed(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetMaxBoostMultiplier(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetMaxBoostMultiplier, gridId);
        }

        public ApiReadResult<float> TryGetMaxBoostMultiplier(IMyCubeGrid grid)
        {
            return TryGetMaxBoostMultiplier(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetBoostDuration(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetBoostDuration, gridId);
        }

        public ApiReadResult<float> TryGetBoostDuration(IMyCubeGrid grid)
        {
            return TryGetBoostDuration(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetBoostCooldown(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetBoostCooldown, gridId);
        }

        public ApiReadResult<float> TryGetBoostCooldown(IMyCubeGrid grid)
        {
            return TryGetBoostCooldown(GetEntityId(grid));
        }

        public ApiReadResult<bool> TryGetFrictionEnabledForGroup(long gridId)
        {
            return InvokePrimitive<bool>(ApiMethodId.GetFrictionEnabledForGroup, gridId);
        }

        public ApiReadResult<bool> TryGetFrictionEnabledForGroup(IMyCubeGrid grid)
        {
            return TryGetFrictionEnabledForGroup(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetFrictionMaximumDecelerationForGroup(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetFrictionMaximumDecelerationForGroup, gridId);
        }

        public ApiReadResult<float> TryGetFrictionMaximumDecelerationForGroup(IMyCubeGrid grid)
        {
            return TryGetFrictionMaximumDecelerationForGroup(GetEntityId(grid));
        }

        public ApiReadResult<int> TryGetFrictionSpeedValueMode()
        {
            return InvokePrimitive<int>(ApiMethodId.GetFrictionSpeedValueMode, null);
        }

        public ApiReadResult<float> TryGetFrictionMinimumSpeedAbsoluteForGroup(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetFrictionMinimumSpeedAbsoluteForGroup, gridId);
        }

        public ApiReadResult<float> TryGetFrictionMinimumSpeedAbsoluteForGroup(IMyCubeGrid grid)
        {
            return TryGetFrictionMinimumSpeedAbsoluteForGroup(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetFrictionMaximumSpeedAbsoluteForGroup(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetFrictionMaximumSpeedAbsoluteForGroup, gridId);
        }

        public ApiReadResult<float> TryGetFrictionMaximumSpeedAbsoluteForGroup(IMyCubeGrid grid)
        {
            return TryGetFrictionMaximumSpeedAbsoluteForGroup(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetFrictionMinimumSpeedModifierForGroup(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetFrictionMinimumSpeedModifierForGroup, gridId);
        }

        public ApiReadResult<float> TryGetFrictionMinimumSpeedModifierForGroup(IMyCubeGrid grid)
        {
            return TryGetFrictionMinimumSpeedModifierForGroup(GetEntityId(grid));
        }

        public ApiReadResult<float> TryGetFrictionMaximumSpeedModifierForGroup(long gridId)
        {
            return InvokePrimitive<float>(ApiMethodId.GetFrictionMaximumSpeedModifierForGroup, gridId);
        }

        public ApiReadResult<float> TryGetFrictionMaximumSpeedModifierForGroup(IMyCubeGrid grid)
        {
            return TryGetFrictionMaximumSpeedModifierForGroup(GetEntityId(grid));
        }

        public ApiReadResult<bool> TryIsGroupDeactivated(long gridId)
        {
            return InvokePrimitive<bool>(ApiMethodId.IsGroupDeactivated, gridId);
        }

        public ApiReadResult<bool> TryIsGroupDeactivated(IMyCubeGrid grid)
        {
            return TryIsGroupDeactivated(GetEntityId(grid));
        }

        [Obsolete("Use TryGetGridCore and inspect ApiReadStatusData.")]
        public ShipCoreData GetGridCore(long gridId)
        {
            return LegacyValue(TryGetGridCore(gridId), null);
        }

        [Obsolete("Use TryGetGridCore and inspect ApiReadStatusData.")]
        public ShipCoreData GetGridCore(IMyCubeGrid grid)
        {
            return GetGridCore(GetEntityId(grid));
        }

        [Obsolete("Use TryGetCoreBySubtypeId and inspect ApiReadStatusData.")]
        public ShipCoreData GetCoreBySubtypeId(string subtypeId)
        {
            return LegacyValue(TryGetCoreBySubtypeId(subtypeId), null);
        }

        [Obsolete("Use TryGetAllCoreConfigs and inspect ApiReadStatusData.")]
        public List<ShipCoreData> GetAllCoreConfigs()
        {
            return LegacyValue(TryGetAllCoreConfigs(), new List<ShipCoreData>());
        }

        [Obsolete("Use TryGetBlockLimitsStatus and inspect ApiReadStatusData.")]
        public Dictionary<string, LimitStatusData> GetBlockLimitsStatus(long gridId)
        {
            return LegacyValue(TryGetBlockLimitsStatus(gridId),
                new Dictionary<string, LimitStatusData>());
        }

        [Obsolete("Use TryGetBlockLimitsStatus and inspect ApiReadStatusData.")]
        public Dictionary<string, LimitStatusData> GetBlockLimitsStatus(IMyCubeGrid grid)
        {
            return GetBlockLimitsStatus(GetEntityId(grid));
        }

        [Obsolete("Use TryIsBlockAllowed and inspect ApiReadStatusData.")]
        public bool IsBlockAllowed(long gridId, string typeId, string subtypeId, int count)
        {
            return LegacyValue(TryIsBlockAllowed(gridId, typeId, subtypeId, count), true);
        }

        [Obsolete("Use TryIsBlockAllowed and inspect ApiReadStatusData.")]
        public bool IsBlockAllowed(IMyCubeGrid grid, string typeId, string subtypeId, int count)
        {
            return IsBlockAllowed(GetEntityId(grid), typeId, subtypeId, count);
        }

        [Obsolete("Use TryGetGridModifiers and inspect ApiReadStatusData.")]
        public GridModifiersData GetGridModifiers(long gridId)
        {
            return LegacyValue(TryGetGridModifiers(gridId), null);
        }

        [Obsolete("Use TryGetGridModifiers and inspect ApiReadStatusData.")]
        public GridModifiersData GetGridModifiers(IMyCubeGrid grid)
        {
            return GetGridModifiers(GetEntityId(grid));
        }

        [Obsolete("Use TryGetMaxSpeed and inspect ApiReadStatusData.")]
        public float GetMaxSpeed(long gridId)
        {
            return LegacyValue(TryGetMaxSpeed(gridId), 100f);
        }

        [Obsolete("Use TryGetMaxSpeed and inspect ApiReadStatusData.")]
        public float GetMaxSpeed(IMyCubeGrid grid)
        {
            return GetMaxSpeed(GetEntityId(grid));
        }

        [Obsolete("Use TryIsBoostActive and inspect ApiReadStatusData.")]
        public bool IsBoostActive(long gridId)
        {
            return LegacyValue(TryIsBoostActive(gridId), false);
        }

        [Obsolete("Use TryIsBoostActive and inspect ApiReadStatusData.")]
        public bool IsBoostActive(IMyCubeGrid grid)
        {
            return IsBoostActive(GetEntityId(grid));
        }

        [Obsolete("Use TryGetNoCoreConfig and inspect ApiReadStatusData.")]
        public ShipCoreData GetNoCoreConfig()
        {
            return LegacyValue(TryGetNoCoreConfig(), null);
        }

        [Obsolete("Use TryGetFullConfig and inspect ApiReadStatusData.")]
        public ModConfigData GetFullConfig()
        {
            return LegacyValue(TryGetFullConfig(), null);
        }

        [Obsolete("Use TryGetSpeedModifiers and inspect ApiReadStatusData.")]
        public SpeedModifiersData GetSpeedModifiers(long gridId)
        {
            return LegacyValue(TryGetSpeedModifiers(gridId), null);
        }

        [Obsolete("Use TryGetSpeedModifiers and inspect ApiReadStatusData.")]
        public SpeedModifiersData GetSpeedModifiers(IMyCubeGrid grid)
        {
            return GetSpeedModifiers(GetEntityId(grid));
        }

        [Obsolete("Use TryGetGridCore and inspect ApiReadStatusData.")]
        public string GetGridCoreSubtypeId(IMyCubeGrid grid)
        {
            ShipCoreData core = GetGridCore(grid);
            return core == null ? string.Empty : core.SubtypeId ?? string.Empty;
        }

        [Obsolete("Use TryGetBaseMaxSpeed and inspect ApiReadStatusData.")]
        public float GetBaseMaxSpeed(long gridId)
        {
            return LegacyValue(TryGetBaseMaxSpeed(gridId), 100f);
        }

        [Obsolete("Use TryGetBoostResistance and inspect ApiReadStatusData.")]
        public float GetBoostResistance(long gridId)
        {
            return LegacyValue(TryGetBoostResistance(gridId), 0f);
        }

        [Obsolete("Use TryGetBoostResistance and inspect ApiReadStatusData.")]
        public float GetBoostResistance(IMyCubeGrid grid)
        {
            return GetBoostResistance(GetEntityId(grid));
        }

        [Obsolete("Use TryGetBaseMaxSpeed and inspect ApiReadStatusData.")]
        public float GetBaseMaxSpeed(IMyCubeGrid grid)
        {
            return GetBaseMaxSpeed(GetEntityId(grid));
        }

        [Obsolete("Use TryGetMaxBoostMultiplier and inspect ApiReadStatusData.")]
        public float GetMaxBoostMultiplier(long gridId)
        {
            return LegacyValue(TryGetMaxBoostMultiplier(gridId), 0f);
        }

        [Obsolete("Use TryGetMaxBoostMultiplier and inspect ApiReadStatusData.")]
        public float GetMaxBoostMultiplier(IMyCubeGrid grid)
        {
            return GetMaxBoostMultiplier(GetEntityId(grid));
        }

        [Obsolete("Use TryGetBoostDuration and inspect ApiReadStatusData.")]
        public float GetBoostDuration(long gridId)
        {
            return LegacyValue(TryGetBoostDuration(gridId), 0f);
        }

        [Obsolete("Use TryGetBoostDuration and inspect ApiReadStatusData.")]
        public float GetBoostDuration(IMyCubeGrid grid)
        {
            return GetBoostDuration(GetEntityId(grid));
        }

        [Obsolete("Use TryGetBoostCooldown and inspect ApiReadStatusData.")]
        public float GetBoostCooldown(long gridId)
        {
            return LegacyValue(TryGetBoostCooldown(gridId), 0f);
        }

        [Obsolete("Use TryGetBoostCooldown and inspect ApiReadStatusData.")]
        public float GetBoostCooldown(IMyCubeGrid grid)
        {
            return GetBoostCooldown(GetEntityId(grid));
        }

        [Obsolete("Use TryGetFrictionEnabledForGroup and inspect ApiReadStatusData.")]
        public bool GetFrictionEnabledForGroup(long gridId)
        {
            return LegacyValue(TryGetFrictionEnabledForGroup(gridId), false);
        }

        [Obsolete("Use TryGetFrictionEnabledForGroup and inspect ApiReadStatusData.")]
        public bool GetFrictionEnabledForGroup(IMyCubeGrid grid)
        {
            return GetFrictionEnabledForGroup(GetEntityId(grid));
        }

        [Obsolete("Use TryGetFrictionMaximumDecelerationForGroup and inspect ApiReadStatusData.")]
        public float GetFrictionMaximumDecelerationForGroup(long gridId)
        {
            return LegacyValue(TryGetFrictionMaximumDecelerationForGroup(gridId), -1f);
        }

        [Obsolete("Use TryGetFrictionMaximumDecelerationForGroup and inspect ApiReadStatusData.")]
        public float GetFrictionMaximumDecelerationForGroup(IMyCubeGrid grid)
        {
            return GetFrictionMaximumDecelerationForGroup(GetEntityId(grid));
        }

        [Obsolete("Use TryGetFrictionSpeedValueMode and inspect ApiReadStatusData.")]
        public FrictionSpeedValueModeData GetFrictionSpeedValueMode()
        {
            ApiReadResult<int> result = TryGetFrictionSpeedValueMode();
            return result.Success
                ? (FrictionSpeedValueModeData)result.Value
                : FrictionSpeedValueModeData.Modifier;
        }

        [Obsolete("Use the ApiReadResult<float> overload and inspect ApiReadStatusData.")]
        public bool TryGetFrictionMinimumSpeedAbsoluteForGroup(long gridId, out float speed,
            out string error)
        {
            return LegacyTry(TryGetFrictionMinimumSpeedAbsoluteForGroup(gridId), out speed, out error);
        }

        [Obsolete("Use the ApiReadResult<float> overload and inspect ApiReadStatusData.")]
        public bool TryGetFrictionMinimumSpeedAbsoluteForGroup(IMyCubeGrid grid, out float speed,
            out string error)
        {
            return TryGetFrictionMinimumSpeedAbsoluteForGroup(GetEntityId(grid), out speed, out error);
        }

        [Obsolete("Use the ApiReadResult<float> overload and inspect ApiReadStatusData.")]
        public bool TryGetFrictionMaximumSpeedAbsoluteForGroup(long gridId, out float speed,
            out string error)
        {
            return LegacyTry(TryGetFrictionMaximumSpeedAbsoluteForGroup(gridId), out speed, out error);
        }

        [Obsolete("Use the ApiReadResult<float> overload and inspect ApiReadStatusData.")]
        public bool TryGetFrictionMaximumSpeedAbsoluteForGroup(IMyCubeGrid grid, out float speed,
            out string error)
        {
            return TryGetFrictionMaximumSpeedAbsoluteForGroup(GetEntityId(grid), out speed, out error);
        }

        [Obsolete("Use the ApiReadResult<float> overload and inspect ApiReadStatusData.")]
        public bool TryGetFrictionMinimumSpeedModifierForGroup(long gridId, out float modifier,
            out string error)
        {
            return LegacyTry(TryGetFrictionMinimumSpeedModifierForGroup(gridId), out modifier, out error);
        }

        [Obsolete("Use the ApiReadResult<float> overload and inspect ApiReadStatusData.")]
        public bool TryGetFrictionMinimumSpeedModifierForGroup(IMyCubeGrid grid, out float modifier,
            out string error)
        {
            return TryGetFrictionMinimumSpeedModifierForGroup(GetEntityId(grid), out modifier, out error);
        }

        [Obsolete("Use the ApiReadResult<float> overload and inspect ApiReadStatusData.")]
        public bool TryGetFrictionMaximumSpeedModifierForGroup(long gridId, out float modifier,
            out string error)
        {
            return LegacyTry(TryGetFrictionMaximumSpeedModifierForGroup(gridId), out modifier, out error);
        }

        [Obsolete("Use the ApiReadResult<float> overload and inspect ApiReadStatusData.")]
        public bool TryGetFrictionMaximumSpeedModifierForGroup(IMyCubeGrid grid, out float modifier,
            out string error)
        {
            return TryGetFrictionMaximumSpeedModifierForGroup(GetEntityId(grid), out modifier, out error);
        }

        [Obsolete("Use TryIsGroupDeactivated and inspect ApiReadStatusData.")]
        public bool IsGroupDeactivated(long gridId)
        {
            return LegacyValue(TryIsGroupDeactivated(gridId), false);
        }

        [Obsolete("Use TryIsGroupDeactivated and inspect ApiReadStatusData.")]
        public bool IsGroupDeactivated(IMyCubeGrid grid)
        {
            return IsGroupDeactivated(GetEntityId(grid));
        }

        protected ApiReadResult<bool> InvokeCommand(int methodId, object argument)
        {
            ApiReadStatusData status;
            object value;
            string error;
            if (!TryInvoke(methodId, argument, out status, out value, out error))
                return Result(status, false, error);
            if (!(value is MyTuple<bool, string>))
                return Result(ApiReadStatusData.Error, false, "Invalid command response.");
            MyTuple<bool, string> command = (MyTuple<bool, string>)value;
            return command.Item1
                ? Result(ApiReadStatusData.Success, true, string.Empty)
                : Result(ApiReadStatusData.Error, false, command.Item2);
        }

        private void OnApiPayloadReceived(object value)
        {
            try
            {
                MyTuple<int, int, Func<int, Func<object, object>>> payload =
                    (MyTuple<int, int, Func<int, Func<object, object>>>)value;
                ProviderApiVersion = payload.Item1;
                ProviderRole = (ApiProviderRoleData)payload.Item2;
                if (!ApiConstants.IsApiCompatible(ProviderApiVersion) || ProviderRole != _expectedRole)
                {
                    Reset();
                    return;
                }

                _factory = payload.Item3;
                ProviderReady = _factory != null;
                if (!ProviderReady) return;

                ApiReadResult<int> capabilities = InvokePrimitive<int>(ApiMethodId.GetCapabilities, null);
                if (capabilities.Success) Capabilities = (ApiCapabilityData)capabilities.Value;
                RefreshReadiness();
            }
            catch (Exception exception)
            {
                MyLog.Default.WriteLine("[SCF] API v4 payload failed: " + exception);
                Reset();
            }
        }

        private ApiReadResult<T> InvokePrimitive<T>(int methodId, object argument)
        {
            ApiReadStatusData status;
            object value;
            string error;
            if (!TryInvoke(methodId, argument, out status, out value, out error))
                return Result(status, default(T), error);
            if (!(value is T))
                return Result(ApiReadStatusData.Error, default(T), "Invalid primitive response.");
            return Result(ApiReadStatusData.Success, (T)value, string.Empty);
        }

        private ApiReadResult<T> InvokeBinary<T>(int methodId, object argument) where T : class
        {
            ApiReadStatusData status;
            object value;
            string error;
            if (!TryInvoke(methodId, argument, out status, out value, out error))
                return Result(status, default(T), error);

            byte[] bytes = value as byte[];
            if (bytes == null || bytes.Length == 0)
                return Result(ApiReadStatusData.Error, default(T), "Invalid serialized response.");
            try
            {
                T result = MyAPIGateway.Utilities.SerializeFromBinary<T>(bytes);
                return result == null
                    ? Result(ApiReadStatusData.Error, default(T), "Response deserialization failed.")
                    : Result(ApiReadStatusData.Success, result, string.Empty);
            }
            catch (Exception exception)
            {
                return Result(ApiReadStatusData.Error, default(T), exception.Message);
            }
        }

        private bool TryInvoke(int methodId, object argument, out ApiReadStatusData status,
            out object value, out string error)
        {
            value = null;
            error = string.Empty;
            if (!ProviderReady || _factory == null)
            {
                status = ApiReadStatusData.ProviderNotReady;
                return false;
            }

            try
            {
                Func<object, object> method = _factory(methodId);
                if (method == null)
                {
                    status = ApiReadStatusData.Unsupported;
                    return false;
                }

                object raw = method.Invoke(argument);
                if (!(raw is MyTuple<int, object>))
                {
                    status = ApiReadStatusData.Error;
                    error = "Invalid API v4 response envelope.";
                    return false;
                }

                MyTuple<int, object> response = (MyTuple<int, object>)raw;
                status = (ApiReadStatusData)response.Item1;
                value = response.Item2;
                if (status == ApiReadStatusData.Success) return true;
                error = status.ToString();
                return false;
            }
            catch (Exception exception)
            {
                status = ApiReadStatusData.Error;
                error = exception.Message;
                return false;
            }
        }

        private static ApiReadResult<T> Result<T>(ApiReadStatusData status, T value, string error)
        {
            return new ApiReadResult<T>
            {
                Status = status,
                Value = value,
                Error = error ?? string.Empty
            };
        }

        private static T LegacyValue<T>(ApiReadResult<T> result, T fallback)
        {
            return result != null && result.Success ? result.Value : fallback;
        }

        private static bool LegacyTry(ApiReadResult<float> result, out float value, out string error)
        {
            value = result != null && result.Success ? result.Value : -1f;
            error = result == null ? "Invalid response." : result.Error;
            return result != null && result.Success;
        }

        private void Reset()
        {
            _factory = null;
            ProviderReady = false;
            ConfigReady = false;
            RuntimeSnapshotReady = false;
            ProviderApiVersion = 0;
            ProviderRole = ApiProviderRoleData.Unknown;
            Capabilities = ApiCapabilityData.None;
        }

        protected static long GetEntityId(IMyCubeGrid grid)
        {
            return grid == null ? 0L : grid.EntityId;
        }

        private static IMyCubeGrid ResolveGrid(long gridId)
        {
            IMyEntity entity;
            return gridId != 0 && MyAPIGateway.Entities.TryGetEntityById(gridId, out entity)
                ? entity as IMyCubeGrid
                : null;
        }

        private static IMyGridGroupData ResolveLogicalGroup(IMyCubeGrid grid)
        {
            return grid == null
                ? null
                : MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
        }

        private void OnCoreActivated(object value)
        {
            if (!ProviderReady) return;
            CoreActivatedEventArgs eventData = Deserialize<CoreActivatedEventArgs>(value);
            if (eventData == null) return;
            if (CoreActivated != null) CoreActivated(eventData);
            if (CoreActivatedResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GroupGridId);
            CoreActivatedResolved(eventData, grid, ResolveLogicalGroup(grid));
        }

        private void OnCoreDeactivated(object value)
        {
            if (!ProviderReady) return;
            CoreDeactivatedEventArgs eventData = Deserialize<CoreDeactivatedEventArgs>(value);
            if (eventData == null) return;
            if (CoreDeactivated != null) CoreDeactivated(eventData);
            if (CoreDeactivatedResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GroupGridId);
            CoreDeactivatedResolved(eventData, grid, ResolveLogicalGroup(grid));
        }

        private void OnLimitsRecalculated(object value)
        {
            if (!ProviderReady) return;
            LimitsRecalculatedEventArgs eventData = Deserialize<LimitsRecalculatedEventArgs>(value);
            if (eventData == null) return;
            if (LimitsRecalculated != null) LimitsRecalculated(eventData);
            if (LimitsRecalculatedResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GroupGridId);
            LimitsRecalculatedResolved(eventData, grid, ResolveLogicalGroup(grid));
        }

        private void OnLimitsEnforced(object value)
        {
            if (!ProviderReady) return;
            LimitsEnforcedEventArgs eventData = Deserialize<LimitsEnforcedEventArgs>(value);
            if (eventData == null) return;
            if (LimitsEnforced != null) LimitsEnforced(eventData);
            if (LimitsEnforcedResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GroupGridId);
            LimitsEnforcedResolved(eventData, grid, ResolveLogicalGroup(grid));
        }

        private void OnBoostActivated(object value)
        {
            if (!ProviderReady) return;
            BoostEventArgs eventData = Deserialize<BoostEventArgs>(value);
            if (eventData == null) return;
            if (BoostActivated != null) BoostActivated(eventData);
            if (BoostActivatedResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GroupGridId);
            BoostActivatedResolved(eventData, grid, ResolveLogicalGroup(grid));
        }

        private void OnBoostDeactivated(object value)
        {
            if (!ProviderReady) return;
            BoostEventArgs eventData = Deserialize<BoostEventArgs>(value);
            if (eventData == null) return;
            if (BoostDeactivated != null) BoostDeactivated(eventData);
            if (BoostDeactivatedResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GroupGridId);
            BoostDeactivatedResolved(eventData, grid, ResolveLogicalGroup(grid));
        }

        private void OnActiveDefenseActivated(object value)
        {
            if (!ProviderReady) return;
            ActiveDefenseEventArgs eventData = Deserialize<ActiveDefenseEventArgs>(value);
            if (eventData == null) return;
            if (ActiveDefenseActivated != null) ActiveDefenseActivated(eventData);
            if (ActiveDefenseActivatedResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GroupGridId);
            ActiveDefenseActivatedResolved(eventData, grid, ResolveLogicalGroup(grid));
        }

        private void OnActiveDefenseDeactivated(object value)
        {
            if (!ProviderReady) return;
            ActiveDefenseEventArgs eventData = Deserialize<ActiveDefenseEventArgs>(value);
            if (eventData == null) return;
            if (ActiveDefenseDeactivated != null) ActiveDefenseDeactivated(eventData);
            if (ActiveDefenseDeactivatedResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GroupGridId);
            ActiveDefenseDeactivatedResolved(eventData, grid, ResolveLogicalGroup(grid));
        }

        private void OnGridAddedToGroup(object value)
        {
            if (!ProviderReady) return;
            GridGroupEventArgs eventData = Deserialize<GridGroupEventArgs>(value);
            if (eventData == null) return;
            if (GridAddedToGroup != null) GridAddedToGroup(eventData);
            if (GridAddedToGroupResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GridId);
            IMyCubeGrid groupGrid = ResolveGrid(eventData.GroupGridId);
            GridAddedToGroupResolved(eventData, grid, groupGrid,
                ResolveLogicalGroup(groupGrid ?? grid));
        }

        private void OnGridRemovedFromGroup(object value)
        {
            if (!ProviderReady) return;
            GridGroupEventArgs eventData = Deserialize<GridGroupEventArgs>(value);
            if (eventData == null) return;
            if (GridRemovedFromGroup != null) GridRemovedFromGroup(eventData);
            if (GridRemovedFromGroupResolved == null) return;
            IMyCubeGrid grid = ResolveGrid(eventData.GridId);
            IMyCubeGrid groupGrid = ResolveGrid(eventData.GroupGridId);
            GridRemovedFromGroupResolved(eventData, grid, groupGrid,
                ResolveLogicalGroup(groupGrid ?? grid));
        }

        private void OnConfigReceived(object value)
        {
            if (!ProviderReady) return;
            ConfigReceivedEventArgs eventData = Deserialize<ConfigReceivedEventArgs>(value);
            if (eventData == null) return;
            ConfigReady = true;
            RuntimeSnapshotReady = false;
            if (ConfigReceived != null) ConfigReceived(eventData);
        }

        private void OnRuntimeReady(object value)
        {
            if (!ProviderReady) return;
            RuntimeSnapshotReadyEventArgs eventData = Deserialize<RuntimeSnapshotReadyEventArgs>(value);
            if (eventData == null) return;
            RuntimeSnapshotReady = true;
            if (RuntimeReady != null) RuntimeReady(eventData);
        }

        private static T Deserialize<T>(object value) where T : class
        {
            try
            {
                byte[] bytes = value as byte[];
                return bytes == null || bytes.Length == 0
                    ? null
                    : MyAPIGateway.Utilities.SerializeFromBinary<T>(bytes);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Read-only API backed by synchronized replicas on remote clients and local authority on hosts.
    /// </summary>
    public class ShipCoreFrameworkClientApi : ShipCoreFrameworkApiBase
    {
        public ShipCoreFrameworkClientApi()
            : base(ApiConstants.CLIENT_REPLICA_API_ID, ApiProviderRoleData.ClientLocalReplica)
        {
        }
    }

    /// <summary>
    /// Authoritative API available only inside a server process.
    /// </summary>
    public sealed class ShipCoreFrameworkServerApi : ShipCoreFrameworkApiBase
    {
        public ShipCoreFrameworkServerApi()
            : base(ApiConstants.SERVER_LOCAL_API_ID, ApiProviderRoleData.ServerLocalAuthority)
        {
        }

        public ApiReadResult<bool> TrySetFrictionEnabledForGroup(long gridId, bool enabled)
        {
            return InvokeCommand(ApiMethodId.SetFrictionEnabledForGroup, MyTuple.Create(gridId, enabled));
        }

        public ApiReadResult<bool> TrySetFrictionMaximumDecelerationForGroup(long gridId, float deceleration)
        {
            return InvokeCommand(ApiMethodId.SetFrictionMaximumDecelerationForGroup,
                MyTuple.Create(gridId, deceleration));
        }

        public ApiReadResult<bool> TryClearFrictionMaximumDecelerationForGroup(long gridId)
        {
            return InvokeCommand(ApiMethodId.ClearFrictionMaximumDecelerationForGroup, gridId);
        }

        public ApiReadResult<bool> TrySetFrictionMinimumSpeedAbsoluteForGroup(long gridId, float speed)
        {
            return InvokeCommand(ApiMethodId.SetFrictionMinimumSpeedAbsoluteForGroup,
                MyTuple.Create(gridId, speed));
        }

        public ApiReadResult<bool> TrySetFrictionMaximumSpeedAbsoluteForGroup(long gridId, float speed)
        {
            return InvokeCommand(ApiMethodId.SetFrictionMaximumSpeedAbsoluteForGroup,
                MyTuple.Create(gridId, speed));
        }

        public ApiReadResult<bool> TrySetFrictionMinimumSpeedModifierForGroup(long gridId, float modifier)
        {
            return InvokeCommand(ApiMethodId.SetFrictionMinimumSpeedModifierForGroup,
                MyTuple.Create(gridId, modifier));
        }

        public ApiReadResult<bool> TrySetFrictionMaximumSpeedModifierForGroup(long gridId, float modifier)
        {
            return InvokeCommand(ApiMethodId.SetFrictionMaximumSpeedModifierForGroup,
                MyTuple.Create(gridId, modifier));
        }

        [Obsolete("Use TrySetFrictionEnabledForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionEnabledForGroup(long gridId, bool enabled)
        {
            return TrySetFrictionEnabledForGroup(gridId, enabled).Success;
        }

        [Obsolete("Use TrySetFrictionEnabledForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionEnabledForGroup(IMyCubeGrid grid, bool enabled)
        {
            return SetFrictionEnabledForGroup(GetEntityId(grid), enabled);
        }

        [Obsolete("Use TrySetFrictionMaximumDecelerationForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMaximumDecelerationForGroup(long gridId, float deceleration)
        {
            return TrySetFrictionMaximumDecelerationForGroup(gridId, deceleration).Success;
        }

        [Obsolete("Use TrySetFrictionMaximumDecelerationForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMaximumDecelerationForGroup(IMyCubeGrid grid, float deceleration)
        {
            return SetFrictionMaximumDecelerationForGroup(GetEntityId(grid), deceleration);
        }

        [Obsolete("Use TryClearFrictionMaximumDecelerationForGroup and inspect ApiReadStatusData.")]
        public bool ClearFrictionMaximumDecelerationForGroup(long gridId)
        {
            return TryClearFrictionMaximumDecelerationForGroup(gridId).Success;
        }

        [Obsolete("Use TryClearFrictionMaximumDecelerationForGroup and inspect ApiReadStatusData.")]
        public bool ClearFrictionMaximumDecelerationForGroup(IMyCubeGrid grid)
        {
            return ClearFrictionMaximumDecelerationForGroup(GetEntityId(grid));
        }

        [Obsolete("Use TrySetFrictionMinimumSpeedAbsoluteForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMinimumSpeedAbsoluteForGroup(long gridId, float speed, out string error)
        {
            return LegacyCommand(TrySetFrictionMinimumSpeedAbsoluteForGroup(gridId, speed), out error);
        }

        [Obsolete("Use TrySetFrictionMinimumSpeedAbsoluteForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMinimumSpeedAbsoluteForGroup(IMyCubeGrid grid, float speed, out string error)
        {
            return SetFrictionMinimumSpeedAbsoluteForGroup(GetEntityId(grid), speed, out error);
        }

        [Obsolete("Use TrySetFrictionMaximumSpeedAbsoluteForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMaximumSpeedAbsoluteForGroup(long gridId, float speed, out string error)
        {
            return LegacyCommand(TrySetFrictionMaximumSpeedAbsoluteForGroup(gridId, speed), out error);
        }

        [Obsolete("Use TrySetFrictionMaximumSpeedAbsoluteForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMaximumSpeedAbsoluteForGroup(IMyCubeGrid grid, float speed, out string error)
        {
            return SetFrictionMaximumSpeedAbsoluteForGroup(GetEntityId(grid), speed, out error);
        }

        [Obsolete("Use TrySetFrictionMinimumSpeedModifierForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMinimumSpeedModifierForGroup(long gridId, float modifier, out string error)
        {
            return LegacyCommand(TrySetFrictionMinimumSpeedModifierForGroup(gridId, modifier), out error);
        }

        [Obsolete("Use TrySetFrictionMinimumSpeedModifierForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMinimumSpeedModifierForGroup(IMyCubeGrid grid, float modifier, out string error)
        {
            return SetFrictionMinimumSpeedModifierForGroup(GetEntityId(grid), modifier, out error);
        }

        [Obsolete("Use TrySetFrictionMaximumSpeedModifierForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMaximumSpeedModifierForGroup(long gridId, float modifier, out string error)
        {
            return LegacyCommand(TrySetFrictionMaximumSpeedModifierForGroup(gridId, modifier), out error);
        }

        [Obsolete("Use TrySetFrictionMaximumSpeedModifierForGroup and inspect ApiReadStatusData.")]
        public bool SetFrictionMaximumSpeedModifierForGroup(IMyCubeGrid grid, float modifier, out string error)
        {
            return SetFrictionMaximumSpeedModifierForGroup(GetEntityId(grid), modifier, out error);
        }

        private static bool LegacyCommand(ApiReadResult<bool> result, out string error)
        {
            error = result == null ? "Invalid response." : result.Error;
            return result != null && result.Success && result.Value;
        }
    }

    /// <summary>
    /// Temporary v4 source-compatibility alias. It is intentionally read-only.
    /// </summary>
    [Obsolete("Use ShipCoreFrameworkClientApi for replicas or ShipCoreFrameworkServerApi for authority.")]
    public sealed class ShipCoreFrameworkClient : ShipCoreFrameworkClientApi
    {
    }
}
