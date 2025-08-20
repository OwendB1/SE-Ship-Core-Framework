using System;
using System.Collections.Generic;
using System.Linq;
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
            MaxBlocks = 30000,
            Modifiers = new GridModifiers(), // Use default modifiers
            PassiveDefenseModifiers = new GridDefenseModifiers(),
            ActiveDefenseModifiers = new GridDefenseModifiers(),
            BlockLimits = new BlockLimit[]
            {
                new BlockLimit
                {
                    Name = "Ship Tools",
                    BlockGroupsShortHand = new string[]{"Drills","Welders","Grinders"},
                    MaxCount = 10f,
                    TurnedOffByNoFlyZone = true,
                    PunishmentType = PunishmentType.Delete,
                    AllowedDirections =new List<DirectionType> {DirectionType.Forward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right},
                },
                new BlockLimit
                {
                    Name = "Weapons",
                    BlockGroupsShortHand = new string[]{"Weaponry"},
                    MaxCount = 1f,
                    TurnedOffByNoFlyZone = true,
                    PunishmentType = PunishmentType.Delete,
                    AllowedDirections =new List<DirectionType> {DirectionType.Forward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right},
                },
                new BlockLimit
                {
                    Name = "Production",
                    BlockGroupsShortHand = new string[]{"Production"},
                    MaxCount = 10f,
                    TurnedOffByNoFlyZone = true,
                    PunishmentType = PunishmentType.Delete,
                    AllowedDirections =new List<DirectionType> {DirectionType.Forward,DirectionType.Backward,DirectionType.Up,DirectionType.Down,DirectionType.Left,DirectionType.Right},
                },
            }
        };
    }
}