#region

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
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

        private bool _activeDefenseEnabled;
        private float _activeDefenseCooldownTimer;
        private float _activeDefenseDurationTimer;

        private bool _needsSubgridsRedone;
        public bool NeedStaticCheck;
        
        public CoreLogic CoreLogic => Utils.GetGridCore(Grid, ShipCore);
        
        private float BoostDuration => ShipCore.Modifiers.BoostDuration;
        private float BoostCoolDown => ShipCore.Modifiers.BoostCoolDown;
        public float ActiveDefenseDuration => ShipCore.ActiveDefenseModifiers.Duration * CoreLogic.CoreBlock.UpgradeValues["DurationDuration"];
        public float ActiveDefenseCoolDown => ShipCore.ActiveDefenseModifiers.Cooldown * CoreLogic.CoreBlock.UpgradeValues["DamageCooldown"];
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
            if(_activeDefenseEnabled)
            {
                Utils.ShowNotification($"Active Defense Time Remaining:{_activeDefenseDurationTimer/60f:0.0}", 1000);
                return;
            }
            if (_activeDefenseCooldownTimer > 0f)
            {
                Utils.ShowNotification($"Active Defense is cooling down! Cooldown Time:{_boostCooldownTimer/60f:0.0}", 1000);
                return;
            }
            _activeDefenseEnabled = true;
            CubeGridModifiers.DefenseModifiers[Grid.EntityId] = GetActiveDefenseModifiers();
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
                    var fatBlocks = grid.GetFatBlocks<MyCubeBlock>().Where(b => !b.IsPreview);
                    Blocks.UnionWith(fatBlocks);
                }
                Enforcement.UpdateLimitsAndApplyModifiers(BlocksPerLimit, ShipCore, Blocks, Modifiers);
                Enforcement.EnforceGridPunishment(Grid);
                _needsSubgridsRedone=false;
            }
            if (NeedStaticCheck)
            {
                if (ShipCore.LargeGridStatic && !ShipCore.LargeGridMobile && !mainGrid.IsStatic) { MyVisualScriptLogicProvider.SetGridStatic(mainGrid.Name, true); }
                if (!ShipCore.LargeGridStatic && mainGrid.IsStatic) { MyVisualScriptLogicProvider.SetGridStatic(mainGrid.Name, false); }
                NeedStaticCheck = false;
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
            if (_activeDefenseEnabled)
            {
                _activeDefenseDurationTimer -= 1f;
                if (!(_activeDefenseDurationTimer <= 0f)) return;
                _activeDefenseEnabled = false;
                CubeGridModifiers.DefenseModifiers[Grid.EntityId] = GetPassiveDefenseModifiers();
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
            //Forwhatever reason on load, the code does not know the difference between a projeced grid and a real one, so we're just going to turn off the projectors :)
            var projectors = new List<IMyProjector>();//ProjectedGrid
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid).GetBlocksOfType(projectors);
            projectors.ForEach(p => p.SetProjectedGrid(null));

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
                Grid.OnIsStaticChanged += OnIsStaticChanged;
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
        
        private static void OnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            var mainLogic = grid.GetMainGridLogic();
            mainLogic.NeedStaticCheck = true;
        }
        
        private void OnBlockAdded(IMySlimBlock obj) //Now tells player why
        {
            var builderId = obj.BuiltBy;
            //Ignore core placement
            var blockDefinition = Utils.GetBlockSubtypeId(obj);
            if (blockDefinition !=null) { if (ModSessionManager.Config.IsValidCoreType(blockDefinition)) { return; } }
            
            //Ignore Admins with Ignore Limits
            
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            if(players.Count > 0)
            {
                var myPlayer = players.FirstOrDefault(p => p.IdentityId == builderId);
                if (myPlayer != null 
                    && MyAPIGateway.Session.IsUserAdmin(myPlayer.SteamUserId) 
                    && MyAPIGateway.Session.IsUserIgnorePCULimit(myPlayer.SteamUserId))
                {
                    Utils.ShowNotification("Block Was Placed By Admin, Block limits NOT Applied.");
                    return;
                }
            }
            var concreteGrid = Grid as MyCubeGrid;
            
            
            Utils.Log($"{Grid.CustomName}: Block Added: {Utils.GetBlockTypeId(obj)} | {Utils.GetBlockSubtypeId(obj)}");
            //MaxBlocks
            if (concreteGrid?.BlocksCount >= ShipCore.MaxBlocks && ShipCore.MaxBlocks > 0)
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == Grid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification($"{Utils.GetBlockSubtypeId(obj)} Violates MaxBlocks: {concreteGrid.BlocksCount > ShipCore.MaxBlocks}", 10000, true);
                }
                Enforcement.RemoveAndRefund(obj);
                return;
            }
            //Missing MaxPCU
            if (Blocks.Sum(b => b?.BlockDefinition?.PCU ?? 1) >= ShipCore.MaxPCU && ShipCore.MaxPCU > 0)
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == Grid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification($"{Utils.GetBlockSubtypeId(obj)} Violates MaxPCU: {concreteGrid.BlocksCount > ShipCore.MaxPCU}", 10000, true);
                }
                Enforcement.RemoveAndRefund(obj);
                return;
            }
            // MaxMass, Currently WET MASS
            if (concreteGrid?.Mass >= ShipCore.MaxMass && ShipCore.MaxMass > 0f)
            {
                if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == Grid.BigOwners.FirstOrDefault())
                {
                    Utils.ShowNotification($"{Utils.GetBlockSubtypeId(obj)} Violates MaxMass: {concreteGrid.BlocksCount > ShipCore.MaxMass}", 10000, true);
                }
                Enforcement.RemoveAndRefund(obj);
                return;
            }

            foreach (var limit in ShipCore.BlockLimits)
            {
                var match = limit.BlockGroups
                    .SelectMany(g => g.BlockTypes)
                    .Any(b => b.TypeId == Utils.GetBlockTypeId(obj) && (b.SubtypeId == "any" || b.SubtypeId == Utils.GetBlockSubtypeId(obj)));

                if (!match) continue;
                if (!BlocksPerLimit.ContainsKey(limit)){continue;}
                var limitBlocks = BlocksPerLimit[limit];
                var countWeight = limitBlocks.Sum(b => b.Value);
                var countForSpecificBlock = limit.BlockGroups.SelectMany(g => g.BlockTypes).First(b => b.TypeId == Utils.GetBlockTypeId(obj) && (b.SubtypeId == "any" || b.SubtypeId == Utils.GetBlockSubtypeId(obj))).CountWeight;

                Utils.Log($"{countWeight} | {countForSpecificBlock} | {limit.MaxCount}");
                if (countWeight + countForSpecificBlock > limit.MaxCount)
                {
                    if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == Grid.BigOwners.FirstOrDefault())
                        Utils.ShowNotification($"{Utils.GetBlockSubtypeId(obj)} Violates Blocklimit {limit.Name}: {countWeight + countForSpecificBlock}/{limit.MaxCount}", 10000, true);
                    //Enhanced to remove it no matter what grid it's on :)
                    Enforcement.RemoveAndRefund(obj);
                    return;
                }
                if (CoreLogic?.CoreBlock != null)
                {
                    if (!Enforcement.IsValidDirection(CoreLogic.CoreBlock, obj, limit.AllowedDirections))
                    { 
                        Enforcement.RemoveAndRefund(obj);
                        return;
                    }
                }
                else Utils.Log("Log Direction Check: \nCoreBlock is null", 3);

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
        private static void OnConnectionChangeCompleted(MyCubeGrid myGrid, GridLinkTypeEnum gridGroupTypeChanged)
        {
            Enforcement.EnforceOverCapacity(myGrid);
            if(gridGroupTypeChanged != GridLinkTypeEnum.Mechanical) return;
            Utils.Log($"Subgrid Status Changed: {(myGrid as IMyCubeGrid).CustomName})");
            List<IMyCubeGrid> subgrids;
            var mainGrid = myGrid.GetMainCubeGrid(out subgrids);
            var mainLogic = mainGrid.GetMainGridLogic();
            mainLogic._needsSubgridsRedone=true;
        }
        
        private static void OnGridMergeOrSplit(IMyCubeGrid main, IMyCubeGrid sub)
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

        public void DefenseValuesChanged()
        {
            CubeGridModifiers.DefenseModifiers[Grid.EntityId] = GetPassiveDefenseModifiers();
        }

        public GridDefenseModifiers GetActiveDefenseModifiers()
        {
            return new GridDefenseModifiers
            {
                Bullet = ShipCore.ActiveDefenseModifiers.Bullet * CoreLogic?.CoreBlock.UpgradeValues["ActiveBulletDamage"] ?? 1,
                Rocket = ShipCore.ActiveDefenseModifiers.Rocket * CoreLogic?.CoreBlock.UpgradeValues["ActiveRocketDamage"] ?? 1,
                Explosion = ShipCore.ActiveDefenseModifiers.Explosion *  CoreLogic?.CoreBlock.UpgradeValues["ActiveExplosionDamage"] ?? 1,
                Environment = ShipCore.ActiveDefenseModifiers.Environment * CoreLogic?.CoreBlock.UpgradeValues["ActiveEnvironmentDamage"] ?? 1,
                Energy = ShipCore.ActiveDefenseModifiers.Energy * CoreLogic?.CoreBlock.UpgradeValues["ActiveEnergyDamage"] ?? 1,
                Kinetic = ShipCore.ActiveDefenseModifiers.Kinetic * CoreLogic?.CoreBlock.UpgradeValues["ActiveKineticDamage"] ?? 1
            };
        }
        
        public GridDefenseModifiers GetPassiveDefenseModifiers()
        {
            return new GridDefenseModifiers
            {
                Bullet = ShipCore.PassiveDefenseModifiers.Bullet * CoreLogic?.CoreBlock.UpgradeValues["PassiveBulletDamage"] ?? 1,
                Rocket = ShipCore.PassiveDefenseModifiers.Rocket * CoreLogic?.CoreBlock.UpgradeValues["PassiveRocketDamage"] ?? 1,
                Explosion = ShipCore.PassiveDefenseModifiers.Explosion *  CoreLogic?.CoreBlock.UpgradeValues["PassiveExplosionDamage"] ?? 1,
                Environment = ShipCore.PassiveDefenseModifiers.Environment * CoreLogic?.CoreBlock.UpgradeValues["PassiveEnvironmentDamage"] ?? 1,
                Energy = ShipCore.PassiveDefenseModifiers.Energy * CoreLogic?.CoreBlock.UpgradeValues["PassiveEnergyDamage"] ?? 1,
                Kinetic = ShipCore.PassiveDefenseModifiers.Kinetic * CoreLogic?.CoreBlock.UpgradeValues["PassiveKineticDamage"] ?? 1
            };
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