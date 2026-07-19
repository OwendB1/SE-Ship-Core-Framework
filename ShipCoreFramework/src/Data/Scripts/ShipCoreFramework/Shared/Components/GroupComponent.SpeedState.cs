namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal bool GetFrictionEnforcementEnabled()
        {
            lock (SpeedStateLock)
            {
                var shipCore = ShipCore;
                return shipCore != null && shipCore.SpeedLimitType == SpeedLimitType.Friction && FrictionEnforcementEnabled;
            }
        }

        internal float GetFrictionMaximumDecelerationOverride()
        {
            lock (SpeedStateLock)
            {
                return FrictionMaximumDecelerationOverride;
            }
        }

        internal float GetMinimumFrictionSpeedAbsoluteOverride()
        {
            lock (SpeedStateLock)
            {
                return MinimumFrictionSpeedAbsoluteOverride;
            }
        }

        internal float GetMaximumFrictionSpeedAbsoluteOverride()
        {
            lock (SpeedStateLock)
            {
                return MaximumFrictionSpeedAbsoluteOverride;
            }
        }

        internal float GetMinimumFrictionSpeedModifierOverride()
        {
            lock (SpeedStateLock)
            {
                return MinimumFrictionSpeedModifierOverride;
            }
        }

        internal float GetMaximumFrictionSpeedModifierOverride()
        {
            lock (SpeedStateLock)
            {
                return MaximumFrictionSpeedModifierOverride;
            }
        }
    }
}
