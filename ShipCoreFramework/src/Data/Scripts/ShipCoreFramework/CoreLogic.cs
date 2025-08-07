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
        public string SubtypeId;
        public IMyTerminalBlock CoreBlock;
        public MySync<bool, SyncDirection.BothWays> SyncIsMainCore = null;//Default is true.

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            CoreBlock = (IMyTerminalBlock)Entity;
            if (ModSessionManager.Config.ShipCores.All(core => core.SubtypeId != CoreBlock.BlockDefinition.SubtypeId)) return;
            SyncIsMainCore.ValidateAndSet(false);
            CoreBlock.CubeGrid.OnPhysicsChanged += InitOnPhysicsChanged;
        }

        private void InitOnPhysicsChanged(IMyEntity obj)
        {
            if (CoreBlock.CubeGrid?.Physics == null){Utils.Log($"Missing Physics {CoreBlock.CubeGrid?.CustomName} ({CoreBlock.CubeGrid?.Physics})", 3); return;}//is this is?
            CoreBlock.OnPhysicsChanged -= InitOnPhysicsChanged;//Either way we want the triger if the block now exist.
            if (ModSessionManager.Config.ShipCores.All(core => core.SubtypeId != CoreBlock.BlockDefinition.SubtypeId)) return;
            SubtypeId = CoreBlock.BlockDefinition.SubtypeId;
            
            if (!GridsPerFactionClassManager.WillGridBeWithinFactionLimits(CoreBlock.CubeGrid.GetMainGridLogic(), SubtypeId))
            {
                Utils.Log("Per faction limit of this core has been hit!", 3);
                CoreBlock.Delete();
                return;
            }

            if (!GridsPerPlayerClassManager.WillGridBeWithinPlayerLimits(CoreBlock.CubeGrid.GetMainGridLogic(), SubtypeId))
            {
                Utils.Log("Per player limit of this core has been hit!", 3);
                CoreBlock.Delete();
                return;
            }
            
            if (CheckIfCoreOfOtherTypeExists())
            {
                Utils.Log($"Other Core Exist: {CoreBlock.CubeGrid.CustomName}", 3);
                CoreBlock.Delete();
                return;
            }
            
            if (CoreBlock.Storage != null && CoreBlock.Storage.ContainsKey(Constants.CoreStateStorageGUID))
            {
                var syncVar = CoreBlock.Storage[Constants.CoreStateStorageGUID] == "1";
                SyncIsMainCore.ValidateAndSet(syncVar);
            }
            ///No log fours?
            var onlyCore = IsOnlyCoreOfThisTypeOnGrid();
            Utils.Log($"Core Initial: {CoreBlock.CustomName}, SyncValue: {!SyncIsMainCore.Value}, onlyCore: {onlyCore}", 3);
            if ((!SyncIsMainCore.Value && onlyCore)||(SyncIsMainCore.Value))
            {
                SyncIsMainCore.ValidateAndSet(true);
                CoreBlock.CubeGrid.GetMainGridLogic().Activate(SubtypeId);
                SaveCoreState();
            }
            Utils.Log($"Core Initial: {CoreBlock.CustomName}", 3);
            CoreBlock.CubeGrid.OnGridMerge += OnGridMerge;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
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
        }

        private bool CheckIfCoreOfOtherTypeExists()
        {
            var fatTerminals = CoreBlock.CubeGrid.GetFatBlocks<IMyTerminalBlock>();
            var coreSubtypeId = ModSessionManager.Config.ShipCores.Select(core => core.SubtypeId).ToList();
            coreSubtypeId.Remove(SubtypeId);
            
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
            if (CoreBlock.CubeGrid.EntityId != actualMainGrid.EntityId) CoreBlock.Delete();
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
                return l != null && l.SyncIsMainCore.Value;
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
                    if (otherLogic == null || !otherLogic.SyncIsMainCore.Value) continue;
                    otherLogic.SyncIsMainCore.ValidateAndSet(false);
                    terminal.RefreshCustomInfo();
                }

                l.SyncIsMainCore.Value = true;
                b.RefreshCustomInfo();
            };

            checkbox.Enabled = delegate(IMyTerminalBlock b)
            {
                var l = b.GameLogic?.GetAs<CoreLogic>();
                return l != null && !l.SyncIsMainCore.Value;
            };

            controls.Add(checkbox);
        }

        private void OnUpgradeValuesChanged()
        {
            var AssemblerSpeed = CoreBlock.UpgradeValues["AssemblerSpeed"];
            var DrillHarvestMultiplier = CoreBlock.UpgradeValues["DrillHarvestMultiplier"];
            //so on and so forth more of an example, you would likely want to call these in the modifiers file....
        }

        public override void Close()
        {
            if (CoreBlock?.CubeGrid == null) return;

            var grid = CoreBlock.CubeGrid;
            var gridLogic = grid.GameLogic?.GetAs<GridLogic>();
            if (gridLogic == null) return;
            
            // If this core is NOT the main core, nothing to reassign
            if (!SyncIsMainCore.Value)
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
                where terminal != null && terminal != CoreBlock
                select terminal.GameLogic?.GetAs<CoreLogic>()
                ).FirstOrDefault(otherLogic => otherLogic != null && otherLogic.SubtypeId == SubtypeId);


            if (newMainCore != null)
            {
                newMainCore.SyncIsMainCore.ValidateAndSet(true);
                newMainCore.SaveCoreState();
                newMainCore.CoreBlock.RefreshCustomInfo();
                Utils.ShowNotification($"{grid.CustomName}'s main core destroyed! Successfully switched to backup core.",10000, true);
            }
            else
            {
                // No other core of this type found — reset the grid to no core
                Utils.ShowNotification($"All cores destroyed! {grid.CustomName} has become inactive!",5000, true);
                gridLogic.ResetCore();
            }
            
            base.Close();
        }
        
        private bool IsOnlyCoreOfThisTypeOnGrid()
        {
            var fatTerminals = CoreBlock.CubeGrid.GetFatBlocks<IMyTerminalBlock>();
            return fatTerminals.Count(fatTerminal => fatTerminal.BlockDefinition.SubtypeId == SubtypeId) == 1;
        }
        private void SaveCoreState()
        {
            if (CoreBlock.Storage == null) CoreBlock.Storage = new MyModStorageComponent();
            CoreBlock.Storage[Constants.CoreStateStorageGUID] = SyncIsMainCore ? "1" : "0";
        }
    }
}