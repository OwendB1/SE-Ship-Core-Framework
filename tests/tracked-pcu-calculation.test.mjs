import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [grid, trackedBlocks, sharedCache, cache, placement, limits, lifecycle, merge] =
  await Promise.all([
    read("Shared/Components/GridComponent.cs"),
    read("Shared/Components/GridComponent.Blocks.cs"),
    read("Shared/Components/GroupComponent.CachedState.cs"),
    read("Server/Components/GroupComponent.CachedState.cs"),
    read("Server/Components/GridComponent.Blocks.cs"),
    read("Server/Components/GroupComponent.Limits.cs"),
    read("Shared/Components/GroupComponent.Lifecycle.cs"),
    read("Server/Components/GroupComponent.MergeInterception.cs"),
  ]);

assert.match(grid, /struct TrackedContribution/);
assert.match(grid, /Dictionary<IMySlimBlock, TrackedContribution> _blocks/);
assert.match(grid, /private int _trackedPcu/);
assert.match(grid, /internal TrackedContribution TrackedTotals/);

assert.match(trackedBlocks, /block\.ComponentStack\.IsFunctional/);
assert.match(trackedBlocks, /definition\.PCU/);
assert.match(trackedBlocks, /MyCubeBlockDefinition\.PCU_CONSTRUCTION_STAGE_COST/);
assert.match(trackedBlocks, /_trackedPcu \+= contribution\.Pcu/);
assert.match(trackedBlocks, /_trackedPcu -= contribution\.Pcu/);
assert.match(trackedBlocks, /UpdateTrackedBlockPcu\(IMySlimBlock block, out int delta\)/);
assert.match(trackedBlocks, /delta = pcu - current\.Pcu/);
assert.doesNotMatch(trackedBlocks, /GetTrackedPCU/);

assert.match(placement, /GroupPCU \+ contribution\.Pcu > maxPCU/);
assert.match(placement, /SubscribeForIsFunctionalChanged\(BlockFunctionalStateChanged\)/);
assert.match(placement, /UnsubscribeFromIsFunctionalChanged\(BlockFunctionalStateChanged\)/);
assert.match(placement, /BlockFunctionalStateChanged\(MySlimBlock block\)/);
assert.match(placement, /UpdateTrackedBlockPcu\(block, out delta\)/);
assert.match(placement, /groupComponent\.OnBlockPcuChanged\(delta\)/);

assert.match(sharedCache, /return Interlocked\.CompareExchange\(ref _cachedGroupPCU, 0, 0\)/);
assert.match(sharedCache, /ApplyGroupContribution\(GridComponent\.TrackedContribution contribution, int sign\)/);
assert.doesNotMatch(sharedCache, /_pcuCacheDirty|InvalidatePcuCache|RefreshPcuCache/);
assert.doesNotMatch(cache, /GetTrackedPCU|RefreshPcuCache|BlocksPCU/);

assert.match(limits, /ApplyGroupContribution\(contribution, 1\)/);
assert.match(limits, /ApplyGroupContribution\(contribution, -1\)/);
assert.match(limits, /internal void OnBlockPcuChanged\(int delta\)/);
assert.match(limits, /AddGroupPCU\(delta\)/);
assert.match(lifecycle, /var totals = comp\.TrackedTotals/);
assert.match(lifecycle, /ApplyGroupContribution\(totals, -1\)/);
assert.match(lifecycle, /Interlocked\.Exchange\(ref _cachedGroupPCU, 0\)/);

assert.match(merge, /var contribution = GridComponent\.GetBlockContribution\(block\)/);
assert.match(merge, /totalPcu \+= contribution\.Pcu/);
assert.doesNotMatch(merge, /BlocksPCU/);

console.log("tracked-pcu-calculation: ok");
