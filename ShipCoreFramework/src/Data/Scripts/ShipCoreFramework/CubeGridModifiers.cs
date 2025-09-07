#region

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;

#endregion

namespace ShipCoreFramework
{
    public static class CubeGridModifiers
    {
        private static readonly MyStringHash EnergyDamageType = MyStringHash.GetOrCompute("Energy");
        private static readonly MyStringHash KineticDamageType = MyStringHash.GetOrCompute("Kinetic");
        public static void AddModifiers(IMyCubeBlock CoreBlock)
        {
            CoreBlock.AddUpgradeValue("AssemblerSpeed", 1f);
            CoreBlock.AddUpgradeValue("DrillHarvestMultiplier", 1f);
            CoreBlock.AddUpgradeValue("GyroEfficiency", 1f);
            CoreBlock.AddUpgradeValue("GyroForce", 1f);
            CoreBlock.AddUpgradeValue("PowerProducersOutput", 1f);
            CoreBlock.AddUpgradeValue("RefineEfficiency", 1f);
            CoreBlock.AddUpgradeValue("RefineSpeed", 1f);
            CoreBlock.AddUpgradeValue("ThrusterEfficiency", 1f);
            CoreBlock.AddUpgradeValue("ThrusterForce", 1f);
            /*
                    CoreBlock.AddUpgradeValue("MaxBlocks", 1f);
                    CoreBlock.AddUpgradeValue("MaxMass", 1f);
                    CoreBlock.AddUpgradeValue("MaxPCU", 1f);
                    */
            CoreBlock.AddUpgradeValue("MaxSpeed", 1f);
            CoreBlock.AddUpgradeValue("MaxBoost", 1f);
            CoreBlock.AddUpgradeValue("BoostDuration", 1f);
            CoreBlock.AddUpgradeValue("BoostCoolDown", 1f);

            CoreBlock.AddUpgradeValue("ReloadModifier", 1f);

            CoreBlock.AddUpgradeValue("PassiveBulletDamage", 1f);
            CoreBlock.AddUpgradeValue("PassiveRocketDamage", 1f);
            CoreBlock.AddUpgradeValue("PassiveExplosionDamage", 1f);
            CoreBlock.AddUpgradeValue("PassiveEnvironmentDamage", 1f);
            CoreBlock.AddUpgradeValue("PassiveEnergyDamage", 1f);
            CoreBlock.AddUpgradeValue("PassiveKineticDamage", 1f);

            CoreBlock.AddUpgradeValue("ActiveBulletDamage", 1f);
            CoreBlock.AddUpgradeValue("ActiveRocketDamage", 1f);
            CoreBlock.AddUpgradeValue("ActiveExplosionDamage", 1f);
            CoreBlock.AddUpgradeValue("ActiveEnvironmentDamage", 1f);
            CoreBlock.AddUpgradeValue("ActiveEnergyDamage", 1f);
            CoreBlock.AddUpgradeValue("ActiveKineticDamage", 1f);
            CoreBlock.AddUpgradeValue("DurationDuration", 1f);
            CoreBlock.AddUpgradeValue("DamageCooldown", 1f);
        }
        public static GridModifiers GetActiveModifiers(GridLogic gridLogic)
        {
            var shipCore = gridLogic.ShipCore;

            if (shipCore == ModSessionManager.Config.SelectedNoCore) return (shipCore.Modifiers);
            var enhancedModifiers = new GridModifiers();
            if (enhancedModifiers == null) throw new ArgumentNullException(nameof(enhancedModifiers));
            //MyCore._syncIsMainCore

            var myBlock = gridLogic.CoreBlock;
            if (myBlock == null)
            {
                return (shipCore.Modifiers);
            }
            enhancedModifiers.AssemblerSpeed = shipCore.Modifiers.AssemblerSpeed * myBlock.CoreBlock.UpgradeValues["AssemblerSpeed"];
            enhancedModifiers.DrillHarvestMultiplier = shipCore.Modifiers.DrillHarvestMultiplier * myBlock.CoreBlock.UpgradeValues["DrillHarvestMultiplier"];
            enhancedModifiers.GyroEfficiency = shipCore.Modifiers.GyroEfficiency * myBlock.CoreBlock.UpgradeValues["GyroEfficiency"];
            enhancedModifiers.GyroForce = shipCore.Modifiers.GyroForce * myBlock.CoreBlock.UpgradeValues["GyroForce"];
            enhancedModifiers.PowerProducersOutput = shipCore.Modifiers.PowerProducersOutput * myBlock.CoreBlock.UpgradeValues["PowerProducersOutput"];
            enhancedModifiers.RefineEfficiency = shipCore.Modifiers.RefineEfficiency * myBlock.CoreBlock.UpgradeValues["RefineEfficiency"];
            enhancedModifiers.RefineSpeed = shipCore.Modifiers.RefineSpeed * myBlock.CoreBlock.UpgradeValues["RefineSpeed"];
            enhancedModifiers.ThrusterEfficiency = shipCore.Modifiers.ThrusterEfficiency * myBlock.CoreBlock.UpgradeValues["ThrusterEfficiency"];
            enhancedModifiers.ThrusterForce = shipCore.Modifiers.ThrusterForce * myBlock.CoreBlock.UpgradeValues["ThrusterForce"];
            enhancedModifiers.MaxSpeed = shipCore.Modifiers.MaxSpeed * myBlock.CoreBlock.UpgradeValues["MaxSpeed"];
            enhancedModifiers.MaxBoost = shipCore.Modifiers.MaxBoost * myBlock.CoreBlock.UpgradeValues["MaxBoost"];
            enhancedModifiers.BoostDuration = shipCore.Modifiers.BoostDuration * myBlock.CoreBlock.UpgradeValues["BoostDuration"];
            enhancedModifiers.BoostCoolDown = shipCore.Modifiers.BoostCoolDown * myBlock.CoreBlock.UpgradeValues["BoostCoolDown"];
            return enhancedModifiers;
        }
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
        //High overhead being reported mpt sure why
        {
            var myBlock = target as IMySlimBlock;
            if (myBlock == null) return;
            var myGrid = myBlock.CubeGrid;
            var myGridLogic = myGrid.GetMainGridLogic();
            if (myGridLogic == null) return;
            if (myGridLogic.ActiveDefenseEnabled)
            {
                if (damageInfo.Type == MyDamageType.Bullet) damageInfo.Amount *= myGridLogic.ShipCore.ActiveDefenseModifiers.Bullet;
                if (damageInfo.Type == MyDamageType.Rocket) damageInfo.Amount *= myGridLogic.ShipCore.ActiveDefenseModifiers.Rocket;
                if (damageInfo.Type == MyDamageType.Explosion) damageInfo.Amount *= myGridLogic.ShipCore.ActiveDefenseModifiers.Explosion;
                if (damageInfo.Type == MyDamageType.Environment) damageInfo.Amount *= myGridLogic.ShipCore.ActiveDefenseModifiers.Environment;
                if (damageInfo.Type == EnergyDamageType) damageInfo.Amount *= myGridLogic.ShipCore.ActiveDefenseModifiers.Energy;
                if (damageInfo.Type == KineticDamageType) damageInfo.Amount *= myGridLogic.ShipCore.ActiveDefenseModifiers.Kinetic;
            }
            else
            {
                if (damageInfo.Type == MyDamageType.Bullet) damageInfo.Amount *= myGridLogic.ShipCore.PassiveDefenseModifiers.Bullet;
                if (damageInfo.Type == MyDamageType.Rocket) damageInfo.Amount *= myGridLogic.ShipCore.PassiveDefenseModifiers.Rocket;
                if (damageInfo.Type == MyDamageType.Explosion) damageInfo.Amount *= myGridLogic.ShipCore.PassiveDefenseModifiers.Explosion;
                if (damageInfo.Type == MyDamageType.Environment) damageInfo.Amount *= myGridLogic.ShipCore.PassiveDefenseModifiers.Environment;
                if (damageInfo.Type == EnergyDamageType) damageInfo.Amount *= myGridLogic.ShipCore.PassiveDefenseModifiers.Energy;
                if (damageInfo.Type == KineticDamageType) damageInfo.Amount *= myGridLogic.ShipCore.PassiveDefenseModifiers.Kinetic;
            }
        }
    }
}