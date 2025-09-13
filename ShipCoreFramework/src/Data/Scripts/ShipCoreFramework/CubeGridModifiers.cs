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
        public static readonly Dictionary<long, GridDefenseModifiers> DefenseModifiers = new Dictionary<long, GridDefenseModifiers>();
        private static readonly MyStringHash EnergyDamageType = MyStringHash.GetOrCompute("Energy");
        private static readonly MyStringHash KineticDamageType = MyStringHash.GetOrCompute("Kinetic");
        
        public static void AddModifiers(IMyCubeBlock coreBlock)
        {
            coreBlock.AddUpgradeValue("AssemblerSpeed", 1f);
            coreBlock.AddUpgradeValue("DrillHarvestMultiplier", 1f);
            coreBlock.AddUpgradeValue("GyroEfficiency", 1f);
            coreBlock.AddUpgradeValue("GyroForce", 1f);
            coreBlock.AddUpgradeValue("PowerProducersOutput", 1f);
            coreBlock.AddUpgradeValue("RefineEfficiency", 1f);
            coreBlock.AddUpgradeValue("RefineSpeed", 1f);
            coreBlock.AddUpgradeValue("ThrusterEfficiency", 1f);
            coreBlock.AddUpgradeValue("ThrusterForce", 1f);

            coreBlock.AddUpgradeValue("MaxSpeed", 1f);
            coreBlock.AddUpgradeValue("MaxBoost", 1f);
            coreBlock.AddUpgradeValue("BoostDuration", 1f);
            coreBlock.AddUpgradeValue("BoostCoolDown", 1f);

            coreBlock.AddUpgradeValue("PassiveBulletDamage", 1f);
            coreBlock.AddUpgradeValue("PassiveRocketDamage", 1f);
            coreBlock.AddUpgradeValue("PassiveExplosionDamage", 1f);
            coreBlock.AddUpgradeValue("PassiveEnvironmentDamage", 1f);
            coreBlock.AddUpgradeValue("PassiveEnergyDamage", 1f);
            coreBlock.AddUpgradeValue("PassiveKineticDamage", 1f);

            coreBlock.AddUpgradeValue("ActiveBulletDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveRocketDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveExplosionDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveEnvironmentDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveEnergyDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveKineticDamage", 1f);
            coreBlock.AddUpgradeValue("DurationDuration", 1f);
            coreBlock.AddUpgradeValue("DamageCooldown", 1f);
        }
        
        public static GridModifiers GetActiveModifiers(GridLogic gridLogic)
        {
            if (gridLogic.PunishModifiers) return ModSessionManager.Config.SelectedNoCore.Modifiers;
            var shipCore = gridLogic.ShipCore;
            
            if (shipCore.SubtypeId == ModSessionManager.Config.SelectedNoCore.SubtypeId) return shipCore.Modifiers;
            var enhancedModifiers = new GridModifiers();
            
            var myBlock = gridLogic.CoreLogic;
            if (myBlock == null)
            {
                return shipCore.Modifiers;
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

        public static void GridCoreDamageHandler(object target, ref MyDamageInformation damageInfo)
        {
            var myBlock = target as IMySlimBlock;
            if (myBlock == null) return;
            
            GridDefenseModifiers modifiers;
            var succeeded = DefenseModifiers.TryGetValue(myBlock.CubeGrid.EntityId, out modifiers);
            if (!succeeded) return;
            
            if (damageInfo.Type == MyDamageType.Bullet) damageInfo.Amount *= modifiers.Bullet;
            if (damageInfo.Type == MyDamageType.Rocket) damageInfo.Amount *= modifiers.Rocket;
            if (damageInfo.Type == MyDamageType.Explosion) damageInfo.Amount *= modifiers.Explosion;
            if (damageInfo.Type == MyDamageType.Environment) damageInfo.Amount *= modifiers.Environment;
            if (damageInfo.Type == EnergyDamageType) damageInfo.Amount *= modifiers.Energy;
            if (damageInfo.Type == KineticDamageType) damageInfo.Amount *= modifiers.Kinetic;
        }
    }
}