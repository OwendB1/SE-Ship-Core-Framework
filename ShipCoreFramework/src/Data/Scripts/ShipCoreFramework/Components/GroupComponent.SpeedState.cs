using System.Threading;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void InvalidateSpeedStateCache()
        {
            Volatile.Write(ref LastSpeedStateUpdateTick, -1);
        }

        internal void SetFrictionEnforcementEnabled(bool enabled)
        {
            lock (SpeedStateLock)
            {
                FrictionEnforcementEnabled = enabled;
                InvalidateSpeedStateCache();
            }
        }

        internal bool GetFrictionEnforcementEnabled()
        {
            lock (SpeedStateLock)
            {
                return FrictionEnforcementEnabled;
            }
        }

        internal void SetFrictionMaximumDecelerationOverride(float value)
        {
            lock (SpeedStateLock)
            {
                FrictionMaximumDecelerationOverride = value;
                InvalidateSpeedStateCache();
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
