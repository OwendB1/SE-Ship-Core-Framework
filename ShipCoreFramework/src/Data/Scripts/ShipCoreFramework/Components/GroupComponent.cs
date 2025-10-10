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
        internal long MajorityOwningPlayerId => this.GetMajorityOwnerId();
        internal IMyFaction OwningFaction => this.GetOwningFaction();
        internal int GroupBlocksCount => GridDictionary.Sum(g => g.Key.BlocksCount);
        internal int GroupPCU => GridDictionary.Sum(g => g.Key.BlocksPCU);
        internal float GroupMass => GridDictionary.Sum(g => g.Key.Mass);

        private float BoostDuration => ShipCore.Modifiers.BoostDuration;
        private float BoostCoolDown => ShipCore.Modifiers.BoostCoolDown;
        
        internal Guid EntityId = Guid.NewGuid();
        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;

        internal readonly ConcurrentDictionary<BlockLimit, LimitBucket> Limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();
        internal readonly ConcurrentDictionary<IMyCubeBlock, CoreComponent> CoreDictionary = new ConcurrentDictionary<IMyCubeBlock, CoreComponent>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridComponent> GridDictionary = new ConcurrentDictionary<MyCubeGrid, GridComponent>();

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
                    return ShipCore.ActiveDefenseModifiers.Duration * MainCoreComponent?.CoreBlock.UpgradeValues["DurationDuration"] ?? 1f;
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
                    return ShipCore.ActiveDefenseModifiers.Cooldown * MainCoreComponent?.CoreBlock.UpgradeValues["DamageCooldown"] ?? 1f;
                }
                return ShipCore.ActiveDefenseModifiers.Cooldown;
            }
        }

        private void RecalculateAllLimits()
        {
            Limits.Clear();

            foreach (var comp in GridDictionary.Values)
            {
                comp.RecalculateLimits(this);

                foreach (var gridLimitKv in comp.Limits)
                {
                    var limit = gridLimitKv.Key;
                    var gridBucket = gridLimitKv.Value;

                    LimitBucket groupBucket;
                    if (!Limits.TryGetValue(limit, out groupBucket))
                    {
                        groupBucket = new LimitBucket(0d);
                        Limits[limit] = groupBucket;
                    }

                    groupBucket.TotalWeight += gridBucket.TotalWeight;
                    groupBucket.Members.AddRange(gridBucket.Members);
                }
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
            Utils.Log("Activate: Activating logic for " + ((IMyCubeGrid)grid).CustomName + " (group id: " + EntityId + ")!");

            GridsPerFactionManager.AddGridGroup(this);
            GridsPerPlayerManager.AddGridGroup(this);
            
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                RebuildGroupState();
                RecalculateAllLimits();
                ApplyModifiers(Modifiers);
                EnforceGroupPunishment();
            });
        }

        internal void ResetCore()
        {
            var old = MainCoreComponent;
            if (old != null)
            {
                var grid = old.GridComponent.Grid;
                Utils.Log("Reset: Resetting logic for " + ((IMyCubeGrid)grid).CustomName + " (group id: " + EntityId + ")!");
                old.IsMainCore = false;
            }
            MainCoreComponent = null;

            GridsPerFactionManager.RemoveGridGroup(this);
            GridsPerPlayerManager.RemoveGridGroup(this);

            if (!Session.HasStarted || Session.IsShuttingDown) return;
            
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                RebuildGroupState();
                RecalculateAllLimits();
                ApplyModifiers(Modifiers);
                EnforceGroupPunishment();
            });
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

            RebuildGroupState();
            RecalculateAllLimits();
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
            RecalculateAllLimits();
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
            RecalculateAllLimits();
        }

        private void EnforceGroupPunishment()
        {
            EnforceOverCapacity();

            foreach (var kv in Limits)
            {
                var limit = kv.Key;
                var bucket = kv.Value;

                if (limit == null || bucket == null) continue;

                var total = bucket.TotalWeight;
                if (total <= limit.MaxCount) continue;

                var over = total - limit.MaxCount;
                var candidates = new List<KeyValuePair<IMySlimBlock, double>>(bucket.Members.Count);

                var membersCopy = new List<IMySlimBlock>(bucket.Members);
                foreach (var blk in membersCopy)
                {
                    if (blk == null || blk.IsMovedBySplit || blk.CubeGrid == null) continue;

                    if (limit.AllowedDirections != null && MainCoreComponent?.CoreBlock != null)
                    {
                        if (!IsValidDirection(MainCoreComponent.CoreBlock, blk, limit.AllowedDirections))
                        {
                            WhackABlock(blk, limit.PunishmentType);
                            continue;
                        }
                    }

                    var w = limit.GetWeight(GridComponent.KeyOf(blk));
                    if (w > 0d) candidates.Add(new KeyValuePair<IMySlimBlock, double>(blk, w));
                }

                candidates.Sort((a, b) => a.Value.CompareTo(b.Value));

                foreach (var t in candidates)
                {
                    if (over <= 0d) break;
                    if (t.Key == null) continue;

                    WhackABlock(t.Key, limit.PunishmentType);
                    over -= t.Value;
                }
            }
        }

        internal void WhackABlock(IMySlimBlock block, PunishmentType harm, MyStringHash? customDamageType = null)
        {
            var damageType = customDamageType ?? DamageTypeBlockLimit;
            var func = block.FatBlock as IMyFunctionalBlock;

            switch (harm)
            {
                case PunishmentType.Damage:
                    var damageRequired = block.Integrity - block.MaxIntegrity * 0.5;
                    if (damageRequired < 0) damageRequired = 0;
                    block.DoDamage((float)damageRequired, damageType, true);
                    break;
                case PunishmentType.Delete:
                    if (func != null) func.Enabled = false;
                    if (GridDictionary.ContainsKey((MyCubeGrid)block.CubeGrid))
                    {
                        var gridComponent = GridDictionary[(MyCubeGrid)block.CubeGrid];
                        gridComponent.RemoveAndRefund(block);
                    }
                    break;
                case PunishmentType.Explode:
                    block.DoDamage(block.Integrity, damageType, true);
                    break;
                case PunishmentType.ShutOff:
                default:
                    if (func != null) func.Enabled = false;
                    break;
            }
        }

        private void EnforceOverCapacity()
        {
            if ((ShipCore.MaxBlocks > 0 && GroupBlocksCount > ShipCore.MaxBlocks) ||
                (ShipCore.MaxPCU > 0 && GroupPCU > ShipCore.MaxPCU) ||
                (ShipCore.MaxMass > 0 && GroupMass > ShipCore.MaxMass))
            {
                if (ShipCore.LargeGridMobile) PunishSpeed = true;
                if (ShipCore.LargeGridStatic) PunishModifiers = true;
            }

            if ((ShipCore.MaxBlocks > 0 && GroupBlocksCount >= ShipCore.MaxBlocks) ||
                (ShipCore.MaxPCU > 0 && GroupPCU >= ShipCore.MaxPCU) ||
                (ShipCore.MaxMass > 0 && GroupMass >= ShipCore.MaxMass)) return;

            if (ShipCore.LargeGridMobile) PunishSpeed = false;
            if (ShipCore.LargeGridStatic) PunishModifiers = false;

            if (!ShipCore.ForceBroadCast || CoreDictionary.Select(kvp => kvp.Key as IMyFunctionalBlock).Any(func => func != null && func.Enabled)) return;

            if (ShipCore.LargeGridMobile) PunishSpeed = true;
            if (ShipCore.LargeGridStatic) PunishModifiers = true;
        }

        internal void ApplyModifiers(GridModifiers modifiers)
        {
            foreach (var kv in GridDictionary)
            {
                var blocksCopy = kv.Value.GetBlocksCopy();
                foreach (var bl in blocksCopy)
                {
                    var fatBlock = bl?.FatBlock;
                    var terminalBlock = fatBlock as IMyTerminalBlock;
                    if (terminalBlock != null)
                        CubeGridModifiers.ApplyModifiers(terminalBlock, modifiers);
                }
            }
        }

        internal static bool IsValidDirection(IMyCubeBlock myCore, IMySlimBlock block, List<DirectionType> allowedDirections)
        {
            if (myCore?.Orientation == null || block?.Orientation == null || allowedDirections == null || allowedDirections.Count == 0)
                return true;

            if (myCore.CubeGrid != block.CubeGrid) return true;

            var coreFDir = myCore.Orientation.Forward;
            var coreUDir = myCore.Orientation.Up;

            var f = Base6Directions.GetVector(coreFDir);
            var u = Base6Directions.GetVector(coreUDir);
            var b = Base6Directions.GetVector(Base6Directions.GetOppositeDirection(coreFDir));

            Vector3 l, r;
            Vector3.Cross(ref u, ref f, out l);
            Vector3.Cross(ref f, ref u, out r);

            var bf = Base6Directions.GetVector(block.Orientation.Forward);
            var xyDirection =
                bf == f ? DirectionType.Forward :
                bf == b ? DirectionType.Backward :
                bf == l ? DirectionType.Left :
                bf == r ? DirectionType.Right :
                bf == u ? DirectionType.Up :
                DirectionType.Down;

            var isValid = allowedDirections.Contains(xyDirection);
            if (!isValid)
                Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + ": the direction " + xyDirection + " is invalid",
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
            if (_activeDefenseEnabled)
            {
                Utils.ShowNotification("Active Defense Time Remaining:" + (_activeDefenseDurationTimer / 60f).ToString("0.0"), 1000);
                return;
            }
            if (_activeDefenseCooldownTimer > 0f)
            {
                Utils.ShowNotification("Active Defense is cooling down! Cooldown Time:" + (_boostCooldownTimer / 60f).ToString("0.0"), 1000);
                return;
            }
            _activeDefenseEnabled = true;
            _activeDefenseDurationTimer = ActiveDefenseDuration * 60f;
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
                Utils.ShowNotification("Boost Time Remaining:" + (_boostDurationTimer / 60f).ToString("0.0"), 1000);
                return;
            }
            if (_boostCooldownTimer > 0f)
            {
                Utils.ShowNotification("Boost is cooling down! Cooldown Time:" + (_boostCooldownTimer / 60f).ToString("0.0"), 1000);
                return;
            }
            BoostEnabled = true;
            _boostDurationTimer = BoostDuration * 60f;
            Utils.ShowNotification("Boost Engaged!", 1000);
        }

        internal GridDefenseModifiers GetActiveDefenseModifiers()
        {
            if (MainCoreComponent == null || MainCoreComponent.CoreBlock == null)
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
            if (MainCoreComponent == null || MainCoreComponent.CoreBlock == null)
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
            if (!Session.HasStarted || Session.IsShuttingDown) return;

            CoreDictionary.Clear();

            foreach (var comp in GridDictionary.Values)
            {
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

            if (!ReferenceEquals(newMain, oldMain))
            {
                if (newMain == null)
                {
                    ResetCore();
                    return;
                }

                if (oldMain != null) ResetCore();
                Activate(newMain);
                return;
            }

            RecalculateAllLimits();
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
            Limits.Clear();
        }
    }
}
