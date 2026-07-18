namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void InvalidateSpeedStateCache()
        {
            LastSpeedStateUpdateTick = -1;
        }

        internal void SetFrictionEnforcementEnabled(bool enabled)
        {
            lock (SpeedStateLock)
            {
                FrictionEnforcementEnabled = enabled;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
            }
        }

        internal bool GetFrictionEnforcementEnabled()
        {
            lock (SpeedStateLock)
            {
                var shipCore = ShipCore;
                return shipCore != null && shipCore.SpeedLimitType == SpeedLimitType.Friction && FrictionEnforcementEnabled;
            }
        }

        internal void SetFrictionMaximumDecelerationOverride(float value)
        {
            lock (SpeedStateLock)
            {
                FrictionMaximumDecelerationOverride = value;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
            }
        }

        internal float GetFrictionMaximumDecelerationOverride()
        {
            lock (SpeedStateLock)
            {
                return FrictionMaximumDecelerationOverride;
            }
        }

        internal void SetMinimumFrictionSpeedAbsoluteOverride(float value)
        {
            lock (SpeedStateLock)
            {
                MinimumFrictionSpeedAbsoluteOverride = value;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
            }
        }

        internal float GetMinimumFrictionSpeedAbsoluteOverride()
        {
            lock (SpeedStateLock)
            {
                return MinimumFrictionSpeedAbsoluteOverride;
            }
        }

        internal void SetMaximumFrictionSpeedAbsoluteOverride(float value)
        {
            lock (SpeedStateLock)
            {
                MaximumFrictionSpeedAbsoluteOverride = value;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
            }
        }

        internal float GetMaximumFrictionSpeedAbsoluteOverride()
        {
            lock (SpeedStateLock)
            {
                return MaximumFrictionSpeedAbsoluteOverride;
            }
        }

        internal void SetMinimumFrictionSpeedModifierOverride(float value)
        {
            lock (SpeedStateLock)
            {
                MinimumFrictionSpeedModifierOverride = value;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
            }
        }

        internal float GetMinimumFrictionSpeedModifierOverride()
        {
            lock (SpeedStateLock)
            {
                return MinimumFrictionSpeedModifierOverride;
            }
        }

        internal void SetMaximumFrictionSpeedModifierOverride(float value)
        {
            lock (SpeedStateLock)
            {
                MaximumFrictionSpeedModifierOverride = value;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
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
