using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        [Flags]
        private enum GroupPunishmentFlags
        {
            None = 0,
            Speed = 1,
            Modifiers = 2,
            Both = Speed | Modifiers
        }

        internal void RefreshPunishmentState()
        {
            if (_closing || Session.IsShuttingDown || IsInitializingGrids) return;
            if (!Session.IsGameThread)
            {
                var groupKey = GetThreadWorkKey();
                ThreadWork.Enqueue(ThreadWork.StateCategory, "punishment-refresh:" + groupKey,
                    "Punishment state refresh for group " + groupKey,
                    () => !_closing && !Session.IsShuttingDown,
                    RefreshPunishmentState);
                return;
            }

            if (Deactivated || IsIgnoredByAiOrFactionTagThreadSafe())
            {
                ClearDeactivatedLimitState();
                RefreshModifierStateCache();
                ApplyModifiers(Modifiers);
                RefreshDefenseModifierCache();
                return;
            }

            var mainCoreChanged = EnsureWorkingMainCore();
            var previousPunishModifiers = PunishModifiers;

            RefreshLimitedBlockPunishmentState();
            RefreshPunishmentFlags();
            RefreshModifierStateCache();
            if (mainCoreChanged || previousPunishModifiers != PunishModifiers) ApplyModifiers(Modifiers);
            RefreshDefenseModifierCache();
        }

        internal void RefreshPunishmentFlags()
        {
            if (Deactivated || IsIgnoredByAiOrFactionTagThreadSafe())
            {
                PunishSpeed = false;
                if (PunishModifiers)
                    InvalidateModifierStateCache();
                PunishModifiers = false;
                return;
            }

            ApplyPunishmentFlags(EvaluatePunishmentGates());
        }

        internal List<string> GetSpeedPunishmentGateDescriptions()
        {
            var speedReasons = new List<string>();
            CollectTriggeredPunishmentGates(speedReasons, null);
            return speedReasons;
        }

        internal List<string> GetModifierPunishmentGateDescriptions()
        {
            var modifierReasons = new List<string>();
            CollectTriggeredPunishmentGates(null, modifierReasons);
            return modifierReasons;
        }

        private GroupPunishmentFlags EvaluatePunishmentGates()
        {
            var punishments = GroupPunishmentFlags.None;

            ChainPunishmentGate(ref punishments, IsOverCoreCapacity(), GetCoreCapacityPunishmentFlags());
            ChainPunishmentGate(ref punishments, HasMobilityTypeMismatch(), GroupPunishmentFlags.Both);
            ChainPunishmentGate(ref punishments, IsBelowMinPlayers(), GroupPunishmentFlags.Both);
            ChainPunishmentGate(ref punishments, IsAtOrOverMaxPlayers(), GroupPunishmentFlags.Both);
            ChainPunishmentGate(ref punishments, HasBrokenMainCore(), GroupPunishmentFlags.Both);
            ChainPunishmentGate(ref punishments, ShipCore.ForceBroadCast && !HasWorkingBeacon(), GroupPunishmentFlags.Both);

            return punishments;
        }

        private static void ChainPunishmentGate(ref GroupPunishmentFlags punishments, bool isTriggered,
            GroupPunishmentFlags flags)
        {
            if (!isTriggered) return;
            punishments |= flags;
        }

        private void CollectTriggeredPunishmentGates(ICollection<string> speedReasons,
            ICollection<string> modifierReasons)
        {
            AddTriggeredPunishmentGate(speedReasons, modifierReasons, IsOverCoreCapacity(),
                GetCoreCapacityPunishmentFlags(), GetCoreCapacityPunishmentReason());
            AddTriggeredPunishmentGate(speedReasons, modifierReasons, HasMobilityTypeMismatch(),
                GroupPunishmentFlags.Both, GetMobilityMismatchPunishmentReason());
            AddTriggeredPunishmentGate(speedReasons, modifierReasons, IsBelowMinPlayers(),
                GroupPunishmentFlags.Both, GetBelowMinimumPlayersPunishmentReason());
            AddTriggeredPunishmentGate(speedReasons, modifierReasons, IsAtOrOverMaxPlayers(),
                GroupPunishmentFlags.Both, GetAtOrOverMaxPlayersPunishmentReason());
            AddTriggeredPunishmentGate(speedReasons, modifierReasons, HasBrokenMainCore(),
                GroupPunishmentFlags.Both, "Main core is offline");
            AddTriggeredPunishmentGate(speedReasons, modifierReasons,
                ShipCore.ForceBroadCast && !HasWorkingBeacon(),
                GroupPunishmentFlags.Both, "Required working beacon missing");
        }

        private static void AddTriggeredPunishmentGate(ICollection<string> speedReasons,
            ICollection<string> modifierReasons, bool isTriggered, GroupPunishmentFlags flags, string description)
        {
            if (!isTriggered) return;

            if ((flags & GroupPunishmentFlags.Speed) != 0 && speedReasons != null)
                speedReasons.Add(description);

            if ((flags & GroupPunishmentFlags.Modifiers) != 0 && modifierReasons != null)
                modifierReasons.Add(description);
        }

        private GroupPunishmentFlags GetCoreCapacityPunishmentFlags()
        {
            switch (ShipCore.MobilityType)
            {
                case MobilityType.Mobile:
                    return GroupPunishmentFlags.Speed;
                case MobilityType.Static:
                    return GroupPunishmentFlags.Modifiers;
                case MobilityType.Both:
                default:
                    return GroupPunishmentFlags.Both;
            }
        }

        private bool HasMobilityTypeMismatch()
        {
            var referenceGrid = GetMobilityReferenceGrid();
            var shipCore = ShipCore;
            if (referenceGrid == null || shipCore == null) return false;

            switch (shipCore.MobilityType)
            {
                case MobilityType.Static:
                    return !referenceGrid.IsStatic;
                case MobilityType.Mobile:
                    return referenceGrid.IsStatic;
                case MobilityType.Both:
                default:
                    return false;
            }
        }

        private bool IsOverCoreCapacity()
        {
            return (ShipCore.MaxBlocks > 0 && GroupBlocksCount >= GetEffectiveMaxBlocks()) ||
                   (ShipCore.MaxPCU > 0 && GroupPCU >= GetEffectiveMaxPCU()) ||
                   (ShipCore.MaxMass > 0 && GroupMass >= GetEffectiveMaxMass());
        }

        private bool IsAtOrOverMaxPlayers()
        {
            if (OwnerId == 0) return false;
            return ShipCore.MaxPlayers > 0 && GetFactionPlayerCount() >= ShipCore.MaxPlayers;
        }

        private bool IsBelowMinPlayers()
        {
            if (OwnerId == 0) return false;
            return ShipCore.MinPlayers > 0 && GetMinimumFactionPlayerCount() < ShipCore.MinPlayers;
        }

        private string GetCoreCapacityPunishmentReason()
        {
            var triggeredLimits = new List<string>();

            if (ShipCore.MaxBlocks > 0 && GroupBlocksCount >= GetEffectiveMaxBlocks())
                triggeredLimits.Add($"blocks {GroupBlocksCount}/{GetEffectiveMaxBlocks()}");

            if (ShipCore.MaxPCU > 0 && GroupPCU >= GetEffectiveMaxPCU())
                triggeredLimits.Add($"PCU {GroupPCU}/{GetEffectiveMaxPCU()}");

            if (ShipCore.MaxMass > 0 && GroupMass >= GetEffectiveMaxMass())
                triggeredLimits.Add($"mass {GroupMass:F0}/{GetEffectiveMaxMass():F0} kg");

            return triggeredLimits.Count == 0
                ? "Core capacity exceeded"
                : $"Core capacity exceeded ({string.Join(", ", triggeredLimits)})";
        }

        private string GetMobilityMismatchPunishmentReason()
        {
            var referenceGrid = GetMobilityReferenceGrid();
            var shipCore = ShipCore;
            if (referenceGrid == null || shipCore == null) return "Core mobility mismatch";

            var currentMobility = referenceGrid.IsStatic ? "static" : "mobile";
            switch (shipCore.MobilityType)
            {
                case MobilityType.Static:
                    return $"Mobility mismatch ({currentMobility} grid, static core)";
                case MobilityType.Mobile:
                    return $"Mobility mismatch ({currentMobility} grid, mobile core)";
                case MobilityType.Both:
                default:
                    return "Core mobility mismatch";
            }
        }

        private string GetBelowMinimumPlayersPunishmentReason()
        {
            var factionPlayerCount = GetMinimumFactionPlayerCount();
            return $"Below minimum players ({factionPlayerCount}/{ShipCore.MinPlayers})";
        }

        private string GetAtOrOverMaxPlayersPunishmentReason()
        {
            var factionPlayerCount = GetFactionPlayerCount();
            return $"At or over max players ({factionPlayerCount}/{ShipCore.MaxPlayers})";
        }

        private int GetFactionPlayerCount()
        {
            return PerFactionManager.GetFactionPlayerCount(OwningFaction, OwnerId);
        }

        private int GetMinimumFactionPlayerCount()
        {
            return PerFactionManager.GetFactionMemberCount(OwningFaction);
        }

        private bool HasBrokenMainCore()
        {
            var mainCoreBlock = MainCoreComponent?.CoreBlock;
            return mainCoreBlock != null && !mainCoreBlock.IsWorking;
        }

        private bool EnsureWorkingMainCore()
        {
            if (Deactivated) return false;

            var currentMain = MainCoreComponent;
            var mainCoreBlock = currentMain?.CoreBlock;
            if (mainCoreBlock == null || mainCoreBlock.IsWorking || !CoreDictionary.Any()) return false;

            var newMain = GetBestReplacementMainCoreCandidate(currentMain, true);
            if (newMain == null) return false;

            Utils.ShowNotification(
                $"Switching to new main core: {newMain.CoreBlock.CustomName}, old one was no longer functional!");

            currentMain.IsMainCore = false;
            MainCoreComponent = newMain;
            MainCoreComponent.IsMainCore = true;
            InvalidateGameThreadStateCache(true);
            InvalidateModifierStateCache();
            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);
            SyncBeaconComponents();
            return true;
        }

        private void ApplyPunishmentFlags(GroupPunishmentFlags punishments)
        {
            var punishSpeed = (punishments & GroupPunishmentFlags.Speed) != 0;
            if (PunishSpeed != punishSpeed)
                InvalidateSpeedStateCache();

            var punishModifiers = (punishments & GroupPunishmentFlags.Modifiers) != 0;
            if (PunishModifiers != punishModifiers)
                InvalidateModifierStateCache();

            PunishSpeed = punishSpeed;
            PunishModifiers = punishModifiers;
        }

        private GridDefenseModifiers GetCurrentDefenseModifiers()
        {
            bool activeDefenseEnabled;
            lock (_abilityStateLock)
            {
                activeDefenseEnabled = _activeDefenseEnabled;
            }

            return activeDefenseEnabled ? GetActiveDefenseModifiers() : GetPassiveDefenseModifiers();
        }

        internal void RefreshDefenseModifierCache()
        {
            GridDefenseModifiers discardedModifiers;
            if (Deactivated || MainCoreComponent == null)
            {
                foreach (var kvp in GridDictionary)
                    CubeGridModifiers.DefenseModifiers.TryRemove(kvp.Key.EntityId, out discardedModifiers);
                return;
            }

            var modifiers = GetCurrentDefenseModifiers();
            foreach (var kvp in GridDictionary) CubeGridModifiers.DefenseModifiers.Set(kvp.Key.EntityId, modifiers);
        }

        internal void RemoveDefenseModifierCache(long gridEntityId)
        {
            GridDefenseModifiers discardedModifiers;
            CubeGridModifiers.DefenseModifiers.TryRemove(gridEntityId, out discardedModifiers);
        }

        internal void ClearDefenseModifierCache()
        {
            GridDefenseModifiers discardedModifiers;
            foreach (var kvp in GridDictionary)
                CubeGridModifiers.DefenseModifiers.TryRemove(kvp.Key.EntityId, out discardedModifiers);
        }

        private bool HasWorkingBeacon()
        {
            foreach (var grid in GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                var beacons = ((IMyCubeGrid)grid).GetFatBlocks<IMyBeacon>();
                if (beacons == null) continue;

                if (beacons.Any(beacon => beacon.IsWorking)) return true;
            }

            return false;
        }

        internal void ApplyModifiers(GridModifiers modifiers)
        {
            if (!Session.IsGameThread)
            {
                var groupKey = GetThreadWorkKey();
                ThreadWork.Enqueue(ThreadWork.StateCategory, "apply-modifiers:" + groupKey,
                    "Apply modifiers for group " + groupKey,
                    delegate { return !_closing && !Session.IsShuttingDown; },
                    delegate { ApplyModifiers(modifiers); });
                return;
            }

            foreach (var kvp in GridDictionary)
            {
                var blocksCopy = kvp.Value.GetBlocksCopy();
                foreach (var block in blocksCopy)
                {
                    var terminalBlock = block?.FatBlock as IMyTerminalBlock;
                    if (terminalBlock != null) CubeGridModifiers.ApplyModifiers(terminalBlock, modifiers);
                }
            }
        }

        internal void DefenseValuesChanged()
        {
            RefreshDefenseModifierCache();
        }

        internal void RunBoostTimerTick()
        {
            var expired = false;
            lock (SpeedStateLock)
            {
                if (BoostEnabled)
                {
                    _boostDurationTimer -= 1f;
                    if (!(_boostDurationTimer <= 0f)) return;

                    BoostEnabled = false;
                    InvalidateSpeedStateCache();
                    _boostCooldownTimer = BoostCoolDown * 60f;

                    PostBoostRampActive = true;
                    PostBoostRampCap = -1f;
                    expired = true;
                }
                else if (_boostCooldownTimer > 0f)
                {
                    _boostCooldownTimer -= 1f;
                    if (_boostCooldownTimer < 0f) _boostCooldownTimer = 0f;
                }
            }

            if (expired)
                QueueBoostDeactivatedSideEffects();
        }

        internal void RunActiveDefenseTimerTick()
        {
            var expired = false;
            lock (_abilityStateLock)
            {
                if (_activeDefenseEnabled)
                {
                    _activeDefenseDurationTimer -= 1f;
                    if (!(_activeDefenseDurationTimer <= 0f)) return;

                    _activeDefenseEnabled = false;

                    _activeDefenseCooldownTimer = ActiveDefenseCoolDown * 60f;
                    expired = true;
                }
                else if (_activeDefenseCooldownTimer > 0f)
                {
                    _activeDefenseCooldownTimer -= 1f;
                    if (_activeDefenseCooldownTimer < 0f) _activeDefenseCooldownTimer = 0f;
                }
            }

            if (!expired) return;
            RefreshDefenseModifierCache();
            QueueActiveDefenseDeactivatedSideEffects();
        }

        private void QueueBoostDeactivatedSideEffects()
        {
            if (Session.IsGameThread)
            {
                RunBoostDeactivatedSideEffects();
                return;
            }

            var groupKey = GetThreadWorkKey();
            ThreadWork.Enqueue(ThreadWork.StateCategory, "boost-deactivated:" + groupKey,
                "Boost deactivated side effects for group " + groupKey,
                delegate { return !_closing && !Session.IsShuttingDown; },
                RunBoostDeactivatedSideEffects);
        }

        private void RunBoostDeactivatedSideEffects()
        {
            Utils.ShowNotification("Boost Disengaged! Cooldown started.");

            var representativeGridId = GetRepresentativeGridId();
            if (representativeGridId != 0)
                ModAPI.BroadcastBoostDeactivated(representativeGridId);
        }

        private void QueueActiveDefenseDeactivatedSideEffects()
        {
            if (Session.IsGameThread)
            {
                RunActiveDefenseDeactivatedSideEffects();
                return;
            }

            var groupKey = GetThreadWorkKey();
            ThreadWork.Enqueue(ThreadWork.StateCategory, "active-defense-deactivated:" + groupKey,
                "Active defense deactivated side effects for group " + groupKey,
                delegate { return !_closing && !Session.IsShuttingDown; },
                RunActiveDefenseDeactivatedSideEffects);
        }

        private void RunActiveDefenseDeactivatedSideEffects()
        {
            Utils.ShowNotification("Active Defense Disengaged! Cooldown started.");

            var representativeGridId = GetRepresentativeGridId();
            if (representativeGridId != 0)
                ModAPI.BroadcastActiveDefenseDeactivated(representativeGridId);
        }

        internal void ActivateDefense()
        {
            if (!ShipCore.EnableActiveDefenseModifiers)
            {
                Utils.ShowNotification("Active defense is not allowed on this grid!");
                return;
            }

            string rejectedMessage = null;
            lock (_abilityStateLock)
            {
                if (_activeDefenseEnabled)
                {
                    rejectedMessage = "Active Defense Time Remaining:" + (_activeDefenseDurationTimer / 60f).ToString("0.0");
                }
                else if (_activeDefenseCooldownTimer > 0f)
                {
                    rejectedMessage = "Active Defense is cooling down! Cooldown Time:" +
                                      (_activeDefenseCooldownTimer / 60f).ToString("0.0");
                }
                else
                {
                    _activeDefenseDurationTimer = ActiveDefenseDuration * 60f;
                    _activeDefenseEnabled = true;
                }
            }

            if (rejectedMessage != null)
            {
                Utils.ShowNotification(rejectedMessage);
                return;
            }

            RefreshDefenseModifierCache();

            Utils.ShowNotification("Active Defense Engaged!");

            if (MainCoreComponent?.GridComponent?.Grid != null)
                ModAPI.BroadcastActiveDefenseActivated(GetRepresentativeGridId());
        }

        internal void ActivateBoost()
        {
            if (!ShipCore.SpeedBoostEnabled)
            {
                Utils.ShowNotification("Boosting is not allowed on this grid!");
                return;
            }

            string rejectedMessage = null;
            lock (SpeedStateLock)
            {
                if (BoostEnabled)
                {
                    rejectedMessage = "Boost Time Remaining:" + (_boostDurationTimer / 60f).ToString("0.0");
                }
                else if (_boostCooldownTimer > 0f)
                {
                    rejectedMessage = "Boost is cooling down! Cooldown Time:" +
                                      (_boostCooldownTimer / 60f).ToString("0.0");
                }
                else
                {
                    BoostEnabled = true;
                    InvalidateSpeedStateCache();
                    PostBoostRampActive = false;
                    PostBoostRampCap = -1f;

                    _boostDurationTimer = BoostDuration * 60f;
                }
            }

            if (rejectedMessage != null)
            {
                Utils.ShowNotification(rejectedMessage);
                return;
            }

            Utils.ShowNotification("Boost Engaged!");

            if (MainCoreComponent?.GridComponent?.Grid != null)
                ModAPI.BroadcastBoostActivated(MainCoreComponent.GridComponent.Grid.EntityId);
        }

        internal GridDefenseModifiers GetActiveDefenseModifiers()
        {
            return Session.IsGameThread ? ComputeActiveDefenseModifiers() : GetCachedActiveDefenseModifiers();
        }

        private GridDefenseModifiers ComputeActiveDefenseModifiers()
        {
            var modifiers = CubeGridModifiers.GetEffectiveDefenseModifiers(ShipCore.ActiveDefenseModifiers,
                GetEffectiveUpgradeModules(true).Select(module => module.GetConfig()),
                DefenseModifierTarget.Active);

            return PunishModifiers ? CubeGridModifiers.ScaleDefenseModifiers(modifiers, 2f) : modifiers;
        }

        internal GridDefenseModifiers GetPassiveDefenseModifiers()
        {
            return Session.IsGameThread ? ComputePassiveDefenseModifiers() : GetCachedPassiveDefenseModifiers();
        }

        private GridDefenseModifiers ComputePassiveDefenseModifiers()
        {
            var modifiers = CubeGridModifiers.GetEffectiveDefenseModifiers(ShipCore.PassiveDefenseModifiers,
                GetEffectiveUpgradeModules(true).Select(module => module.GetConfig()),
                DefenseModifierTarget.Passive);

            return PunishModifiers ? CubeGridModifiers.ScaleDefenseModifiers(modifiers, 2f) : modifiers;
        }
    }
}
