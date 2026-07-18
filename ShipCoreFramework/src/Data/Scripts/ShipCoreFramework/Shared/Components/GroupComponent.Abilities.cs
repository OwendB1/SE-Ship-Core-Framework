using System.Collections.Generic;
using Sandbox.ModAPI;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal List<string> GetSpeedPunishmentGateDescriptions()
        {
            if (!Session.IsServer)
                return new List<string>(_runtimeSpeedPunishmentReasons);
            var speedReasons = new List<string>();
            CollectTriggeredPunishmentGates(speedReasons, null);
            return speedReasons;
        }

        internal List<string> GetModifierPunishmentGateDescriptions()
        {
            if (!Session.IsServer)
                return new List<string>(_runtimeModifierPunishmentReasons);
            var modifierReasons = new List<string>();
            CollectTriggeredPunishmentGates(null, modifierReasons);
            return modifierReasons;
        }


        internal void ApplyModifiers(GridModifiers modifiers)
        {
            if (Session.IsServer && IsCoreRecoveryGraceActive())
            {
                Utils.Log("ApplyModifiers: suppressed modifier application during core recovery grace for group " +
                          GetGroupKey() + ".", 2);
                return;
            }

            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate
                {
                    if (_closing || Session.IsShuttingDown) return;
                    ApplyModifiers(modifiers);
                });
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

        internal bool IsPowerOverclockActive()
        {
            lock (_abilityStateLock)
            {
                return _powerOverclockActive;
            }
        }

        internal GridDefenseModifiers GetActiveDefenseModifiers()
        {
            return Session.IsServer && Session.IsGameThread
                ? ComputeActiveDefenseModifiers()
                : GetCachedActiveDefenseModifiers();
        }


        internal GridDefenseModifiers GetPassiveDefenseModifiers()
        {
            return Session.IsServer && Session.IsGameThread
                ? ComputePassiveDefenseModifiers()
                : GetCachedPassiveDefenseModifiers();
        }

    }
}
