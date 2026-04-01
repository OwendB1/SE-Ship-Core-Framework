using System;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class GroupComponent
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
            var mainCoreChanged = EnsureWorkingMainCore();
            var previousPunishModifiers = PunishModifiers;

            RefreshPunishmentFlags();
            if (mainCoreChanged || previousPunishModifiers != PunishModifiers) ApplyModifiers(Modifiers);
            UpdateDefenseModifierCache();
        }

        internal void RefreshPunishmentFlags()
        {
            ApplyPunishmentFlags(EvaluatePunishmentGates());
        }

        private GroupPunishmentFlags EvaluatePunishmentGates()
        {
            var punishments = GroupPunishmentFlags.None;

            ChainPunishmentGate(ref punishments, IsOverCoreCapacity(), GetMobilityPunishmentFlags());
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

        private GroupPunishmentFlags GetMobilityPunishmentFlags()
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
            return ShipCore.MinPlayers > 0 && GetFactionPlayerCount() < ShipCore.MinPlayers;
        }

        private int GetFactionPlayerCount()
        {
            var faction = OwningFaction;
            if (faction != null) return faction.Members.Count;
            return OwnerId > 0 ? 1 : 0;
        }

        private bool HasBrokenMainCore()
        {
            var mainCoreBlock = MainCoreComponent?.CoreBlock;
            return mainCoreBlock != null && !mainCoreBlock.IsWorking;
        }

        private bool EnsureWorkingMainCore()
        {
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

        private void UpdateDefenseModifierCache()
        {
            var modifiers = _activeDefenseEnabled ? GetActiveDefenseModifiers() : GetPassiveDefenseModifiers();
            foreach (var kvp in GridDictionary) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
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

        public void DefenseValuesChanged()
        {
            var modifiers = GetActiveDefenseModifiers();
            foreach (var kvp in GridDictionary) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
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

                var modifiers = GetPassiveDefenseModifiers();
                foreach (var kvp in GridDictionary) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;

                _activeDefenseCooldownTimer = ActiveDefenseCoolDown * 60f;
                Utils.ShowNotification("Active Defense Disengaged! Cooldown started.", 1000);

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
                Utils.ShowNotification("Active defense is not allowed on this grid!", 1000);
                return;
            }

            if (_activeDefenseEnabled)
            {
                Utils.ShowNotification(
                    "Active Defense Time Remaining:" + (_activeDefenseDurationTimer / 60f).ToString("0.0"), 1000);
                return;
            }

            if (_activeDefenseCooldownTimer > 0f)
            {
                Utils.ShowNotification(
                    "Active Defense is cooling down! Cooldown Time:" + (_boostCooldownTimer / 60f).ToString("0.0"),
                    1000);
                return;
            }

            _activeDefenseEnabled = true;
            _activeDefenseDurationTimer = ActiveDefenseDuration * 60f;

            var modifiers = GetActiveDefenseModifiers();
            foreach (var kvp in GridDictionary) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;

            Utils.ShowNotification("Active Defense Engaged!", 1000);

            if (MainCoreComponent?.GridComponent?.Grid != null)
                ModAPI.BroadcastActiveDefenseActivated(GetRepresentativeGridId());
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
                Utils.ShowNotification(
                    "Boost is cooling down! Cooldown Time:" + (_boostCooldownTimer / 60f).ToString("0.0"), 1000);
                return;
            }

            BoostEnabled = true;
            PostBoostRampActive = false;
            PostBoostRampCap = -1f;

            _boostDurationTimer = BoostDuration * 60f;
            Utils.ShowNotification("Boost Engaged!", 1000);

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
