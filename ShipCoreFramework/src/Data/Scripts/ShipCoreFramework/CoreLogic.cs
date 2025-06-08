using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;

namespace ShipCoreFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false)]
    public class CoreLogic : MyGameLogicComponent, IMyEventProxy
    {
        private string _subtypeId;
        private IMyTerminalBlock _coreBlock;
        private MySync<bool, SyncDirection.BothWays> _syncIsMainCore;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _coreBlock = Entity as IMyTerminalBlock;
            
            if (_coreBlock == null) return;
            _subtypeId = _coreBlock.BlockDefinition.SubtypeId;
            
            if (CheckIfCoreOfOtherTypeExists())
            {
                _coreBlock.Delete();
                return;
            }
            
            if (_coreBlock == null) return;

            if (_coreBlock.Storage != null && _coreBlock.Storage.ContainsKey(Constants.CoreStateStorageGUID))
            {
                _syncIsMainCore.Value = _coreBlock.Storage[Constants.CoreStateStorageGUID] == "1";
            }
            
            if (!_syncIsMainCore && IsOnlyCoreOfThisTypeOnGrid())
            {
                _syncIsMainCore.Value = true;
                _coreBlock.CubeGrid.GetMainGridLogic().Activate(_subtypeId);
                SaveCoreState();
            }
            
            // Grab MyThruster
            _coreBlock.CubeGrid.OnGridMerge += OnGridMerge;
            
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
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
            
            _coreBlock.RefreshCustomInfo();
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

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (MyAPIGateway.TerminalControls == null) return;
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
        }

        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (!Constants.IsClient) return;
            var logic = block?.GameLogic?.GetAs<CoreLogic>();
            if (logic == null) return;

            if (MyAPIGateway.TerminalControls == null) return;

            const string controlId = "MainCoreCheckbox";

            if (controls.Any(t => t.Id == controlId))
            {
                return; // Avoid duplicates
            }

            var checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>(controlId);
            checkbox.Title = MyStringId.GetOrCompute("Main Core");
            checkbox.Tooltip = MyStringId.GetOrCompute("Mark this core as the main core for the grid.");
            checkbox.SupportsMultipleBlocks = false;

            checkbox.Getter = delegate(IMyTerminalBlock b)
            {
                var l = b.GameLogic?.GetAs<CoreLogic>();
                return l != null && l._syncIsMainCore;
            };

            checkbox.Setter = delegate(IMyTerminalBlock b, bool val)
            {
                if (!Constants.IsServer) return;
                var l = b.GameLogic?.GetAs<CoreLogic>();
                if (l == null) return;
                if (!val) return; // Unchecking not supported

                var grid = b.CubeGrid;
                var fatBlocks = new List<IMySlimBlock>();
                grid.GetBlocks(fatBlocks, slim => slim.FatBlock is IMyTerminalBlock);

                foreach (var t in fatBlocks)
                {
                    var terminal = t.FatBlock as IMyTerminalBlock;
                    if (terminal == null || terminal == b) continue;

                    var otherLogic = terminal.GameLogic?.GetAs<CoreLogic>();
                    if (otherLogic == null || !otherLogic._syncIsMainCore) continue;
                    otherLogic._syncIsMainCore.Value = false;
                    terminal.RefreshCustomInfo();
                }

                l._syncIsMainCore.Value = true;
                b.RefreshCustomInfo();
            };

            checkbox.Enabled = delegate(IMyTerminalBlock b)
            {
                var l = b.GameLogic?.GetAs<CoreLogic>();
                return l != null && !l._syncIsMainCore;
            };

            controls.Add(checkbox);
        }

        private void OnUpgradeValuesChanged()
        {
            var AssemblerSpeed = _coreBlock.UpgradeValues["AssemblerSpeed"];
            var DrillHarvestMultiplier = _coreBlock.UpgradeValues["DrillHarvestMultiplier"];
            //so on and so forth more of an example, you would likely want to call these in the modifiers file....
        }

        public override void Close()
        {
            if (_coreBlock?.CubeGrid == null) return;

            var grid = _coreBlock.CubeGrid;
            var gridLogic = grid.GameLogic?.GetAs<GridLogic>();
            if (gridLogic == null) return;

            // If this core is NOT the main core, nothing to reassign
            if (!_syncIsMainCore)
            {
                Utils.ShowNotification($"A backup core of grid {grid.CustomName} was destroyed!",0, true);
                return;
            }

            // Try to find another core of the same type on the grid
            var slimBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(slimBlocks, b => b.FatBlock is IMyTerminalBlock);

            var newMainCore = (
                from terminal in slimBlocks.Select(slim => slim.FatBlock as IMyTerminalBlock) 
                where terminal != _coreBlock 
                select terminal.GameLogic?.GetAs<CoreLogic>())
                .FirstOrDefault(otherLogic => otherLogic._subtypeId == _subtypeId);

            if (newMainCore != null)
            {
                newMainCore._syncIsMainCore.Value = true;
                newMainCore.SaveCoreState();
                newMainCore._coreBlock.RefreshCustomInfo();
                Utils.ShowNotification($"{grid.CustomName}'s main core destroyed! Successfully switched to backup core.",0, true);
            }
            else
            {
                // No other core of this type found — deactivate the grid
                Utils.ShowNotification($"All cores destroyed! {grid.CustomName} has become inactive!",5000, true);
                gridLogic.IsActive = false;
            }
        }
        
        private bool IsOnlyCoreOfThisTypeOnGrid()
        {
            var slimBlocks = new List<IMySlimBlock>();
            _coreBlock.CubeGrid.GetBlocks(slimBlocks, b => b.FatBlock is IMyTerminalBlock);

            return (from block in slimBlocks.Select(slim => slim.FatBlock as IMyTerminalBlock) 
                where block != _coreBlock 
                select block.GameLogic?.GetAs<CoreLogic>())
                .All(logic => logic._subtypeId != _coreBlock.BlockDefinition.SubtypeId);
        }

        private void SaveCoreState()
        {
            if (_coreBlock.Storage == null) _coreBlock.Storage = new MyModStorageComponent();
            _coreBlock.Storage[Constants.CoreStateStorageGUID] = _syncIsMainCore ? "1" : "0";
        }
    }
}