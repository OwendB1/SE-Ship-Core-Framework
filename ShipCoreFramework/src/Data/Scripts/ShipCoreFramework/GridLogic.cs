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
        private readonly HashSet<MyCubeBlock> _blocks = new HashSet<MyCubeBlock>();
        private static ModConfig Config => ModSessionManager.Config;
        private readonly MyStringHash _damageTypeBlockLimit = MyStringHash.GetOrCompute("BlockLimitsViolation");
        public MyStringHash DamageTypeNoFlyZone = MyStringHash.GetOrCompute("NoFLyZoneViolation");
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
        
        public GridModifiers Modifiers => CubeGridModifiers.GetActiveModifiers(this);

        public IMyCubeGrid Grid;
        
        private string _shipCoreTypeId = string.Empty;
        
        public IMyFaction OwningFaction => Grid.GetOwningFaction();

        public long MajorityOwningPlayerId => GetMajorityOwner();

        public ShipCore ShipCore => Config.GetShipCoreByTypeId(_shipCoreTypeId);
        
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
            
            UpdateLimitsAndApplyModifiers();
            EnforceBlockPunishment();
        }

        public void ResetCore()
        {
            Utils.Log($"Reset: Resetting logic for {Grid.CustomName} (entity id: {Grid.EntityId})!");
            _shipCoreTypeId = string.Empty;
            
            GridsPerFactionClassManager.RemoveCubeGrid(this);
            GridsPerPlayerClassManager.RemoveCubeGrid(this);
            
            UpdateLimitsAndApplyModifiers();
            EnforceBlockPunishment();
        }

        private void UpdateLimitsAndApplyModifiers()
        {
            BlocksPerLimit.Clear();
            foreach (var blockLimit in ShipCore.BlockLimits)
            {
                var blockVals = new List<KeyValuePair<IMyCubeBlock, double>>();
                foreach (var blockGroup in blockLimit.BlockGroups)
                {
                    foreach (var blockType in blockGroup.BlockTypes)
                    {
                        var countingBlocks = _blocks.Where(b => Utils.GetBlockTypeId(b) == blockType.TypeId && Utils.GetBlockSubtypeId(b) == blockType.SubtypeId);
                        blockVals.AddRange(countingBlocks.Select(bl => new KeyValuePair<IMyCubeBlock, double>(bl, blockType.CountWeight)));
                    }
                }

                BlocksPerLimit[blockLimit] = blockVals;
            }
            ApplyModifiers();
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
            SpeedEnforcement.EnforceSpeedLimit(this);
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
            if (Grid?.Physics == null) return;
            
            Grid.OnPhysicsChanged -= InitOnPhysicsChanged;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            List<IMyCubeGrid> subgrids;
            var main = Grid.GetMainCubeGrid(out subgrids);

            if (main.EntityId == Grid.EntityId)
            {
                _blocks.UnionWith(Grid.GetFatBlocks<MyCubeBlock>().Where(b => !b.IsPreview));
            }
            else
            {
                Utils.Log($"Delayed Init: subgrid {Grid.CustomName} (id: {Grid.EntityId})");
                var mainLogic = main.GetMainGridLogic();
                mainLogic._blocks.UnionWith(Grid.GetFatBlocks<MyCubeBlock>().Where(b => !b.IsPreview));
                Grid.OnBlockOwnershipChanged += mainLogic.OnBlockOwnershipChanged;
                Grid.OnIsStaticChanged += mainLogic.OnIsStaticChanged;
                Grid.OnBlockAdded += mainLogic.OnBlockAdded;
                Grid.OnBlockRemoved += mainLogic.OnBlockRemoved;
                mainLogic.UpdateLimitsAndApplyModifiers();
                return;
            }

            Utils.Log($"Delayed Init: main grid {Grid.CustomName} (id: {Grid.EntityId})");

            Grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            Grid.OnIsStaticChanged += OnIsStaticChanged;
            Grid.OnBlockAdded += OnBlockAdded;
            Grid.OnBlockRemoved += OnBlockRemoved;
            Grid.OnGridMerge += OnGridMerge;

            foreach (var funcBlock in _blocks.OfType<IMyFunctionalBlock>())
            {
                funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                funcBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            }

            UpdateLimitsAndApplyModifiers();
        }

        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null) return;
            if (HasFunctioningBeaconIfNeeded() == false)
            {
                foreach (var block in _blocks) CubeGridModifiers.ApplyModifiers(block, ShipCore.Modifiers);
            }

            if (func.Enabled) EnforceBlockPunishment(func);
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
            EnforceBlockPunishment();

            if (OwningFaction != null)
            {
                if ((!Config.IncludeAiFactions && OwningFaction.IsEveryoneNpc()) || Config.IgnoreFactionTags.Contains(OwningFaction.Tag))
                {
                    return;
                }
                GridsPerPlayerClassManager.RemoveCubeGrid(this);
                GridsPerFactionClassManager.RemoveCubeGrid(this);
                
                GridsPerPlayerClassManager.AddCubeGrid(this);
                GridsPerFactionClassManager.AddCubeGrid(this);
            }
            else
            {
                GridsPerPlayerClassManager.RemoveCubeGrid(this);
                GridsPerPlayerClassManager.AddCubeGrid(this);
            }
        }
        
        private void OnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            if (ShipCore.LargeGridStatic && !ShipCore.LargeGridMobile && !isStatic) grid.IsStatic = true;
            if (!ShipCore.LargeGridStatic && isStatic) grid.IsStatic = false;
        }

        private void OnBlockAdded(IMySlimBlock obj) //This is working now.
        {
            Utils.Log($"{Utils.GetBlockTypeId(obj)} | {Utils.GetBlockSubtypeId(obj)}");
            var concreteGrid = Grid as MyCubeGrid;
             //MaxBlocks
            if (concreteGrid?.BlocksCount >= ShipCore.MaxBlocks && ShipCore.MaxBlocks > 0)
            {
                MyAPIGateway.Utilities.ShowMessage("ShipCores:", $"{Utils.GetBlockSubtypeId(obj)} Violates MaxBlocks: {concreteGrid?.BlocksCount > ShipCore.MaxBlocks}");
                Grid.RemoveBlock(obj);
                return;
            }
            //Missing MaxPCU
            if (concreteGrid?.BlocksPCU >= ShipCore.MaxPCU && ShipCore.MaxPCU > 0)
            {
                MyAPIGateway.Utilities.ShowMessage("ShipCores:", $"{Utils.GetBlockSubtypeId(obj)} Violates MaxPCU: {concreteGrid?.BlocksCount > ShipCore.MaxPCU}");
                Grid.RemoveBlock(obj);
                return;
            }
            // MaxMass, Not sure if this is dry or wet mass... testing required
            if (concreteGrid?.Mass >= ShipCore.MaxMass && ShipCore.MaxMass > 0f)
            {
                MyAPIGateway.Utilities.ShowMessage("ShipCores:", $"{Utils.GetBlockSubtypeId(obj)} Violates MaxMass: {concreteGrid?.BlocksCount > ShipCore.MaxMass}");
                Grid.RemoveBlock(obj);
                return;
            } 
            
            foreach(var limit in ShipCore.BlockLimits)
            {
                var match = limit.BlockGroups
                    .SelectMany(g => g.BlockTypes)
                    .Any(b => b.TypeId == Utils.GetBlockTypeId(obj) && b.SubtypeId == Utils.GetBlockSubtypeId(obj));

                if (!match) continue;
                var limitBlocks = BlocksPerLimit[limit];
                var countWeight = limitBlocks.Sum(b => b.Value);
                //I'll fix it later
                var countForSpecificBlock = limit.BlockGroups.SelectMany(g => g.BlockTypes).FirstOrDefault(b => b.TypeId == Utils.GetBlockTypeId(obj) && b.SubtypeId == Utils.GetBlockSubtypeId(obj)).CountWeight;

                Utils.Log($"{countWeight} | {countForSpecificBlock} | {limit.MaxCount}");
                if (countWeight + countForSpecificBlock > limit.MaxCount)
                {
                    Utils.Log("Removing Bad block", 3);
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

        private void OnBlockRemoved(IMySlimBlock obj)//This works, IDK how I feel about min grid
        {
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
        
        private void OnGridMerge(IMyCubeGrid main, IMyCubeGrid sub)
        {
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
        public void WhackABlock(IMyCubeBlock block, PunishmentType harm, MyStringHash? CustomDamageType = null)
        {
            MyStringHash DamageType = CustomDamageType?? _damageTypeBlockLimit;
            double damageRequired = 0;
            switch (harm)
            {
                //case PunishmentType.ShutOff:
                    //break;
                case PunishmentType.Damage:
                    // Whack,50%
                    damageRequired = block.SlimBlock.Integrity - (block.SlimBlock.MaxIntegrity * 0.5);
                    if (damageRequired < 0) damageRequired = 0;
                    block.SlimBlock.DoDamage((float)damageRequired, DamageType, true);
                    break;

                case PunishmentType.Delete:
                    Grid.RemoveBlock(block.SlimBlock);
                    break;
                case PunishmentType.Explode:
                    //Game will cause explosion on damage = integridy, if block explodes on destruction most do, if not... I don't care that much.
                    block.SlimBlock.DoDamage((float)block.SlimBlock.Integrity, DamageType, true);
                    break;

                default:
                    //Shut off, or whack if that's not possible
                    var func = block as IMyFunctionalBlock;
                    if (func != null)
                    {
                        func.Enabled = false;
                    }
                    else
                    {
                        damageRequired = block.SlimBlock.Integrity - (block.SlimBlock.MaxIntegrity * 0.2);
                        if (damageRequired < 0) damageRequired = 0;
                        block.SlimBlock.DoDamage((float)damageRequired, DamageType, true);
                    }
                    break;
            }
        }
        private void EnforceBlockPunishment(IMyCubeBlock block = null)//this probably needs more attention
        {
            //wtf
            if (block != null)
            {
                foreach (var limit in ShipCore.BlockLimits)
                {
                    var limitBlocks = BlocksPerLimit[limit];
                    var countWeight = limitBlocks.Sum(l => l.Value);
                    Utils.Log($"Block check: {limit.Name} | {countWeight} | {limit.MaxCount}");
                    if (countWeight <= limit.MaxCount) continue;
                    WhackABlock(block,limit.PunishmentType);
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
                        WhackABlock(block,limit.PunishmentType);
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
            return ShipCore.BlockLimits.Where(limit => limit.BlockGroups.Any(group => group.BlockTypes.Any(type =>
                type.TypeId == Utils.GetBlockTypeId(block) && type.SubtypeId == Utils.GetBlockSubtypeId(block))));
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