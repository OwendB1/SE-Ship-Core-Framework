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

#endregion

namespace ShipCoreFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), false)]
    public class GridLogic : MyGameLogicComponent
    {
        private readonly HashSet<MyCubeBlock> _blocks = new HashSet<MyCubeBlock>();
        private static ModConfig Config => ModSessionManager.Config;
        private bool _isMainGrid = false;
        private bool _isDisabled = false;
        private bool _once = true;
        
        public readonly Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>> BlocksPerLimit = new Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>>();
        
        public bool BoostEnabled;
        private float _boostCooldownTimer;
        private float _boostDurationTimer;
        
        public bool ActiveDefenseEnabled;
        private float _activeDefenseCooldownTimer;
        private float _activeDefenseDurationTimer;
        
        private float BoostDuration => ShipCore.Modifiers.BoostDuration;
        private float BoostCoolDown => ShipCore.Modifiers.BoostCoolDown;
        private float ActiveDefenseDuration => ShipCore.ActiveDefenseModifiers.Duration;
        private float ActiveDefenseCoolDown => ShipCore.ActiveDefenseModifiers.Cooldown;
        
        public GridModifiers Modifiers => ShipCore.Modifiers;

        public IMyCubeGrid Grid;

        public bool ActiveNoCore = true;
        private string _shipCoreTypeId = string.Empty;

        public IMyFaction OwningFaction => Grid.GetOwningFaction();

        public long MajorityOwningPlayerId => GetMajorityOwner();

        public ShipCore ShipCore => Config.GetShipCoreByTypeId(ActiveNoCore ? string.Empty : _shipCoreTypeId);

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            Grid = (IMyCubeGrid)Entity;
            Grid.OnPhysicsChanged += InitOnPhysicsChanged;
        }

        public void Activate(string shipCoreTypeId, bool force = false)
        {
            Utils.Log($"Activate: Activating logic for {Grid.CustomName} (entity id: {Grid.EntityId})!");
            
            if (!ActiveNoCore && !force) return;
            _shipCoreTypeId = shipCoreTypeId;
            ActiveNoCore = false;
            _isDisabled = false;
        }

        public void ActivateDefense()
        {
            if (ActiveDefenseEnabled || _activeDefenseCooldownTimer > 0f)
            {
                Utils.ShowNotification("Active Defense is cooling down!", 1000);
                return;
            }
            ActiveDefenseEnabled = true;
            _activeDefenseDurationTimer = ActiveDefenseDuration * 60f; // duration in seconds to ticks
            Utils.ShowNotification("Active Defense Engaged!", 1000);
        }
        
        public void ActivateBoost()
        {
            if (BoostEnabled || _boostCooldownTimer > 0f)
            {
                Utils.ShowNotification("Boost is cooling down!", 1000);
                return;
            }
            BoostEnabled = true;
            _boostDurationTimer = BoostDuration * 60f; // assuming BoostDuration in seconds, converting to ticks (60 per second)
            Utils.ShowNotification("Boost Engaged!", 1000);
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            if (!_isMainGrid || !ActiveNoCore) return;
            RunBoostTimerTick();
            RunActiveDefenseTimerTick();
            
            SpeedEnforcement.EnforceSpeedLimit(this);
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
            var grid = obj as IMyCubeGrid;
            if (grid?.Physics == null) return;
            Utils.Log($"PhysicsChanged: change triggered. Initialising logic for {grid.CustomName} (entity id: {grid.EntityId})!");
            
            Grid.OnPhysicsChanged -= InitOnPhysicsChanged;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            
            List<IMyCubeGrid> subs;
            var main = Grid.GetMainCubeGrid(out subs);
            if (main.EntityId == Grid.EntityId) _isMainGrid = true;
            
            //Init event handlers
            Grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            Grid.OnIsStaticChanged += OnIsStaticChanged;
            Grid.OnBlockAdded += OnBlockAdded;
            Grid.OnBlockRemoved += OnBlockRemoved;
            Grid.OnBlockIntegrityChanged += OnBlockIntegrityChanged;
            Grid.OnGridMerge += OnGridMerge;

            _blocks.UnionWith(Grid.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false));

            List<IMyCubeGrid> subgrids;
            Grid.GetMainCubeGrid(out subgrids);
            foreach (var subgrid in subgrids)
            {
                subgrid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
                subgrid.OnIsStaticChanged += OnIsStaticChanged;
                subgrid.OnBlockAdded += OnBlockAdded;
                subgrid.OnBlockRemoved += OnBlockRemoved;

                _blocks.UnionWith(subgrid.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false));
            }

            foreach (var blockLimit in ShipCore.BlockLimits)
            {
                var blockVals = new List<KeyValuePair<IMyCubeBlock, double>>();
                foreach (var blockGroup in blockLimit.BlockGroups)
                {
                    foreach (var blockType in blockGroup.BlockTypes)
                    {
                        var countingBlocks = _blocks
                            .Where(b => Utils.GetBlockTypeId(b) == blockType.TypeId &&
                                        Utils.GetBlockSubtypeId(b) == blockType.SubtypeId);
                        blockVals.AddRange(countingBlocks.Select(bl =>
                            new KeyValuePair<IMyCubeBlock, double>(bl, blockType.CountWeight)));
                    }
                }

                BlocksPerLimit[blockLimit] = blockVals;
            }

            foreach (var funcBlock in _blocks.OfType<IMyFunctionalBlock>())
            {
                funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                funcBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            }

            EnforceBlockPunishment();
            ApplyModifiers();
            
        }

        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null) return;
            if (HasFunctioningBeaconIfNeeded() == false)
            {
                foreach (var block in _blocks) CubeGridModifiers.ApplyModifiers(block, ShipCore.Modifiers);
            }

            if (func.Enabled)
                EnforceBlockPunishment(func);
        }

        private void OnUpgradeValuesChanged()
        {
            ApplyModifiers();
        }

        private void ApplyModifiers(GridModifiers modifiers = null)
        {
            foreach (var block in from block in _blocks
                     let terminalBlock = block as IMyTerminalBlock
                     where terminalBlock != null
                     select block) CubeGridModifiers.ApplyModifiers(block, modifiers ?? Modifiers);
        }
        
        //Event handlers
        private void OnBlockOwnershipChanged(IMyCubeGrid obj)
        {
            if (_isDisabled) return;
            EnforceBlockPunishment();

            if (OwningFaction != null)
            {
                if ((!Config.IncludeAiFactions && OwningFaction.IsEveryoneNpc()) || Config.IgnoreFactionTags.Contains(OwningFaction.Tag))
                {
                    _isDisabled = true;
                    return;
                }
                if(_once)
                {
                    GridsPerPlayerClassManager.AddCubeGrid(this);
                    GridsPerFactionClassManager.AddCubeGrid(this);
                }
            }
            else if(_once) GridsPerPlayerClassManager.AddCubeGrid(this);
            _once = false;
        }
        
        private void OnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            if (_isDisabled) return;
            if (ShipCore.LargeGridStatic && !ShipCore.LargeGridMobile && !isStatic) grid.IsStatic = true;
            if (!ShipCore.LargeGridStatic && isStatic) grid.IsStatic = false;
        }

        private void OnBlockAdded(IMySlimBlock obj) //This is working now.
        {
            try{
                if (_isDisabled) return;
                Utils.Log($"{Utils.GetBlockTypeId(obj)} | {Utils.GetBlockSubtypeId(obj)}");
                var concreteGrid = Grid as MyCubeGrid;
                 //MaxBlocks
                if ((concreteGrid?.BlocksCount >= ShipCore.MaxBlocks )&& ShipCore.MaxBlocks!=-1)
                {
                    MyAPIGateway.Utilities.ShowMessage("ShipCores:", $"{Utils.GetBlockSubtypeId(obj)} Violates MaxBlocks: {concreteGrid?.BlocksCount > ShipCore.MaxBlocks}");
                    Grid.RemoveBlock(obj);
                    return;
                }
                //Missing MaxPCU
                if ((concreteGrid?.BlocksPCU >= ShipCore.MaxPCU )&& ShipCore.MaxPCU!=-1)
                {
                    MyAPIGateway.Utilities.ShowMessage("ShipCores:", $"{Utils.GetBlockSubtypeId(obj)} Violates MaxPCU: {concreteGrid?.BlocksCount > ShipCore.MaxPCU}");
                    Grid.RemoveBlock(obj);
                    return;
                }
                // MaxMass, Not sure if this is dry or wet mass... testing required
               if ((concreteGrid?.Mass >= ShipCore.MaxMass )&& ShipCore.MaxMass!=-1)
                {
                    MyAPIGateway.Utilities.ShowMessage("ShipCores:", $"{Utils.GetBlockSubtypeId(obj)} Violates MaxMass: {concreteGrid?.BlocksCount > ShipCore.MaxMass}");
                    Grid.RemoveBlock(obj);
                    return;
                }        
                foreach(BlockLimit limit in ShipCore.BlockLimits)
                {
                    bool match = limit.BlockGroups
                        .SelectMany(g => g.BlockTypes)
                        .Any(b => b.TypeId == Utils.GetBlockTypeId(obj) && b.SubtypeId == Utils.GetBlockSubtypeId(obj));

                    if(!match){continue;}
                    if (!BlocksPerLimit.ContainsKey(limit)) InitOnPhysicsChanged(concreteGrid);//Weird Fix I know but it does have to be fixed there are grids that just don't seem to under go a physics change
                    var limitBlocks = BlocksPerLimit[limit];
                    var countWeight = limitBlocks.Sum(b => b.Value);
                    //I'll fix it later
                    var countForSpecificBlock = limit.BlockGroups.SelectMany(g => g.BlockTypes).FirstOrDefault(b => b.TypeId == Utils.GetBlockTypeId(obj) && b.SubtypeId == Utils.GetBlockSubtypeId(obj)).CountWeight;

                    Utils.Log($"{countWeight} | {countForSpecificBlock} | {limit.MaxCount}");
                    if (countWeight + countForSpecificBlock > limit.MaxCount)
                    {
                        MyAPIGateway.Utilities.ShowMessage("LimitStatus",$"Removing Bad block");
                        Grid.RemoveBlock(obj);
                        List<IMyCubeGrid> subs;
                        Grid.GetMainCubeGrid(out subs);
                        foreach (var subgrid in subs) subgrid.RemoveBlock(obj);
                        return;
                    }

                    BlocksPerLimit[limit].Add(new KeyValuePair<IMyCubeBlock, double>(obj.FatBlock, countForSpecificBlock));
                }

                _blocks.Add(obj.FatBlock as MyCubeBlock);

                var funcBlock = obj.FatBlock as IMyFunctionalBlock;
                if (funcBlock != null)
                    funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                ApplyModifiers();
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("ShipCores:", "Error OnBlockAdded" + e);
            }

        }

        private void OnBlockRemoved(IMySlimBlock obj)//This works, IDK how I feel about min grid
        {
            try{
                if (_isDisabled) return;
                if (obj.FatBlock != null && HasFunctioningBeaconIfNeeded() == false)
                {
                    foreach (var block in _blocks) CubeGridModifiers.ApplyModifiers(block, ShipCore.Modifiers);
                }
                
                foreach (var limit in ShipCore.BlockLimits)
                {
                    if (!BlocksPerLimit.ContainsKey(limit)) return;
                    var index = BlocksPerLimit[limit].FindIndex(b => b.Key == obj.FatBlock);
                    if (index >= 0)
                        BlocksPerLimit[limit].RemoveAt(index);
                }

                var concreteGrid = Grid as MyCubeGrid;
                if (concreteGrid?.BlocksCount < ShipCore.MinBlocks)
                {
                    //Damage x2?, honestly I still want to just remove MinBlocks
                }

                _blocks.Remove(obj.FatBlock as MyCubeBlock);
                ApplyModifiers();
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("ShipCores:", "Error OnBlockRemoved" + e);
            }
        }
        
        private void OnBlockIntegrityChanged(IMySlimBlock obj) // What's this for?
        {
            if (_isDisabled) return;
            //throw new NotImplementedException(); Owen, that's forbidden :(
        }
        
        private void OnGridMerge(IMyCubeGrid main, IMyCubeGrid sub)
        {
            if (_isDisabled) return;
            var mainLogic = main.GetMainGridLogic();
            if (mainLogic.Grid.EntityId == main.EntityId) // this check makes sure that the blocks and events are always added to the biggest grid instead of the rotor order
            {
                sub.OnBlockOwnershipChanged += mainLogic.OnBlockOwnershipChanged;
                sub.OnIsStaticChanged += mainLogic.OnIsStaticChanged;
                sub.OnBlockAdded += mainLogic.OnBlockAdded;
                sub.OnBlockRemoved += mainLogic.OnBlockRemoved;
                var fatBlocks = sub.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false);
                mainLogic._blocks.UnionWith(fatBlocks);
            }
            else
            {
                main.OnBlockOwnershipChanged += mainLogic.OnBlockOwnershipChanged;
                main.OnIsStaticChanged += mainLogic.OnIsStaticChanged;
                main.OnBlockAdded += mainLogic.OnBlockAdded;
                main.OnBlockRemoved += mainLogic.OnBlockRemoved;
                var fatBlocks = main.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false);
                mainLogic._blocks.UnionWith(fatBlocks);
            }
        }
        
        private void EnforceBlockPunishment(IMyCubeBlock block = null)//this probably needs more attention
        {
            if (block != null)
            {
            
                foreach (var limit in ShipCore.BlockLimits)
                {
                    var limitBlocks = BlocksPerLimit[limit];
                    var countWeight = limitBlocks.Sum(l => l.Value);
                    Utils.Log($"Block check: {limit.Name} | {countWeight} | {limit.MaxCount}");
                    if (countWeight <= limit.MaxCount) continue;
                    var func = block as IMyFunctionalBlock;
                    if (func != null)
                    {
                        func.Enabled = false;
                    }
                    else
                    {
                        var slim = block.SlimBlock;
                        var targetIntegrity = slim.MaxIntegrity * 0.2;
                        var damageRequired = slim.Integrity - targetIntegrity;

                        if (damageRequired < 0) damageRequired = 0;
                        slim.DoDamage((float)damageRequired, MyDamageType.Bullet, true);
                    }
                }
            }
            else
            {
                foreach (var limit in ShipCore.BlockLimits)
                {
                    if (!BlocksPerLimit.ContainsKey(limit)) return;
                    var limitBlocks = BlocksPerLimit[limit];
                    double countWeight = 0;
                    foreach (var limitBlock in limitBlocks)
                    {
                        countWeight += limitBlock.Value;
                        if (countWeight <= limit.MaxCount) continue;
                        var func = limitBlock.Key as IMyFunctionalBlock;
                        if (func != null)
                        {
                            func.Enabled = false;
                        }
                        else
                        {
                            var slim = limitBlock.Key.SlimBlock;
                            var targetIntegrity = slim.MaxIntegrity * 0.2;
                            var damageRequired = slim.Integrity - targetIntegrity;

                            if (damageRequired < 0) damageRequired = 0;
                            slim.DoDamage((float)damageRequired, MyDamageType.Bullet, true);
                        }
                    }
                }
            }
        }

        private bool HasFunctioningBeaconIfNeeded()
        {
            return ShipCore.ForceBroadCast == false ||
                   _blocks.OfType<IMyFunctionalBlock>().Any(block => block is IMyBeacon && block.Enabled);
        }

        private IEnumerable<BlockLimit> GetRelevantLimits(IMySlimBlock block)
        {
            //Something is incredibly wrong with this line
            //return ShipCore.BlockLimits.Where(limit => limit.GetBlockTypes().Any(type =>type.TypeId == Utils.GetBlockTypeId(block) && type.SubtypeId == Utils.GetBlockSubtypeId(block)));
            string blockTypeId = Utils.GetBlockTypeId(block);
            string blockSubtypeId = Utils.GetBlockSubtypeId(block);

            List<BlockLimit> matchingLimits = new List<BlockLimit>();
            foreach (BlockLimit limit in ShipCore.BlockLimits)
            {
                foreach (var group in ModSessionManager.Config.BlockGroups)
                {
                    if(group.Name==limit.Name)
                    {
                        matchingLimits.Add(limit);
                    }
                        
                }
                /*
                var blockTypes = limit.GetBlockTypes();//Always fucking zero
                bool matches = false;
                MyAPIGateway.Utilities.ShowMessage("Number of BlockTypes:",$"{blockTypes.Count()}");
                foreach (var type in blockTypes)
                {
                    MyAPIGateway.Utilities.ShowMessage("Match:",$"{type.TypeId} | {blockTypeId}");
                    MyAPIGateway.Utilities.ShowMessage("Match:",$"{type.SubtypeId} | {blockSubtypeId}");
                    if (type.TypeId == blockTypeId && type.SubtypeId == blockSubtypeId)
                    {
                        matches = true;
                        break;
                    }
                }
                if (matches)
                {
                    matchingLimits.Add(limit);
                }
                */
            }
            return matchingLimits;
        }

        private long GetMajorityOwner()
        {
            return Grid.BigOwners.FirstOrDefault();
        }

        public override void Close()
        {
            GridsPerFactionClassManager.RemoveCubeGrid(this);
            GridsPerPlayerClassManager.RemoveCubeGrid(this);
            base.Close();
        }
    }
}