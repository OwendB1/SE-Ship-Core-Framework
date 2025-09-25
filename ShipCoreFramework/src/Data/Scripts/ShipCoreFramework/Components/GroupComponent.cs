using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ShipCoreFramework
{
    internal class GroupComponent
    {
        internal ShipCore ShipCore => Session.Config.GetShipCoreByTypeId(MainCoreComponent?.SubtypeId ?? string.Empty);
        internal GridModifiers Modifiers => CubeGridModifiers.GetActiveModifiers(this);
        internal long MajorityOwningPlayerId => this.GetMajorityOwner();
        internal IMyFaction OwningFaction => this.GetOwningFaction();
        internal int GroupBlocksCount => GridDictionary.Sum(g => g.Key.BlocksCount);
        internal int GroupPCU => GridDictionary.Sum(g => g.Key.BlocksPCU);
        internal float GroupMass => GridDictionary.Sum(g => g.Key.Mass);
        
        private float BoostDuration => ShipCore.Modifiers.BoostDuration;
        private float BoostCoolDown => ShipCore.Modifiers.BoostCoolDown;

        internal Guid EntityId = Guid.NewGuid();
        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;
        internal readonly ConcurrentDictionary<BlockLimit, Dictionary<MyCubeBlock, double>> BlocksPerLimit = new ConcurrentDictionary<BlockLimit, Dictionary<MyCubeBlock, double>>();
        internal readonly ConcurrentDictionary<MyCubeBlock, CoreComponent> CoreDictionary = new ConcurrentDictionary<MyCubeBlock, CoreComponent>();
        internal readonly Dictionary<MyCubeGrid, GridComponent> GridDictionary = new Dictionary<MyCubeGrid, GridComponent>();
        
        internal bool PunishModifiers;
        internal bool PunishSpeed;
        internal bool BoostEnabled;
        
        private float _boostCooldownTimer;
        private float _boostDurationTimer;

        private bool _activeDefenseEnabled;
        private float _activeDefenseCooldownTimer;
        private float _activeDefenseDurationTimer;
        
        private static readonly MyStringHash DamageTypeBlockLimit = MyStringHash.GetOrCompute("BlockLimitsViolation");
        
        internal float ActiveDefenseDuration
        {
            get
            {
                if (MainCoreComponent?.CoreBlock != null)
                {
                    return ShipCore.ActiveDefenseModifiers.Duration* MainCoreComponent?.CoreBlock.UpgradeValues["DurationDuration"] ?? 1f; 
                }
                return ShipCore.ActiveDefenseModifiers.Duration; 
            }
        }
        internal float ActiveDefenseCoolDown
        {
            get
            {
                if (MainCoreComponent?.CoreBlock != null)
                {
                    return ShipCore.ActiveDefenseModifiers.Cooldown* MainCoreComponent?.CoreBlock.UpgradeValues["DamageCooldown"] ?? 1f; 
                }
                return ShipCore.ActiveDefenseModifiers.Cooldown; 
            }
        }
        
        internal void Activate(CoreComponent coreComponent)
        {
            var old = MainCoreComponent;
            if (!ReferenceEquals(old, coreComponent))
            {
                if (old != null) old.IsMainCore = false;
                coreComponent.IsMainCore = true;
            }

            MainCoreComponent = coreComponent;

            var grid = MainCoreComponent.GridComponent.Grid;
            Utils.Log($"Activate: Activating logic for {((IMyCubeGrid)grid).CustomName} (group id: {EntityId})!");

            GridsPerFactionManager.AddGridGroup(this);
            GridsPerPlayerManager.AddGridGroup(this);

            ApplyModifiers(Modifiers);
            EnforceGroupPunishment();
        }

        internal void ResetCore()
        {
            var old = MainCoreComponent;
            if (old != null)
            {
                var grid = old.GridComponent.Grid;
                Utils.Log($"Reset: Resetting logic for {((IMyCubeGrid)grid).CustomName} (group id: {EntityId})!");
                old.IsMainCore = false;
            }
            MainCoreComponent = null;

            GridsPerFactionManager.RemoveGridGroup(this);
            GridsPerPlayerManager.RemoveGridGroup(this);

            ApplyModifiers(Modifiers);
            EnforceGroupPunishment();
        }
        
        internal void InitGrids()
        {
            var tempGridList = new List<IMyCubeGrid>();
            MyGroup.GetGrids(tempGridList);
            
            foreach (var myCubeGrid in tempGridList)
            {
                var startGrid = (MyCubeGrid)myCubeGrid;
                if (startGrid.IsPreview) continue;

                var gridComp = new GridComponent();
                gridComp.Init(startGrid, MyGroup);
                GridDictionary[startGrid] = gridComp;
            }
            
            EnforceGroupPunishment();
        }
        
        internal void OnGridAdded(IMyGridGroupData addedTo, IMyCubeGrid grid, IMyGridGroupData removedFrom)
        {
            var g = grid as MyCubeGrid;
            if (g == null || g.IsPreview) return;

            if (removedFrom != null)
            {
                GroupComponent src;
                GridComponent moved;
                if (Session.GroupDict.TryGetValue(removedFrom, out src) &&
                    src.GridDictionary.TryGetValue(g, out moved))
                {
                    src.GridDictionary.Remove(g);
                    moved.GroupData = addedTo;
                    GridDictionary[g] = moved;
                }
                else
                {
                    var gc = new GridComponent();
                    gc.Init(g, MyGroup);
                    GridDictionary[g] = gc;
                }
            }
            else
            {
                var gc = new GridComponent();
                gc.Init(g, MyGroup);
                GridDictionary[g] = gc;
            }

            RebuildGroupState();
        }

        internal void OnGridRemoved(IMyGridGroupData removedFrom, IMyCubeGrid grid, IMyGridGroupData addedTo)
        {
            var g = grid as MyCubeGrid;
            if (g == null) return;

            GridComponent comp;
            if (GridDictionary.TryGetValue(g, out comp))
            {
                comp.Clean();
                GridDictionary.Remove(g);
            }

            RebuildGroupState();
        }
        
        private void EnforceGroupPunishment()
        {
            EnforceOverCapacity();
            
            var core = MainCoreComponent?.CoreBlock;
            foreach (var kv in BlocksPerLimit)
            {
                var limit = kv.Key;
                var entries = kv.Value;
                if (entries == null || entries.Count == 0) continue;

                var allowed = limit.AllowedDirections;
                var total = 0d;

                // Collect only direction-valid candidates; punish invalid direction immediately.
                var candidates = new List<KeyValuePair<MyCubeBlock, double>>(entries.Count);

                foreach (var entry in entries)
                {
                    var blk = entry.Key;
                    if (blk == null || blk.Closed || blk.CubeGrid == null) continue;

                    var w = entry.Value;
                    if (w <= 0d) continue;

                    if (allowed != null && core != null && blk.SlimBlock != null)
                    {
                        if (!IsValidDirection(core, blk.SlimBlock, allowed))
                        {
                            WhackABlock(blk, limit.PunishmentType);
                            continue;
                        }
                    }

                    candidates.Add(new KeyValuePair<MyCubeBlock, double>(blk, w));
                    total += w;
                }

                if (total <= limit.MaxCount) continue;
                var over = total - limit.MaxCount;
                foreach (var e in candidates.OrderByDescending(x => x.Value))
                {
                    if (over <= 0d) break;
                    WhackABlock(e.Key, limit.PunishmentType);
                    over -= e.Value;
                }
            }
        }
        
        internal void WhackABlock(IMyCubeBlock block, PunishmentType harm, MyStringHash? customDamageType = null)
        {
            if (block?.SlimBlock == null) return;
            var damageType = customDamageType ?? DamageTypeBlockLimit;
            var func = block as IMyFunctionalBlock;

            switch (harm)
            {
                case PunishmentType.Damage:
                    // Whack,50%
                    var damageRequired = block.SlimBlock.Integrity - block.SlimBlock.MaxIntegrity * 0.5;
                    if (damageRequired < 0) damageRequired = 0;
                    block.SlimBlock.DoDamage((float)damageRequired, damageType, true);
                    break;
                case PunishmentType.Delete:
                    if (func != null) func.Enabled = false;
                    block.CubeGrid.RemoveBlock(block.SlimBlock, true);
                    break;
                case PunishmentType.Explode:
                    //Game will cause explosion on damage = Integrity, if block explodes on destruction most do, if not... I don't care that much.
                    block.SlimBlock.DoDamage(block.SlimBlock.Integrity, damageType, true);
                    break;
                case PunishmentType.ShutOff:
                default:
                    if (func != null) func.Enabled = false;
                    break;
            }
        }
        
        private void EnforceOverCapacity()
        {
            if ((GroupBlocksCount > ShipCore.MaxBlocks && ShipCore.MaxBlocks > 0) ||
                (GroupPCU > ShipCore.MaxPCU && ShipCore.MaxPCU > 0) ||
                (GroupMass > ShipCore.MaxMass && ShipCore.MaxMass > 0f))
            {
                if (ShipCore.LargeGridMobile) PunishSpeed = true;
                if (ShipCore.LargeGridStatic) PunishModifiers = true;
            }

            if ((GroupBlocksCount >= ShipCore.MaxBlocks && ShipCore.MaxBlocks > 0) ||
                (GroupPCU >= ShipCore.MaxPCU && ShipCore.MaxPCU > 0)||
                (GroupMass >= ShipCore.MaxMass && ShipCore.MaxMass > 0)) return;
            
            if (ShipCore.LargeGridMobile) PunishSpeed = false;
            if (ShipCore.LargeGridStatic) PunishModifiers = false;
            
            if (!ShipCore.ForceBroadCast == false || GridDictionary.Any(dict => dict.Value.Blocks.OfType<IMyFunctionalBlock>().Any(block => block.Enabled))) PunishSpeed = true;
        }
        
        internal void ApplyModifiers(GridModifiers modifiers)
        {
            foreach (var kv in GridDictionary)
            foreach (var blk in kv.Value.Blocks)
                if (blk is IMyTerminalBlock) CubeGridModifiers.ApplyModifiers(blk, modifiers);
        }
        
        internal static bool IsValidDirection(IMyCubeBlock myCore, IMySlimBlock block, List<DirectionType> allowedDirections)
        {
            // basic guards
            if (myCore?.Orientation == null || block?.Orientation == null || allowedDirections == null || allowedDirections.Count == 0)
                return true;

            // different subgrids -> ignore locking (matches your original)
            if (myCore.CubeGrid != block.CubeGrid) return true;

            // core axes as Base6Directions
            var coreFDir = myCore.Orientation.Forward;
            var coreUDir = myCore.Orientation.Up;

            // convert to Vector3I
            var f = Base6Directions.GetVector(coreFDir);
            var u = Base6Directions.GetVector(coreUDir);

            // derive opposite directions as vectors
            var b = Base6Directions.GetVector(Base6Directions.GetOppositeDirection(coreFDir));

            // derive left/right via cross products (note the ref/in/out form)
            Vector3 l, r;
            Vector3.Cross(ref u, ref f, out l); // left  = U × F
            Vector3.Cross(ref f, ref u, out r); // right = F × U

            // block forward vector
            var bf = Base6Directions.GetVector(block.Orientation.Forward);

            // classify relative direction exactly like your original logic
            var xyDirection =
                bf == f ? DirectionType.Forward :
                bf == b ? DirectionType.Backward :
                bf == l ? DirectionType.Left :
                bf == r ? DirectionType.Right :
                bf == u ? DirectionType.Up :
                DirectionType.Down; // must be opposite of U

            var isValid = allowedDirections.Contains(xyDirection);
            if (!isValid)
                Utils.ShowNotification($"{Utils.GetBlockSubtypeId(block)}: the direction {xyDirection} is invalid",
                    10000, myCore.CubeGrid.BigOwners.FirstOrDefault(), true);

            return isValid;
        }
        
        public void DefenseValuesChanged()
        {
            foreach (var kvp in GridDictionary)
            {
                CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = GetActiveDefenseModifiers();
            }
        }
        
        internal void RunBoostTimerTick()
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

        internal void RunActiveDefenseTimerTick()
        {
            if (_activeDefenseEnabled)
            {
                _activeDefenseDurationTimer -= 1f;
                if (!(_activeDefenseDurationTimer <= 0f)) return;
                _activeDefenseEnabled = false;
                foreach (var kvp in GridDictionary)
                {
                    CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = GetPassiveDefenseModifiers();
                }
                _activeDefenseCooldownTimer = ActiveDefenseCoolDown * 60f;
                Utils.ShowNotification("Active Defense Disengaged! Cooldown started.", 1000);
            }
            else if (_activeDefenseCooldownTimer > 0f)
            {
                _activeDefenseCooldownTimer -= 1f;
                if (_activeDefenseCooldownTimer < 0f) _activeDefenseCooldownTimer = 0f;
            }
        }
        
        internal void ActivateDefense()
        {
            if (!ShipCore.EnableActiveDefenseModifiers)
            {
                Utils.ShowNotification("Active defense is not allowed on this grid!", 1000);
                return;
            }
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
            _activeDefenseDurationTimer = ActiveDefenseDuration * 60f; // duration in seconds to ticks
            foreach (var kvp in GridDictionary)
            {
                CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = GetActiveDefenseModifiers();
            }
            Utils.ShowNotification("Active Defense Engaged!", 1000);
        }
        
        internal void ActivateBoost()
        {
            if (!ShipCore.SpeedBoostEnabled)
            {
                Utils.ShowNotification("Boosting is not allowed on this grid!", 1000);
                return;
            }
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
        
        internal GridDefenseModifiers GetActiveDefenseModifiers()
        {
            if (MainCoreComponent?.CoreBlock == null)
            {
                return ShipCore.ActiveDefenseModifiers;
            }
            
            return new GridDefenseModifiers
            {
                Bullet = ShipCore.ActiveDefenseModifiers.Bullet * MainCoreComponent.CoreBlock.UpgradeValues["ActiveBulletDamage"],
                Rocket = ShipCore.ActiveDefenseModifiers.Rocket * MainCoreComponent.CoreBlock.UpgradeValues["ActiveRocketDamage"],
                Explosion = ShipCore.ActiveDefenseModifiers.Explosion * MainCoreComponent.CoreBlock.UpgradeValues["ActiveExplosionDamage"],
                Environment = ShipCore.ActiveDefenseModifiers.Environment * MainCoreComponent.CoreBlock.UpgradeValues["ActiveEnvironmentDamage"],
                PostShield = ShipCore.ActiveDefenseModifiers.PostShield * MainCoreComponent.CoreBlock.UpgradeValues["ActivePostShieldDamage"],
                Kinetic = ShipCore.ActiveDefenseModifiers.Kinetic * MainCoreComponent.CoreBlock.UpgradeValues["ActiveKineticDamage"],
                Energy = ShipCore.ActiveDefenseModifiers.Energy * MainCoreComponent.CoreBlock.UpgradeValues["ActiveEnergyDamage"]
            };
        }
        
        internal GridDefenseModifiers GetPassiveDefenseModifiers()
        {
            if (MainCoreComponent?.CoreBlock == null)
            {
                return ShipCore.PassiveDefenseModifiers;
            }
            return new GridDefenseModifiers
            {
                Bullet = ShipCore.PassiveDefenseModifiers.Bullet * MainCoreComponent.CoreBlock.UpgradeValues["PassiveBulletDamage"],
                Rocket = ShipCore.PassiveDefenseModifiers.Rocket * MainCoreComponent.CoreBlock.UpgradeValues["PassiveRocketDamage"],
                Explosion = ShipCore.PassiveDefenseModifiers.Explosion * MainCoreComponent.CoreBlock.UpgradeValues["PassiveExplosionDamage"],
                Environment = ShipCore.PassiveDefenseModifiers.Environment * MainCoreComponent.CoreBlock.UpgradeValues["PassiveEnvironmentDamage"],
                PostShield = ShipCore.PassiveDefenseModifiers.PostShield * MainCoreComponent.CoreBlock.UpgradeValues["PassivePostShieldDamage"],
                Kinetic = ShipCore.PassiveDefenseModifiers.Kinetic * MainCoreComponent.CoreBlock.UpgradeValues["PassiveKineticDamage"],
                Energy = ShipCore.PassiveDefenseModifiers.Energy * MainCoreComponent.CoreBlock.UpgradeValues["PassiveEnergyDamage"]
            };
        }
        
        internal void OnCoreRemoved(CoreComponent lost)
        {
            var blk = (MyCubeBlock)lost.CoreBlock;
            var gc = lost.GridComponent;
            gc?.CoreDictionary.Remove(blk);
            CoreDictionary.Remove(blk);
            MyAPIGateway.Utilities.InvokeOnGameThread(RebuildGroupState);
        }
        
        private void RebuildGroupState()
        {
            var comps = GridDictionary.Values.ToList();
            
            BlocksPerLimit.Clear();
            CoreDictionary.Clear();

            foreach (var comp in comps)
            {
                foreach (var kv in comp.BlocksPerLimit)
                {
                    Dictionary<MyCubeBlock, double> groupInner;

                    if (!BlocksPerLimit.TryGetValue(kv.Key, out groupInner))
                    {
                        groupInner = kv.Value;
                        BlocksPerLimit[kv.Key] = groupInner;
                    }
                    else
                    {
                        if (ReferenceEquals(groupInner, kv.Value)) continue;
                        foreach (var entry in kv.Value.Where(entry => !groupInner.ContainsKey(entry.Key)))
                        {
                            groupInner[entry.Key] = entry.Value;
                        }
                    }
                }
                
                foreach (var coreKvp in comp.CoreDictionary)
                {
                    CoreDictionary[coreKvp.Key] = coreKvp.Value;
                }
            }
            
            var oldMain = MainCoreComponent;
            var candidates = CoreDictionary.Values
                .Where(c => c?.CoreBlock?.CubeGrid != null && GridDictionary.ContainsKey((MyCubeGrid)c.CoreBlock.CubeGrid))
                .OrderBy(c => c.CoreBlock.EntityId)
                .ToList();
            
            var newMain = candidates.FirstOrDefault();
            if (newMain == null)
            {
                ResetCore();
                return;
            }

            if (!ReferenceEquals(newMain, oldMain))
            {
                if (oldMain != null) ResetCore();
                Activate(newMain);
                return;
            }
            
            ApplyModifiers(Modifiers);
            EnforceGroupPunishment();
        }
        
        internal void Clean()
        {
            foreach (var kvp in GridDictionary)
            {
                kvp.Value.Clean();
            }
            GridDictionary.Clear();
            CoreDictionary.Clear();
            BlocksPerLimit.Clear();
        }
    }
}