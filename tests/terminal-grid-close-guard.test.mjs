import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [clientTick, clientLifecycle, sessionRun, sessionEvents] =
  await Promise.all([
    read("Session/Client/Session.ClientTick.cs"),
    read("Session/Client/Session.ClientLifecycle.cs"),
    read("Session/Session.Run.cs"),
    read("Session/Session.Events.cs"),
  ]);

assert.match(clientTick, /UpdateTerminalGridCloseGuard\(\)/);
assert.match(clientTick, /OnMarkForClose \+= TerminalGridMarkedForClose/);
assert.match(clientTick, /ChangeInteractedEntity\(null, false\)/);
assert.match(clientLifecycle, /ResetTerminalGridCloseGuard\(\)/);
assert.doesNotMatch(
  sessionRun + sessionEvents,
  /OnBlocksChangeFinishedGlobally/,
);

console.log("Terminal grid-close guard contract checks passed.");
