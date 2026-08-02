import assert from 'node:assert/strict';
import fs from 'node:fs';

const networking = fs.readFileSync(
  'ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Shared/Network/Networking.cs',
  'utf8',
);
const publisher = fs.readFileSync(
  'ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Session/Server/Session.RuntimeStatePublisher.cs',
  'utf8',
);
const notifications = fs.readFileSync(
  'ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Server/Utilities/Utils.Notifications.cs',
  'utf8',
);

// Extracts a method body by brace matching from the signature onward.
function methodBody(source, signature) {
  const start = source.indexOf(signature);
  assert.notEqual(start, -1, `missing method: ${signature}`);
  const open = source.indexOf('{', start);
  let depth = 0;
  for (let i = open; i < source.length; i++) {
    if (source[i] === '{') depth++;
    else if (source[i] === '}' && --depth === 0) return source.slice(open, i + 1);
  }
  throw new Error(`unbalanced braces for ${signature}`);
}

// --- the core invariant: one serialization, reused across every recipient ---------
const fanout = methodBody(networking, 'internal void SendToPlayers(');

const serializeAt = fanout.indexOf('SerializeToBinary');
const loopAt = fanout.indexOf('for (');
assert.notEqual(serializeAt, -1, 'SendToPlayers must serialize the packet');
assert.notEqual(loopAt, -1, 'SendToPlayers must loop over recipients');
assert.ok(
  serializeAt < loopAt,
  'SendToPlayers must serialize BEFORE the recipient loop, not inside it',
);
assert.equal(
  (fanout.match(/SerializeToBinary/g) || []).length,
  1,
  'SendToPlayers must serialize exactly once per packet',
);

// Serializing as the concrete type would drop the ProtoInclude subtype header and
// break SerializeFromBinary<PacketBase> on the receiving end.
assert.match(networking, /SerializeToBinary<PacketBase>\(packet\)/);
assert.doesNotMatch(networking, /SerializeToBinary\(packet\)/);

// --- bulk runtime-state sends must go through the shared-buffer path --------------
assert.match(publisher, /SendRuntimeStatePacketsToAll\(/);
assert.match(publisher, /SendRuntimeStateDeltaPacketsToAll\(/);
assert.doesNotMatch(
  publisher,
  /SendRuntimeStateDeltaPacketsTo\(/,
  'per-recipient delta send should be gone; use the ToAll variant',
);

for (const sig of [
  'private static void SendRuntimeStatePacketsToAll(',
  'private static void SendRuntimeStateDeltaPacketsToAll(',
]) {
  const body = methodBody(publisher, sig);
  assert.match(body, /Networking\.SendToPlayers\(/, `${sig} must use SendToPlayers`);
  assert.doesNotMatch(
    body,
    /Networking\.SendToPlayer\(/,
    `${sig} must not fall back to the single-recipient send`,
  );
}

// The single-recipient path is still needed for join-time state requests.
assert.match(publisher, /private static void SendRuntimeStatePacketsTo\(/);

// --- the O(groups) liveness scan must not run every tick --------------------------
assert.match(publisher, /RuntimeStateRemovalScanIntervalTicks/);
const syncTick = methodBody(publisher, 'private void RunRuntimeStateSyncTick()');
assert.match(
  syncTick,
  /CurrentTick % RuntimeStateRemovalScanIntervalTicks == 0\) QueueRemovedRuntimeStates\(\)/,
  'QueueRemovedRuntimeStates must be interval-gated, not called every tick',
);

// The full-snapshot tick must always coincide with a fresh removal scan.
const interval = Number(
  /RuntimeStateSyncIntervalTicks = (\d+)/.exec(publisher)[1],
);
const scan = Number(
  /RuntimeStateRemovalScanIntervalTicks = (\d+)/.exec(publisher)[1],
);
assert.equal(
  interval % scan,
  0,
  'snapshot interval must be a multiple of the removal-scan interval',
);

// --- notification fan-outs share one buffer too -----------------------------------
for (const sig of ['static partial void ForwardServerLogMessage(', 'internal static void ShowNotification(']) {
  const body = methodBody(notifications, sig);
  assert.match(body, /SendToPlayers\(/, `${sig} must batch its fan-out`);
}

console.log('runtime-state-fanout: ok');
