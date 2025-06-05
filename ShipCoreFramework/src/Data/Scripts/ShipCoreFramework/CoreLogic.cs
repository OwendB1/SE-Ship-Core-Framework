using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using ShipCoreFramework;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace MyCoreBlock
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false)]
    public class CoreLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _coreBlock;
        private string _subtypeId;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            //TODO: 
            // - Add check against other core types
            // - Add main core option and auto set this core as main if first core on grid
            // - 

            if (CheckIfCoreOfOtherTypeExists())
            {
                _coreBlock.Delete();
                return;
            }
            // Grab MyThruster
            _coreBlock = Entity as IMyTerminalBlock;
            _coreBlock.CubeGrid.OnGridMerge += OnGridMerge;
            
            if (_coreBlock == null) return;
            _subtypeId = _coreBlock.BlockDefinition.SubtypeId;
            
            NeedsUpdate = MyEntityUpdateEnum.NONE;
            ActivationCheck(_coreBlock.CubeGrid.EntityId);
            
            if (!ModSessionManager.Config.ShipCores.Any(shipClass => _subtypeId.Contains(shipClass.UniqueName))) return;
            
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
            _coreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            _coreBlock.AddUpgradeValue("AssemblerSpeed", 1f);
            _coreBlock.AddUpgradeValue("DrillHarvestMultiplier", 1f);
            _coreBlock.AddUpgradeValue("GyroEfficiency", 1f);
            _coreBlock.AddUpgradeValue("GyroForce", 1f);
            _coreBlock.AddUpgradeValue("PowerProducersOutput", 1f);
            _coreBlock.AddUpgradeValue("RefineEfficiency", 1f);
            _coreBlock.AddUpgradeValue("RefineSpeed", 1f);
            _coreBlock.AddUpgradeValue("ThrusterEfficiency", 1f);
            _coreBlock.AddUpgradeValue("ThrusterForce", 1f);
            /*
                    CoreBlock.AddUpgradeValue("MaxBlocks", 1f);
                    CoreBlock.AddUpgradeValue("MaxMass", 1f);
                    CoreBlock.AddUpgradeValue("MaxPCU", 1f);
                    */
            _coreBlock.AddUpgradeValue("MaxSpeed", 1f);
            _coreBlock.AddUpgradeValue("MaxBoost", 1f);
            _coreBlock.AddUpgradeValue("BoostDuration", 1f);
            _coreBlock.AddUpgradeValue("BoostCoolDown", 1f);

            _coreBlock.AddUpgradeValue("ReloadModifier", 1f);

            _coreBlock.AddUpgradeValue("PassiveBulletDamage", 1f);
            _coreBlock.AddUpgradeValue("PassiveRocketDamage", 1f);
            _coreBlock.AddUpgradeValue("PassiveExplosionDamage", 1f);
            _coreBlock.AddUpgradeValue("PassiveEnvironmentDamage", 1f);
            _coreBlock.AddUpgradeValue("PassiveEnergyDamage", 1f);
            _coreBlock.AddUpgradeValue("PassiveKineticDamage", 1f);

            _coreBlock.AddUpgradeValue("ActiveBulletDamage", 1f);
            _coreBlock.AddUpgradeValue("ActiveRocketDamage", 1f);
            _coreBlock.AddUpgradeValue("ActiveExplosionDamage", 1f);
            _coreBlock.AddUpgradeValue("ActiveEnvironmentDamage", 1f);
            _coreBlock.AddUpgradeValue("ActiveEnergyDamage", 1f);
            _coreBlock.AddUpgradeValue("ActiveKineticDamage", 1f);
            _coreBlock.AddUpgradeValue("DurationDuration", 1f);
            _coreBlock.AddUpgradeValue("DamageCooldown", 1f);
        }

        private bool CheckIfCoreOfOtherTypeExists()
        {
            var fatTerminals = _coreBlock.CubeGrid.GetFatBlocks<MyTerminalBlock>();
            return fatTerminals.Select(fatTerminal => fatTerminal.GameLogic.GetAs<CoreLogic>()).Any(otherCoreLogic => otherCoreLogic._subtypeId != _subtypeId);
        }

        private void OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            List<IMyCubeGrid> ignored;
            var actualMainGrid = arg1.GetMainCubeGrid(out ignored);
            if (_coreBlock.CubeGrid.EntityId != actualMainGrid.EntityId) _coreBlock.Delete();
        }

        private void ActivationCheck(long entityId)
        {
            var gridLogic = _coreBlock.CubeGrid.GameLogic.GetAs<GridLogic>();
            
        }

        public override void UpdateAfterSimulation()
        {
            
        }

        private void OnUpgradeValuesChanged()
        {
            var AssemblerSpeed = _coreBlock.UpgradeValues["AssemblerSpeed"];
            var DrillHarvestMultiplier = _coreBlock.UpgradeValues["DrillHarvestMultiplier"];
            //so on and so forth more of an example, you would likely want to call these in the modifiers file....
        }

        public override void Close()
        {
            var grid = _coreBlock.CubeGrid;
            var gridLogic = _coreBlock.CubeGrid.GameLogic.GetAs<GridLogic>();
            
        }
    }
}