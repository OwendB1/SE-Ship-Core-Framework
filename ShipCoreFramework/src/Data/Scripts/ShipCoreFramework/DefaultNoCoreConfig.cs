namespace ShipCoreFramework
{
    public static class DefaultNoCoreConfig
    {
        // If a property is not defined here, the default value is used.
        public static readonly ShipCore ShipCore = new ShipCore
        {
            UniqueName = "DEFAULT-NO-CORE-ALL-GRID-TYPES",
            LargeGridStatic = true,
            LargeGridMobile = true,
            SmallGrid = true,
            MaxBlocks = 30000,
            Modifiers = new GridModifiers(), // Use default modifiers
            PassiveDefenseModifiers = new GridDefenseModifiers(),
            ActiveDefenseModifiers = new GridDefenseModifiers(),
            BlockLimits = new []
            { 
                new BlockLimit
                {
                    Name = "SAMPLE-LIMIT",
                    BlockGroups = new []{ "TEST-GROUP" }
                } 
            }
        };
    }
}