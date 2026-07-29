using System;
using System.Collections.Generic;
using VRage;

namespace ShipCoreFramework
{
    public static partial class ModAPI
    {
        private static Func<object, object> ClientMethodFactory(int methodId)
        {
            Func<object, object> clientRead = ClientReplicaMethodFactory(methodId);
            if (!Session.IsServer || clientRead == null ||
                methodId == ApiMethodId.GetCapabilities ||
                methodId == ApiMethodId.GetReadiness_Binary)
                return clientRead;
            return ServerMethodFactory(methodId);
        }

        private static Func<object, object> ClientReplicaMethodFactory(int methodId)
        {
            switch (methodId)
            {
                case ApiMethodId.GetApiVersion:
                    return ignored => Success(ApiConstants.API_VERSION);
                case ApiMethodId.GetCapabilities:
                    return ignored => Success((int)(ApiCapabilityData.ConfigQueries |
                                                     ApiCapabilityData.RuntimeQueries |
                                                     (Session.IsServer
                                                         ? ApiCapabilityData.Authoritative
                                                         : ApiCapabilityData.Replicated) |
                                                     ApiCapabilityData.BestEffortPlacementChecks));
                case ApiMethodId.GetReadiness_Binary:
                    return ignored => SerializedSuccess(
                        GetReadiness(ApiProviderRoleData.ClientLocalReplica));
                case ApiMethodId.GetRuntimeStateAvailability:
                    return ClientRuntimeStateAvailability;
                case ApiMethodId.GetGridCore_Binary:
                    return argument => ReplicaRuntime(argument, state =>
                    {
                        ShipCore core = Session.Config.GetShipCoreByTypeId(state.CoreSubtypeId ?? string.Empty);
                        return SerializedSuccess(ConvertToShipCoreData(core, state.Deactivated));
                    });
                case ApiMethodId.GetCoreBySubtypeId_Binary:
                    return ClientGetCoreBySubtypeId;
                case ApiMethodId.GetAllCoreConfigs_Binary:
                    return ignored => ClientConfigResult(() => SerializedSuccess(GetAllCoreConfigs()));
                case ApiMethodId.GetBlockLimitsStatus_Binary:
                    return argument => ReplicaRuntime(argument,
                        state => SerializedSuccess(GetReplicaLimitStatus(state)));
                case ApiMethodId.IsBlockAllowed:
                    return ClientIsBlockAllowed;
                case ApiMethodId.GetGridModifiers_Binary:
                    return argument => ReplicaRuntime(argument,
                        state => SerializedSuccess(state.Modifiers ?? ConvertToGridModifiersData(null)));
                case ApiMethodId.GetMaxSpeed:
                    return argument => ReplicaRuntime(argument, state => Success(state.EffectiveSpeed));
                case ApiMethodId.IsBoostActive:
                    return argument => ReplicaRuntime(argument, state => Success(state.EffectiveBoostActive));
                case ApiMethodId.GetNoCoreConfig_Binary:
                    return ignored => ClientConfigResult(() => SerializedSuccess(GetNoCoreConfig()));
                case ApiMethodId.GetSpeedModifiers_Binary:
                    return argument => ReplicaRuntime(argument,
                        state => SerializedSuccess(state.SpeedModifiers ?? ConvertToSpeedModifiersData(null)));
                case ApiMethodId.GetBoostResistance:
                    return argument => ReplicaRuntime(argument,
                        state => Success(state.SpeedModifiers == null ? 0f : state.SpeedModifiers.BoostResistance));
                case ApiMethodId.GetBaseMaxSpeed:
                    return argument => ReplicaRuntime(argument, state => Success(state.BaseSpeed));
                case ApiMethodId.GetMaxBoostMultiplier:
                    return argument => ReplicaRuntime(argument,
                        state => Success(state.SpeedModifiers == null ? 0f : state.SpeedModifiers.MaxBoost));
                case ApiMethodId.GetBoostDuration:
                    return argument => ReplicaRuntime(argument,
                        state => Success(state.SpeedModifiers == null ? 0f : state.SpeedModifiers.BoostDuration));
                case ApiMethodId.GetBoostCooldown:
                    return argument => ReplicaRuntime(argument,
                        state => Success(state.SpeedModifiers == null ? 0f : state.SpeedModifiers.BoostCoolDown));
                case ApiMethodId.GetFrictionEnabledForGroup:
                    return argument => ReplicaRuntime(argument, state => Success(state.FrictionEnabled));
                case ApiMethodId.GetFrictionMaximumDecelerationForGroup:
                    return argument => ReplicaRuntime(argument,
                        state => Success(state.FrictionMaximumDecelerationOverride));
                case ApiMethodId.GetFrictionSpeedValueMode:
                    return ignored => ClientConfigResult(
                        () => Success((int)Session.Config.FrictionSpeedValueMode));
                case ApiMethodId.GetFrictionMinimumSpeedAbsoluteForGroup:
                    return argument => ClientGetFrictionValue(argument, FrictionSpeedValueMode.Absolute,
                        state => state.MinimumFrictionSpeedAbsoluteOverride);
                case ApiMethodId.GetFrictionMaximumSpeedAbsoluteForGroup:
                    return argument => ClientGetFrictionValue(argument, FrictionSpeedValueMode.Absolute,
                        state => state.MaximumFrictionSpeedAbsoluteOverride);
                case ApiMethodId.GetFrictionMinimumSpeedModifierForGroup:
                    return argument => ClientGetFrictionValue(argument, FrictionSpeedValueMode.Modifier,
                        state => state.MinimumFrictionSpeedModifierOverride);
                case ApiMethodId.GetFrictionMaximumSpeedModifierForGroup:
                    return argument => ClientGetFrictionValue(argument, FrictionSpeedValueMode.Modifier,
                        state => state.MaximumFrictionSpeedModifierOverride);
                case ApiMethodId.IsGroupDeactivated:
                    return argument => ReplicaRuntime(argument, state => Success(state.Deactivated));
                case ApiMethodId.GetFullConfig_Binary:
                    return ignored => ClientConfigResult(() => SerializedSuccess(GetFullConfig()));
                default:
                    return null;
            }
        }

        private static int GetReplicatedManifestGroupCount(string name)
        {
            int count;
            return RuntimeStateStore.TryGetManifestCount(name, out count) ? count : 0;
        }

        private static object ClientConfigResult(Func<object> action)
        {
            object failure = CheckConfigReady();
            return failure ?? action();
        }

        private static object ReplicaRuntime(object argument, Func<GroupRuntimeState, object> action)
        {
            object failure = CheckRuntimeReady();
            if (failure != null) return failure;

            long gridId;
            if (!TryGetLong(argument, out gridId)) return Failure(ApiReadStatusData.InvalidArgument);

            GroupRuntimeState state;
            if (!RuntimeStateStore.TryGetByGrid(gridId, out state) || state == null || state.Removed)
                return Failure(ApiReadStatusData.GridNotReplicated);

            try
            {
                return action(state);
            }
            catch (Exception exception)
            {
                Utils.Log("ModAPI v4 client replica query failed: " + exception, 3);
                return Failure(ApiReadStatusData.Error);
            }
        }

        private static object ClientRuntimeStateAvailability(object argument)
        {
            object failure = CheckRuntimeReady();
            if (failure != null) return failure;

            long gridId;
            if (!TryGetLong(argument, out gridId)) return Failure(ApiReadStatusData.InvalidArgument);

            GroupRuntimeState state;
            return Success(RuntimeStateStore.TryGetByGrid(gridId, out state) &&
                           state != null && !state.Removed);
        }

        private static object ClientGetCoreBySubtypeId(object argument)
        {
            object failure = CheckConfigReady();
            if (failure != null) return failure;
            string subtypeId = argument as string;
            if (string.IsNullOrWhiteSpace(subtypeId)) return Failure(ApiReadStatusData.InvalidArgument);
            return SerializedSuccess(GetCoreBySubtypeId(subtypeId));
        }

        private static Dictionary<string, LimitStatusData> GetReplicaLimitStatus(GroupRuntimeState state)
        {
            Dictionary<string, LimitStatusData> result =
                new Dictionary<string, LimitStatusData>(StringComparer.OrdinalIgnoreCase);
            RuntimeLimitState[] limits = state.Limits ?? Array.Empty<RuntimeLimitState>();
            for (int index = 0; index < limits.Length; index++)
            {
                RuntimeLimitState limit = limits[index];
                if (limit == null || string.IsNullOrWhiteSpace(limit.Name)) continue;
                result[limit.Name] = new LimitStatusData
                {
                    Name = limit.Name,
                    Current = limit.CurrentCount,
                    Max = limit.MaxCount,
                    IsOverLimit = limit.CurrentCount > limit.MaxCount
                };
            }

            return result;
        }

        private static object ClientIsBlockAllowed(object argument)
        {
            if (!(argument is MyTuple<long, string, string, int>))
                return Failure(ApiReadStatusData.InvalidArgument);

            MyTuple<long, string, string, int> values =
                (MyTuple<long, string, string, int>)argument;
            if (string.IsNullOrWhiteSpace(values.Item2) || values.Item4 < 1)
                return Failure(ApiReadStatusData.InvalidArgument);

            return ReplicaRuntime(values.Item1, state =>
            {
                ShipCore core = Session.Config.GetShipCoreByTypeId(state.CoreSubtypeId ?? string.Empty);
                BlockLimit[] configuredLimits = core == null
                    ? Array.Empty<BlockLimit>()
                    : core.BlockLimits ?? Array.Empty<BlockLimit>();
                RuntimeLimitState[] runtimeLimits = state.Limits ?? Array.Empty<RuntimeLimitState>();
                BlockKey blockKey = new BlockKey(values.Item2, values.Item3 ?? string.Empty);

                for (int configuredIndex = 0; configuredIndex < configuredLimits.Length; configuredIndex++)
                {
                    BlockLimit configured = configuredLimits[configuredIndex];
                    if (configured == null) continue;
                    double weight = configured.GetWeight(blockKey);
                    if (weight <= 0d) continue;

                    for (int runtimeIndex = 0; runtimeIndex < runtimeLimits.Length; runtimeIndex++)
                    {
                        RuntimeLimitState runtime = runtimeLimits[runtimeIndex];
                        if (runtime == null ||
                            !string.Equals(configured.Name, runtime.Name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (runtime.CurrentCount + weight * values.Item4 > runtime.MaxCount)
                            return Success(false);
                        break;
                    }
                }

                return Success(true);
            });
        }

        private static object ClientGetFrictionValue(object argument, FrictionSpeedValueMode requiredMode,
            Func<GroupRuntimeState, float> selector)
        {
            object configFailure = CheckConfigReady();
            if (configFailure != null) return configFailure;
            if (Session.Config.FrictionSpeedValueMode != requiredMode)
                return Failure(ApiReadStatusData.InvalidArgument);
            return ReplicaRuntime(argument, state => Success(selector(state)));
        }
    }
}
