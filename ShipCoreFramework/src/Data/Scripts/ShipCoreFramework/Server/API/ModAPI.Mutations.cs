using System;
using VRage;

namespace ShipCoreFramework
{
    public static partial class ModAPI
    {
        /// <summary>
        /// Enables/disables friction-based speed limiting for a logical grid group.
        /// Friction cores are enabled by default; this is a runtime override.
        /// </summary>
        public static bool SetFrictionEnabledForGroup(long gridId, bool enabled)
        {
            if (!Session.IsServer) return false;

            try
            {
                GroupComponent groupComponent;
                if (!TryGetGroupComponent(gridId, out groupComponent)) return false;

                groupComponent.SetFrictionEnforcementEnabled(enabled);
                return true;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.SetFrictionEnabledForGroup: Exception - {ex}");
                return false;
            }
        }

        /// <summary>
        /// Sets the maximum friction deceleration override (m/s^2) for a logical grid group.
        /// </summary>
        public static bool SetFrictionMaximumDecelerationForGroup(long gridId, float deceleration)
        {
            if (!Session.IsServer) return false;
            if (deceleration < 0f) return false;

            try
            {
                GroupComponent groupComponent;
                if (!TryGetGroupComponent(gridId, out groupComponent)) return false;

                groupComponent.SetFrictionMaximumDecelerationOverride(deceleration);
                return true;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.SetFrictionMaximumDecelerationForGroup: Exception - {ex}");
                return false;
            }
        }

        /// <summary>
        /// Clears the maximum friction deceleration override for a logical grid group.
        /// </summary>
        public static bool ClearFrictionMaximumDecelerationForGroup(long gridId)
        {
            if (!Session.IsServer) return false;

            try
            {
                GroupComponent groupComponent;
                if (!TryGetGroupComponent(gridId, out groupComponent)) return false;

                groupComponent.SetFrictionMaximumDecelerationOverride(-1f);
                return true;
            }
            catch (Exception ex)
            {
                Utils.Log($"ModAPI.ClearFrictionMaximumDecelerationForGroup: Exception - {ex}");
                return false;
            }
        }

        public static MyTuple<bool, string> SetFrictionMinimumSpeedAbsoluteForGroup(long gridId, float speedMetersPerSecond)
        {
            if (!Session.IsServer)
                return MyTuple.Create(false, "Runtime friction overrides are server-authoritative.");

            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Absolute)
                return MyTuple.Create(false, "World config uses modifier-based friction speeds; use SetFrictionMinimumSpeedModifierForGroup.");

            GroupComponent groupComponent;
            if (!TryGetGroupComponent(gridId, out groupComponent))
                return MyTuple.Create(false, "Could not resolve logical grid group for the provided grid.");

            if (speedMetersPerSecond < 0f)
            {
                groupComponent.SetMinimumFrictionSpeedAbsoluteOverride(-1f);
                return MyTuple.Create(true, string.Empty);
            }

            groupComponent.SetMinimumFrictionSpeedAbsoluteOverride(speedMetersPerSecond);
            return MyTuple.Create(true, string.Empty);
        }

        public static MyTuple<bool, string> SetFrictionMaximumSpeedAbsoluteForGroup(long gridId, float speedMetersPerSecond)
        {
            if (!Session.IsServer)
                return MyTuple.Create(false, "Runtime friction overrides are server-authoritative.");

            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Absolute)
                return MyTuple.Create(false, "World config uses modifier-based friction speeds; use SetFrictionMaximumSpeedModifierForGroup.");

            GroupComponent groupComponent;
            if (!TryGetGroupComponent(gridId, out groupComponent))
                return MyTuple.Create(false, "Could not resolve logical grid group for the provided grid.");

            if (speedMetersPerSecond < 0f)
            {
                groupComponent.SetMaximumFrictionSpeedAbsoluteOverride(-1f);
                return MyTuple.Create(true, string.Empty);
            }

            groupComponent.SetMaximumFrictionSpeedAbsoluteOverride(speedMetersPerSecond);
            return MyTuple.Create(true, string.Empty);
        }

        public static MyTuple<bool, string> SetFrictionMinimumSpeedModifierForGroup(long gridId, float modifier)
        {
            if (!Session.IsServer)
                return MyTuple.Create(false, "Runtime friction overrides are server-authoritative.");

            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Modifier)
                return MyTuple.Create(false, "World config uses absolute friction speeds; use SetFrictionMinimumSpeedAbsoluteForGroup.");

            GroupComponent groupComponent;
            if (!TryGetGroupComponent(gridId, out groupComponent))
                return MyTuple.Create(false, "Could not resolve logical grid group for the provided grid.");

            if (modifier < 0f)
            {
                groupComponent.SetMinimumFrictionSpeedModifierOverride(-1f);
                return MyTuple.Create(true, string.Empty);
            }

            groupComponent.SetMinimumFrictionSpeedModifierOverride(modifier);
            return MyTuple.Create(true, string.Empty);
        }

        public static MyTuple<bool, string> SetFrictionMaximumSpeedModifierForGroup(long gridId, float modifier)
        {
            if (!Session.IsServer)
                return MyTuple.Create(false, "Runtime friction overrides are server-authoritative.");

            if (Session.Config.FrictionSpeedValueMode != FrictionSpeedValueMode.Modifier)
                return MyTuple.Create(false, "World config uses absolute friction speeds; use SetFrictionMaximumSpeedAbsoluteForGroup.");

            GroupComponent groupComponent;
            if (!TryGetGroupComponent(gridId, out groupComponent))
                return MyTuple.Create(false, "Could not resolve logical grid group for the provided grid.");

            if (modifier < 0f)
            {
                groupComponent.SetMaximumFrictionSpeedModifierOverride(-1f);
                return MyTuple.Create(true, string.Empty);
            }

            groupComponent.SetMaximumFrictionSpeedModifierOverride(modifier);
            return MyTuple.Create(true, string.Empty);
        }
    }
}

