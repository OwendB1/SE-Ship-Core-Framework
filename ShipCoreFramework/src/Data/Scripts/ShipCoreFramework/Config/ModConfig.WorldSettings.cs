using System.Collections.Generic;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        internal void ApplyWorldSettingsFrom(ModConfig import)
        {
            EnsurePersistedWorldSettings();
            DebugMode = import.DebugMode;
            CombatLogging = import.CombatLogging;
            LogLevel = import.LogLevel;
            ClientOutputLogLevel = import.ClientOutputLogLevel;
            IgnoreAiFactions = import.IgnoreAiFactions;
            IgnoredFactionTags = import.IgnoredFactionTags != null
                ? new List<string>(import.IgnoredFactionTags)
                : new List<string>(DefaultIgnoredFactionTagValues);
            SelectedNoCoreUniqueName = import.SelectedNoCoreUniqueName ?? string.Empty;

            if (import.MaxPossibleSpeedMetersPerSecond <= 0 || import.MaxPossibleSpeedMetersPerSecond > 10000)
            {
                Utils.Log("MaxPossibleSpeedMetersPerSecond validation failed - using default 300", 0,
                    "Config Validation");
                MaxPossibleSpeedMetersPerSecond = 300;
            }
            else
            {
                MaxPossibleSpeedMetersPerSecond = import.MaxPossibleSpeedMetersPerSecond;
            }

            MassTypeMode = import.MassTypeMode;
            FrictionSpeedValueMode = import.FrictionSpeedValueMode;
            BlockDirectionalPlacementOnSubgrids = import.BlockDirectionalPlacementOnSubgrids;
            AllowUnattachedUpgradeModules = import.AllowUnattachedUpgradeModules;
            NoCoreGraceSeconds = ClampTimerSeconds(import.NoCoreGraceSeconds, 30, "NoCoreGraceSeconds");
            MinimumBlocksGraceSeconds = ClampTimerSeconds(import.MinimumBlocksGraceSeconds, 30,
                "MinimumBlocksGraceSeconds");
            NoFlyZones = import.NoFlyZones ?? new List<Zones>();
        }

        private static int ClampTimerSeconds(int value, int fallback, string settingName)
        {
            if (value < 0)
            {
                Utils.Log(settingName + " validation failed - using default " + fallback, 1,
                    "Config Validation");
                return fallback;
            }

            const int maxTimerSeconds = 60 * 60;
            if (value > maxTimerSeconds)
            {
                Utils.Log(settingName + " was above " + maxTimerSeconds + " seconds; clamping.", 1,
                    "Config Validation");
                return maxTimerSeconds;
            }

            return value;
        }
    }
}
