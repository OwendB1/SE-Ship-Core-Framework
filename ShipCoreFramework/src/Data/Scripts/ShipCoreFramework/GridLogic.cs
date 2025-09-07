#region

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
#endregion

namespace ShipCoreFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), false)]
    public class GridLogic : MyGameLogicComponent
    {
        public readonly HashSet<MyCubeBlock> Blocks = new HashSet<MyCubeBlock>();
        
        public MyStringHash DamageTypeNoFlyZone = MyStringHash.GetOrCompute("NoFLyZoneViolation");
        public readonly Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>> BlocksPerLimit = new Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>>();

        public bool PunishModifiers;
        public bool PunishSpeed;
        
        public bool BoostEnabled;
        private float _boostCooldownTimer;
        private float _boostDurationTimer;
        
        public bool ActiveDefenseEnabled;
        private float _activeDefenseCooldownTimer;
        private float _activeDefenseDurationTimer;

        private bool _needsSubgridsRedone;

        public CoreLogic CoreBlock => Utils.GetGridCore(Grid,ShipCore);

        private float BoostDuration => ShipCore.Modifiers.BoostDuration;
        private float BoostCoolDown => ShipCore.Modifiers.BoostCoolDown;
        private float ActiveDefenseDuration => ShipCore.ActiveDefenseModifiers.Duration;
        private float ActiveDefenseCoolDown => ShipCore.ActiveDefenseModifiers.Cooldown;
        public GridModifiers Modifiers => PunishModifiers ? ModSessionManager.Config.SelectedNoCore.Modifiers : CubeGridModifiers.GetActiveModifiers(this);

        public IMyCubeGrid Grid;
        
        private string _shipCoreTypeId = string.Empty;
        
        public IMyFaction OwningFaction => Grid.GetOwningFaction();

        public long MajorityOwningPlayerId => GetMajorityOwner();

        public ShipCore ShipCore => ModSessionManager.Config.GetShipCoreByTypeId(_shipCoreTypeId);
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            Grid = (IMyCubeGrid)Entity;
            Grid.OnPhysicsChanged += InitOnPhysicsChanged;
        }

        public void Activate(string shipCoreTypeId)
        {
            Utils.Log($"Activate: Activating logic for {Grid.CustomName} (entity id: {Grid.EntityId})!");
            _shipCoreTypeId = shipCoreTypeId;
            
            Enforcement.UpdateLimitsAndApplyModifiers(BlocksPerLimit, ShipCore, Blocks, Modifiers);
            Enforcement.EnforceGridPunishment(Grid);
        }

        public void ResetCore()
        {
            Utils.Log($"Reset: Resetting logic for {Grid.CustomName} (entity id: {Grid.EntityId})!");
            _shipCoreTypeId = string.Empty;
            
            GridsPerFactionManager.RemoveCubeGrid(this);
            GridsPerPlayerManager.RemoveCubeGrid(this);
            
            Enforcement.UpdateLimitsAndApplyModifiers(BlocksPerLimit, ShipCore, Blocks, Modifiers);
            Enforcement.EnforceGridPunishment(Grid);
        }
        
        public void ActivateDefense()
        {
            if(ActiveDefenseEnabled)
            {
                Utils.ShowNotification($"Active Defense Time Remaining:{_activeDefenseDurationTimer/60f:0.0}", 1000);
                return;
            }
            if (_activeDefenseCooldownTimer > 0f)
            {
                Utils.ShowNotification($"Active Defense is cooling down! Cooldown Time:{_boostCooldownTimer/60f:0.0}", 1000);
                return;
            }
            ActiveDefenseEnabled = true;
            _activeDefenseDurationTimer = ActiveDefenseDuration * 60f; // duration in seconds to ticks
            Utils.ShowNotification("Active Defense Engaged!", 1000);
        }
        
        public void ActivateBoost()
        {
            if (BoostEnabled)
            {
                Utils.ShowNotification($"Boost Time Remaining:{_boostDurationTimer/60f:0.0}", 1000);
                return;
            }
            if (_boostCooldownTimer > 0f)
            {
                Utils.ShowNotification($"Boost is cooling down! Cooldown Time:{_boostCooldownTimer/60f:0.0}", 1000);
                return;
            }
            BoostEnabled = true;
            _boostDurationTimer = BoostDuration * 60f; // assuming BoostDuration in seconds, converting to ticks (60 per second)
            Utils.ShowNotification("Boost Engaged!", 1000);
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
            List<IMyCubeGrid> subgrids;
            var mainGrid = Grid.GetMainCubeGrid(out subgrids);
            if (mainGrid != Grid) return;
            if (_needsSubgridsRedone)
            {
                Blocks.Clear();
                Blocks.UnionWith(Grid.GetFatBlocks<MyCubeBlock>().Where(b => !b.IsPreview));
                foreach(var grid in subgrids)
                {
                    grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
                    grid.OnIsStaticChanged -= OnIsStaticChanged;
                    grid.OnBlockAdded -= OnBlockAdded;
                    grid.OnBlockRemoved -= OnBlockRemoved;     
                    grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
                    grid.OnIsStaticChanged += OnIsStaticChanged;
                    grid.OnBlockAdded += OnBlockAdded;
                    grid.OnBlockRemoved += OnBlockRemoved;     
                    var fatBlocks = grid.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false);
                    Blocks.UnionWith(fatBlocks);
                }
                Enforcement.UpdateLimitsAndApplyModifiers(BlocksPerLimit, ShipCore, Blocks, Modifiers);
                Enforcement.EnforceGridPunishment(Grid);
                _needsSubgridsRedone=false;
            }
            SpeedEnforcement.EnforceSpeedLimit(this, PunishSpeed);
            if (_shipCoreTypeId == string.Empty) return;
            NoFlyZones.EnforceNoFlyZones(this);
            RunBoostTimerTick();
            RunActiveDefenseTimerTick();
        }

        private void RunBoostTimerTick()
        {
            if (BoostEnabled)
            {
                _boostDurationTimer -= 1f;
                if (!(_boostDurationTimer <= 0f)) return;
                BoostEnabled = false;
                _boostCooldownTimer = BoostCoolDown * 60f;
                Utils.ShowNotification("Boost Disengaged! Cooldown started.", 1000);
            }
            else if (_boostCooldownTimer > 0f)
            {
                _boostCooldownTimer -= 1f;
                if (_boostCooldownTimer < 0f) _boostCooldownTimer = 0f;
            }
        }

        private void RunActiveDefenseTimerTick()
        {
            if (ActiveDefenseEnabled)
            {
                _activeDefenseDurationTimer -= 1f;
                if (!(_activeDefenseDurationTimer <= 0f)) return;
                ActiveDefenseEnabled = false;
                _activeDefenseCooldownTimer = ActiveDefenseCoolDown * 60f;
                Utils.ShowNotification("Active Defense Disengaged! Cooldown started.", 1000);
            }
            else if (_activeDefenseCooldownTimer > 0f)
            {
                _activeDefenseCooldownTimer -= 1f;
                if (_activeDefenseCooldownTimer < 0f) _activeDefenseCooldownTimer = 0f;
            }
        }

        private void InitOnPhysicsChanged(IMyEntity obj)
        {
            if (ModSessionManager.Config.SelectedNoCore == null)
            {
                Utils.Log("NOCORE is NULL for GRID");
                return;
            }
            if (Grid?.Physics == null) return;
            
            Grid.OnPhysicsChanged -= InitOnPhysicsChanged;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            /*if (OwningFaction == null) a player can have no faction
            {
                // Try again next frame—ownership often appears shortly after spawn/merge
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                return;
            }*/ 
            
            if (OwningFaction != null && (ModSessionManager.Config.IgnoreAiFactions && OwningFaction.IsEveryoneNpc() ||
                ModSessionManager.Config.IgnoredFactionTags.Contains(OwningFaction.Tag)))
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                return;
            }
            
            List<IMyCubeGrid> subgrids;
            var main = Grid.GetMainCubeGrid(out subgrids);
            
            if (main.EntityId == Grid.EntityId)
            {
                Blocks.UnionWith(Grid.GetFatBlocks<MyCubeBlock>().Where(b => !b.IsPreview));
            }
            else
            {
                Utils.Log($"Delayed Init: subgrid {Grid.CustomName} (id: {Grid.EntityId})");
                var mainLogic = main.GetMainGridLogic();
                mainLogic.Blocks.UnionWith(Grid.GetFatBlocks<MyCubeBlock>().Where(b => !b.IsPreview));
                
                Grid.OnBlockOwnershipChanged += mainLogic.OnBlockOwnershipChanged;
                Grid.OnIsStaticChanged += mainLogic.OnIsStaticChanged;
                Grid.OnBlockAdded += mainLogic.OnBlockAdded;
                Grid.OnBlockRemoved += mainLogic.OnBlockRemoved;

                foreach (var funcBlock in Blocks.OfType<IMyFunctionalBlock>())
                {
                    funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                    funcBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
                }
                
                Enforcement.UpdateLimitsAndApplyModifiers(mainLogic.BlocksPerLimit, mainLogic.ShipCore, mainLogic.Blocks, Modifiers);
                Utils.Log("8");
                return;
            }

            Utils.Log($"Delayed Init: main grid {Grid.CustomName} (id: {Grid.EntityId})");

            Grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            Grid.OnIsStaticChanged += OnIsStaticChanged;
            Grid.OnBlockAdded += OnBlockAdded;
            Grid.OnBlockRemoved += OnBlockRemoved;
            Grid.OnGridMerge += OnGridMergeOrSplit;
            Grid.OnGridSplit += OnGridMergeOrSplit;
            ((MyCubeGrid)Grid).OnConnectionChangeCompleted += OnConnectionChangeCompleted;

            foreach (var funcBlock in Blocks.OfType<IMyFunctionalBlock>())
            {
                funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                funcBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            }

            Enforcement.UpdateLimitsAndApplyModifiers(BlocksPerLimit, ShipCore, Blocks, Modifiers);
        }

        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null) return;
            if (HasFunctioningBeaconIfNeeded() == false)
            {
                foreach (var block in Blocks) CubeGridModifiers.ApplyModifiers(block, Modifiers);
            }

            if (func.Enabled) Enforcement.EnforceBlockPunishment(func);
        }

        private void OnUpgradeValuesChanged()
        {
            Enforcement.ApplyModifiers(Blocks, Modifiers);
        }
        
        //Event handlers
        private void OnBlockOwnershipChanged(IMyCubeGrid obj)
        {
            GridsPerPlayerManager.RemoveCubeGrid(this);
            GridsPerPlayerManager.AddCubeGrid(this);
            if (OwningFaction != null)
            {
                GridsPerFactionManager.RemoveCubeGrid(this);
                if (ModSessionManager.Config.IgnoreAiFactions && OwningFaction.IsEveryoneNpc() || ModSessionManager.Config.IgnoredFactionTags.Contains(OwningFaction.Tag)) return;
                GridsPerFactionManager.AddCubeGrid(this);
            }
            Enforcement.EnforceGridPunishment(Grid);
        }

        private void OnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            if (ShipCore.LargeGridStatic && !ShipCore.LargeGridMobile && !isStatic) grid.IsStatic = true;
            if (!ShipCore.LargeGridStatic && isStatic) grid.IsStatic = false;
        }

        private void OnBlockAdded(IMySlimBlock obj) //Now tells player why
        {
            Utils.Log($"{Utils.GetBlockTypeId(obj)} | {Utils.GetBlockSubtypeId(obj)}");
            var concreteGrid = Grid as MyCubeGrid;
             //MaxBlocks
            if (concreteGrid?.BlocksCount >= ShipCore.MaxBlocks && ShipCore.MaxBlocks > 0)
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == Grid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification($"{Utils.GetBlockSubtypeId(obj)} Violates MaxBlocks: {concreteGrid.BlocksCount > ShipCore.MaxBlocks}",10000, true);
                }
                Grid.RemoveBlock(obj);
                return;
            }
            //Missing MaxPCU
            if (concreteGrid?.BlocksPCU >= ShipCore.MaxPCU && ShipCore.MaxPCU > 0)
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == Grid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification($"{Utils.GetBlockSubtypeId(obj)} Violates MaxPCU: {concreteGrid.BlocksCount > ShipCore.MaxPCU}",10000, true);
                }
                Grid.RemoveBlock(obj);
                return;
            }
            // MaxMass, Currently WET MASS
            if (concreteGrid?.Mass >= ShipCore.MaxMass && ShipCore.MaxMass > 0f)
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == Grid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification($"{Utils.GetBlockSubtypeId(obj)} Violates MaxMass: {concreteGrid.BlocksCount > ShipCore.MaxMass}",10000, true);
                }
                Grid.RemoveBlock(obj);
                return;
            } 
            
            foreach(var limit in ShipCore.BlockLimits)
            {
                var match = limit.BlockGroups
                    .SelectMany(g => g.BlockTypes)
                    .Any(b => b.TypeId == Utils.GetBlockTypeId(obj) && (b.SubtypeId=="any" || b.SubtypeId == Utils.GetBlockSubtypeId(obj)));

                if (!match) continue;
                var limitBlocks = BlocksPerLimit[limit];
                var countWeight = limitBlocks.Sum(b => b.Value);
                var countForSpecificBlock = limit.BlockGroups.SelectMany(g => g.BlockTypes).First(b => b.TypeId == Utils.GetBlockTypeId(obj) && (b.SubtypeId=="any" || b.SubtypeId == Utils.GetBlockSubtypeId(obj))).CountWeight;

                Utils.Log($"{countWeight} | {countForSpecificBlock} | {limit.MaxCount}");
                
                var validDirection = true;
                if (CoreBlock?.CoreBlock != null) validDirection=Enforcement.IsValidDirection(CoreBlock.CoreBlock, obj, limit.AllowedDirections); 
                else Utils.Log("Log Direction Check: \nCoreBlock is null", 3); 
                
                if (countWeight + countForSpecificBlock > limit.MaxCount||!validDirection)
                {
                    if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == Grid.BigOwners.FirstOrDefault())
                        Utils.ShowNotification($"{Utils.GetBlockSubtypeId(obj)} Violates Blocklimit {limit.Name}: {countWeight}/{limit.MaxCount}",10000, true);
                    
                    Grid.RemoveBlock(obj);
                    List<IMyCubeGrid> subs;
                    Grid.GetMainCubeGrid(out subs);
                    foreach (var subgrid in subs) subgrid.RemoveBlock(obj);
                    return;
                }

                BlocksPerLimit[limit].Add(new KeyValuePair<IMyCubeBlock, double>(obj.FatBlock, countForSpecificBlock));
            }

            Blocks.Add(obj.FatBlock as MyCubeBlock);

            var funcBlock = obj.FatBlock as IMyFunctionalBlock;
            if (funcBlock != null)
                funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
            Enforcement.ApplyModifiers(Blocks, Modifiers);
        }

        private void OnBlockRemoved(IMySlimBlock obj)
        {
            Enforcement.EnforceOverCapacity(obj.CubeGrid);
            //Can this be done anywhere else?    
            foreach (var limit in ShipCore.BlockLimits)
            {
                if (!BlocksPerLimit.ContainsKey(limit)) return;
                var index = BlocksPerLimit[limit].FindIndex(b => b.Key == obj.FatBlock);
                if (index >= 0) BlocksPerLimit[limit].RemoveAt(index);
            }
            Blocks.Remove(obj.FatBlock as MyCubeBlock);
            Enforcement.ApplyModifiers(Blocks, Modifiers);
        }
        private void OnConnectionChangeCompleted(MyCubeGrid myGrid, GridLinkTypeEnum gridGroupTypeChanged)
        {
            Enforcement.EnforceOverCapacity(myGrid);
            if(gridGroupTypeChanged != GridLinkTypeEnum.Mechanical) return;
            Utils.Log($"Subgrid Status Changed: {(myGrid as IMyCubeGrid).CustomName})");
            List<IMyCubeGrid> subgrids;
            var mainGrid = myGrid.GetMainCubeGrid(out subgrids);
            var mainLogic = mainGrid.GetMainGridLogic();
            mainLogic._needsSubgridsRedone=true;
        }
        
        private void OnGridMergeOrSplit(IMyCubeGrid main, IMyCubeGrid sub)
        {
            Utils.Log($"OnGridMergeOrSplit: {main.CustomName} Sub: {sub.CustomName})");
            List<IMyCubeGrid> mainSubgrids;
            List<IMyCubeGrid> subgrids;
            var mainGrid = main.GetMainCubeGrid(out mainSubgrids);
            var mainLogic = mainGrid.GetMainGridLogic();
            
            Enforcement.EnforceOverCapacity(mainGrid);
            mainLogic._needsSubgridsRedone=true;
            var subLogic = sub.GetMainCubeGrid(out subgrids).GetMainGridLogic();
            subLogic.Close();
        }
        
        private bool HasFunctioningBeaconIfNeeded()
        {
            return ShipCore.ForceBroadCast == false || Blocks.OfType<IMyFunctionalBlock>().Any(block => block is IMyBeacon && block.Enabled);
        }

        private long GetMajorityOwner()
        {
            return Grid.BigOwners.FirstOrDefault();
        }

        public override void Close()
        {
            if (ModSessionManager.Config.SelectedNoCore != null)
            {
                GridsPerFactionManager.RemoveCubeGrid(this); 
                GridsPerPlayerManager.RemoveCubeGrid(this);
            }
            base.Close();
        }
    }
}