#region
using System;
using System.Collections.Generic;
using System.Linq;
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

#endregion

namespace ShipCoreFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false)]
    public class CoreLogic : MyGameLogicComponent, IMyEventProxy
    {
        public string _subtypeId;
        public IMyTerminalBlock _coreBlock;
        public MySync<bool, SyncDirection.BothWays> _syncIsMainCore = null;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _coreBlock = (IMyTerminalBlock)Entity;
            if (ModSessionManager.Config.ShipCores.All(core => core.SubtypeId != _coreBlock.BlockDefinition.SubtypeId)) return;
            _coreBlock.CubeGrid.OnPhysicsChanged += InitOnPhysicsChanged;
        }

        private void InitOnPhysicsChanged(IMyEntity obj)
        {
            if (ModSessionManager.Config.ShipCores.All(core => core.SubtypeId != _coreBlock.BlockDefinition.SubtypeId)) return;
            if (_coreBlock.CubeGrid?.Physics == null) return;
            _subtypeId = _coreBlock.BlockDefinition.SubtypeId;
            
            _coreBlock.OnPhysicsChanged -= InitOnPhysicsChanged; //This line does not seem to do shit 
            if (CheckIfCoreOfOtherTypeExists())
            {
                _coreBlock.Close();
                return;
            }
            if (_coreBlock.Storage != null && _coreBlock.Storage.ContainsKey(Constants.CoreStateStorageGUID))
            {
                _syncIsMainCore.Value = _coreBlock.Storage[Constants.CoreStateStorageGUID] == "1";
            }
            ///No log fours?
            var onlyCore = IsOnlyCoreOfThisTypeOnGrid();
            if (!_syncIsMainCore && onlyCore)
            {
                _syncIsMainCore.Value = true;
                _coreBlock.CubeGrid.GetMainGridLogic().Activate(_subtypeId, true);
                SaveCoreState();
            }
            
            _coreBlock.CubeGrid.OnGridMerge += OnGridMerge;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
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
            var fatTerminals = _coreBlock.CubeGrid.GetFatBlocks<IMyTerminalBlock>();
            var coreSubtypeId = ModSessionManager.Config.ShipCores.Select(core => core.SubtypeId).ToList();
            coreSubtypeId.Remove(_subtypeId);
            
            return fatTerminals.Any(terminal =>
            {
                var subtype = Utils.GetBlockSubtypeId(terminal.SlimBlock);
                return coreSubtypeId.Any(sub => sub == subtype);
            });
        }

        private void OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            List<IMyCubeGrid> ignored;
            var actualMainGrid = arg1.GetMainCubeGrid(out ignored);
            if (_coreBlock.CubeGrid.EntityId != actualMainGrid.EntityId) _coreBlock.Delete();
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (MyAPIGateway.TerminalControls == null) return;
            //Think this can be done in init/ after init just once
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

        public override void MarkForClose()
        {
            if (_coreBlock?.CubeGrid == null) return;

            var grid = _coreBlock.CubeGrid;
            var gridLogic = grid.GameLogic?.GetAs<GridLogic>();
            if (gridLogic == null) return;
            
            // If this core is NOT the main core, nothing to reassign
            if (!_syncIsMainCore)
            {
                //Anoying
                Utils.ShowNotification($"A backup core of grid {grid.CustomName} was destroyed!",10000, true);
                return;
            }
            
            // Try to find another core of the same type on the grid
            var slimBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(slimBlocks, b => b.FatBlock is IMyTerminalBlock);

            CoreLogic newMainCore = (
                from terminal in slimBlocks.Select(slim => slim.FatBlock as IMyTerminalBlock)
                where terminal != null && terminal != _coreBlock
                select terminal.GameLogic?.GetAs<CoreLogic>()
                ).FirstOrDefault(otherLogic => otherLogic != null && otherLogic._subtypeId == _subtypeId);


            if (newMainCore != null)
            {
                newMainCore._syncIsMainCore.Value = true;
                newMainCore.SaveCoreState();
                newMainCore._coreBlock.RefreshCustomInfo();
                Utils.ShowNotification($"{grid.CustomName}'s main core destroyed! Successfully switched to backup core.",10000, true);
            }
            else
            {
                // No other core of this type found — deactivate the grid
                Utils.ShowNotification($"All cores destroyed! {grid.CustomName} has become inactive!",5000, true);
                gridLogic.ActiveNoCore = false;
            }
            
            base.MarkForClose();
        }
        
        private bool IsOnlyCoreOfThisTypeOnGrid()
        {
            var fatTerminals = _coreBlock.CubeGrid.GetFatBlocks<IMyTerminalBlock>();
            return fatTerminals.Count(fatTerminal => fatTerminal.BlockDefinition.SubtypeId == _subtypeId) == 1;
        }

        private void SaveCoreState()
        {
            if (_coreBlock.Storage == null) _coreBlock.Storage = new MyModStorageComponent();
            _coreBlock.Storage[Constants.CoreStateStorageGUID] = _syncIsMainCore ? "1" : "0";
        }
    }
}