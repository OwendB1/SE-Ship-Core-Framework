using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;
using VRageMath;

namespace ShipCoreFramework
{
    internal enum DefenseModifierTarget
    {
        Passive = 0,
        Active = 1
    }

    internal static class CubeGridModifiers
    {
        internal static readonly GameThreadWriteDictionary<long, GridDefenseModifiers> DefenseModifiers =
            new GameThreadWriteDictionary<long, GridDefenseModifiers>(null, ThreadWork.StateCategory, "defense-modifiers");
        private const string UpgradeModuleLinkType = "ShipCoreLink";
        private static readonly List<MyEntity> ExplosionEntities = new List<MyEntity>();
        private static readonly MyStringHash EnergyDamageType = MyStringHash.GetOrCompute("Energy");
        private static readonly MyStringHash KineticDamageType = MyStringHash.GetOrCompute("Kinetic");

        internal static void RegisterUpgradeModuleLink(IMyCubeBlock block)
        {
            if (block == null) return;
            if (!Session.IsGameThread)
            {
                var blockId = block.EntityId;
                ThreadWork.Enqueue(ThreadWork.StateCategory, "upgrade-link:" + blockId,
                    "Register upgrade link " + blockId,
                    delegate
                    {
                        return block != null &&
                               !block.MarkedForClose &&
                               !block.Closed &&
                               !Session.IsShuttingDown;
                    },
                    delegate { RegisterUpgradeModuleLink(block); });
                return;
            }

            block.AddUpgradeValue(UpgradeModuleLinkType, 0f);
        }

        internal static float ApplyUpgradeModifier(float currentValue, float modifierValue,
            UpgradeModifierOperation modifierType)
        {
            return modifierType == UpgradeModifierOperation.Additive
                ? currentValue + modifierValue
                : currentValue * modifierValue;
        }

        internal static GridDefenseModifiers ScaleDefenseModifiers(GridDefenseModifiers modifiers, float factor)
        {
            return new GridDefenseModifiers
            {
                Bullet = modifiers.Bullet * factor,
                PostShield = modifiers.PostShield * factor,
                Duration = modifiers.Duration * factor,
                Cooldown = modifiers.Cooldown * factor,
                Rocket = modifiers.Rocket * factor,
                Explosion = modifiers.Explosion * factor,
                Environment = modifiers.Environment * factor,
                Energy = modifiers.Energy * factor,
                Kinetic = modifiers.Kinetic * factor
            };
        }

        internal static GridModifiers GetActiveModifiers(GroupComponent groupComponent)
        {
            if (groupComponent.PunishModifiers) return Session.Config.SelectedNoCore.Modifiers;
            var shipCore = groupComponent.ShipCore;

            var modifiers = Clone(shipCore.Modifiers);
            foreach (var module in groupComponent.GetEffectiveUpgradeModules(true))
            {
                var config = module.GetConfig();
                if (config?.Modifiers == null) continue;

                foreach (var modifier in config.Modifiers.Where(modifier => modifier != null))
                {
                    switch (modifier.Stat)
                    {
                        case "AssemblerSpeed":
                            modifiers.AssemblerSpeed = ApplyUpgradeModifier(modifiers.AssemblerSpeed, modifier.Value, modifier.ModifierType);
                            break;
                        case "DrillHarvestMultiplier":
                            modifiers.DrillHarvestMultiplier = ApplyUpgradeModifier(modifiers.DrillHarvestMultiplier, modifier.Value, modifier.ModifierType);
                            break;
                        case "GyroEfficiency":
                            modifiers.GyroEfficiency = ApplyUpgradeModifier(modifiers.GyroEfficiency, modifier.Value, modifier.ModifierType);
                            break;
                        case "GyroForce":
                            modifiers.GyroForce = ApplyUpgradeModifier(modifiers.GyroForce, modifier.Value, modifier.ModifierType);
                            break;
                        case "PowerProducersOutput":
                            modifiers.PowerProducersOutput = ApplyUpgradeModifier(modifiers.PowerProducersOutput, modifier.Value, modifier.ModifierType);
                            break;
                        case "RefineEfficiency":
                            modifiers.RefineEfficiency = ApplyUpgradeModifier(modifiers.RefineEfficiency, modifier.Value, modifier.ModifierType);
                            break;
                        case "RefineSpeed":
                            modifiers.RefineSpeed = ApplyUpgradeModifier(modifiers.RefineSpeed, modifier.Value, modifier.ModifierType);
                            break;
                        case "ThrusterEfficiency":
                            modifiers.ThrusterEfficiency = ApplyUpgradeModifier(modifiers.ThrusterEfficiency, modifier.Value, modifier.ModifierType);
                            break;
                        case "ThrusterForce":
                            modifiers.ThrusterForce = ApplyUpgradeModifier(modifiers.ThrusterForce, modifier.Value, modifier.ModifierType);
                            break;
                    }
                }
            }

            return modifiers;
        }
        
        internal static SpeedModifiers GetActiveSpeedModifiers(GroupComponent groupComponent)
        {
            var shipCore = groupComponent.ShipCore;
            var modifiers = Clone(shipCore.SpeedModifiers);
            foreach (var module in groupComponent.GetEffectiveUpgradeModules(true))
            {
                var config = module.GetConfig();
                if (config?.Modifiers == null) continue;

                foreach (var modifier in config.Modifiers.Where(modifier => modifier != null))
                {
                    switch (modifier.Stat)
                    {
                        case "MaxSpeed":
                            modifiers.MaxSpeed = ApplyUpgradeModifier(modifiers.MaxSpeed, modifier.Value, modifier.ModifierType);
                            break;
                        case "MaxBoost":
                            modifiers.MaxBoost = ApplyUpgradeModifier(modifiers.MaxBoost, modifier.Value, modifier.ModifierType);
                            break;
                        case "BoostDuration":
                            modifiers.BoostDuration = ApplyUpgradeModifier(modifiers.BoostDuration, modifier.Value, modifier.ModifierType);
                            break;
                        case "BoostCoolDown":
                            modifiers.BoostCoolDown = ApplyUpgradeModifier(modifiers.BoostCoolDown, modifier.Value, modifier.ModifierType);
                            break;
                        case "MinimumFrictionSpeedAbsolute":
                            modifiers.MinimumFrictionSpeedAbsolute = ApplyUpgradeModifier(modifiers.MinimumFrictionSpeedAbsolute, modifier.Value, modifier.ModifierType);
                            break;
                        case "MaximumFrictionSpeedAbsolute":
                            modifiers.MaximumFrictionSpeedAbsolute = ApplyUpgradeModifier(modifiers.MaximumFrictionSpeedAbsolute, modifier.Value, modifier.ModifierType);
                            break;
                        case "MinimumFrictionSpeedModifier":
                            modifiers.MinimumFrictionSpeedModifier = ApplyUpgradeModifier(modifiers.MinimumFrictionSpeedModifier, modifier.Value, modifier.ModifierType);
                            break;
                        case "MaximumFrictionSpeedModifier":
                            modifiers.MaximumFrictionSpeedModifier = ApplyUpgradeModifier(modifiers.MaximumFrictionSpeedModifier, modifier.Value, modifier.ModifierType);
                            break;
                        case "MaximumFrictionDeceleration":
                            modifiers.MaximumFrictionDeceleration = ApplyUpgradeModifier(modifiers.MaximumFrictionDeceleration, modifier.Value, modifier.ModifierType);
                            break;
                        case "CruiseFrictionMultiplier":
                            modifiers.CruiseFrictionMultiplier = ApplyUpgradeModifier(modifiers.CruiseFrictionMultiplier, modifier.Value, modifier.ModifierType);
                            break;
                        case "CruiseAccelerationThreshold":
                            modifiers.CruiseAccelerationThreshold = ApplyUpgradeModifier(modifiers.CruiseAccelerationThreshold, modifier.Value, modifier.ModifierType);
                            break;
                        case "AtmosphericCruiseFrictionMultiplier":
                            EnsureAtmosphericFrictionSettings(modifiers);
                            modifiers.AtmosphericFriction.CruiseFrictionMultiplier = ApplyUpgradeModifier(modifiers.AtmosphericFriction.CruiseFrictionMultiplier, modifier.Value, modifier.ModifierType);
                            break;
                        case "AtmosphericCruiseAccelerationThreshold":
                            EnsureAtmosphericFrictionSettings(modifiers);
                            modifiers.AtmosphericFriction.CruiseAccelerationThreshold = ApplyUpgradeModifier(modifiers.AtmosphericFriction.CruiseAccelerationThreshold, modifier.Value, modifier.ModifierType);
                            break;
                        case "AtmosphericAirDensityThreshold":
                            EnsureAtmosphericFrictionSettings(modifiers);
                            modifiers.AtmosphericFriction.AirDensityThreshold = ApplyUpgradeModifier(modifiers.AtmosphericFriction.AirDensityThreshold, modifier.Value, modifier.ModifierType);
                            break;
                    }
                }
            }

            return modifiers;
        }

        internal static GridDefenseModifiers GetEffectiveDefenseModifiers(GridDefenseModifiers baseModifiers,
            IEnumerable<UpgradeModuleConfig> upgradeModules, DefenseModifierTarget target)
        {
            var modifiers = Clone(baseModifiers);
            foreach (var module in upgradeModules.Where(module => module?.Modifiers != null))
            {
                foreach (var modifier in module.Modifiers.Where(modifier => modifier != null))
                {
                    switch (modifier.Stat)
                    {
                        case "PassiveBulletDamage":
                            if (target != DefenseModifierTarget.Passive) break;
                            modifiers.Bullet = ApplyUpgradeModifier(modifiers.Bullet, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActiveBulletDamage":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.Bullet = ApplyUpgradeModifier(modifiers.Bullet, modifier.Value, modifier.ModifierType);
                            break;
                        case "PassiveRocketDamage":
                            if (target != DefenseModifierTarget.Passive) break;
                            modifiers.Rocket = ApplyUpgradeModifier(modifiers.Rocket, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActiveRocketDamage":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.Rocket = ApplyUpgradeModifier(modifiers.Rocket, modifier.Value, modifier.ModifierType);
                            break;
                        case "PassiveExplosionDamage":
                            if (target != DefenseModifierTarget.Passive) break;
                            modifiers.Explosion = ApplyUpgradeModifier(modifiers.Explosion, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActiveExplosionDamage":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.Explosion = ApplyUpgradeModifier(modifiers.Explosion, modifier.Value, modifier.ModifierType);
                            break;
                        case "PassiveEnvironmentDamage":
                            if (target != DefenseModifierTarget.Passive) break;
                            modifiers.Environment = ApplyUpgradeModifier(modifiers.Environment, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActiveEnvironmentDamage":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.Environment = ApplyUpgradeModifier(modifiers.Environment, modifier.Value, modifier.ModifierType);
                            break;
                        case "PassivePostShieldDamage":
                            if (target != DefenseModifierTarget.Passive) break;
                            modifiers.PostShield = ApplyUpgradeModifier(modifiers.PostShield, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActivePostShieldDamage":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.PostShield = ApplyUpgradeModifier(modifiers.PostShield, modifier.Value, modifier.ModifierType);
                            break;
                        case "PassiveEnergyDamage":
                            if (target != DefenseModifierTarget.Passive) break;
                            modifiers.Energy = ApplyUpgradeModifier(modifiers.Energy, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActiveEnergyDamage":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.Energy = ApplyUpgradeModifier(modifiers.Energy, modifier.Value, modifier.ModifierType);
                            break;
                        case "PassiveKineticDamage":
                            if (target != DefenseModifierTarget.Passive) break;
                            modifiers.Kinetic = ApplyUpgradeModifier(modifiers.Kinetic, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActiveKineticDamage":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.Kinetic = ApplyUpgradeModifier(modifiers.Kinetic, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActiveDefenseDuration":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.Duration = ApplyUpgradeModifier(modifiers.Duration, modifier.Value, modifier.ModifierType);
                            break;
                        case "ActiveDefenseCooldown":
                            if (target != DefenseModifierTarget.Active) break;
                            modifiers.Cooldown = ApplyUpgradeModifier(modifiers.Cooldown, modifier.Value, modifier.ModifierType);
                            break;
                    }
                }
            }

            return modifiers;
        }

        private static GridModifiers Clone(GridModifiers modifiers)
        {
            if (modifiers == null) return new GridModifiers();

            return new GridModifiers
            {
                AssemblerSpeed = modifiers.AssemblerSpeed,
                DrillHarvestMultiplier = modifiers.DrillHarvestMultiplier,
                GyroEfficiency = modifiers.GyroEfficiency,
                GyroForce = modifiers.GyroForce,
                PowerProducersOutput = modifiers.PowerProducersOutput,
                RefineEfficiency = modifiers.RefineEfficiency,
                RefineSpeed = modifiers.RefineSpeed,
                ThrusterEfficiency = modifiers.ThrusterEfficiency,
                ThrusterForce = modifiers.ThrusterForce
            };
        }

        private static SpeedModifiers Clone(SpeedModifiers modifiers)
        {
            if (modifiers == null) return new SpeedModifiers();

            return new SpeedModifiers
            {
                MaxSpeed = modifiers.MaxSpeed,
                MaxBoost = modifiers.MaxBoost,
                BoostDuration = modifiers.BoostDuration,
                BoostCoolDown = modifiers.BoostCoolDown,
                MinimumFrictionSpeedAbsolute = modifiers.MinimumFrictionSpeedAbsolute,
                MaximumFrictionSpeedAbsolute = modifiers.MaximumFrictionSpeedAbsolute,
                MinimumFrictionSpeedModifier = modifiers.MinimumFrictionSpeedModifier,
                MaximumFrictionSpeedModifier = modifiers.MaximumFrictionSpeedModifier,
                MaximumFrictionDeceleration = modifiers.MaximumFrictionDeceleration,
                CruiseFrictionMultiplier = modifiers.CruiseFrictionMultiplier,
                CruiseAccelerationThreshold = modifiers.CruiseAccelerationThreshold,
                FrictionCurve = Clone(modifiers.FrictionCurve),
                AtmosphericFriction = Clone(modifiers.AtmosphericFriction)
            };
        }

        private static void EnsureAtmosphericFrictionSettings(SpeedModifiers modifiers)
        {
            if (modifiers.AtmosphericFriction == null)
                modifiers.AtmosphericFriction = new AtmosphericFrictionSettings();
        }

        private static FrictionCurve Clone(FrictionCurve curve)
        {
            if (curve == null) return null;

            var sourceSegments = curve.Segments ?? new FrictionCurveSegment[0];
            var segments = new FrictionCurveSegment[sourceSegments.Length];
            for (var i = 0; i < sourceSegments.Length; i++)
            {
                var source = sourceSegments[i];
                if (source == null) continue;

                segments[i] = new FrictionCurveSegment
                {
                    StartSpeed = source.StartSpeed,
                    EndSpeed = source.EndSpeed,
                    StartDeceleration = source.StartDeceleration,
                    EndDeceleration = source.EndDeceleration
                };
            }

            return new FrictionCurve
            {
                Segments = segments
            };
        }

        private static AtmosphericFrictionSettings Clone(AtmosphericFrictionSettings settings)
        {
            if (settings == null) return null;

            return new AtmosphericFrictionSettings
            {
                FrictionCurve = Clone(settings.FrictionCurve),
                CruiseFrictionMultiplier = settings.CruiseFrictionMultiplier,
                CruiseAccelerationThreshold = settings.CruiseAccelerationThreshold,
                AirDensityThreshold = settings.AirDensityThreshold
            };
        }

        private static GridDefenseModifiers Clone(GridDefenseModifiers modifiers)
        {
            if (modifiers == null) return new GridDefenseModifiers();

            return new GridDefenseModifiers
            {
                Bullet = modifiers.Bullet,
                PostShield = modifiers.PostShield,
                Duration = modifiers.Duration,
                Cooldown = modifiers.Cooldown,
                Rocket = modifiers.Rocket,
                Explosion = modifiers.Explosion,
                Environment = modifiers.Environment,
                Energy = modifiers.Energy,
                Kinetic = modifiers.Kinetic
            };
        }
        
        public static void ApplyModifiers(IMyCubeBlock block, GridModifiers modifiers)
        {
            var id = ((IMyTerminalBlock)block).BlockDefinition;
            var cubeDef = MyDefinitionManager.Static.GetCubeBlockDefinition(id);

            var thruster = block as IMyThrust;
            if (thruster != null)
            {
                if(modifiers.ThrusterForce != -1) thruster.ThrustMultiplier = modifiers.ThrusterForce;
                if(modifiers.ThrusterEfficiency != -1) thruster.PowerConsumptionMultiplier = 1f / modifiers.ThrusterEfficiency;
            }

            var gyro = block as IMyGyro;
            if (gyro != null)
            {
                if(gyro.GyroStrengthMultiplier != -1) gyro.GyroStrengthMultiplier = modifiers.GyroForce;
                if(gyro.PowerConsumptionMultiplier != -1) gyro.PowerConsumptionMultiplier = 1f / modifiers.GyroEfficiency;
            }

            var refinery = block as IMyRefinery;
            if (refinery != null && (modifiers.RefineSpeed != -1f && modifiers.RefineEfficiency != -1))
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
                                    if(up.ModifierType == MyUpgradeModifierType.Additive)
                                    {prodSum += up.Modifier;}
                                    else
                                    {prodSum *= up.Modifier;}
                                    break;
                                case "Effectiveness":
                                    if(up.ModifierType == MyUpgradeModifierType.Additive)
                                    {effSum += up.Modifier;}
                                    else
                                    {effSum *= up.Modifier;}
                                    break;
                            }
                        }
                    }
                }
                
                var prodValue = (baseSpeed * modifiers.RefineSpeed) + (prodSum * modifiers.RefineSpeed) - baseSpeed;
                var yieldValue = (baseYield * modifiers.RefineEfficiency) + (effSum * modifiers.RefineEfficiency);
                refinery.UpgradeValues["Productivity"] = prodValue;
                refinery.UpgradeValues["Effectiveness"] = yieldValue;
            }

            var assembler = block as IMyAssembler;
            if (assembler != null && modifiers.AssemblerSpeed != -1)
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
                        //I (Blue) need to fix this line:
                        // Using foreach loop
                        foreach (var up in ups)
                        {
                            if (up.UpgradeType == "Productivity")
                            {
                                if(up.ModifierType == MyUpgradeModifierType.Additive)
                                {prodSum += up.Modifier;}
                                else
                                {prodSum *= up.Modifier;}
                                
                            }
                        }
                    }
                }
                
                var prodValue = baseSpeed * modifiers.AssemblerSpeed + prodSum * modifiers.AssemblerSpeed - baseSpeed;
                assembler.UpgradeValues["Productivity"] = prodValue;
            }

            var reactor = block as IMyReactor;
            if (reactor != null)
            {
                reactor.PowerOutputMultiplier = modifiers.PowerProducersOutput;
            }

            var drill = block as IMyShipDrill;
            if (drill != null)
            {
                drill.DrillHarvestMultiplier = modifiers.DrillHarvestMultiplier;
            }
            
        }
        
        public static void GridCoreDamageHandler(object target, ref MyDamageInformation damageInfo)
        {
            var myBlock = target as IMySlimBlock;
            var cubeGrid = myBlock?.CubeGrid ?? target as IMyCubeGrid;
            if (cubeGrid == null) return;

            GridDefenseModifiers modifiers;
            if (!DefenseModifiers.TryGetValue(cubeGrid.EntityId, out modifiers)) return;

            if (damageInfo.Type == MyDamageType.Bullet)
            {
                if(damageInfo.ExtraInfo == EnergyDamageType) damageInfo.Amount *= modifiers.Energy;
                else if(damageInfo.ExtraInfo == KineticDamageType) damageInfo.Amount *= modifiers.Kinetic;
                else damageInfo.Amount *= modifiers.PostShield;
            }
            if (damageInfo.Type == MyDamageType.Rocket) damageInfo.Amount *= modifiers.Rocket;
            if (damageInfo.Type == MyDamageType.Explosion) damageInfo.Amount *= modifiers.Explosion;
            if (damageInfo.Type == MyDamageType.Environment || damageInfo.Type == MyDamageType.Deformation) damageInfo.Amount *= modifiers.Environment;
            if (damageInfo.Type == MyDamageType.Drill) damageInfo.Amount *= modifiers.PostShield;
            Utils.Log($"AttackerId: {damageInfo.AttackerId} Type: {damageInfo.Type} Amount: {damageInfo.Amount} Extra: {damageInfo.ExtraInfo}");
        }

        public static void HandleLightningExplosions(ref MyExplosionInfo explosionInfo)
        {
            var closestPlanet = MyGamePruningStructure.GetClosestPlanet(explosionInfo.ExplosionSphere.Center);
            var vanilla = closestPlanet != null &&
                          explosionInfo.VoxelCutoutScale == 0 &&
                          explosionInfo.OwnerEntity == closestPlanet;
            
            var nebula = explosionInfo.PlayerDamage == 50 && 
                         explosionInfo.Damage == 300 && 
                         explosionInfo.ExplosionType == MyExplosionTypeEnum.CUSTOM && 
                         explosionInfo.AffectVoxels == false; //For Jaks nebula mod
            
            if (!vanilla && !nebula) return;

            
            var grid = FindClosestAffectedGrid(ref explosionInfo.ExplosionSphere);
            if (grid == null) return;

            GridDefenseModifiers modifiers;
            if (!DefenseModifiers.TryGetValue(grid.EntityId, out modifiers)) return;

            explosionInfo.Damage *= modifiers.Environment;
        }

        private static MyCubeGrid FindClosestAffectedGrid(ref BoundingSphereD explosionSphere)
        {
            ExplosionEntities.Clear();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref explosionSphere, ExplosionEntities);

            MyCubeGrid closestGrid = null;
            var closestDistanceSquared = double.MaxValue;

            foreach (var entity in ExplosionEntities)
            {
                var grid = entity as MyCubeGrid;
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                var distanceSquared = Vector3D.DistanceSquared(grid.PositionComp.WorldAABB.Center, explosionSphere.Center);
                if (distanceSquared >= closestDistanceSquared) continue;

                closestDistanceSquared = distanceSquared;
                closestGrid = grid;
            }

            ExplosionEntities.Clear();
            return closestGrid;
        }
    }
}
