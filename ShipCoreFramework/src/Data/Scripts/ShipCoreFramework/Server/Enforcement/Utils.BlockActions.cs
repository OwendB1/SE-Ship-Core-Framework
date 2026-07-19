using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        internal static void RemoveAndRefund(this IMySlimBlock block)
        {
            if (!Session.IsServer) return;

            if (!Session.IsGameThread)
            {
                var capturedBlock = block;
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate
                {
                    if (capturedBlock?.CubeGrid == null ||
                        capturedBlock.CubeGrid.MarkedForClose ||
                        capturedBlock.CubeGrid.Closed ||
                        Session.IsShuttingDown)
                        return;

                    RemoveAndRefund(capturedBlock);
                });
                return;
            }

            var grid = block?.CubeGrid;
            if (grid == null) return;

            var cargoContainers = grid.GetFatBlocks<IMyCargoContainer>().ToList();
            var thisCargo = block.FatBlock as IMyCargoContainer;
            if (thisCargo != null) cargoContainers.Remove(thisCargo);

            IMyInventory selectedInventory = null;
            var maxAvailableVolume = -1f;
            foreach (var cargo in cargoContainers)
            {
                var inv = cargo.GetInventory();
                if (inv == null) continue;

                var avail = (float)inv.MaxVolume - (float)inv.CurrentVolume;
                if (avail <= maxAvailableVolume) continue;

                maxAvailableVolume = avail;
                selectedInventory = inv;
            }

            if (selectedInventory != null)
            {
                var refund = ComputeRefundComponents(block);
                PutComponentsIntoInventory(selectedInventory, refund);
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (grid.MarkedForClose || grid.Closed) return;
                grid.RemoveBlock(block, Session.HasStarted);
                var projectors = grid.GetFatBlocks<IMyProjector>().ToList();
                foreach (var projector in projectors) projector.Enabled = false;
            });
        }

        private static Dictionary<string, int> ComputeRefundComponents(IMySlimBlock block)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var def = block.BlockDefinition as MyCubeBlockDefinition;
            if (def == null) return result;

            var full = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in def.Components)
            {
                var subtype = component.Definition.Id.SubtypeName;
                int existing;
                if (!full.TryGetValue(subtype, out existing)) full[subtype] = component.Count;
                else full[subtype] = existing + component.Count;
            }

            var missing = new Dictionary<string, int>();
            block.ComponentStack.GetAllMissingComponents(missing);

            foreach (var kv in full)
            {
                int miss;
                missing.TryGetValue(kv.Key, out miss);

                var built = kv.Value - miss;
                if (built > 0) result[kv.Key] = built;
            }

            return result;
        }

        private static void PutComponentsIntoInventory(IMyInventory inv, Dictionary<string, int> refund)
        {
            foreach (var kv in refund)
            {
                var id = new MyDefinitionId(typeof(MyObjectBuilder_Component), kv.Key);
                var builder = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(id);
                if (builder == null) continue;

                inv.AddItems(kv.Value, builder);
            }
        }

        internal static void WhackABlock(this IMySlimBlock block, PunishmentType harm,
            MyStringHash? customDamageType = null)
        {
            if (!Session.IsServer) return;

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
                    block.RemoveAndRefund();
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
    }
}
