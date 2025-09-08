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
        private bool _hasPhysics;
        
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
            if (CoreBlock.CubeGrid?.Physics == null) return;
            _hasPhysics = true;
            CoreBlock.CubeGrid.OnPhysicsChanged -= InitOnPhysicsChanged;
            if (ModSessionManager.Config.ShipCores.All(core => core.SubtypeId != CoreBlock.BlockDefinition.SubtypeId)) return;
            SubtypeId = CoreBlock.BlockDefinition.SubtypeId;
            
            if (CoreBlock.Storage != null && CoreBlock.Storage.ContainsKey(Constants.CoreStateStorageGUID))
            {
                var syncVar = CoreBlock.Storage[Constants.CoreStateStorageGUID] == "1";
                SyncIsMainCore.ValidateAndSet(syncVar);
            }
            
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            Utils.Log($"Core Initial: {CoreBlock.CustomName}", 3);
        }

        #endregion

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            //LimitRescheduler.ValidateOrSchedule(CoreBlock, CoreBlock.CubeGrid, SubtypeId);
            if (CheckIfCoreOfOtherTypeExists())
            {
                Utils.Log($"Other Core Exist: {CoreBlock.CubeGrid.CustomName}", 3);
                //This crashes the game
                CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock, true);
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == CoreBlock.CubeGrid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification($"Other Core Type Exist On Grid", 10000, true);
                }
                return;
            }
            if (CoreBlock.OwnerId != CoreBlock.CubeGrid.BigOwners.FirstOrDefault())
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == CoreBlock.OwnerId)
                {
                    Utils.ShowNotification("Cores can only be built by the grid owner!", 10000, true);
                }
                CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock, true);
                return;
            }
            var onlyCore = IsOnlyCoreOfThisTypeOnGrid();
            var mainGridLogic = CoreBlock.CubeGrid.GetMainGridLogic();
            Utils.Log($"Core Initial: {CoreBlock.CustomName}, SyncValue: {!SyncIsMainCore.Value}, onlyCore: {onlyCore}", 3);
            if ((!SyncIsMainCore.Value && onlyCore) || (SyncIsMainCore.Value))
            {
                SyncIsMainCore.ValidateAndSet(true);
                mainGridLogic.Activate(SubtypeId);
                SaveCoreState();
            }
            if (!GridsPerFactionManager.WillGridBeWithinFactionLimits(mainGridLogic, SubtypeId))
            {
                /*if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == CoreBlock.CubeGrid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification("Per faction limit of this core has been hit!", 10000, true);
                }*/
                mainGridLogic.ResetCore();
                CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock, true);
                return;
            }
            if (!GridsPerPlayerManager.WillGridBeWithinPlayerLimits(mainGridLogic, SubtypeId))
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == CoreBlock.CubeGrid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification("Per player limit of this core has been hit!", 10000, true);
                }
                mainGridLogic.ResetCore();
                CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock, true);
                return;
            }
            //Best not to add it until you know it satisfies both conditions
            GridsPerFactionManager.AddCubeGrid(mainGridLogic);
            GridsPerPlayerManager.AddCubeGrid(mainGridLogic);
            mainGridLogic._NeedStaticCheck = true;
            if (MyAPIGateway.TerminalControls == null)
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == CoreBlock.CubeGrid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification("WARNING: Terminal Controls Missing on Core", 10000, true);
                }
            }
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            CoreBlock.CubeGrid.OnGridMerge += OnGridMerge;
            RegisterToolbarActionsOnce();
            //LimitRescheduler.Tick(CoreBlock);
        }
        
        public override void UpdateAfterSimulation10()
        {
            //LimitRescheduler.Tick(CoreBlock);
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
            Utils.Log(_hasPhysics.ToString(), 0, "Core Close");
            if (_hasPhysics == false)
            {
                base.Close();
                return;
            }
            
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            CoreBlock.CubeGrid.OnGridMerge -= OnGridMerge;
            
            var grid = CoreBlock.CubeGrid;
            var gridLogic = grid.GameLogic?.GetAs<GridLogic>();
            if (gridLogic == null)
            {
                return;
            }
            // If this core is NOT the main core, nothing to reassign
            if (!SyncIsMainCore.Value)
            {
                //Trigers on concealment, not cool you can delete this,k or move it ot OnDestroy
                //if(Constants.LocalPlayer!=null && (Constants.LocalPlayer.PlayerID==grid.BigOwners.FirstOrDefault())){Utils.ShowNotification($"A backup core of grid {grid.CustomName} was destroyed!",10000, true);}
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
            }
            else
            {
                // NO! Don't send this triggers on concealment
                /*if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == grid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification($"Main core of grid {grid.CustomName} was destroyed!", 10000, true);
                }*/
                GridsPerFactionManager.RemoveCubeGrid(gridLogic,SubtypeId);
                GridsPerPlayerManager.RemoveCubeGrid(gridLogic,SubtypeId);
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
            if (CoreBlock.CubeGrid.EntityId == actualMainGrid.EntityId) return;
            CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock,true);
            if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == CoreBlock.CubeGrid.BigOwners.FirstOrDefault())
            {
                Utils.ShowNotification($"Core Block Not on Main Grid",10000, true);
            }
        }
        
        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (Constants.LocalPlayer == null) return;
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
            if (!SyncIsMainCore.Value)
            {
                Utils.ShowNotification("Only the main core can trigger boost.", 1000); 
                return;
            }
            if (Constants.IsServer) CoreBlock.CubeGrid.GetMainGridLogic()?.ActivateBoost();
            else _syncBoostReq.Value += 1;
        }

        private void TriggerDefenseFromClient()
        {
            if (CoreBlock?.CubeGrid == null) return;
            if (!SyncIsMainCore.Value) { Utils.ShowNotification("Only the main core can trigger defense.", 1000); return; }
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