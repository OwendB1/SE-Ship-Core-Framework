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

        internal void SetFrictionMaximumDecelerationOverride(float value)
        {
            lock (SpeedStateLock)
            {
                FrictionMaximumDecelerationOverride = value;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
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

        internal void SetMaximumFrictionSpeedAbsoluteOverride(float value)
        {
            lock (SpeedStateLock)
            {
                MaximumFrictionSpeedAbsoluteOverride = value;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
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

        internal void SetMaximumFrictionSpeedModifierOverride(float value)
        {
            lock (SpeedStateLock)
            {
                MaximumFrictionSpeedModifierOverride = value;
                InvalidateSpeedStateCache();
                Session.MarkRuntimeStateDirty(this);
            }
        }
    }
}
