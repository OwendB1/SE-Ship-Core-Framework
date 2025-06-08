using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;

namespace ShipCoreFramework
{
    public static class CubeGridModifiers
    {
        public static void ApplyModifiers(IMyCubeBlock block, GridModifiers modifiers)
        {
            var thruster = block as IMyThrust;
            if (thruster != null)
            {
                thruster.ThrustMultiplier = modifiers.ThrusterForce;
                thruster.PowerConsumptionMultiplier = 1f / modifiers.ThrusterEfficiency;
            }

            var gyro = block as IMyGyro;
            if (gyro != null)
            {
                gyro.GyroStrengthMultiplier = modifiers.GyroForce;
                gyro.PowerConsumptionMultiplier = 1f / modifiers.GyroEfficiency;
            }

            var refinery = block as IMyRefinery;
            if (refinery != null)
            {
                var rawRefinery = block as MyCubeBlock;
                if (rawRefinery?.CurrentAttachedUpgradeModules != null)
                {
                    var productivity = refinery.UpgradeValues["Productivity"] > 0
                        ? 2f * modifiers.RefineSpeed
                        : modifiers.RefineSpeed;
                    var effectiveness = 1f * modifiers.RefineEfficiency;
                    foreach (var blockModule in rawRefinery.CurrentAttachedUpgradeModules.Select(module =>
                                 module.Value.Block))
                    {
                        List<MyUpgradeModuleInfo> upgrades;
                        blockModule.GetUpgradeList(out upgrades);
                        foreach (var upgrade in upgrades)
                            switch (upgrade.UpgradeType)
                            {
                                case "Productivity":
                                    productivity += upgrade.Modifier * modifiers.RefineSpeed;
                                    break;
                                case "Effectiveness":
                                    effectiveness += upgrade.Modifier * modifiers.RefineEfficiency;
                                    break;
                            }
                    }

                    refinery.UpgradeValues["Productivity"] = productivity;
                    refinery.UpgradeValues["Effectiveness"] = effectiveness;
                }
                else
                {
                    refinery.UpgradeValues["Productivity"] = refinery.UpgradeValues["Productivity"] > 0
                        ? 2f * modifiers.RefineSpeed
                        : modifiers.RefineSpeed;
                    refinery.UpgradeValues["Effectiveness"] = modifiers.RefineEfficiency;
                }
            }

            var assembler = block as IMyAssembler;
            if (assembler != null)
            {
                assembler.UpgradeValues["Productivity"] *= modifiers.AssemblerSpeed;
                var rawAssembler = block as MyCubeBlock;
                if (rawAssembler?.CurrentAttachedUpgradeModules != null)
                {
                    var productivity = 1f * modifiers.AssemblerSpeed;
                    foreach (var blockModule in rawAssembler.CurrentAttachedUpgradeModules.Select(module =>
                                 module.Value.Block))
                    {
                        List<MyUpgradeModuleInfo> upgrades;
                        blockModule.GetUpgradeList(out upgrades);
                        if (blockModule.BlockDefinition.SubtypeId == "LargeProductivityModule")
                            productivity += upgrades[0].Modifier * modifiers.AssemblerSpeed;
                    }

                    assembler.UpgradeValues["Productivity"] = productivity;
                }
                else
                {
                    assembler.UpgradeValues["Productivity"] = modifiers.AssemblerSpeed;
                }
            }

            var reactor = block as IMyReactor;
            if (reactor != null) reactor.PowerOutputMultiplier = modifiers.PowerProducersOutput;

            var drill = block as IMyShipDrill;
            if (drill != null) drill.DrillHarvestMultiplier = modifiers.DrillHarvestMultiplier;
        }

        public static void GridClassDamageHandler(object target, ref MyDamageInformation damageInfo)
        {
            var myBlock = target as IMySlimBlock;
            if (myBlock == null) return;
            var myGrid = myBlock.CubeGrid;
            var myGridLogic = myGrid.GetMainGridLogic();
            if (myGridLogic == null) return;
            if (damageInfo.Type == MyDamageType.Bullet) damageInfo.Amount *= myGridLogic.DefenseModifiers.Bullet;
            if (damageInfo.Type == MyDamageType.Rocket) damageInfo.Amount *= myGridLogic.DefenseModifiers.Rocket;
            if (damageInfo.Type == MyDamageType.Explosion) damageInfo.Amount *= myGridLogic.DefenseModifiers.Explosion;
            if (damageInfo.Type == MyDamageType.Environment)
                damageInfo.Amount *= myGridLogic.DefenseModifiers.Environment;
            if (damageInfo.Type == MyStringHash.GetOrCompute("Energy"))
                damageInfo.Amount *= myGridLogic.DefenseModifiers.Energy;
            if (damageInfo.Type == MyStringHash.GetOrCompute("Kinetic"))
                damageInfo.Amount *= myGridLogic.DefenseModifiers.Kinetic;
        }
    }
}