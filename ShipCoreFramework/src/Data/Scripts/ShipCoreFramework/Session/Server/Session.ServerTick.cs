using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private void RunServerSimulationTick()
        {
            foreach (var kvp in GroupDict)
            {
                GroupComponent group = kvp.Value;
                if (group != null)
                {
                    group.RefreshGameThreadStateCache();
                    group.RunMissingCoreRescanTick();
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
                        group.RefreshPunishmentState();
                }
            }

            MyAPIGateway.Parallel.StartBackground(() =>
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
            });
        }
    }
}
