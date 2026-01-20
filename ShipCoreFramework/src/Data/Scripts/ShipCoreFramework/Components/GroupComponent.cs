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
    public class GroupComponent
    {
        internal ShipCore ShipCore => Session.Config.GetShipCoreByTypeId(MainCoreComponent?.SubtypeId ?? string.Empty);
        internal GridModifiers Modifiers => CubeGridModifiers.GetActiveModifiers(this);
        internal long OwnerId
        {
            get
            {
                var ownerId = MainCoreComponent?.CoreBlock.OwnerId ?? 0;
                return ownerId != 0 ? ownerId : this.GetMajorityOwnerId();
            }
        }
        internal IMyFaction OwningFaction => MyAPIGateway.Session.Factions.TryGetPlayerFaction(OwnerId);
        internal int GroupBlocksCount => GridDictionary.Sum(g => g.Key.BlocksCount);
        internal int GroupPCU => GridDictionary.Sum(g => g.Key.BlocksPCU);
        internal float GroupMass => GridDictionary.Sum(g => g.Key.Mass);

        private float BoostDuration => ShipCore.Modifiers.BoostDuration;
        private float BoostCoolDown => ShipCore.Modifiers.BoostCoolDown;
        
        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;
        
        internal readonly ConcurrentDictionary<BlockLimit, LimitBucket> Limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();
        internal Dictionary<IMyCubeBlock, CoreComponent> CoreDictionary => Utils.Flatten(GridDictionary.Values, component => component.CoreDictionary);
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
        
        internal void InitGrids()
        {
            // Skip initialization for ignored factions/AI
            if (Utils.IsIgnoredGroup(this))
            {
                Utils.Log($"InitGrids: Skipping ignored group (Faction: {OwningFaction?.Tag ?? "None"})", 2);
                return;
            }

            var tempGridList = new List<IMyCubeGrid>();
            MyGroup.GetGrids(tempGridList);

            foreach (var myCubeGrid in tempGridList)
            {
                var startGrid = (MyCubeGrid)myCubeGrid;
                if (startGrid.IsPreview) return;

                var gridComp = new GridComponent();
                GridDictionary.TryAdd(startGrid, gridComp);
                gridComp.Init(startGrid, MyGroup);
            }
            
            RecalculateAllLimits();
            EnforceGroupPunishment();
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
            Utils.Log($"Activate: Activating logic for {((IMyCubeGrid)grid).CustomName}!", 2);

            GridsPerFactionManager.AddGridGroup(this);
            GridsPerPlayerManager.AddGridGroup(this);

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                RecalculateAllLimits();
                ApplyModifiers(Modifiers);
                EnforceGroupPunishment();
                
                ModAPI.BroadcastCoreActivated(grid, ShipCore.SubtypeId, ShipCore.UniqueName);
            });
        }

        internal void ResetCore()
        {
            var old = MainCoreComponent;
            IMyCubeGrid oldGrid = null;
            var oldCoreSubtype = string.Empty;
            var oldCoreName = string.Empty;

            if (old != null)
            {
                oldGrid = old.GridComponent.Grid;
                oldCoreSubtype = old.SubtypeId;
                var oldCore = Session.Config.GetShipCoreByTypeId(oldCoreSubtype);
                oldCoreName = oldCore?.UniqueName ?? string.Empty;
                Utils.Log($"Reset: Resetting logic for {oldGrid.CustomName}!", 2);
                GridsPerFactionManager.RemoveGridGroup(this);
                GridsPerPlayerManager.RemoveGridGroup(this);
                old.IsMainCore = false;
            }
            MainCoreComponent = null;
            
            if (oldGrid != null)
            {
                ModAPI.BroadcastCoreDeactivated(oldGrid, oldCoreSubtype, oldCoreName);
            }

            if (!Session.HasStarted || Session.IsShuttingDown) return;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                RecalculateAllLimits();
                ApplyModifiers(Modifiers);
                EnforceGroupPunishment();
            });
        }

        internal void OnGridAdded(IMyGridGroupData addedTo, IMyCubeGrid grid, IMyGridGroupData removedFrom)
        {
            var g = grid as MyCubeGrid;
            if (g == null || g.IsPreview) return;

            if (removedFrom != null)
            {
                GroupComponent oldGroup;
                GridComponent movedComp;
                if (Session.GroupDict.TryGetValue(removedFrom, out oldGroup) && oldGroup.GridDictionary.TryGetValue(g, out movedComp))
                {
                    oldGroup.GridDictionary.Remove(g);
                    movedComp.GroupData = addedTo;
                    GridDictionary[g] = movedComp;
                    
                    oldGroup.RecalculateAllLimits();
                }
                else
                {
                    var gc = new GridComponent();
                    gc.Init(g, addedTo);
                    GridDictionary[g] = gc;
                }
            }
            else
            {
                var gc = new GridComponent();
                gc.Init(g, addedTo);
                GridDictionary[g] = gc;
            }
            
            if (Utils.IsIgnoredGroup(this))
            {
                Utils.Log($"OnGridAdded: Group became ignored after grid addition (Faction: {OwningFaction?.Tag ?? "None"})", 2);
                if (MainCoreComponent != null)
                {
                    ResetCore();
                }
                return;
            }
            RecalculateAllLimits();
            
            ModAPI.BroadcastGridAddedToGroup(grid, addedTo);
        }

        internal void OnGridRemoved(IMyGridGroupData removedFrom, IMyCubeGrid grid, IMyGridGroupData addedTo)
        {
            if (addedTo != null) return;
            var g = grid as MyCubeGrid;
            if (g == null) return;

            GridComponent comp;
            if (GridDictionary.TryGetValue(g, out comp))
            {
                if (MainCoreComponent?.GridComponent.Grid.EntityId == g.EntityId) CoreRemoved(MainCoreComponent);
                comp.Clean();
                GridDictionary.Remove(g);
            }
            RecalculateAllLimits();
            
            ModAPI.BroadcastGridRemovedFromGroup(grid, removedFrom);
        }

        private void EnforceGroupPunishment()
        {
            // Skip block limit enforcement for ignored factions/AI
            if (Utils.IsIgnoredGroup(this))
            {
                return;
            }

            EnforceOverCapacity();

            var totalBlocksPunished = 0;
            foreach (var kv in Limits)
            {
                var limit = kv.Key;
                var bucket = kv.Value;

                if (limit == null || bucket == null) continue;

                double total;
                List<IMySlimBlock> membersCopy;
                lock (bucket.BucketLock)
                {
                    total = bucket.TotalWeight;
                    membersCopy = new List<IMySlimBlock>(bucket.Members);
                }

                if (total <= limit.MaxCount) continue;

                var over = total - limit.MaxCount;
                var candidates = new List<KeyValuePair<IMySlimBlock, double>>(membersCopy.Count);

                foreach (var blk in membersCopy)
                {
                    if (blk == null || blk.IsMovedBySplit || blk.CubeGrid == null) continue;

                    if (limit.AllowedDirections != null && MainCoreComponent?.CoreBlock != null)
                    {
                        if (!IsValidDirection(MainCoreComponent.CoreBlock, blk, limit.AllowedDirections))
                        {
                            WhackABlock(blk, limit.PunishmentType);
                            totalBlocksPunished++;
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
                    totalBlocksPunished++;
                    over -= t.Value;
                }
            }
            
            if (totalBlocksPunished > 0 && MyGroup != null)
            {
                ModAPI.BroadcastLimitsEnforced(MyGroup, totalBlocksPunished);
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
            if ((ShipCore.MaxBlocks > 0 && GroupBlocksCount >= ShipCore.MaxBlocks) ||
                (ShipCore.MaxPCU > 0 && GroupPCU >= ShipCore.MaxPCU) ||
                (ShipCore.MaxMass > 0 && GroupMass >= ShipCore.MaxMass))
            {
                if (ShipCore.LargeGridMobile) PunishSpeed = true;
                if (ShipCore.LargeGridStatic) PunishModifiers = true;
                var modifiers = GetPassiveDefenseModifiers();
                foreach (var kvp in GridDictionary)
                {
                    CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
                }
            }
            else
            {
                if (ShipCore.LargeGridMobile) PunishSpeed = false;
                if (ShipCore.LargeGridStatic) PunishModifiers = false;
                var modifiers1 = GetPassiveDefenseModifiers();
                foreach (var kvp in GridDictionary)
                {
                    CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers1;
                }

                if (!ShipCore.ForceBroadCast || CoreDictionary.Select(kvp => kvp.Key as IMyFunctionalBlock).Any(func => func != null && func.Enabled)) return;

                if (ShipCore.LargeGridMobile) PunishSpeed = true;
                if (ShipCore.LargeGridStatic) PunishModifiers = true;
                var modifiers2 = GetPassiveDefenseModifiers();
                foreach (var kvp in GridDictionary)
                {
                    CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers2;
                }
            }
        }

        internal void ApplyModifiers(GridModifiers modifiers)
        {
            foreach (var kvp in GridDictionary)
            {
                var blocksCopy = kvp.Value.GetBlocksCopy();
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
            var modifiers = GetActiveDefenseModifiers();
            foreach (var kvp in GridDictionary)
            {
                CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
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
                
                if (MainCoreComponent?.GridComponent?.Grid != null)
                {
                    ModAPI.BroadcastBoostDeactivated(MainCoreComponent.GridComponent.Grid);
                }
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

                var modifiers = GetPassiveDefenseModifiers();
                foreach (var kvp in GridDictionary)
                {
                    CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
                }

                _activeDefenseCooldownTimer = ActiveDefenseCoolDown * 60f;
                Utils.ShowNotification("Active Defense Disengaged! Cooldown started.", 1000);
                
                if (MainCoreComponent?.GridComponent?.Grid != null)
                {
                    ModAPI.BroadcastActiveDefenseDeactivated(MainCoreComponent.GridComponent.Grid);
                }
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

            var modifiers = GetActiveDefenseModifiers();
            foreach (var kvp in GridDictionary)
            {
                CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
            }

            Utils.ShowNotification("Active Defense Engaged!", 1000);
            
            if (MainCoreComponent?.GridComponent?.Grid != null)
            {
                ModAPI.BroadcastActiveDefenseActivated(MainCoreComponent.GridComponent.Grid);
            }
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
            
            if (MainCoreComponent?.GridComponent?.Grid != null)
            {
                ModAPI.BroadcastBoostActivated(MainCoreComponent.GridComponent.Grid);
            }
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

            if (PunishModifiers)
            {
                return new GridDefenseModifiers
                {
                    Bullet = ShipCore.PassiveDefenseModifiers.Bullet * MainCoreComponent.CoreBlock.UpgradeValues["PassiveBulletDamage"] * 2,
                    Rocket = ShipCore.PassiveDefenseModifiers.Rocket * MainCoreComponent.CoreBlock.UpgradeValues["PassiveRocketDamage"] * 2,
                    Explosion = ShipCore.PassiveDefenseModifiers.Explosion * MainCoreComponent.CoreBlock.UpgradeValues["PassiveExplosionDamage"] * 2,
                    Environment = ShipCore.PassiveDefenseModifiers.Environment * MainCoreComponent.CoreBlock.UpgradeValues["PassiveEnvironmentDamage"] * 2,
                    PostShield = ShipCore.PassiveDefenseModifiers.PostShield * MainCoreComponent.CoreBlock.UpgradeValues["PassivePostShieldDamage"] * 2,
                    Kinetic = ShipCore.PassiveDefenseModifiers.Kinetic * MainCoreComponent.CoreBlock.UpgradeValues["PassiveKineticDamage"] * 2,
                    Energy = ShipCore.PassiveDefenseModifiers.Energy * MainCoreComponent.CoreBlock.UpgradeValues["PassiveEnergyDamage"] * 2
                };
            }
            else
            {
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
        }

        internal void CoreRemoved(CoreComponent lost)
        {
            if(!ReferenceEquals(lost, MainCoreComponent)) return;
            var newMain = CoreDictionary.Values.FirstOrDefault();
            if (newMain == null)
            {
                ResetCore();
            }
            else
            {
                MainCoreComponent = newMain;
                MainCoreComponent.IsMainCore = true;
                MainCoreComponent.SaveCoreState();
                MainCoreComponent.CoreBlock?.RefreshCustomInfo();
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

                    lock (gridBucket.BucketLock)
                    {
                        lock (groupBucket.BucketLock)
                        {
                            groupBucket.TotalWeight += gridBucket.TotalWeight;
                            groupBucket.Members.AddRange(gridBucket.Members);
                        }
                    }
                }
            }
            
            if (MyGroup != null)
            {
                ModAPI.BroadcastLimitsRecalculated(MyGroup);
            }
        }

        internal void Clean()
        {
            try
            {
                GridsPerFactionManager.RemoveGridGroup(this);
                GridsPerPlayerManager.RemoveGridGroup(this);
            }
            catch (Exception e)
            {
                Utils.Log("LOGGING EXCEPTION (most likely due to closure of world): " + e, 1);
            }
            
            foreach (var kvp in GridDictionary)
            {
                kvp.Value.Clean();
            }
            GridDictionary.Clear();
            Limits.Clear();
        }
    }
}
