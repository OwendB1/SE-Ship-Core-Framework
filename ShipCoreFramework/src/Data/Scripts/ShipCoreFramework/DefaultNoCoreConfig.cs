namespace ShipCoreFramework
{
    public static class DefaultNoCoreConfig
    {
        // If a property is not defined here, the default value is used.
        public static readonly ShipCore ShipCore = new ShipCore
        {
            UniqueName = "DEFAULT-NO-CORE-ALL-GRID-TYPES",
            SubtypeId = "NO-CORE",
            LargeGridStatic = true,
            LargeGridMobile = true,
            SmallGrid = true,
            MaxBlocks = 30000,
            Modifiers = new GridModifiers(), // Use default modifiers
            PassiveDefenseModifiers = new GridDefenseModifiers(),
            ActiveDefenseModifiers = new GridDefenseModifiers(),
            BlockLimits = new BlockLimit[]
            {
                new BlockLimit
                {
                    Name = "Example: Drills",
                    BlockGroupsShortHand = new string[]{"Drills",},
                    MaxCount = 10f,
                    TurnedOffByNoFlyZone = true,
                    PunishmentType = PunishmentType.Delete,
                    DirectionType = DirectionType.Any,
                },
            }
        };
    }
}