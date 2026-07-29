using System;
using Sandbox.ModAPI;
using VRage;

namespace ShipCoreFramework
{
    public static partial class ModAPI
    {
        private static bool _configReady;
        private static bool _runtimeSnapshotReady;

        internal static void ResetReadiness()
        {
            _configReady = false;
            _runtimeSnapshotReady = false;
        }

        internal static void MarkConfigReady(bool runtimeSnapshotRequired = false)
        {
            _configReady = true;
            if (runtimeSnapshotRequired)
                _runtimeSnapshotReady = false;
        }

        internal static void MarkRuntimeSnapshotReady(int sequence = 0, int snapshotRevision = 0)
        {
            if (_runtimeSnapshotReady) return;
            _runtimeSnapshotReady = true;

            if (!_isInitialized) return;
            try
            {
                RuntimeSnapshotReadyEventArgs eventData = new RuntimeSnapshotReadyEventArgs
                {
                    Sequence = sequence,
                    SnapshotRevision = snapshotRevision,
                    Timestamp = DateTime.UtcNow
                };
                byte[] payload = MyAPIGateway.Utilities.SerializeToBinary(eventData);
                MyAPIGateway.Utilities.SendModMessage(ApiConstants.EVENT_RUNTIME_SNAPSHOT_READY, payload);
            }
            catch (Exception exception)
            {
                Utils.Log("ModAPI v4 runtime-ready event failed: " + exception, 3);
            }
        }

        private static ApiReadinessData GetReadiness(ApiProviderRoleData role)
        {
            return new ApiReadinessData
            {
                Role = role,
                ProviderReady = _isInitialized,
                ConfigReady = _configReady,
                RuntimeSnapshotReady = _runtimeSnapshotReady
            };
        }

        private static object Success(object value)
        {
            return MyTuple.Create((int)ApiReadStatusData.Success, value);
        }

        private static object Failure(ApiReadStatusData status)
        {
            return MyTuple.Create((int)status, (object)null);
        }

        private static object SerializedSuccess<T>(T value)
        {
            return Success(MyAPIGateway.Utilities.SerializeToBinary(value));
        }

        private static bool TryGetLong(object argument, out long value)
        {
            if (argument is long)
            {
                value = (long)argument;
                return value != 0;
            }

            value = 0;
            return false;
        }

        private static object CheckConfigReady()
        {
            if (!_isInitialized) return Failure(ApiReadStatusData.ProviderNotReady);
            return _configReady ? null : Failure(ApiReadStatusData.ConfigPending);
        }

        private static object CheckRuntimeReady()
        {
            object configFailure = CheckConfigReady();
            if (configFailure != null) return configFailure;
            return _runtimeSnapshotReady ? null : Failure(ApiReadStatusData.RuntimePending);
        }
    }
}
