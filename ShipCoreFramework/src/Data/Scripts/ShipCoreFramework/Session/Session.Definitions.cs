using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static readonly string[] AmmoDefinitionIds =
        {
            "Missile", "LargeCalibreShell", "MediumCalibreShell", "LargeCaliber", "AutocannonShell",
            "LargeRailgunSlug", "SmallRailgunSlug", "SmallCaliber", "PistolCaliber", "Shrapnel"
        };

        internal static void ApplyConfigToDefinitions()
        {
            if (MyDefinitionManager.Static?.EnvironmentDefinition != null)
            {
                MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed =
                    Config.MaxPossibleSpeedMetersPerSecond;
                MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed =
                    Config.MaxPossibleSpeedMetersPerSecond;
            }

            var newDifferential = Config.MaxPossibleSpeedMetersPerSecond - 100.0f;
            var delta = newDifferential - AppliedSpeedDifferential;
            if (Math.Abs(delta) < 0.001f)
                return;

            foreach (var ammoId in AmmoDefinitionIds)
            {
                try
                {
                    var ammoDefinition = MyDefinitionManager.Static?.GetAmmoDefinition(
                        new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                    if (ammoDefinition != null)
                        ammoDefinition.DesiredSpeed += delta;
                }
                catch
                {
                    // Ignore missing ammo definitions.
                }
            }

            AppliedSpeedDifferential = newDifferential;
        }

        private static void RevertAmmoSpeedAdjustments()
        {
            var delta = -AppliedSpeedDifferential;
            if (Math.Abs(delta) < 0.001f)
                return;

            foreach (var ammoId in AmmoDefinitionIds)
            {
                try
                {
                    var ammoDefinition = MyDefinitionManager.Static.GetAmmoDefinition(
                        new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), ammoId));
                    if (ammoDefinition != null)
                        ammoDefinition.DesiredSpeed += delta;
                }
                catch
                {
                    // Ignore missing ammo definitions.
                }
            }

            AppliedSpeedDifferential = 0f;
        }

        internal static void RefreshGroupsAfterConfigChanged()
        {
            var groups = new List<GroupComponent>(GroupDict.Values);
            foreach (var group in groups)
            {
                if (group == null) continue;
                group.OnConfigChanged();
            }
        }
    }
}
