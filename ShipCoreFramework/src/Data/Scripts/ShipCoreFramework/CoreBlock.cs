using ProtoBuf;
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
using ShipCoreFramework;

namespace MyCoreBlock
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false)]
    public class MyCoreBlock : MyGameLogicComponent
    {
        IMyTerminalBlock CoreBlock;
        string SubtypeId;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            // Grab MyThruster
            CoreBlock = Entity as IMyTerminalBlock;

            if (CoreBlock != null)
            {
                SubtypeId = CoreBlock.BlockDefinition.SubtypeId;
                foreach(ShipCore ShipClass in ModSessionManager.Config.ShipCores)
                {
                    if (SubtypeId.Contains(ShipClass.SubtypeId))
                    {
                        NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
                        CoreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
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
                        
                        return;
                    }
                }
                NeedsUpdate=MyEntityUpdateEnum.NONE;
            }
        }
        public override void UpdateAfterSimulation()
        {

        }
        private void OnUpgradeValuesChanged()
        {
			var AssemblerSpeed =CoreBlock.UpgradeValues["AssemblerSpeed"];
            var DrillHarvestMultiplier =CoreBlock.UpgradeValues["DrillHarvestMultiplier"];
            //so on and so forth more of an example, you would likely want to call these in the modifiers file....
            
        }
    }
}