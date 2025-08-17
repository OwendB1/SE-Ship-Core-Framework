using System.Collections.Generic;
using System.Linq;
namespace ShipCoreFramework
{
    internal static class DefaultGridClassConfig
    {
        public static readonly BlockGroup VanillaSmallGridFixedWeapons = new BlockGroup
        {
            Name = "VanillaSmallGridFixedWeapons",
            BlockTypes = new List<BlockType>
            {
                new BlockType("SmallGatlingGun"),
                new BlockType("SmallGatlingGun", "SmallGatlingGunWarfare2"),
                new BlockType("SmallGatlingGun", "SmallBlockAutocannon"),
                new BlockType("SmallMissileLauncher"),
                new BlockType("SmallMissileLauncher", "SmallMissileLauncherWarfare2"),
                new BlockType("SmallMissileLauncherReload", "SmallMissileLauncherReload"),
                new BlockType("SmallMissileLauncherReload", "SmallBlockMediumCalibreGun"),
                new BlockType("SmallMissileLauncherReload", "SmallRailgun")
            }
        };

        
        //vanilla small grid turrets
        public static readonly BlockGroup VanillaSmallGridTurretWeapons = new BlockGroup
        {
            Name = "VanillaSmallGridTurretWeapons",
            BlockTypes = new List<BlockType>
            {
                new BlockType("LargeMissileTurret", "SmallBlockMediumCalibreTurret"),
                new BlockType("LargeMissileTurret", "AutoCannonTurret"),
                new BlockType("LargeMissileTurret", "SmallMissileTurret"),
                new BlockType("LargeGatlingTurret", "SmallGatlingTurret"),
            }
        };

        //vanilla large grid fixed weapons
        public static readonly BlockGroup VanillaLargeGridFixedWeapons = new BlockGroup
        {
            Name = "VanillaLargeGridFixedWeapons",
            BlockTypes = new List<BlockType>
            {
                new BlockType("SmallMissileLauncher", "LargeMissileLauncher"),
                new BlockType("SmallMissileLauncher", "LargeBlockLargeCalibreGun"),
                new BlockType("SmallMissileLauncherReload", "LargeRailgun"),
            }
        };

        //vanilla large grid turrets
        public static readonly BlockGroup VanillaLargeGridTurretWeapons = new BlockGroup
        {
            Name = "VanillaLargeGridTurretWeapons",
            BlockTypes = new List<BlockType>
            {
                new BlockType("LargeMissileTurret"),
                new BlockType("LargeMissileTurret", "LargeBlockMediumCalibreTurret"),
                new BlockType("LargeMissileTurret", "LargeCalibreTurret"),
                new BlockType("LargeGatlingTurret"),
                new BlockType("InteriorTurret",  "LargeInteriorTurret"),
            }
        };

        //Tools

        public static readonly BlockGroup Drills = new BlockGroup
        {
            Name = "Drills",
            BlockTypes = new List<BlockType>
            {
                new BlockType("Drill", "SmallBlockDrill"),
                new BlockType("Drill", "LargeBlockDrill")
            }
        };

        public static readonly BlockGroup Welders = new BlockGroup
        {
            Name = "Welders",
            BlockTypes = new List<BlockType>
            {
                new BlockType("ShipWelder", "SmallShipWelder"),
                new BlockType("ShipWelder", "LargeShipWelder")
            }
        };

        public static readonly BlockGroup Grinders = new BlockGroup
        {
            Name = "Grinders",
            BlockTypes = new List<BlockType>
            {
                new BlockType("ShipGrinder", "SmallShipGrinder"),
                new BlockType("ShipGrinder", "LargeShipGrinder")
            }
        };

        //Misc vanilla
        public static readonly BlockGroup SafeZone = new BlockGroup
        {
            Name = "SafeZone",
            BlockTypes = new List<BlockType>
            {
                new BlockType("SafeZoneBlock", "SafeZoneBlock")
            }
        };

        public static readonly BlockGroup ProgrammableBlocks = new BlockGroup
        {
            Name = "ProgrammableBlocks",
            BlockTypes = new List<BlockType>
            {
                new BlockType("MyProgrammableBlock", "LargeProgrammableBlock"),
                new BlockType("MyProgrammableBlock", "LargeProgrammableBlockReskin"),
                new BlockType("MyProgrammableBlock", "SmallProgrammableBlock"),
                new BlockType("MyProgrammableBlock", "SmallProgrammableBlockReskin")
            }
        };

        public static readonly BlockGroup Assemblers = new BlockGroup
        {
            Name = "Assemblers",
            BlockTypes = new List<BlockType>
            {
                new BlockType("Assembler", "BasicAssembler", 0.5f),
                new BlockType("Assembler", "LargeAssemblerIndustrial"),
                new BlockType("Assembler", "LargeAssembler")
            }
        };

        public static readonly BlockGroup Refineries = new BlockGroup
        {
            Name = "Refineries",
            BlockTypes = new List<BlockType>
            {
                new BlockType("Refinery", "Blast Furnace", 0.5f),
                new BlockType("Refinery", "LargeRefineryIndustrial"),
                new BlockType("Refinery", "LargeRefinery")
            }
        };

        public static readonly BlockGroup LargeHydrogenTanks = new BlockGroup
        {
            Name = "LargeHydrogenTanks",
            BlockTypes = new List<BlockType>
            {
                new BlockType("OxygenTank", "LargeHydrogenTank"),
                new BlockType("OxygenTank", "LargeHydrogenTankIndustrial"),
                new BlockType("OxygenTank", "SmallHydrogenTank")
            }
        };

        public static readonly BlockGroup SmallHydrogenTanks = new BlockGroup
        {
            Name = "SmallHydrogenTanks",
            BlockTypes = new List<BlockType>
            {
                new BlockType("OxygenTank", "LargeHydrogenTankSmall"),
                new BlockType("OxygenTank", "SmallHydrogenTankSmall")
            }
        };

        public static readonly BlockGroup SmallCargoContainers = new BlockGroup
        {
            Name = "SmallCargoContainers",
            BlockTypes = new List<BlockType>
            {
                new BlockType("CargoContainer", "SmallBlockSmallContainer"),
                new BlockType("CargoContainer", "LargeBlockSmallContainer")
            }
        };

        public static readonly BlockGroup MediumCargoContainers = new BlockGroup
        {
            Name = "MediumCargoContainers",
            BlockTypes = new List<BlockType>
            {
                new BlockType("CargoContainer", "SmallBlockMediumContainer")
            }
        };

        public static readonly BlockGroup LargeCargoContainers = new BlockGroup
        {
            Name = "LargeCargoContainers",
            BlockTypes = new List<BlockType>
            {
                new BlockType("CargoContainer", "SmallBlockLargeContainer"),
                new BlockType("CargoContainer", "LargeBlockLargeContainer"),
                new BlockType("CargoContainer", "LargeBlockLargeIndustrialContainer")
            }
        };

        public static readonly BlockGroup Gyros = new BlockGroup
        {
            Name = "Gyros",
            BlockTypes = new List<BlockType>
            {
                new BlockType("Gyro", "SmallBlockGyro"),
                new BlockType("Gyro", "LargeBlockGyro")
            }
        };
        public static readonly BlockGroup Collectors = new BlockGroup
        {
            Name = "Collectors",
            BlockTypes = new List<BlockType>
            {
                new BlockType("Collector", "Collector")
            }
        };

        public static readonly BlockGroup ConveyorJunctions = new BlockGroup
        {
            Name = "ConveyorJunctions",
            BlockTypes = new List<BlockType>
            {
                new BlockType("Conveyor", "SmallShipConveyorHub"),
                new BlockType("Conveyor", "SmallBlockConveyor"),
                new BlockType("Conveyor", "LargeBlockConveyor"),
                new BlockType("Conveyor", "LargeBlockConveyorPipeJunction"),
                new BlockType("AirVent", "AirVentFull")
            }
        };
        public static readonly BlockGroup VanillaRailguns = new BlockGroup
        {
            Name = "VanillaRailguns",
            BlockTypes = new List<BlockType>
            {
                new BlockType("ConveyorSorter", "SmallRailgun"),
                new BlockType("ConveyorSorter", "LargeRailgun")
            }
        };
        public static readonly BlockGroup VanillaArtillery = new BlockGroup
        {
            Name = "VanillaArtillery",
            BlockTypes = new List<BlockType>
            {
                new BlockType("LargeMissileTurret", "LargeCalibreTurret"),
                new BlockType("SmallMissileLauncher", "LargeBlockLargeCalibreGun"),
            }
        };
        public static readonly BlockGroup VanillaBrawl = new BlockGroup
        {
            Name = "VanillaBrawl",
            BlockTypes = new List<BlockType>
            {
                new BlockType("SmallGatlingGun", "SmallGatlingGunWarfare2"),
                new BlockType("SmallGatlingGun", "SmallBlockAutocannon"),
                new BlockType("LargeGatlingTurret"),
            }

        };
        public static readonly BlockGroup VanillaPDC = new BlockGroup
        {
            Name = "VanillaPDC",
            BlockTypes = new List<BlockType>
            {
                new BlockType("InteriorTurret",  "LargeInteriorTurret")
            }
        };
        // Concatenated block types
        public static readonly BlockGroup StaticWeaponry = new BlockGroup
        {
            Name = "StaticWeaponry",
            BlockTypes = new List<BlockType>
            {
                new BlockType("SmallGatlingGun"),
                new BlockType("SmallGatlingGun", "SmallGatlingGunWarfare2"),
                new BlockType("SmallGatlingGun", "SmallBlockAutocannon"),
                new BlockType("SmallMissileLauncher"),
                new BlockType("SmallMissileLauncher", "SmallMissileLauncherWarfare2"),
                new BlockType("SmallMissileLauncherReload", "SmallMissileLauncherReload"),
                new BlockType("SmallMissileLauncherReload", "SmallBlockMediumCalibreGun"),
                new BlockType("SmallMissileLauncherReload", "SmallRailgun"),
                new BlockType("SmallMissileLauncher", "LargeMissileLauncher"),
                new BlockType("SmallMissileLauncher", "LargeBlockLargeCalibreGun"),
                new BlockType("SmallMissileLauncherReload", "LargeRailgun"),
            }
        };
        public static readonly BlockGroup Turrets = new BlockGroup
        {
            Name = "Turrets",
            BlockTypes = new List<BlockType>
            {
                new BlockType("LargeMissileTurret", "SmallBlockMediumCalibreTurret"),
                new BlockType("LargeMissileTurret", "AutoCannonTurret"),
                new BlockType("LargeMissileTurret", "SmallMissileTurret"),
                new BlockType("LargeGatlingTurret", "SmallGatlingTurret"),
                new BlockType("LargeMissileTurret"),
                new BlockType("LargeMissileTurret", "LargeBlockMediumCalibreTurret"),
                new BlockType("LargeMissileTurret", "LargeCalibreTurret"),
                new BlockType("LargeGatlingTurret"),
                new BlockType("InteriorTurret",  "LargeInteriorTurret"),
            }
        };
        public static readonly BlockGroup Weaponry = new BlockGroup
        {
            Name = "Weaponry",
            BlockTypes = new List<BlockType>
            {
                new BlockType("SmallGatlingGun"),
                new BlockType("SmallGatlingGun", "SmallGatlingGunWarfare2"),
                new BlockType("SmallGatlingGun", "SmallBlockAutocannon"),
                new BlockType("SmallMissileLauncher"),
                new BlockType("SmallMissileLauncher", "SmallMissileLauncherWarfare2"),
                new BlockType("SmallMissileLauncherReload", "SmallMissileLauncherReload"),
                new BlockType("SmallMissileLauncherReload", "SmallBlockMediumCalibreGun"),
                new BlockType("SmallMissileLauncherReload", "SmallRailgun"),
                new BlockType("SmallMissileLauncher", "LargeMissileLauncher"),
                new BlockType("SmallMissileLauncher", "LargeBlockLargeCalibreGun"),
                new BlockType("SmallMissileLauncherReload", "LargeRailgun"),
                new BlockType("LargeMissileTurret", "SmallBlockMediumCalibreTurret"),
                new BlockType("LargeMissileTurret", "AutoCannonTurret"),
                new BlockType("LargeMissileTurret", "SmallMissileTurret"),
                new BlockType("LargeGatlingTurret", "SmallGatlingTurret"),
                new BlockType("LargeMissileTurret"),
                new BlockType("LargeMissileTurret", "LargeBlockMediumCalibreTurret"),
                new BlockType("LargeMissileTurret", "LargeCalibreTurret"),
                new BlockType("LargeGatlingTurret"),
                new BlockType("InteriorTurret",  "LargeInteriorTurret"),
            }
        };
        public static readonly BlockGroup Production = new BlockGroup
        {
            Name = "Production",
            BlockTypes = new List<BlockType>
            {
                new BlockType("Refinery", "Blast Furnace", 0.5f),
                new BlockType("Refinery", "LargeRefineryIndustrial"),
                new BlockType("Refinery", "LargeRefinery"),
                new BlockType("Assembler", "BasicAssembler", 0.5f),
                new BlockType("Assembler", "LargeAssemblerIndustrial"),
                new BlockType("Assembler", "LargeAssembler")
            }
        };
        public static readonly BlockGroup Tools = new BlockGroup
        {
            Name = "Tools",
            BlockTypes = new List<BlockType>
            {
                new BlockType("ShipWelder", "SmallShipWelder"),
                new BlockType("ShipWelder", "LargeShipWelder"),
                new BlockType("ShipGrinder", "SmallShipGrinder"),
                new BlockType("ShipGrinder", "LargeShipGrinder"),
                new BlockType("ShipGrinder", "SmallShipGrinder"),
                new BlockType("ShipGrinder", "LargeShipGrinder"),
            }
        };
        // Block limits
        /*
        private static readonly BlockLimit NoDrillsLimit = new BlockLimit
            { Name = "Drills", MaxCount = 0, BlockTypes = new List<BlockType> Drills, TurnedOffByNoFlyZone = true };

        private static readonly BlockLimit NoProductionLimit = new BlockLimit
            { Name = "Production", MaxCount = 0, BlockTypes = new List<BlockType> Production };

        private static readonly BlockLimit NoShipToolsLimit = new BlockLimit
            { Name = "Ship Tools", MaxCount = 0, BlockTypes = new List<BlockType> Utils.ConcatArrays(Grinders, Welders), TurnedOffByNoFlyZone = true };

        private static readonly BlockLimit NoWeaponsLimit = new BlockLimit
            { Name = "Weapons", MaxCount = 0, BlockTypes = new List<BlockType> Utils.ConcatArrays(Turrets, StaticWeaponry), TurnedOffByNoFlyZone = true };

        private static readonly BlockLimit NoSafeZoneLimit = new BlockLimit
            { Name = "Safe Zone", MaxCount = 0, BlockTypes = new List<BlockType> SafeZone };

        private static readonly BlockLimit SingleSafeZoneLimit = new BlockLimit
            { Name = "Safe Zone", MaxCount = 1, BlockTypes = new List<BlockType> SafeZone };
        private static readonly BlockLimit NoCollectorLimit = new BlockLimit
            { Name = "Collector", MaxCount = 1, BlockTypes = new List<BlockType> Collectors };

        private static readonly BlockLimit SingleCollectorLimit = new BlockLimit
            { Name = "Collector", MaxCount = 1, BlockTypes = new List<BlockType> Collectors };

        private static readonly BlockLimit NoGyroLimit = new BlockLimit
            { Name = "Gyro", MaxCount = 1, BlockTypes = new List<BlockType> Gyros };
        */
        public static GridModifiers DefaultGridModifiers = new GridModifiers
        {
            ThrusterForce = 1f,
            ThrusterEfficiency = 1,
            GyroForce = 1,
            GyroEfficiency = 1,
            AssemblerSpeed = 1,
            DrillHarvestMultiplier = 1,
            PowerProducersOutput = 1,
            RefineEfficiency = 1,
            RefineSpeed = 1,
            MaxSpeed = 30.0f,
            MaxBoost = 1.2f,
            BoostDuration = 10f,
            BoostCoolDown = 60f
        };

        public static GridDefenseModifiers DefaultGridDefenseModifiers2X = new GridDefenseModifiers
        {
            Explosion = 2f,
            Environment = 2f,
            Energy = 2f,
            Kinetic = 2f,
            Bullet = 2f,
            Rocket = 2f
        };

        public static ShipCore DefaultNoCore = new ShipCore
        {
            UniqueName = "Default No Core",
            MaxBlocks = 10000,
            MaxPCU = 25000,
            LargeGridMobile = true,
            LargeGridStatic = true,
            ForceBroadCast = false,
            ForceBroadCastRange = 2000,
            Modifiers = DefaultGridModifiers
            /*
            BlockLimits = new []
            {
                //NoWeaponsLimit,
                //NoSafeZoneLimit,
                //NoShipToolsLimit,
                //NoDrillsLimit,
                //NoCollectorLimit,
                new BlockLimit
                {
                    Name = "Assemblers",
                    MaxCount = 2,
                    BlockTypes = new List<BlockType> Assemblers
                },
                new BlockLimit
                {
                    Name = "Refineries",
                    MaxCount = 2,
                    BlockTypes = new List<BlockType> Refineries
                },
            }*/
        };
        /*
        public static ModConfig DefaultModConfig = new ModConfig
        {
            DefaultNoCore = DefaultNoCore,
            DebugMode = false,
            //NoFlyZones = new List<Zones>{new Zones{AllowedClassesById=new List<long>{301,302,303},Radius=1000.0f},},
            IgnoreFactionTags = new List<string> { "SPRT" },
            IncludeAiFactions = false,
            MaxPossibleSpeedMetersPerSecond = 120.0f
            
            ShipCores = new[]
            {
                new ShipCore //Outpost
                {
                    Id = 101,
                    SimpleName = "Outpost",
                    SmallGrid = false,
                    LargeGridMobile = false,
                    LargeGridStatic = true,
                    MaxBlocks = 10000,
                    MaxPCU = 60000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 5000,
                    MaxPerFaction = 0,
                    MaxPerPlayer = 1,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 0,
                        GyroForce = 0,
                        AssemblerSpeed = 3,
                        RefineEfficiency = 3,
                        RefineSpeed = 3,
                        DrillHarvestMultiplier = 3,
                        PowerProducersOutput = 3,
                    },
                    BlockLimits = new[]
                    {
                        SingleSafeZoneLimit,
                        SingleCollectorLimit,
                        NoCollectorLimit,
                        NoGyroLimit,
                        new BlockLimit
                        {
                            Name = "Production",
                            MaxCount = 10,
                            BlockTypes = new List<BlockType> Production,
                        },
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Ship Tools",
                            MaxCount = 10,
                            BlockTypes = new List<BlockType> Utils.ConcatArrays(Grinders, Welders, Drills)
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> Turrets,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Static Weaponry",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> StaticWeaponry,
                            TurnedOffByNoFlyZone = true
                        }
                    }
                },
                new ShipCore //Station
                {
                    Id = 102,
                    SimpleName = "Station",
                    SmallGrid = false,
                    LargeGridMobile = false,
                    LargeGridStatic = true,
                    MaxBlocks = 25000,
                    MaxPCU = 120000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 20000,
                    MaxPerFaction = 1,
                    MaxPerPlayer = 1,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 0,
                        GyroForce = 0,
                        AssemblerSpeed = 8,
                        RefineEfficiency = 8,
                        RefineSpeed = 8,
                        DrillHarvestMultiplier = 8,
                        PowerProducersOutput = 8
                    },
                    BlockLimits = new[]
                    {
                        SingleSafeZoneLimit,
                        SingleCollectorLimit,
                        NoGyroLimit,
                        new BlockLimit
                        {
                            Name = "Assemblers",
                            MaxCount = 10,
                            BlockTypes = new List<BlockType> Assemblers,
                        },
                        new BlockLimit
                        {
                            Name = "Refineries",
                            MaxCount = 10,
                            BlockTypes = new List<BlockType> Refineries
                        },
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Welders",
                            MaxCount = 25,
                            BlockTypes = new List<BlockType> Welders
                        },
                        new BlockLimit
                        {
                            Name = "Grinders",
                            MaxCount = 25,
                            BlockTypes = new List<BlockType> Grinders
                        },
                        new BlockLimit
                        {
                            Name = "Drills",
                            MaxCount = 10,
                            BlockTypes = new List<BlockType> Drills
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 40,
                            BlockTypes = new List<BlockType> Turrets,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Static Weaponry",
                            MaxCount = 40,
                            BlockTypes = new List<BlockType> StaticWeaponry,
                            TurnedOffByNoFlyZone = true
                        }
                    }
                },
                new ShipCore //Utility
                {
                    Id =401,
                    SimpleName = "Utility",
                    SmallGrid = true,
                    LargeGridMobile = true,
                    LargeGridStatic = true,
                    MaxBlocks = 5000,
                    MaxPCU = 10000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 1000,
                    MaxPerFaction = 0,
                    MaxPerPlayer = 3,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 1,
                        ThrusterEfficiency = 2,
                        GyroForce = 1,
                        GyroEfficiency = 1,
                        AssemblerSpeed = 2,
                        RefineEfficiency = 2,
                        RefineSpeed = 2,
                        DrillHarvestMultiplier = 2,
                        PowerProducersOutput = 2,
                        MaxSpeed=60.0f,
                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 1.0f,
                        Energy = 1.0f,
                        Environment = 0.5f,
                        Explosion = 1.0f,
                        Kinetic = 1.0f,
                        Rocket = 1.0f
                    },
                    BlockLimits = new[]
                    {
                        NoWeaponsLimit,
                        SingleSafeZoneLimit,
                        new BlockLimit
                        {
                            Name = "Production",
                            MaxCount = 1,
                            BlockTypes = new List<BlockType> Production,
                        },
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Ship Tools",
                            MaxCount = 10,
                            BlockTypes = new List<BlockType> Utils.ConcatArrays(Grinders, Welders, Drills),
                            TurnedOffByNoFlyZone = true
                        },
                    }
                },
                new ShipCore //Corvette
                {
                    Id =201,
                    SimpleName = "Corvette",
                    SmallGrid = false,
                    LargeGridMobile = true,
                    LargeGridStatic = true,
                    MaxBlocks = 6000,
                    MaxPCU = 50000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 20000,
                    MaxPerFaction = 3,
                    MaxPerPlayer = 1,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 2,
                        ThrusterEfficiency = 2,
                        GyroForce = 2,
                        GyroEfficiency = 2,
                        AssemblerSpeed = 0,
                        RefineEfficiency = 0,
                        RefineSpeed = 0,
                        DrillHarvestMultiplier = 0,
                        PowerProducersOutput = 1,
                        MaxSpeed = 100.0f,
                        MaxBoost = 1.1f,
                        BoostDuration = 10f,
                        BoostCoolDown = 60f,
                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 0.5f,
                        Energy = 0.7f,
                        Environment = 1f,
                        Explosion = 0.7f,
                        Kinetic = 0.7f,
                        Rocket = 0.7f
                    },
                    BlockLimits = new[]
                    {
                        NoProductionLimit,
                        NoSafeZoneLimit,
                        NoShipToolsLimit,
                        NoCollectorLimit,
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 8,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> Turrets
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> VanillaRailguns
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> VanillaArtillery
                        },
                    }
                },
                new ShipCore //Cruiser
                {
                    Id =202,
                    SimpleName = "Cruiser",
                    SmallGrid = false,
                    LargeGridMobile = true,
                    LargeGridStatic = true,
                    MaxBlocks = 8000,
                    MaxPCU = 50000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 20000,
                    MaxPerFaction = 2,
                    MaxPerPlayer = 1,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 1,
                        ThrusterEfficiency = 3,
                        GyroForce = 1,
                        GyroEfficiency = 2,
                        AssemblerSpeed = 0,
                        RefineEfficiency = 0,
                        RefineSpeed = 0,
                        DrillHarvestMultiplier = 0,
                        PowerProducersOutput = 1.5f,
                        MaxSpeed=90.0f,
                        MaxBoost=1.1f,
                        BoostDuration=10f,
                        BoostCoolDown=60f,
                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 1.0f,
                        Energy = 0.5f,
                        Environment = 1f,
                        Explosion = 1.0f,
                        Kinetic = 1.0f,
                        Rocket = 1.0f
                    },
                    BlockLimits = new[]
                    {
                        NoProductionLimit,
                        NoSafeZoneLimit,
                        NoShipToolsLimit,
                        NoCollectorLimit,
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 8,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 25,
                            BlockTypes = new List<BlockType> Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Fixed Weapons",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> StaticWeaponry
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> VanillaRailguns
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> VanillaArtillery
                        },
                    }
                },
                new ShipCore //Destroyer
                {
                    Id =203,
                    SimpleName = "Destroyer",
                    SmallGrid = false,
                    LargeGridMobile = true,
                    LargeGridStatic = true,
                    MaxBlocks = 6000,
                    MaxPCU = 40000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 20000,
                    MaxPerFaction = 2,
                    MaxPerPlayer = 1,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 0.8f,
                        ThrusterEfficiency = 1,
                        GyroForce = 2,
                        GyroEfficiency = 3,
                        AssemblerSpeed = 0,
                        RefineEfficiency = 0,
                        RefineSpeed = 0,
                        DrillHarvestMultiplier = 0,
                        PowerProducersOutput = 1f,
                        MaxSpeed=100.0f,
                        MaxBoost=1.1f,
                        BoostDuration=10f,
                        BoostCoolDown=120f,
                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 3.0f,
                        Energy = 2.0f,
                        Environment = 1f,
                        Explosion = 3.0f,
                        Kinetic = 2.0f,
                        Rocket = 2.0f
                    },
                    BlockLimits = new[]
                    {
                        NoProductionLimit,
                        NoSafeZoneLimit,
                        NoShipToolsLimit,
                        NoCollectorLimit,
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 8,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 30,
                            BlockTypes = new List<BlockType> Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Gattling Guns",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> VanillaBrawl
                        },
                        new BlockLimit
                        {
                            Name = "Interior Turrent",
                            MaxCount = 3,
                            BlockTypes = new List<BlockType> VanillaPDC,
                            TurnedOffByNoFlyZone = true
                        },
                    }
                },
                new ShipCore //Battleship
                {
                    Id = 204,
                    SimpleName = "Battleship",
                    SmallGrid = false,
                    LargeGridMobile = true,
                    LargeGridStatic = true,
                    MaxBlocks = 10000,
                    MaxPCU = 50000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 20000,
                    MaxPerFaction = 2,
                    MaxPerPlayer = 1,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 0.8f,
                        ThrusterEfficiency = 0.9f,
                        GyroForce = 0.8f,
                        GyroEfficiency = 0.5f,
                        AssemblerSpeed = 0,
                        RefineEfficiency = 0,
                        RefineSpeed = 0,
                        DrillHarvestMultiplier = 0,
                        PowerProducersOutput = 2f,
                        MaxSpeed=80.0f,
                        MaxBoost=1.2f,
                        BoostDuration=5f,
                        BoostCoolDown=60f,

                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 0.8f,
                        Energy = 0.7f,
                        Environment = 1f,
                        Explosion = 0.4f,
                        Kinetic = 0.7f,
                        Rocket = 0.6f
                    },
                    BlockLimits = new[]
                    {
                        NoProductionLimit,
                        NoSafeZoneLimit,
                        NoShipToolsLimit,
                        NoCollectorLimit,
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 8,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 30,
                            BlockTypes = new List<BlockType> Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> VanillaRailguns
                        },
                    }
                },
                new ShipCore //FlagShip
                {
                    Id =205,
                    SimpleName = "FlagShip",
                    SmallGrid = false,
                    LargeGridMobile = true,
                    LargeGridStatic = true,
                    MaxBlocks = 25000,
                    MaxPCU = 120000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 20000,
                    MaxPerFaction = 1,
                    MaxPerPlayer = 1,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 0.8f,
                        ThrusterEfficiency = 0.9f,
                        GyroForce = 0.8f,
                        GyroEfficiency = 0.5f,
                        AssemblerSpeed = 5,
                        RefineEfficiency = 5,
                        RefineSpeed = 5,
                        DrillHarvestMultiplier = 5,
                        PowerProducersOutput = 2f,
                        MaxSpeed=70.0f,
                        MaxBoost=1.5f,
                        BoostDuration=10f,
                        BoostCoolDown=300f,
                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 0.5f,
                        Energy = 0.6f,
                        Environment = 1f,
                        Explosion = 0.3f,
                        Kinetic = 0.6f,
                        Rocket = 0.6f
                    },
                    BlockLimits = new[]
                    {
                        SingleSafeZoneLimit,
                        SingleCollectorLimit,
                        new BlockLimit
                        {
                            Name = "Production",
                            MaxCount = 10,
                            BlockTypes = new List<BlockType> Production,
                        },
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Ship Tools",
                            MaxCount = 5,
                            BlockTypes = new List<BlockType> Utils.ConcatArrays(Grinders, Welders, Drills),
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Weaponry Assorted",
                            MaxCount = 100,
                            BlockTypes = new List<BlockType> Weaponry,
                            TurnedOffByNoFlyZone = true
                        }
                    }
                },
                new ShipCore //Interceptor
                {
                    Id =301,
                    SimpleName = "Interceptor",
                    SmallGrid = true,
                    LargeGridMobile = false,
                    LargeGridStatic = false,
                    MaxBlocks = 600,
                    MaxPCU = 10000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 500,
                    MaxPerFaction = 20,
                    MaxPerPlayer = 8,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 4f,
                        ThrusterEfficiency = 4,
                        GyroForce = 2,
                        GyroEfficiency = 2,
                        AssemblerSpeed = 0,
                        RefineEfficiency = 0,
                        RefineSpeed = 0,
                        DrillHarvestMultiplier = 0,
                        PowerProducersOutput = 1,
                        MaxSpeed=120.0f,
                        MaxBoost=1f,
                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 0.7f,
                        Energy = 0.5f,
                        Environment = 1f,
                        Explosion = 10f,
                        Kinetic = 0.5f,
                        Rocket = 5.0f
                    },
                    BlockLimits = new[]
                    {
                        NoProductionLimit,
                        NoSafeZoneLimit,
                        NoShipToolsLimit,
                        NoCollectorLimit,
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 100,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> Turrets
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> VanillaRailguns
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 0,
                            BlockTypes = new List<BlockType> VanillaArtillery
                        },
                    }
                },
                new ShipCore //Fighter
                {
                    Id =302,
                    SimpleName = "Fighter",
                    SmallGrid = true,
                    LargeGridMobile = false,
                    LargeGridStatic = false,
                    MaxBlocks = 1200,
                    MaxPCU = 10000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 1000,
                    MaxPerFaction = 20,
                    MaxPerPlayer = 8,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 2f,
                        ThrusterEfficiency = 4,
                        GyroForce = 2,
                        GyroEfficiency = 2,
                        AssemblerSpeed = 0,
                        RefineEfficiency = 0,
                        RefineSpeed = 0,
                        DrillHarvestMultiplier = 0,
                        PowerProducersOutput = 1,
                        MaxSpeed = 120.0f,
                        MaxBoost = 1f,
                        BoostDuration = 5f,
                        BoostCoolDown = 60f,
                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 0.5f,
                        Energy = 0.5f,
                        Environment = 1f,
                        Explosion = 10f,
                        Kinetic = 0.5f,
                        Rocket = 2.0f
                    },
                    BlockLimits = new[]
                    {
                        NoProductionLimit,
                        NoSafeZoneLimit,
                        NoShipToolsLimit,
                        NoCollectorLimit,
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 100,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 1,
                            BlockTypes = new List<BlockType> Turrets,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 1,
                            BlockTypes = new List<BlockType> VanillaRailguns,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 2,
                            BlockTypes = new List<BlockType> VanillaArtillery,
                            TurnedOffByNoFlyZone = true
                        },
                    }
                },
                new ShipCore //Gunship
                {
                    Id = 303,
                    SimpleName = "Gunship",
                    SmallGrid = true,
                    LargeGridMobile = false,
                    LargeGridStatic = false,
                    MaxBlocks = 3000,
                    MaxPCU = 20000,
                    ForceBroadCast = true,
                    ForceBroadCastRange = 1000,
                    MaxPerFaction = 20,
                    MaxPerPlayer = 8,
                    Modifiers = new GridModifiers
                    {
                        ThrusterForce = 2f,
                        ThrusterEfficiency = 4,
                        GyroForce = 2,
                        GyroEfficiency = 2,
                        AssemblerSpeed = 0,
                        RefineEfficiency = 0,
                        RefineSpeed = 0,
                        DrillHarvestMultiplier = 0,
                        PowerProducersOutput = 1,
                        MaxSpeed = 115.0f,
                        MaxBoost = 1.1f,
                        BoostDuration = 15f,
                        BoostCoolDown = 30f,
                    },
                    DamageModifiers = new GridDefenseModifiers
                    {
                        Bullet = 0.3f,
                        Energy = 0.2f,
                        Environment = 0.1f,
                        Explosion = 5.0f,
                        Kinetic = 0.4f,
                        Rocket = 1.0f
                    },
                    BlockLimits = new[]
                    {
                        NoProductionLimit,
                        NoSafeZoneLimit,
                        NoShipToolsLimit,
                        NoCollectorLimit,
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 100,
                            BlockTypes = new List<BlockType> ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 20,
                            BlockTypes = new List<BlockType> Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 1,
                            BlockTypes = new List<BlockType> VanillaRailguns,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 2,
                            BlockTypes = new List<BlockType> VanillaArtillery,
                            TurnedOffByNoFlyZone = true
                        },
                    }
                },
            }
        };
        */
    }
}