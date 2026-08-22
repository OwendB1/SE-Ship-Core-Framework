using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using IngameIMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;

namespace ShipCoreFramework
{
    internal static partial class CubeGridModifiers
    {
        private const string UpgradeModuleLinkType = "ShipCoreLink";
        private const string ProductivityUpgradeType = "Productivity";
        private const string EffectivenessUpgradeType = "Effectiveness";

        private sealed class ProductionModifierState
        {
            internal readonly IMyCubeBlock Block;
            internal GridModifiers Modifiers;
            internal Action UpgradeValuesChanged;
            internal Action<IngameIMyEntity> MarkedForClose;

            internal ProductionModifierState(IMyCubeBlock block, GridModifiers modifiers)
            {
                Block = block;
                Modifiers = modifiers;
            }
        }

        private static readonly Dictionary<long, ProductionModifierState> ProductionModifierStates =
            new Dictionary<long, ProductionModifierState>();

        private static readonly HashSet<long> ProductionModifiersBeingApplied = new HashSet<long>();

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
            IMyTerminalBlock terminalBlock = block as IMyTerminalBlock;
            if (terminalBlock == null || modifiers == null) return;

            MyCubeBlockDefinition cubeDef =
                MyDefinitionManager.Static.GetCubeBlockDefinition(terminalBlock.BlockDefinition);

            IMyThrust thruster = block as IMyThrust;
            if (thruster != null)
            {
                if (modifiers.ThrusterForce != -1) thruster.ThrustMultiplier = modifiers.ThrusterForce;
                if (modifiers.ThrusterEfficiency != -1)
                    thruster.PowerConsumptionMultiplier = 1f / modifiers.ThrusterEfficiency;
            }

            IMyGyro gyro = block as IMyGyro;
            if (gyro != null)
            {
                if (modifiers.GyroForce != -1) gyro.GyroStrengthMultiplier = modifiers.GyroForce;
                if (modifiers.GyroEfficiency != -1)
                    gyro.PowerConsumptionMultiplier = 1f / modifiers.GyroEfficiency;
            }

            IMyRefinery refinery = block as IMyRefinery;
            IMyAssembler assembler = block as IMyAssembler;
            if (refinery != null || assembler != null)
            {
                RegisterProductionModifierState(block, modifiers);
                ApplyProductionModifiers(block, modifiers, cubeDef);
            }

            IMyReactor reactor = block as IMyReactor;
            if (reactor != null && modifiers.PowerProducersOutput != -1)
            {
                reactor.PowerOutputMultiplier = modifiers.PowerProducersOutput;
            }

            IMyShipDrill drill = block as IMyShipDrill;
            if (drill != null && modifiers.DrillHarvestMultiplier != -1)
            {
                drill.DrillHarvestMultiplier = modifiers.DrillHarvestMultiplier;
            }
        }

        internal static void UnregisterProductionModifierState(IMyCubeBlock block)
        {
            if (block == null) return;

            ProductionModifierState state;
            if (!ProductionModifierStates.TryGetValue(block.EntityId, out state)) return;

            ProductionModifierStates.Remove(block.EntityId);
            ProductionModifiersBeingApplied.Remove(block.EntityId);
            block.OnUpgradeValuesChanged -= state.UpgradeValuesChanged;
            block.OnMarkForClose -= state.MarkedForClose;
        }

        private static void RegisterProductionModifierState(IMyCubeBlock block, GridModifiers modifiers)
        {
            ProductionModifierState state;
            if (ProductionModifierStates.TryGetValue(block.EntityId, out state))
            {
                state.Modifiers = modifiers;
                return;
            }

            state = new ProductionModifierState(block, modifiers);
            state.UpgradeValuesChanged = delegate { OnProductionUpgradeValuesChanged(state); };
            state.MarkedForClose = delegate { UnregisterProductionModifierState(block); };
            ProductionModifierStates.Add(block.EntityId, state);
            block.OnUpgradeValuesChanged += state.UpgradeValuesChanged;
            block.OnMarkForClose += state.MarkedForClose;
        }

        private static void OnProductionUpgradeValuesChanged(ProductionModifierState state)
        {
            if (state == null || state.Block == null || state.Modifiers == null) return;

            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate { OnProductionUpgradeValuesChanged(state); });
                return;
            }

            ProductionModifierState registeredState;
            if (state.Block.MarkedForClose || state.Block.Closed || Session.IsShuttingDown ||
                !ProductionModifierStates.TryGetValue(state.Block.EntityId, out registeredState) ||
                !ReferenceEquals(state, registeredState) ||
                ProductionModifiersBeingApplied.Contains(state.Block.EntityId))
                return;

            IMyTerminalBlock terminalBlock = state.Block as IMyTerminalBlock;
            if (terminalBlock == null) return;

            MyCubeBlockDefinition cubeDef =
                MyDefinitionManager.Static.GetCubeBlockDefinition(terminalBlock.BlockDefinition);
            ApplyProductionModifiers(state.Block, state.Modifiers, cubeDef);
        }

        private static void ApplyProductionModifiers(IMyCubeBlock block, GridModifiers modifiers,
            MyCubeBlockDefinition cubeDef)
        {
            if (!ProductionModifiersBeingApplied.Add(block.EntityId)) return;

            try
            {
                float nativeProductivity;
                float nativeEffectiveness;
                GetNativeProductionUpgradeValues((MyCubeBlock)block, out nativeProductivity,
                    out nativeEffectiveness);

                bool changed = false;
                IMyRefinery refinery = block as IMyRefinery;
                if (refinery != null)
                {
                    MyRefineryDefinition refineryDefinition = cubeDef as MyRefineryDefinition;
                    float baseSpeed = refineryDefinition != null ? refineryDefinition.RefineSpeed : 1f;

                    if (modifiers.RefineSpeed != -1f)
                    {
                        float productivityValue =
                            (baseSpeed + nativeProductivity) * modifiers.RefineSpeed - baseSpeed;
                        changed |= SetUpgradeValue(refinery.UpgradeValues, ProductivityUpgradeType,
                            productivityValue);
                    }

                    if (modifiers.RefineEfficiency != -1f)
                    {
                        float effectivenessValue = nativeEffectiveness * modifiers.RefineEfficiency;
                        changed |= SetUpgradeValue(refinery.UpgradeValues, EffectivenessUpgradeType,
                            effectivenessValue);
                    }
                }

                IMyAssembler assembler = block as IMyAssembler;
                if (assembler != null && modifiers.AssemblerSpeed != -1f)
                {
                    MyAssemblerDefinition assemblerDefinition = cubeDef as MyAssemblerDefinition;
                    float baseSpeed = assemblerDefinition != null ? assemblerDefinition.AssemblySpeed : 1f;
                    float productivityValue =
                        (baseSpeed + nativeProductivity) * modifiers.AssemblerSpeed - baseSpeed;
                    changed |= SetUpgradeValue(assembler.UpgradeValues, ProductivityUpgradeType,
                        productivityValue);
                }

                if (changed) ((MyCubeBlock)block).CommitUpgradeValues();
            }
            finally
            {
                ProductionModifiersBeingApplied.Remove(block.EntityId);
            }
        }

        private static void GetNativeProductionUpgradeValues(MyCubeBlock block, out float productivity,
            out float effectiveness)
        {
            // Keen stores productivity as a delta from definition speed, but effectiveness as a multiplier from 1.
            productivity = 0f;
            effectiveness = 1f;

            Dictionary<long, MyCubeBlock.AttachedUpgradeModule> attachedModules =
                block.CurrentAttachedUpgradeModules;
            if (attachedModules == null) return;

            foreach (KeyValuePair<long, MyCubeBlock.AttachedUpgradeModule> pair in attachedModules)
            {
                MyCubeBlock.AttachedUpgradeModule attachedModule = pair.Value;
                IMyUpgradeModule upgradeBlock = attachedModule != null ? attachedModule.Block : null;
                if (upgradeBlock == null || !upgradeBlock.IsWorking || !attachedModule.Compatible ||
                    attachedModule.SlotCount <= 0)
                    continue;

                List<MyUpgradeModuleInfo> upgrades = new List<MyUpgradeModuleInfo>();
                upgradeBlock.FillUpgradeList(upgrades);
                for (int slot = 0; slot < attachedModule.SlotCount; slot++)
                {
                    for (int index = 0; index < upgrades.Count; index++)
                    {
                        MyUpgradeModuleInfo upgrade = upgrades[index];
                        if (upgrade.UpgradeType == ProductivityUpgradeType)
                            productivity = ApplyKeenUpgradeValue(productivity, upgrade);
                        else if (upgrade.UpgradeType == EffectivenessUpgradeType)
                            effectiveness = ApplyKeenUpgradeValue(effectiveness, upgrade);
                    }
                }
            }
        }

        private static float ApplyKeenUpgradeValue(float currentValue, MyUpgradeModuleInfo upgrade)
        {
            return upgrade.ModifierType == MyUpgradeModifierType.Additive
                ? currentValue + upgrade.Modifier
                : currentValue * upgrade.Modifier;
        }

        private static bool SetUpgradeValue(Dictionary<string, float> values, string name, float value)
        {
            float currentValue;
            if (values.TryGetValue(name, out currentValue) && currentValue == value) return false;

            values[name] = value;
            return true;
        }
    }
}
