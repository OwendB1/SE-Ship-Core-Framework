using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class GroupComponent
    {
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
                foreach (var kvp in GetGridEntriesCopy()) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
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
                    if (!mainCoreBlock.IsWorking && CoreDictionary.Any())
                    {
                        var newMain = CoreDictionary.Values.FirstOrDefault(core => !core.IsMainCore && core.CoreBlock.IsWorking);
                        if (newMain != null)
                        {
                            Utils.ShowNotification($"Switching to new main core: {newMain.CoreBlock.CustomName}, old one was no longer functional!");
                            MainCoreComponent.IsMainCore = false;

                            MainCoreComponent = newMain;
                            MainCoreComponent.IsMainCore = true;
                            SyncBeaconComponents();
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

                if (ShipCore.ForceBroadCast && !HasWorkingBeacon())
                {
                    PunishModifiers = true;
                    PunishSpeed = true;
                    ApplyModifiers(Modifiers);
                }

                var modifiers = _activeDefenseEnabled ? GetActiveDefenseModifiers() : GetPassiveDefenseModifiers();
                foreach (var kvp in GetGridEntriesCopy()) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
            }
        }

        private bool HasWorkingBeacon()
        {
            foreach (var grid in GetGridsCopy())
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
            foreach (var kvp in GetGridEntriesCopy())
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
            foreach (var kvp in GetGridEntriesCopy()) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;
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
                foreach (var kvp in GetGridEntriesCopy()) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;

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
            foreach (var kvp in GetGridEntriesCopy()) CubeGridModifiers.DefenseModifiers[kvp.Key.EntityId] = modifiers;

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
