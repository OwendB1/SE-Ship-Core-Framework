using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;

namespace ShipCoreFramework
{
    internal static partial class CubeGridModifiers
    {
        private const string UpgradeModuleLinkType = "ShipCoreLink";

        internal static void RegisterUpgradeModuleLink(IMyCubeBlock block)
        {
            if (block == null) return;
            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate
                {
                    if (block == null || block.MarkedForClose || block.Closed || Session.IsShuttingDown) return;
                    RegisterUpgradeModuleLink(block);
                });
                return;
            }

            block.AddUpgradeValue(UpgradeModuleLinkType, 0f);
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

    }
}
