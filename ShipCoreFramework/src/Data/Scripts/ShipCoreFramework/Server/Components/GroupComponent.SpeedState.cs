namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void InvalidateSpeedStateCache()
        {
            LastSpeedStateUpdateTick = -1;
        }

        private void BeginSpeedRampDown()
        {
            lock (SpeedStateLock)
            {
                float worldSpeedLimit = Session.Config.MaxPossibleSpeedMetersPerSecond;
                float currentCap = EffectiveSpeedLimitMetersPerSecond;
                if (float.IsNaN(currentCap) || float.IsInfinity(currentCap) || currentCap < 0f)
                    currentCap = worldSpeedLimit;
                if (currentCap > worldSpeedLimit)
                    currentCap = worldSpeedLimit;

                SpeedRampDownActive = Session.Config.SpeedRampDownPercentage > 0f;
                SpeedRampDownCap = currentCap;
                SpeedRampDownTargetCap = -1f;
                SpeedRampDownLastTick = Session.CurrentTick;
                PostBoostRampActive = false;
                PostBoostRampCap = -1f;
                InvalidateSpeedStateCache();
            }
        }

        private void ClearSpeedRampDown()
        {
            lock (SpeedStateLock)
            {
                SpeedRampDownActive = false;
                SpeedRampDownCap = -1f;
                SpeedRampDownTargetCap = -1f;
                SpeedRampDownLastTick = -1;
            }
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
