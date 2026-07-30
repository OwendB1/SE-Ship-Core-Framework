import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [authority, selection, lifecycle, group, preview] = await Promise.all([
  read("Server/Components/CoreComponent.Authority.cs"),
  read("Server/Components/GroupComponent.CoreSelection.cs"),
  read("Server/Components/GroupComponent.Lifecycle.cs"),
  read("Shared/Components/GroupComponent.cs"),
  read("Client/UI/BuildPreviewHud.cs"),
]);

assert.match(authority, /HasSameOrientationAsMain\(this\)/);
assert.match(authority, /Core must match the active main core orientation/);
assert.match(group, /CoreOrientationAlignmentDot = 0\.999999d/);
assert.match(group, /Vector3D\.Dot\(left\.Forward, right\.Forward\) >= CoreOrientationAlignmentDot/);
assert.match(group, /Vector3D\.Dot\(left\.Up, right\.Up\) >= CoreOrientationAlignmentDot/);
assert.match(selection, /HasSameCoreOrientation\(leftBlock\.WorldMatrix, rightBlock\.WorldMatrix\)/);
assert.match(selection, /HasSameCoreOrientation\(candidate, currentMain\)/);
assert.match(lifecycle, /if \(!HasSameOrientationAsMain\(coreComponent\)\)/);
assert.equal(
  lifecycle.match(/GetBestReplacementMainCoreCandidate\(lost, false\)/g)?.length,
  2,
);
assert.match(preview, /_coreOrientationMismatch = isCoreType/);
assert.match(preview, /GroupComponent\.HasSameCoreOrientation\([\s\S]*orientation, mainCoreBlock\.WorldMatrix\)/);
assert.match(preview, /Current = _boxWorld\.Forward,[\s\S]*Correct = correct\.Forward/);
assert.match(preview, /Current = _boxWorld\.Up,[\s\S]*Correct = correct\.Up/);
assert.match(preview, /Core orientation: must match active main core/);

console.log("Core orientation guard contract checks passed.");
