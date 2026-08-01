using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

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

            var inventories = new List<IMyInventory>();
            foreach (var cargo in cargoContainers)
            {
                var inv = cargo.GetInventory();
                if (inv == null) continue;

                inventories.Add(inv);
            }

            Dictionary<string, int> refund = ComputeRefundComponents(block);
            Vector3D refundPosition = grid.GridIntegerToWorld(block.Position);
            Vector3D refundForward = grid.WorldMatrix.Forward;
            Vector3D refundUp = grid.WorldMatrix.Up;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (grid.MarkedForClose || grid.Closed) return;
                grid.RemoveBlock(block, Session.HasStarted);
                PutComponentsIntoInventories(inventories, refund, refundPosition, refundForward, refundUp);
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

        private static void PutComponentsIntoInventories(List<IMyInventory> inventories,
            Dictionary<string, int> refund, Vector3D position, Vector3D forward, Vector3D up)
        {
            foreach (var kv in refund)
            {
                MyDefinitionId id = new MyDefinitionId(typeof(MyObjectBuilder_Component), kv.Key);
                MyObjectBuilder_PhysicalObject builder =
                    (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(id);
                if (builder == null) continue;

                int remaining = kv.Value;
                foreach (var inventory in inventories)
                {
                    int amount = AmountThatFits(inventory, id, remaining);
                    if (amount == 0) continue;

                    inventory.AddItems(amount, builder);
                    remaining -= amount;
                    if (remaining == 0) break;
                }

                if (remaining > 0)
                    MyFloatingObjects.Spawn(new MyPhysicalInventoryItem(remaining, builder), position, forward, up);
            }
        }

        private static int AmountThatFits(IMyInventory inventory, MyDefinitionId id, int requested)
        {
            int minimum = 0;
            int maximum = requested;
            while (minimum < maximum)
            {
                int amount = minimum + (maximum - minimum + 1) / 2;
                if (inventory.CanItemsBeAdded(amount, id)) minimum = amount;
                else maximum = amount - 1;
            }

            return minimum;
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
