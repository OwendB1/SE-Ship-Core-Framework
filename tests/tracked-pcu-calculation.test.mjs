import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [trackedBlocks, sharedCache, cache, placement, limits, merge] = await Promise.all([
  read("Shared/Components/GridComponent.Blocks.cs"),
  read("Shared/Components/GroupComponent.CachedState.cs"),
  read("Server/Components/GroupComponent.CachedState.cs"),
  read("Server/Components/GridComponent.Blocks.cs"),
  read("Server/Components/GroupComponent.Limits.cs"),
  read("Server/Components/GroupComponent.MergeInterception.cs"),
]);

assert.match(trackedBlocks, /internal int GetTrackedPCU\(\)/);
assert.match(trackedBlocks, /lock \(_blocksLock\)/);
assert.match(trackedBlocks, /block\.IsMovedBySplit \|\| block\.CubeGrid != Grid/);
assert.match(trackedBlocks, /block\.ComponentStack\.IsFunctional/);
assert.match(trackedBlocks, /definition\.PCU/);
assert.match(trackedBlocks, /MyCubeBlockDefinition\.PCU_CONSTRUCTION_STAGE_COST/);

assert.match(cache, /groupPcu \+= gridComponent\.GetTrackedPCU\(\)/);
assert.doesNotMatch(cache, /BlocksPCU/);
assert.doesNotMatch(cache, /RefreshPcuCache\([\s\S]*?\n        \}\n[\s\S]*?MarkRuntimeStateDirty/);

assert.match(placement, /GroupPCU \+ GetBlockPCU\(block\) > maxPCU/);
assert.match(placement, /InvalidateGameThreadStateCache\([\s\S]*?, false\)/);
assert.match(placement, /IsFunctionalChanged \+= BlockFunctionalStateChanged/);
assert.match(placement, /IsFunctionalChanged -= BlockFunctionalStateChanged/);
assert.match(placement, /groupComponent\.InvalidatePcuCache\(\)/);
assert.doesNotMatch(
  placement,
  /private void BlockFunctionalStateChanged\(\)\s*\{[^}]*MarkRuntimeStateDirty/,
);
assert.match(sharedCache, /if \(pcuMayChange\) _pcuCacheDirty = true/);
assert.match(limits, /AddGroupPCU\(GridComponent\.GetBlockPCU\(block\)\)/);
assert.match(limits, /AddGroupPCU\(-GridComponent\.GetBlockPCU\(block\)\)/);
assert.match(limits, /InvalidateGameThreadStateCache\(true, false\)/);
assert.match(merge, /totalPcu \+= GridComponent\.GetBlockPCU\(block\)/);
assert.doesNotMatch(merge, /BlocksPCU/);

console.log("tracked-pcu-calculation: ok");
