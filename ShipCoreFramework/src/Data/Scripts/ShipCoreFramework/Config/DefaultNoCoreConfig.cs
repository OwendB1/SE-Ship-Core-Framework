using System.Collections.Generic;

namespace ShipCoreFramework
{
    public static class DefaultNoCoreConfig
    {
        // If a property is not defined here, the default value is used.
        public static readonly ShipCore ShipCore = new ShipCore
        {
            UniqueName = "DEFAULT-NO-CORE-ALL-GRID-TYPES",
            SubtypeId = "NO-CORE",
            MobilityType = MobilityType.Both,
            MaxBlocks = 30000,
            Modifiers = new GridModifiers(), // Use default modifiers
            PassiveDefenseModifiers = new GridDefenseModifiers(),
            ActiveDefenseModifiers = new GridDefenseModifiers(),
            BlockLimits = new []
            {
                new BlockLimit
                {
                    Name = "Ship Tools",
                    BlockGroupsShortHand = new []{"Drills","Welders","Grinders"},
                    MaxCount = 10f,
                    PunishByNoFlyZone = true,
                    PunishmentType = PunishmentType.Delete,
                    AllowedDirections =new List<DirectionType> {DirectionType.Forward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right},
                },
                new BlockLimit
                {
                    Name = "Weapons",
                    BlockGroupsShortHand = new []{"Weaponry"},
                    MaxCount = 1f,
                    PunishByNoFlyZone = true,
                    PunishmentType = PunishmentType.Delete,
                    AllowedDirections =new List<DirectionType> {DirectionType.Forward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right},
                },
                new BlockLimit
                {
                    Name = "Production",
                    BlockGroupsShortHand = new []{"Production"},
                    MaxCount = 10f,
                    PunishByNoFlyZone = true,
                    PunishmentType = PunishmentType.Delete,
                    AllowedDirections =new List<DirectionType> {DirectionType.Forward,DirectionType.Backward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right},
                },
            }
        };
    }
}