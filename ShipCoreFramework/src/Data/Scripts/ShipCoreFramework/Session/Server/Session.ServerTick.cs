using System.Threading;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private void RunServerSimulationTick()
        {
            GroupComponent.RunPendingMergeValidationTick();

            foreach (var kvp in GroupDict)
            {
                GroupComponent group = kvp.Value;
                if (group != null)
                {
                    group.RefreshGameThreadStateCache();
                    group.RunMissingCoreRescanTick();
                    group.RunBlockTransferReconcileTick();
                }
            }

            RefreshMassCacheBatch();
            LimitsNexusSync.RunPeriodicSnapshotTick();
            RunRuntimeStateSyncTick();
            bool runNfz = _tick % 10 == 0;
            bool doPunish = _tick % 60 == 0;

            if (doPunish)
            {
                foreach (var kvp in GroupDict)
                {
                    GroupComponent group = kvp.Value;
                    if (group != null)
                    {
                        group.RefreshPunishmentState();
                        group.EnforceConnectorLimitPunishment();
                    }
                }
            }

            if (Interlocked.CompareExchange(ref _serverSimulationBatchRunning, 1, 0) != 0) return;

            try
            {
                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    try
                    {
                        SpeedEnforcement.EnforcementBatch speedBatch = SpeedEnforcement.CreateBatch();
                        MyAPIGateway.Parallel.ForEach(GroupDict, kvp =>
                        {
                            kvp.Value.UpdateDeactivationState();
                            kvp.Value.RunBoostTimerTick();
                            kvp.Value.RunActiveDefenseTimerTick();
                            kvp.Value.RunPowerOverclockTimerTick();
                            kvp.Value.RunLimitedBlockPunishmentTick();
                            kvp.Value.RunExternalLimitValidationTick();
                            SpeedEnforcement.EnforceSpeedLimit(kvp.Value, speedBatch);
                            if (runNfz) NoFlyZoneEnforcement.EnforceNoFlyZones(kvp.Value, doPunish);
                        });

                        SpeedEnforcement.DispatchBatch(speedBatch);
                    }
                    catch (System.Exception exception)
                    {
                        Utils.Log("RunServerSimulationTick: background batch failed: " + exception, 2);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _serverSimulationBatchRunning, 0);
                    }
                });
            }
            catch (System.Exception exception)
            {
                Interlocked.Exchange(ref _serverSimulationBatchRunning, 0);
                Utils.Log("RunServerSimulationTick: failed to schedule background batch: " + exception, 2);
            }
        }
    }
}
