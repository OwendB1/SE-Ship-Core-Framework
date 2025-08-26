#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false)]
    public class CoreLogic : MyGameLogicComponent, IMyEventProxy
    {
        public string SubtypeId;
        public IMyBeacon CoreBlock;
        public MySync<bool, SyncDirection.BothWays> SyncIsMainCore = null;
        private MySync<ulong, SyncDirection.BothWays> _syncBoostReq = null;
        private MySync<ulong, SyncDirection.BothWays> _syncDefenseReq = null;

        private ulong _lastBoostReq;
        private ulong _lastDefenseReq;

        private static bool _actionsRegistered;

        #region Init methods

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            CoreBlock = (IMyBeacon)Entity;
            CubeGridModifiers.AddModifiers(CoreBlock);
            if (ModSessionManager.Config.ShipCores.All(core => core.SubtypeId != CoreBlock.BlockDefinition.SubtypeId)) return;
            SyncIsMainCore.ValidateAndSet(false);
            CoreBlock.CubeGrid.OnPhysicsChanged += InitOnPhysicsChanged;
        }

        private void InitOnPhysicsChanged(IMyEntity obj)
        {
            if (ModSessionManager.Config.SelectedNoCore == null)
            {
                Utils.Log("NOCORE is NULL for CORE");
                return;
            }
            if (CoreBlock.CubeGrid?.Physics == null){Utils.Log($"Missing Physics {CoreBlock.CubeGrid?.CustomName} ({CoreBlock.CubeGrid?.Physics})", 3); return;}
            CoreBlock.CubeGrid.OnPhysicsChanged -= InitOnPhysicsChanged;
            if (ModSessionManager.Config.ShipCores.All(core => core.SubtypeId != CoreBlock.BlockDefinition.SubtypeId)) return;
            SubtypeId = CoreBlock.BlockDefinition.SubtypeId;
            
            LimitRescheduler.ValidateOrSchedule(CoreBlock, CoreBlock.CubeGrid, SubtypeId);
            
            if (CheckIfCoreOfOtherTypeExists())
            {
                Utils.Log($"Other Core Exist: {CoreBlock.CubeGrid.CustomName}", 3);
                //This crashes the game
                CoreBlock.Delete();
                return;
            }
            
            if (CoreBlock.Storage != null && CoreBlock.Storage.ContainsKey(Constants.CoreStateStorageGUID))
            {
                var syncVar = CoreBlock.Storage[Constants.CoreStateStorageGUID] == "1";
                SyncIsMainCore.ValidateAndSet(syncVar);
            }

            var onlyCore = IsOnlyCoreOfThisTypeOnGrid();
            Utils.Log($"Core Initial: {CoreBlock.CustomName}, SyncValue: {!SyncIsMainCore.Value}, onlyCore: {onlyCore}", 3);
            if ((!SyncIsMainCore.Value && onlyCore)||(SyncIsMainCore.Value))
            {
                SyncIsMainCore.ValidateAndSet(true);
                CoreBlock.CubeGrid.GetMainGridLogic().Activate(SubtypeId);
                SaveCoreState();
            }
            
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            Utils.Log($"Core Initial: {CoreBlock.CustomName}", 3);
        }

        #endregion

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (MyAPIGateway.TerminalControls == null) return;

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            CoreBlock.CubeGrid.OnGridMerge += OnGridMerge;
            RegisterToolbarActionsOnce();
            LimitRescheduler.Tick(CoreBlock);
        }
        
        public override void UpdateAfterSimulation10()
        {
            LimitRescheduler.Tick(CoreBlock);
            UpdateBeacon();
        }
        
        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            if (!Constants.IsServer || CoreBlock?.CubeGrid == null) return;
            
            var core = Utils.GetGridCore(CoreBlock.CubeGrid, null);
            if (_syncBoostReq.Value != _lastBoostReq)
            {
                _lastBoostReq = _syncBoostReq.Value;
                if (core != null && core.SyncIsMainCore.Value)
                    CoreBlock.CubeGrid.GetMainGridLogic()?.ActivateBoost();
            }

            if (_syncDefenseReq.Value == _lastDefenseReq) return;
            _lastDefenseReq = _syncDefenseReq.Value;
            if (core != null && core.SyncIsMainCore.Value)
                CoreBlock.CubeGrid.GetMainGridLogic()?.ActivateDefense();
        }
        
        public override void Close()
        {
            if (ModSessionManager.Config.SelectedNoCore == null) return;
            if (CoreBlock?.CubeGrid == null) return;
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            CoreBlock.CubeGrid.OnGridMerge -= OnGridMerge;
            
            var grid = CoreBlock.CubeGrid;
            var gridLogic = grid.GameLogic?.GetAs<GridLogic>();
            if (gridLogic == null) return;
            
            // If this core is NOT the main core, nothing to reassign
            if (!SyncIsMainCore.Value)
            {
                //Anoying
                if(Constants.LocalPlayer!=null && (Constants.LocalPlayer.PlayerID==grid.BigOwners.FirstOrDefault())){Utils.ShowNotification($"A backup core of grid {grid.CustomName} was destroyed!",10000, true);}
                return;
            }
            
            // Try to find another core of the same type on the grid
            var slimBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(slimBlocks, b => b.FatBlock is IMyTerminalBlock);

            var newMainCore = (
                from terminal in slimBlocks.Select(slim => slim.FatBlock as IMyTerminalBlock)
                where terminal != null && terminal != CoreBlock
                select terminal.GameLogic?.GetAs<CoreLogic>()
                ).FirstOrDefault(otherLogic => otherLogic != null && otherLogic.SubtypeId == SubtypeId);


            if (newMainCore != null)
            {
                newMainCore.SyncIsMainCore.ValidateAndSet(true);
                newMainCore.SaveCoreState();
                newMainCore.CoreBlock.RefreshCustomInfo();
                 if(Constants.LocalPlayer!=null && (Constants.LocalPlayer.PlayerID==grid.BigOwners.FirstOrDefault())){Utils.ShowNotification($"{grid.CustomName}'s main core destroyed! Successfully switched to backup core.",10000, true);}
            }
            else
            {
                // No other core of this type found — reset the grid to no core
                 if(Constants.LocalPlayer!=null && (Constants.LocalPlayer.PlayerID==grid.BigOwners.FirstOrDefault())){Utils.ShowNotification($"All cores destroyed! {grid.CustomName} has become inactive!",5000, true);}
                gridLogic.ResetCore();
            }
            
            base.Close();
        }

        #region Event Delegates
        
        private void OnUpgradeValuesChanged()
        {
            var AssemblerSpeed = CoreBlock.UpgradeValues["AssemblerSpeed"];
            var DrillHarvestMultiplier = CoreBlock.UpgradeValues["DrillHarvestMultiplier"];
            //so on and so forth more of an example, you would likely want to call these in the modifiers file....
        }
        private void OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            List<IMyCubeGrid> ignored;
            var actualMainGrid = arg1.GetMainCubeGrid(out ignored);
            if (CoreBlock.CubeGrid.EntityId != actualMainGrid.EntityId) CoreBlock.Delete();
        }
        
        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (!Constants.IsClient) return;
            var logic = block?.GameLogic?.GetAs<CoreLogic>();
            if (logic == null|| MyAPIGateway.TerminalControls == null) return;

            const string controlId = "MainCoreCheckbox";
            if (controls.Any(t => t.Id == controlId))
            {
                return;
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

        #endregion

        #region Helper methods

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
        
        private void RegisterToolbarActionsOnce()
        {
            if (_actionsRegistered) return;
            _actionsRegistered = true;

            var boost = MyAPIGateway.TerminalControls.CreateAction<IMyBeacon>("ShipCore_ActivateBoost");
            boost.Name = new StringBuilder("Activate Boost");
            boost.Icon = @"Textures\BoostButton_Sad_Static.png";
            boost.ValidForGroups = false;
            boost.Action = b => { var l = b?.GameLogic?.GetAs<CoreLogic>(); l?.TriggerBoostFromClient(); };
            MyAPIGateway.TerminalControls.AddAction<IMyBeacon>(boost);

            var defense = MyAPIGateway.TerminalControls.CreateAction<IMyBeacon>("ShipCore_ActivateDefense");
            defense.Name = new StringBuilder("Activate Defense");
            defense.Icon = @"Textures\BoostButton_Sad_Static.png";
            defense.ValidForGroups = false;
            defense.Action = b => { var l = b?.GameLogic?.GetAs<CoreLogic>(); l?.TriggerDefenseFromClient(); };
            MyAPIGateway.TerminalControls.AddAction<IMyBeacon>(defense);
        }

        private void UpdateBeacon()
        {
            var coreTypeDef = CoreBlock.CubeGrid.GetMainGridLogic().ShipCore;
            if (coreTypeDef.ForceBroadCast == false) return; 
            CoreBlock.Enabled = true; 
            CoreBlock.Radius = coreTypeDef.ForceBroadCastRange; 
            if(!CoreBlock.HudText.Contains(coreTypeDef.UniqueName)) CoreBlock.HudText = $"{CoreBlock.CubeGrid.DisplayName} | {coreTypeDef.UniqueName}";
        }
        
        private void TriggerBoostFromClient()
        {
            if (CoreBlock?.CubeGrid == null) return;
            if (!SyncIsMainCore.Value) { if (Constants.IsClient) Utils.ShowNotification("Only the main core can trigger boost.", 1000); return; }
            if (Constants.IsServer) CoreBlock.CubeGrid.GetMainGridLogic()?.ActivateBoost();
            else _syncBoostReq.Value = _syncBoostReq.Value + 1;
        }

        private void TriggerDefenseFromClient()
        {
            if (CoreBlock?.CubeGrid == null) return;
            if (!SyncIsMainCore.Value) { if (Constants.IsClient) Utils.ShowNotification("Only the main core can trigger defense.", 1000); return; }
            if (Constants.IsServer) CoreBlock.CubeGrid.GetMainGridLogic()?.ActivateDefense();
            else _syncDefenseReq.Value = _syncDefenseReq.Value + 1;
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

        #endregion
    }
}