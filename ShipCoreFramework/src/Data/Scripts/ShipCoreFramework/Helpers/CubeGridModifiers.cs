using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
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
        public static readonly ConcurrentDictionary<long, GridDefenseModifiers> DefenseModifiers = new ConcurrentDictionary<long, GridDefenseModifiers>();
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
            coreBlock.AddUpgradeValue("PassivePostShieldDamage", 1f);
            coreBlock.AddUpgradeValue("PassiveEnergyDamage", 1f);
            coreBlock.AddUpgradeValue("PassiveKineticDamage", 1f);

            coreBlock.AddUpgradeValue("ActiveBulletDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveRocketDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveExplosionDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveEnvironmentDamage", 1f);
            coreBlock.AddUpgradeValue("ActivePostShieldDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveEnergyDamage", 1f);
            coreBlock.AddUpgradeValue("ActiveKineticDamage", 1f);
            
            coreBlock.AddUpgradeValue("DurationDuration", 1f);
            coreBlock.AddUpgradeValue("DamageCooldown", 1f);
        }
        
        internal static GridModifiers GetActiveModifiers(GroupComponent gridGroupComponentLogic)
        {
            if (gridGroupComponentLogic.PunishModifiers) return Session.Config.SelectedNoCore.Modifiers;
            var shipCore = gridGroupComponentLogic.ShipCore;
            
            if (shipCore.SubtypeId == Session.Config.SelectedNoCore.SubtypeId) return shipCore.Modifiers;
            var enhancedModifiers = new GridModifiers();
            
            var myBlock = gridGroupComponentLogic.MainCoreComponent;
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

            var id = ((IMyTerminalBlock)block).BlockDefinition;
            var cubeDef = MyDefinitionManager.Static.GetCubeBlockDefinition(id);

            var refinery = block as IMyRefinery;
            if (refinery != null)
            {
                var refDef = cubeDef as MyRefineryDefinition;
                var baseSpeed = refDef?.RefineSpeed ?? 1f;
                var baseYield = refDef?.MaterialEfficiency ?? 1f;

                float prodSum = 0f, effSum = 0f;
                var attachedR = ((MyCubeBlock)block).CurrentAttachedUpgradeModules;
                if (attachedR != null)
                {
                    foreach (var m in attachedR.Select(kv => kv.Value.Block).Where(m => m != null))
                    {
                        var ups = new List<MyUpgradeModuleInfo>(); 
                        m.FillUpgradeList(ups);
                        if (ups.Count == 0) continue;
                        foreach (var up in ups)
                        {
                            switch (up.UpgradeType)
                            {
                                case "Productivity":
                                    prodSum += up.Modifier;
                                    break;
                                case "Effectiveness":
                                    effSum += up.Modifier;
                                    break;
                            }
                        }
                    }
                }

                var targetSpeed = (baseSpeed + prodSum) * modifiers.RefineSpeed;
                var prodValue = targetSpeed - baseSpeed;
                if (prodValue < -baseSpeed) prodValue = -baseSpeed;
                
                var yieldValue = (baseYield + effSum) * modifiers.RefineEfficiency;
                if (yieldValue < 0f) yieldValue = 0f;
                
                refinery.UpgradeValues["Productivity"]  = prodValue;
                refinery.UpgradeValues["Effectiveness"] = yieldValue;
            }

            var assembler = block as IMyAssembler;
            if (assembler != null)
            {
                var asmDef = cubeDef as MyAssemblerDefinition;
                var baseSpeed = asmDef?.AssemblySpeed ?? 1f;

                var prodSum = 0f;
                var attachedA = ((MyCubeBlock)block).CurrentAttachedUpgradeModules;
                if (attachedA != null)
                {
                    foreach (var m in attachedA.Select(kv => kv.Value.Block).Where(m => m != null))
                    {
                        var ups = new List<MyUpgradeModuleInfo>(); 
                        m.FillUpgradeList(ups);
                        if (ups.Count == 0) continue;
                        prodSum += ups.Where(t => t.UpgradeType == "Productivity").Sum(t => t.Modifier);
                    }
                }

                var targetSpeed = (baseSpeed + prodSum) * modifiers.AssemblerSpeed;
                var prodValue = targetSpeed - baseSpeed;
                if (prodValue < -baseSpeed) prodValue = -baseSpeed;

                assembler.UpgradeValues["Productivity"] = prodValue;
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
            if (!DefenseModifiers.TryGetValue(myBlock.CubeGrid.EntityId, out modifiers)) return;

            if (damageInfo.Type == MyDamageType.Bullet)
            {
                if(damageInfo.ExtraInfo == EnergyDamageType) damageInfo.Amount *= modifiers.Energy;
                else if(damageInfo.ExtraInfo == KineticDamageType) damageInfo.Amount *= modifiers.Kinetic;
                else damageInfo.Amount *= modifiers.PostShield;
            }
            if (damageInfo.Type == MyDamageType.Rocket) damageInfo.Amount *= modifiers.Rocket;
            if (damageInfo.Type == MyDamageType.Explosion) damageInfo.Amount *= modifiers.Explosion;
            if (damageInfo.Type == MyDamageType.Environment) damageInfo.Amount *= modifiers.Environment;
            if (damageInfo.Type == MyDamageType.Drill) damageInfo.Amount *= modifiers.PostShield;
        }
    }
}