// System
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
// Sandbox
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI;
using Sandbox.Definitions;
// VRage
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI.Network;
using VRage.Sync;

namespace ShipCoreFramework
{
    internal static class DefaultGridClassConfig
    {
        
        private static readonly BlockGroup VanillaSmallGridFixedWeapons = new BlockGroup
        {
            Name="VanillaSmallGridFixedWeapons",
            BlockTypes =
            {
                new BlockType("SmallGatlingGun"),
                new BlockType("SmallGatlingGun", "SmallGatlingGunWarfare2"),
                new BlockType("SmallGatlingGun", "SmallBlockAutocannon"),
                new BlockType("SmallMissileLauncher"),
                new BlockType("SmallMissileLauncher", "SmallMissileLauncherWarfare2"),
                new BlockType("SmallMissileLauncherReload", "SmallMissileLauncherReload"),
                new BlockType("SmallMissileLauncherReload", "SmallBlockMediumCalibreGun"),
                new BlockType("SmallMissileLauncherReload", "SmallRailgun"),   
            },
        };
        /*
        //vanilla small grid turrets
        private static readonly BlockType[] VanillaSmallGridTurretWeapons =
        {
            new BlockType("LargeMissileTurret", "SmallBlockMediumCalibreTurret"),
            new BlockType("LargeMissileTurret", "AutoCannonTurret"),
            new BlockType("LargeMissileTurret", "SmallMissileTurret"),
            new BlockType("LargeGatlingTurret", "SmallGatlingTurret"),
        };

        //vanilla large grid fixed weapons
        private static readonly BlockType[] VanillaLargeGridFixedWeapons =
        {
            new BlockType("SmallMissileLauncher", "LargeMissileLauncher"),
            new BlockType("SmallMissileLauncher", "LargeBlockLargeCalibreGun"),
            new BlockType("SmallMissileLauncherReload", "LargeRailgun"),
        };

        //vanilla large grid turrets
        private static readonly BlockType[] VanillaLargeGridTurretWeapons =
        {
            new BlockType("LargeMissileTurret"),
            new BlockType("LargeMissileTurret", "LargeBlockMediumCalibreTurret"),
            new BlockType("LargeMissileTurret", "LargeCalibreTurret"),
            new BlockType("LargeGatlingTurret"),
            new BlockType("InteriorTurret",  "LargeInteriorTurret"),
        };

        //Tools

        private static readonly BlockType[] Drills =
        {
            new BlockType("Drill", "SmallBlockDrill"),
            new BlockType("Drill", "LargeBlockDrill")
        };

        private static readonly BlockType[] Welders =
        {
            new BlockType("ShipWelder", "SmallShipWelder"),
            new BlockType("ShipWelder", "LargeShipWelder")
        };

        private static readonly BlockType[] Grinders =
        {
            new BlockType("ShipGrinder", "SmallShipGrinder"),
            new BlockType("ShipGrinder", "LargeShipGrinder")
        };

        //Misc vanilla
        private static readonly BlockType[] SafeZone =
        {
            new BlockType("SafeZoneBlock", "SafeZoneBlock")
        };

        private static readonly BlockType[] ProgrammableBlocks =
        {
            new BlockType("MyProgrammableBlock", "LargeProgrammableBlock"),
            new BlockType("MyProgrammableBlock", "LargeProgrammableBlockReskin"),
            new BlockType("MyProgrammableBlock", "SmallProgrammableBlock"),
            new BlockType("MyProgrammableBlock", "SmallProgrammableBlockReskin")
        };

        private static readonly BlockType[] Assemblers =
        {
            new BlockType("Assembler", "BasicAssembler", 0.5f),
            new BlockType("Assembler", "LargeAssemblerIndustrial"),
            new BlockType("Assembler", "LargeAssembler")
        };

        private static readonly BlockType[] Refineries =
        {
            new BlockType("Refinery", "Blast Furnace", 0.5f),
            new BlockType("Refinery", "LargeRefineryIndustrial"),
            new BlockType("Refinery", "LargeRefinery")
        };

        private static readonly BlockType[] LargeHydrogenTanks =
        {
            new BlockType("OxygenTank", "LargeHydrogenTank"),
            new BlockType("OxygenTank", "LargeHydrogenTankIndustrial"),
            new BlockType("OxygenTank", "SmallHydrogenTank")
        };

        private static readonly BlockType[] SmallHydrogenTanks =
        {
            new BlockType("OxygenTank", "LargeHydrogenTankSmall"),
            new BlockType("OxygenTank", "SmallHydrogenTankSmall")
        };

        private static readonly BlockType[] SmallCargoContainers =
        {
            new BlockType("CargoContainer", "SmallBlockSmallContainer"),
            new BlockType("CargoContainer", "LargeBlockSmallContainer")
        };

        private static readonly BlockType[] MediumCargoContainers =
        {
            new BlockType("CargoContainer", "SmallBlockMediumContainer")
        };

        private static readonly BlockType[] LargeCargoContainers =
        {
            new BlockType("CargoContainer", "SmallBlockLargeContainer"),
            new BlockType("CargoContainer", "LargeBlockLargeContainer"),
            new BlockType("CargoContainer", "LargeBlockLargeIndustrialContainer")
        };

        private static readonly BlockType[] Gyros =
        {
            new BlockType("Gyro", "SmallBlockGyro"),
            new BlockType("Gyro", "LargeBlockGyro")
        };
        private static readonly BlockType[] Collectors =
        {
            new BlockType("Collector", "Collector")
        };

        private static readonly BlockType[] ConveyorJunctions =
        {
            new BlockType("Conveyor", "SmallShipConveyorHub"),
            new BlockType("Conveyor", "SmallBlockConveyor"),
            new BlockType("Conveyor", "LargeBlockConveyor"),
            new BlockType("Conveyor", "LargeBlockConveyorPipeJunction"),
            new BlockType("AirVent", "AirVentFull")
        };
                private static readonly BlockType[] VanillaRailguns =
        {
            new BlockType("ConveyorSorter", "SmallRailgun"),
            new BlockType("ConveyorSorter", "LargeRailgun")
        };
        private static readonly BlockType[] VanillaArtillery =
        {
            new BlockType("LargeMissileTurret", "LargeCalibreTurret"),
            new BlockType("SmallMissileLauncher", "LargeBlockLargeCalibreGun"),
        };
        private static readonly BlockType[] VanillaBrawl =
        {
            new BlockType("SmallGatlingGun", "SmallGatlingGunWarfare2"),
            new BlockType("SmallGatlingGun", "SmallBlockAutocannon"),
            new BlockType("LargeGatlingTurret"),

        };
        private static readonly BlockType[] VanillaPDC =
        {
            new BlockType("InteriorTurret",  "LargeInteriorTurret")
        };     
        // Concatenated block types
        private static readonly BlockType[] StaticWeaponry = Utils.ConcatArrays(VanillaSmallGridFixedWeapons, VanillaLargeGridFixedWeapons);
        private static readonly BlockType[] Turrets = Utils.ConcatArrays(VanillaLargeGridTurretWeapons, VanillaSmallGridTurretWeapons);
        private static readonly BlockType[] Weaponry = Utils.ConcatArrays(Turrets, StaticWeaponry);
        private static readonly BlockType[] Production = Utils.ConcatArrays(Refineries, Assemblers);

        // Block limits

        private static readonly BlockLimit NoDrillsLimit = new BlockLimit
            { Name = "Drills", MaxCount = 0, BlockTypes = Drills, TurnedOffByNoFlyZone = true };

        private static readonly BlockLimit NoProductionLimit = new BlockLimit
            { Name = "Production", MaxCount = 0, BlockTypes = Production };

        private static readonly BlockLimit NoShipToolsLimit = new BlockLimit
            { Name = "Ship Tools", MaxCount = 0, BlockTypes = Utils.ConcatArrays(Grinders, Welders), TurnedOffByNoFlyZone = true };

        private static readonly BlockLimit NoWeaponsLimit = new BlockLimit
            { Name = "Weapons", MaxCount = 0, BlockTypes = Utils.ConcatArrays(Turrets, StaticWeaponry), TurnedOffByNoFlyZone = true };

        private static readonly BlockLimit NoSafeZoneLimit = new BlockLimit
            { Name = "Safe Zone", MaxCount = 0, BlockTypes = SafeZone };

        private static readonly BlockLimit SingleSafeZoneLimit = new BlockLimit
            { Name = "Safe Zone", MaxCount = 1, BlockTypes = SafeZone };
        private static readonly BlockLimit NoCollectorLimit = new BlockLimit
            { Name = "Collector", MaxCount = 1, BlockTypes = Collectors };

        private static readonly BlockLimit SingleCollectorLimit = new BlockLimit
            { Name = "Collector", MaxCount = 1, BlockTypes = Collectors };

        private static readonly BlockLimit NoGyroLimit = new BlockLimit
            { Name = "Gyro", MaxCount = 1, BlockTypes = Gyros };
            
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
            MaxBoost=1.2f,
            BoostDuration=10f,
            BoostCoolDown=60f,
        };

        public static GridDefenseModifiers DefaultGridDefenseModifiers2X = new GridDefenseModifiers
        {
            Explosion = 2f,
            Environment = 2f,
            Energy = 2f,
            Kinetic = 2f,
            Bullet = 2f,
            Rocket = 2f,
        };

        public static ShipCore DefaultNoCore = new ShipCore
        {
            UniqueName = "Default No Core",
            MaxBlocks = 10000,
            MaxPCU = 25000,
            SmallGrid = true,
            LargeGridMobile = true,
            LargeGridStatic = true,
            ForceBroadCast = false,
            ForceBroadCastRange = 2000,
            Modifiers = DefaultGridModifiers,
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
                    BlockTypes = Assemblers
                },
                new BlockLimit
                {
                    Name = "Refineries",
                    MaxCount = 2,
                    BlockTypes = Refineries
                },
            }*/
        };

        public static ModConfig DefaultModConfig = new ModConfig
        {
            DefaultNoCore = DefaultNoCore,
            DebugMode = false,
            //NoFlyZones = new List<Zones>{new Zones{AllowedClassesById=new List<long>{301,302,303},Radius=1000.0f},},
            IgnoreFactionTags = new List<string>{"SPRT"},
            IncludeAiFactions = false,
            MaxPossibleSpeedMetersPerSecond = 120.0f,
            /*
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
                            BlockTypes = Production,
                        },
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 20,
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Ship Tools",
                            MaxCount = 10,
                            BlockTypes = Utils.ConcatArrays(Grinders, Welders, Drills)
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 20,
                            BlockTypes = Turrets,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Static Weaponry",
                            MaxCount = 20,
                            BlockTypes = StaticWeaponry,
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
                            BlockTypes = Assemblers,
                        },
                        new BlockLimit
                        {
                            Name = "Refineries",
                            MaxCount = 10,
                            BlockTypes = Refineries
                        },
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 20,
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Welders",
                            MaxCount = 25,
                            BlockTypes = Welders
                        },
                        new BlockLimit
                        {
                            Name = "Grinders",
                            MaxCount = 25,
                            BlockTypes = Grinders
                        },
                        new BlockLimit
                        {
                            Name = "Drills",
                            MaxCount = 10,
                            BlockTypes = Drills
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 40,
                            BlockTypes = Turrets,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Static Weaponry",
                            MaxCount = 40,
                            BlockTypes = StaticWeaponry,
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
                            BlockTypes = Production,
                        },
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 20,
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Ship Tools",
                            MaxCount = 10,
                            BlockTypes = Utils.ConcatArrays(Grinders, Welders, Drills),
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
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 20,
                            BlockTypes = Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 0,
                            BlockTypes = Turrets
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 0,
                            BlockTypes = VanillaRailguns
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 0,
                            BlockTypes = VanillaArtillery
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
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 25,
                            BlockTypes = Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Fixed Weapons",
                            MaxCount = 0,
                            BlockTypes = StaticWeaponry
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 0,
                            BlockTypes = VanillaRailguns
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 0,
                            BlockTypes = VanillaArtillery
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
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 30,
                            BlockTypes = Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Gattling Guns",
                            MaxCount = 0,
                            BlockTypes = VanillaBrawl
                        },
                        new BlockLimit
                        {
                            Name = "Interior Turrent",
                            MaxCount = 3,
                            BlockTypes = VanillaPDC,
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
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 30,
                            BlockTypes = Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 0,
                            BlockTypes = VanillaRailguns
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
                            BlockTypes = Production,
                        },
                        new BlockLimit
                        {
                            Name = "Conveyor Junctions",
                            MaxCount = 20,
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Ship Tools",
                            MaxCount = 5,
                            BlockTypes = Utils.ConcatArrays(Grinders, Welders, Drills),
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Weaponry Assorted",
                            MaxCount = 100,
                            BlockTypes = Weaponry,
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
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 20,
                            BlockTypes = Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 0,
                            BlockTypes = Turrets
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 0,
                            BlockTypes = VanillaRailguns
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 0,
                            BlockTypes = VanillaArtillery
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
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 20,
                            BlockTypes = Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Turrets",
                            MaxCount = 1,
                            BlockTypes = Turrets,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 1,
                            BlockTypes = VanillaRailguns,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 2,
                            BlockTypes = VanillaArtillery,
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
                            BlockTypes = ConveyorJunctions
                        },
                        new BlockLimit
                        {
                            Name = "Weapons",
                            MaxCount = 20,
                            BlockTypes = Weaponry,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "RailGuns",
                            MaxCount = 1,
                            BlockTypes = VanillaRailguns,
                            TurnedOffByNoFlyZone = true
                        },
                        new BlockLimit
                        {
                            Name = "Artillery",
                            MaxCount = 2,
                            BlockTypes = VanillaArtillery,
                            TurnedOffByNoFlyZone = true
                        },
                    }
                },
            }*/
        };
    }
}