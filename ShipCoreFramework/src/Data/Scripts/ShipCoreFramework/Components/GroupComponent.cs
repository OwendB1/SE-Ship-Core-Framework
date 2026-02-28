using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    public class GroupComponent
    {
        internal ShipCore ShipCore => Session.Config.GetShipCoreByTypeId(MainCoreComponent?.SubtypeId ?? string.Empty);
        internal GridModifiers Modifiers => CubeGridModifiers.GetActiveModifiers(this);
        internal SpeedModifiers SpeedModifiers => CubeGridModifiers.GetActiveSpeedModifiers(this);
        private long _lastOwnerId;
        internal long OwnerId
        {
            get
            {
                long ownerId;
                if (MainCoreComponent != null)
                {
                    ownerId = MainCoreComponent.CoreBlock.OwnerId;
                    ownerId = ownerId == 0 ? MainCoreComponent.CoreBlock.SlimBlock.BuiltBy : ownerId;
                } else ownerId = this.GetMajorityOwnerId();
                
                if (_lastOwnerId != 0 && ownerId != 0 && _lastOwnerId != ownerId)
                {
                    var relation = MyIDModule.GetRelationPlayerPlayer(_lastOwnerId, ownerId);
                    if (relation != MyRelationsBetweenPlayers.Allies)
                    {
                        Utils.ShowChatMessage($"Not changing ownership to {ownerId} because it's not an ally!");
                        var cube = MainCoreComponent?.CoreBlock as MyCubeBlock;
                        cube?.ChangeOwner(_lastOwnerId, MyOwnershipShareModeEnum.Faction);
                        return _lastOwnerId;
                    }
                    
                    Utils.Log($"OwnerId: Changed from {_lastOwnerId} to {ownerId}", 2);
                    var newOwningFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
                    var oldOwningFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(_lastOwnerId);
                    
                    var coreType = ShipCore.SubtypeId;
                    GridsPerFactionManager.RemoveGridGroup(oldOwningFaction, coreType);
                    GridsPerPlayerManager.RemoveGridGroup(_lastOwnerId, coreType);
                    
                    GridsPerFactionManager.AddGridGroup(newOwningFaction, coreType);
                    GridsPerPlayerManager.AddGridGroup(ownerId, coreType);
                    
                    var isWithinFactionLimits = GridsPerFactionManager.IsGroupWithinFactionLimits(newOwningFaction, ownerId, coreType);
                    var isWithinPlayerLimits = GridsPerPlayerManager.IsGroupWithinPlayerLimits(ownerId, coreType);
                    if (!isWithinFactionLimits || !isWithinPlayerLimits)
                    {
                        var cube = MainCoreComponent?.CoreBlock as MyCubeBlock;
                        cube?.ChangeOwner(_lastOwnerId, MyOwnershipShareModeEnum.Faction);
                        
                        GridsPerFactionManager.RemoveGridGroup(newOwningFaction, coreType);
                        GridsPerPlayerManager.RemoveGridGroup(ownerId, coreType);
                    
                        GridsPerFactionManager.AddGridGroup(oldOwningFaction, coreType);
                        GridsPerPlayerManager.AddGridGroup(_lastOwnerId, coreType);
                        
                        ownerId = _lastOwnerId;
                    }
                }
                _lastOwnerId = ownerId;
                return ownerId;
            }
        }
        internal IMyFaction OwningFaction => MyAPIGateway.Session.Factions.TryGetPlayerFaction(OwnerId);
        internal int GroupBlocksCount => GridDictionary.Sum(g => g.Key.BlocksCount);
        internal int GroupPCU => GridDictionary.Sum(g => g.Key.BlocksPCU);
        internal float GroupMass => GridDictionary.Sum(g => g.Key.Mass);

        private float BoostDuration => ShipCore.SpeedModifiers.BoostDuration;
        private float BoostCoolDown => ShipCore.SpeedModifiers.BoostCoolDown;
        
        internal IMyGridGroupData MyGroup;
        internal CoreComponent MainCoreComponent;
        
        internal readonly Dictionary<BlockLimit, LimitBucket> Limits = new Dictionary<BlockLimit, LimitBucket>();
        internal readonly Dictionary<MyCubeGrid, GridComponent> GridDictionary = new Dictionary<MyCubeGrid, GridComponent>();
        
        internal Dictionary<IMyCubeBlock, CoreComponent> CoreDictionary => Utils.Flatten(GridDictionary.Values, component => component.CoreDictionary);
        
        internal bool PunishModifiers;
        internal bool PunishSpeed;
        internal bool BoostEnabled;
        
        internal bool FrictionEnforcementEnabled = true;
        
		internal bool PostBoostRampActive;
		internal float PostBoostRampCap = -1f;
        
        // Optional runtime overrides (per logical group).
        // -1 means "no override, use config-derived values".
        internal float FrictionMaximumDecelerationOverride = -1f;
        internal float MinimumFrictionSpeedAbsoluteOverride = -1f;
        internal float MaximumFrictionSpeedAbsoluteOverride = -1f;
        internal float MinimumFrictionSpeedModifierOverride = -1f;
        internal float MaximumFrictionSpeedModifierOverride = -1f;

        private float _boostCooldownTimer;
        private float _boostDurationTimer;

        private bool _activeDefenseEnabled;
        private float _activeDefenseCooldownTimer;
        private float _activeDefenseDurationTimer;

        private bool _closing;

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
            var tempGridList = new List<IMyCubeGrid>();
            MyGroup.GetGrids(tempGridList);
            
            foreach (var myCubeGrid in tempGridList)
            {
                var startGrid = (MyCubeGrid)myCubeGrid;
                if (startGrid.IsPreview) return;

                var gridComp = new GridComponent();
                GridDictionary.Add(startGrid, gridComp);
                gridComp.Init(startGrid, MyGroup);
            }

            // Needs to be done full frame later as otherwise not all grids have gone through activation
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if(_closing) return;
                RecalculateAllLimits();
                EnforceGroupPunishment();
                EnforceOverCapacity();
            });
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
            Utils.Log($"Activate: Activating logic for {((IMyCubeGrid)grid).CustomName}!", 1);

            GridsPerFactionManager.AddGridGroup(OwningFaction, ShipCore.SubtypeId);
            GridsPerPlayerManager.AddGridGroup(OwnerId, ShipCore.SubtypeId);
            
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if(_closing) return;
                RecalculateAllLimits();
                ApplyModifiers(Modifiers);
                EnforceGroupPunishment();
                EnforceOverCapacity();
                
                ModAPI.BroadcastCoreActivated(GetRepresentativeGridId(), ShipCore.SubtypeId, ShipCore.UniqueName);
            });
        }

        internal void ResetCore()
        {
            var old = MainCoreComponent;
            var type = ShipCore.SubtypeId;
            var grid = old.CoreBlock.CubeGrid;
            Utils.Log($"Reset: Resetting logic for {grid.CustomName}!", 2);
            
            GridsPerFactionManager.RemoveGridGroup(OwningFaction, type);
            GridsPerPlayerManager.RemoveGridGroup(OwnerId, type);
            
            ModAPI.BroadcastCoreDeactivated(GetRepresentativeGridId(), type, old.CoreBlock.CustomName);
            
            MainCoreComponent = null;

            if (!Session.HasStarted || Session.IsShuttingDown) return;
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if(_closing) return;
                RecalculateAllLimits();
                ApplyModifiers(Modifiers);
                EnforceGroupPunishment();
                EnforceOverCapacity();
            });
        }

        internal void OnGridAdded(IMyGridGroupData addedTo, IMyCubeGrid grid, IMyGridGroupData removedFrom)
        {
            var g = grid as MyCubeGrid;
            if (g == null || g.IsPreview) return;
            
            if (GridDictionary.ContainsKey(g)) return;
            var gc = new GridComponent();
            gc.Init(g, addedTo);
            GridDictionary.Add(g,gc);

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if(grid.MarkedForClose) return;
                if (IsIgnoredGroup())
                {
                    Utils.Log($"OnGridAdded: {grid.EntityId}, {OwnerId}, {grid.CustomName}", 2);
                    Utils.Log($"OnGridAdded: Group became ignored after grid addition (Faction: {OwningFaction?.Tag ?? "None"})", 2);
                
                    if (MainCoreComponent == null) return;
                    MainCoreComponent.CoreBlock.SlimBlock.RemoveAndRefund();
                    ResetCore();
                    return;
                }
            
                RecalculateAllLimits();
                ModAPI.BroadcastGridAddedToGroup(grid.EntityId);
            });
        }

        internal void OnGridRemoved(IMyGridGroupData removedFrom, IMyCubeGrid grid, IMyGridGroupData addedTo)
        {
            if (addedTo != null) return;
            
            var g = grid as MyCubeGrid;
            if (g == null || g.IsPreview) return;

            GridComponent comp;
            if (GridDictionary.TryGetValue(g, out comp))
            {
                if (MainCoreComponent?.GridComponent.Grid.EntityId == g.EntityId) ResetCore();
                comp.Clean();
                GridDictionary.Remove(g);
            }
            RecalculateAllLimits();
            ModAPI.BroadcastGridRemovedFromGroup(grid.EntityId, GetRepresentativeGridId());
        }

        private void EnforceGroupPunishment()
        {
            // Skip block limit enforcement for ignored factions/AI
            if (IsIgnoredGroup())
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
                            blk.WhackABlock(limit.PunishmentType);
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

                    t.Key.WhackABlock(limit.PunishmentType);
                    totalBlocksPunished++;
                    over -= t.Value;
                }
            }
            
            if (totalBlocksPunished > 0 && MyGroup != null)
            {
                ModAPI.BroadcastLimitsEnforced(GetRepresentativeGridId(), totalBlocksPunished);
            }
        }

        internal void EnforceOverCapacity()
        {
            if ((ShipCore.MaxBlocks > 0 && GroupBlocksCount >= ShipCore.MaxBlocks) ||
                (ShipCore.MaxPCU > 0 && GroupPCU >= ShipCore.MaxPCU) ||
                (ShipCore.MaxMass > 0 && GroupMass >= ShipCore.MaxMass))
            {
                if (ShipCore.MobilityType == MobilityType.Mobile) PunishSpeed = true;
                if (ShipCore.MobilityType == MobilityType.Static) PunishModifiers = true;
                if (ShipCore.MobilityType == MobilityType.Both)
                {
                    PunishModifiers = true;
                    PunishSpeed = true;
                }
                
                var modifiers = _activeDefenseEnabled ? GetActiveDefenseModifiers() : GetPassiveDefenseModifiers();
                foreach (var kvp in GridDictionary)
                {
                    CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
                }
            }
            else
            {
                if (ShipCore.MobilityType == MobilityType.Mobile) PunishSpeed = false;
                if (ShipCore.MobilityType == MobilityType.Static) PunishModifiers = false;
                if (ShipCore.MobilityType == MobilityType.Both)
                {
                    PunishModifiers = false;
                    PunishSpeed = false;
                }
                
                var mainCoreBlock = MainCoreComponent?.CoreBlock;
                if (mainCoreBlock != null)
                {
                    if (!mainCoreBlock.IsWorking)
                    {
                        var newMain = CoreDictionary.Values.FirstOrDefault(core => !core.IsMainCore && core.CoreBlock.IsWorking);
                        if (newMain != null)
                        {
                            Utils.ShowNotification($"Switching to new main core: {newMain.CoreBlock.CustomName}, old one was no longer functional!");
                            MainCoreComponent.IsMainCore = false;
                            
                            MainCoreComponent = newMain;
                            MainCoreComponent.IsMainCore = true;
                        }
                        else
                        {
                            PunishModifiers = true;
                            PunishSpeed = true;
                            ApplyModifiers(Modifiers);
                        }
                    }
                    else
                    {
                        PunishModifiers = false;
                        PunishSpeed = false;
                        ApplyModifiers(Modifiers);
                    }
                }
                
                var modifiers = _activeDefenseEnabled ? GetActiveDefenseModifiers() : GetPassiveDefenseModifiers();
                foreach (var kvp in GridDictionary)
                {
                    CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
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
                    if (terminalBlock != null) CubeGridModifiers.ApplyModifiers(terminalBlock, modifiers);
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
            if (!isValid) Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + ": the direction " + xyDirection + " is invalid",
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

		        // Start the post-boost ramp-down cap from boosted max speed.
		        // The enforcement loop will slowly reduce this cap back to base max.
		        PostBoostRampActive = true;
		        PostBoostRampCap = -1f;
		            
		        if (MainCoreComponent?.GridComponent?.Grid != null)
		        {
		            ModAPI.BroadcastBoostDeactivated(GetRepresentativeGridId());
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
                    ModAPI.BroadcastActiveDefenseDeactivated(GetRepresentativeGridId());
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
                ModAPI.BroadcastActiveDefenseActivated(GetRepresentativeGridId());
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
		    PostBoostRampActive = false;
		    PostBoostRampCap = -1f;

		    _boostDurationTimer = BoostDuration * 60f;
		    Utils.ShowNotification("Boost Engaged!", 1000);
		    
		    if (MainCoreComponent?.GridComponent?.Grid != null)
	        {
                ModAPI.BroadcastBoostActivated(MainCoreComponent.GridComponent.Grid.EntityId);
            }
        }

        internal GridDefenseModifiers GetActiveDefenseModifiers()
        {
            if (MainCoreComponent?.CoreBlock == null)
            {
                return ShipCore.ActiveDefenseModifiers;
            }

            if (PunishModifiers)
            {
                return new GridDefenseModifiers
                {
                    Bullet = ShipCore.ActiveDefenseModifiers.Bullet * MainCoreComponent.CoreBlock.UpgradeValues["ActiveBulletDamage"] * 2,
                    Rocket = ShipCore.ActiveDefenseModifiers.Rocket * MainCoreComponent.CoreBlock.UpgradeValues["ActiveRocketDamage"] * 2,
                    Explosion = ShipCore.ActiveDefenseModifiers.Explosion * MainCoreComponent.CoreBlock.UpgradeValues["ActiveExplosionDamage"] * 2,
                    Environment = ShipCore.ActiveDefenseModifiers.Environment * MainCoreComponent.CoreBlock.UpgradeValues["ActiveEnvironmentDamage"] * 2,
                    PostShield = ShipCore.ActiveDefenseModifiers.PostShield * MainCoreComponent.CoreBlock.UpgradeValues["ActivePostShieldDamage"] * 2,
                    Kinetic = ShipCore.ActiveDefenseModifiers.Kinetic * MainCoreComponent.CoreBlock.UpgradeValues["ActiveKineticDamage"] * 2,
                    Energy = ShipCore.ActiveDefenseModifiers.Energy * MainCoreComponent.CoreBlock.UpgradeValues["ActiveEnergyDamage"] * 2
                };
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
            }
            EnforceOverCapacity();
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
                ModAPI.BroadcastLimitsRecalculated(GetRepresentativeGridId());
            }
        }

        private long GetRepresentativeGridId()
        {
            var main = MainCoreComponent?.GridComponent?.Grid;
            var grid = main ?? GridDictionary.Keys.FirstOrDefault();
            return grid?.EntityId ?? 0;
        }
        
        internal bool IsIgnoredGroup()
        {
            if (OwnerId == 0) return true;
            var player = MyAPIGateway.Players.TryGetIdentityId(OwnerId);
            if (player != null && player.PromoteLevel == MyPromoteLevel.Admin && MyAPIGateway.Session.IsUserIgnorePCULimit(player.SteamUserId)) return true;

            var faction = OwningFaction;
            return Session.Config.IgnoredFactionTags != null && Session.Config.IgnoredFactionTags.Contains(faction?.Tag);
        }

        internal void Clean()
        {
            _closing = true;
            try
            {
                GridsPerFactionManager.RemoveGridGroup(OwningFaction, ShipCore.SubtypeId);
                GridsPerPlayerManager.RemoveGridGroup(OwnerId, ShipCore.SubtypeId);
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
