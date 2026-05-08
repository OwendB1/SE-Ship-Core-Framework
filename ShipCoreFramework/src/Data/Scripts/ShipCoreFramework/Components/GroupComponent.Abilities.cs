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

            var mainCoreChanged = EnsureWorkingMainCore();
            var previousPunishModifiers = PunishModifiers;

            RefreshPunishmentFlags();
            if (mainCoreChanged || previousPunishModifiers != PunishModifiers) ApplyModifiers(Modifiers);
            RefreshDefenseModifierCache();
        }

        internal void RefreshPunishmentFlags()
        {
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
            return (ShipCore.MaxBlocks > 0 && GroupBlocksCount >= ShipCore.MaxBlocks) ||
                   (ShipCore.MaxPCU > 0 && GroupPCU >= ShipCore.MaxPCU) ||
                   (ShipCore.MaxMass > 0 && GroupMass >= ShipCore.MaxMass);
        }

        private bool IsAtOrOverMaxPlayers()
        {
            return ShipCore.MaxPlayers > 0 && GetFactionPlayerCount() >= ShipCore.MaxPlayers;
        }

        private bool IsBelowMinPlayers()
        {
            return ShipCore.MinPlayers > 0 && GetMinimumFactionPlayerCount() < ShipCore.MinPlayers;
        }

        private string GetCoreCapacityPunishmentReason()
        {
            var triggeredLimits = new List<string>();

            if (ShipCore.MaxBlocks > 0 && GroupBlocksCount >= ShipCore.MaxBlocks)
                triggeredLimits.Add($"blocks {GroupBlocksCount}/{ShipCore.MaxBlocks}");

            if (ShipCore.MaxPCU > 0 && GroupPCU >= ShipCore.MaxPCU)
                triggeredLimits.Add($"PCU {GroupPCU}/{ShipCore.MaxPCU}");

            if (ShipCore.MaxMass > 0 && GroupMass >= ShipCore.MaxMass)
                triggeredLimits.Add($"mass {GroupMass:F0}/{ShipCore.MaxMass:F0} kg");

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

            var newMain = CoreDictionary.Values.FirstOrDefault(core => !core.IsMainCore && core.CoreBlock.IsWorking);
            if (newMain == null) return false;

            Utils.ShowNotification(
                $"Switching to new main core: {newMain.CoreBlock.CustomName}, old one was no longer functional!");

            currentMain.IsMainCore = false;
            MainCoreComponent = newMain;
            MainCoreComponent.IsMainCore = true;
            SyncBeaconComponents();
            return true;
        }

        private void ApplyPunishmentFlags(GroupPunishmentFlags punishments)
        {
            PunishSpeed = (punishments & GroupPunishmentFlags.Speed) != 0;
            PunishModifiers = (punishments & GroupPunishmentFlags.Modifiers) != 0;
        }

        private GridDefenseModifiers GetCurrentDefenseModifiers()
        {
            return _activeDefenseEnabled ? GetActiveDefenseModifiers() : GetPassiveDefenseModifiers();
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
            foreach (var kvp in GridDictionary) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
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
            if (BoostEnabled)
            {
                _boostDurationTimer -= 1f;
                if (!(_boostDurationTimer <= 0f)) return;
                BoostEnabled = false;
                _boostCooldownTimer = BoostCoolDown * 60f;
                Utils.ShowNotification("Boost Disengaged! Cooldown started.");

                PostBoostRampActive = true;
                PostBoostRampCap = -1f;

                if (MainCoreComponent?.GridComponent?.Grid != null)
                    ModAPI.BroadcastBoostDeactivated(GetRepresentativeGridId());
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

                RefreshDefenseModifierCache();

                _activeDefenseCooldownTimer = ActiveDefenseCoolDown * 60f;
                Utils.ShowNotification("Active Defense Disengaged! Cooldown started.");

                if (MainCoreComponent?.GridComponent?.Grid != null)
                    ModAPI.BroadcastActiveDefenseDeactivated(GetRepresentativeGridId());
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
                Utils.ShowNotification("Active defense is not allowed on this grid!");
                return;
            }

            if (_activeDefenseEnabled)
            {
                Utils.ShowNotification("Active Defense Time Remaining:" + (_activeDefenseDurationTimer / 60f).ToString("0.0"));
                return;
            }

            if (_activeDefenseCooldownTimer > 0f)
            {
                Utils.ShowNotification("Active Defense is cooling down! Cooldown Time:" + (_activeDefenseCooldownTimer / 60f).ToString("0.0"));
                return;
            }

            _activeDefenseDurationTimer = ActiveDefenseDuration * 60f;
            _activeDefenseEnabled = true;

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

            if (BoostEnabled)
            {
                Utils.ShowNotification("Boost Time Remaining:" + (_boostDurationTimer / 60f).ToString("0.0"));
                return;
            }

            if (_boostCooldownTimer > 0f)
            {
                Utils.ShowNotification(
                    "Boost is cooling down! Cooldown Time:" + (_boostCooldownTimer / 60f).ToString("0.0"));
                return;
            }

            BoostEnabled = true;
            PostBoostRampActive = false;
            PostBoostRampCap = -1f;

            _boostDurationTimer = BoostDuration * 60f;
            Utils.ShowNotification("Boost Engaged!");

            if (MainCoreComponent?.GridComponent?.Grid != null)
                ModAPI.BroadcastBoostActivated(MainCoreComponent.GridComponent.Grid.EntityId);
        }

        internal GridDefenseModifiers GetActiveDefenseModifiers()
        {
            var modifiers = CubeGridModifiers.GetEffectiveDefenseModifiers(ShipCore.ActiveDefenseModifiers,
                GetMainCoreUpgradeModules(true).Select(module => module.GetConfig()),
                DefenseModifierTarget.Active);

            return PunishModifiers ? CubeGridModifiers.ScaleDefenseModifiers(modifiers, 2f) : modifiers;
        }

        internal GridDefenseModifiers GetPassiveDefenseModifiers()
        {
            var modifiers = CubeGridModifiers.GetEffectiveDefenseModifiers(ShipCore.PassiveDefenseModifiers,
                GetMainCoreUpgradeModules(true).Select(module => module.GetConfig()),
                DefenseModifierTarget.Passive);

            return PunishModifiers ? CubeGridModifiers.ScaleDefenseModifiers(modifiers, 2f) : modifiers;
        }
    }
}
