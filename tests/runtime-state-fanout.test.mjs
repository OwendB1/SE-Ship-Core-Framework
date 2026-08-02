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

// --- shared-buffer fan-out: one serialization reused across recipients ------------
// Still used by the notification paths, where every recipient gets identical bytes.
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

for (const sig of ['static partial void ForwardServerLogMessage(', 'internal static void ShowNotification(']) {
  const body = methodBody(notifications, sig);
  assert.match(body, /SendToPlayers\(/, `${sig} must batch its fan-out`);
}

// --- runtime state must be range filtered per recipient ---------------------------
// Clients may only be told about groups they could observe in game.
const syncTick = methodBody(publisher, 'private void RunRuntimeStateSyncTick()');

assert.match(syncTick, /GetRuntimeStateRangeSquared\(\)/);
assert.equal(
  (syncTick.match(/FilterRuntimeStates\(/g) || []).length,
  2,
  'both the snapshot and delta paths must filter by range',
);
// Range filtering is per player, so the shared-buffer helper must not be used here:
// it would send one recipient's visible set to everybody.
assert.doesNotMatch(
  syncTick,
  /SendToPlayers\(/,
  'runtime state must not use the shared-buffer fan-out after filtering',
);

const filter = methodBody(publisher, 'private static List<GroupRuntimeState> FilterRuntimeStates(');
// The distance test runs once per entry per player, and the mod profiler rewriter wraps
// every mod method in a try/finally that also blocks JIT inlining. Keep it inline, and
// keep it on the game-binary Vector3D helper, which is not rewritten.
assert.match(filter, /Vector3D\.DistanceSquared\(/, 'distance test must be inline');
assert.doesNotMatch(
  filter,
  /IsWithinRange\(|IsRelevant\w*\(/,
  'do not reintroduce a per-entry helper in the filter loop',
);
assert.match(
  filter,
  /RuntimeStateVisibleBuffer/,
  'filter must reuse its output buffer rather than allocate per player',
);

// Falling back to an unbounded radius would silently restore the leak.
const range = methodBody(publisher, 'private static double GetRuntimeStateRangeSquared()');
assert.match(range, /ViewDistance/);
assert.match(range, /RuntimeStateFallbackRangeMeters/);
assert.doesNotMatch(
  range,
  /double\.MaxValue|MaxValue/,
  'range must never fall back to unbounded',
);

// The join-time request path is a full snapshot too, so it needs the same filter.
const onRequest = methodBody(publisher, 'internal static void SendRuntimeStateTo(');
assert.match(onRequest, /TryGetPlayerPosition\(/);
assert.match(onRequest, /FilterRuntimeStates\(/);

// Tombstones carry the group's last known position so removals are filtered as well.
const identity = methodBody(publisher, 'private sealed class RuntimeStateIdentity');
assert.match(identity, /CachedGridState\[\] Grids/);
const deltaEntries = methodBody(publisher, 'private static List<RuntimeStateEntry> BuildRuntimeStateDeltaEntries(');
assert.match(
  deltaEntries,
  /Grids = identity\.Grids/,
  'tombstones must reuse the last known position for filtering',
);

// --- the O(groups) liveness scan must not run every tick --------------------------
assert.match(
  syncTick,
  /CurrentTick % RuntimeStateRemovalScanIntervalTicks == 0\) QueueRemovedRuntimeStates\(\)/,
  'QueueRemovedRuntimeStates must be interval-gated, not called every tick',
);

// The full-snapshot tick must always coincide with a fresh removal scan.
const interval = Number(/RuntimeStateSyncIntervalTicks = (\d+)/.exec(publisher)[1]);
const scan = Number(/RuntimeStateRemovalScanIntervalTicks = (\d+)/.exec(publisher)[1]);
assert.equal(
  interval % scan,
  0,
  'snapshot interval must be a multiple of the removal-scan interval',
);

console.log('runtime-state-fanout: ok');
