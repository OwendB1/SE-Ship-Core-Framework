# SCF Threading Plan

This document captures the plan for a full multithreading pass in Ship Core Framework. The first piece of groundwork is a game-thread write wrapper for shared state, plus an inspectable queue for work that must be applied later on the game thread.

## Goals

- Keep reads safe from background workers.
- Keep writes deterministic by applying them on the game thread.
- Make queued work visible for debugging and niche interception.
- Avoid live `Dictionary` enumeration while another thread may mutate it.
- Separate collection safety from Space Engineers API safety.
- Give Nexus sync, limit managers, speed enforcement, and delayed validations a consistent threading model.

## Non-Goals

- The wrapper must not make SE game objects safe to touch off-thread. Blocks, grids, physics, definitions, ownership changes, upgrade links, removals, and terminal properties still need game-thread access.
- The wrapper should not hide expensive game-thread work. Large batches must stay visible and measurable.
- The wrapper should not expose the internal mutable collection to callers.

## Current Risk Classes

The diff since `3.6.0` highlighted two examples that this pass should prevent:

- Background validation can call routines that inspect upgrade attachments and apply block modifiers.
- Speed source selection can call grid mass APIs from a background worker.

More generally, SCF has these recurring risks:

- Normal dictionaries are enumerated for snapshots while other paths can mutate counts.
- Delayed work can become stale when a group splits, merges, closes, deactivates, or changes core.
- Counter updates can be half-applied if remove/add operations are not batched.
- Nexus snapshots can observe intermediate state.
- Unload can leave queued work targeting closed entities.

## Wrapper Model

Use a game-thread write wrapper around a thread-safe backing store.

Recommended backing store:

```csharp
ConcurrentDictionary<TKey, TValue>
```

Reads may happen from any thread. Writes are applied only on the game thread. If a caller requests a write from a background thread, the wrapper queues the write for the next game-thread flush.

The wrapper should support:

- `TryGetValue`
- `ContainsKey`
- `GetOrDefault`
- `ToArraySnapshot`
- `ValuesSnapshot`
- `Set`
- `Remove`
- `AddOrUpdate`
- `Increment`
- `EnqueueBatch`
- `FlushPendingWrites`
- `SnapshotPendingWork`
- `CancelPendingWhere`

Avoid nested mutable dictionaries. A wrapper around `Dictionary<long, Dictionary<string, int>>` still leaves the inner dictionaries unsafe. Prefer flattened keys:

```csharp
internal struct CoreCountKey
{
    internal long OwnerOrFactionId;
    internal string CoreType;
}
```

Then store counts as:

```csharp
GameThreadWriteDictionary<CoreCountKey, int>
```

Manifest group counts can use:

```csharp
GameThreadWriteDictionary<string, int>
```

## Queued Work

Queued work should be structured data, not raw anonymous actions. This makes it inspectable, cancellable, and coalescible.

API sketch:

```csharp
internal sealed class QueuedWrite
{
    internal long Id;
    internal int CreatedTick;
    internal int CreatedThreadId;
    internal string Category;
    internal string CoalesceKey;
    internal string DebugDescription;
    internal Func<bool> ShouldApply;
    internal Action Apply;
    internal bool Cancelled;
    internal string CancelReason;
}
```

The wrapper can expose a copy for diagnostics:

```csharp
internal QueuedWriteInfo[] SnapshotPendingWork()
```

`QueuedWriteInfo` should be a safe data object that does not expose the original `Action`.

## Interception Points

Interception is needed for real SCF corner cases, not just debugging.

Before apply:

- Drop all work when `Session.IsShuttingDown`.
- Drop group work when the group was cleaned or no longer matches the expected group id.
- Drop grid or block work when the entity is closed, marked for close, or no longer belongs to the expected group.
- Drop config-sensitive work when the config version changed.
- Drop validation work if a newer validation for the same group replaced it.

Coalescing:

- Keep only the latest `Set` for a given count key.
- Merge repeated `Increment` operations for the same key when ordering does not matter.
- Replace delayed validations for the same group with the latest validation.

Batch boundaries:

- Ownership transfer counters should be one batch.
- Core activation and reset counter changes should be one batch.
- Nexus snapshot preparation should run after a flush barrier.

## Flush Lifecycle

The game-thread flush should happen in `UpdateAfterSimulation` before the background worker starts:

```csharp
ThreadWork.FlushPendingWrites();

MyAPIGateway.Parallel.StartBackground(() =>
{
    // background reads and background-safe calculations only
});
```

Some systems may need an additional flush before Nexus snapshots:

```csharp
ThreadWork.FlushPendingWrites(ThreadWorkCategory.Counts);
LimitsNexusSync.RunPeriodicSnapshotTick();
```

Flush must have a time or operation budget if batches can become large. If the budget is reached, leave the remaining work queued and log the backlog at a throttled rate.

## Game-Thread Detection

Capture the game-thread id during session startup:

```csharp
Session.GameThreadId = Thread.CurrentThread.ManagedThreadId;
```

Expose:

```csharp
internal static bool IsGameThread
{
    get { return Thread.CurrentThread.ManagedThreadId == GameThreadId; }
}
```

Debug behavior:

- Immediate write APIs can log when called off-thread.
- Strict mode can throw on off-thread immediate writes during development.
- Queue APIs should be allowed from any thread.

## Wrapper Usage Rules

- Never return the backing `ConcurrentDictionary`.
- Never return live mutable values.
- Snapshot before enumeration.
- Do not enqueue work that captures live blocks or grids unless `ShouldApply` revalidates them on the game thread.
- Prefer ids and stable keys over captured entity references.
- Prefer `CoalesceKey` for repeated delayed work.
- Keep batches small and named.

## First Migration Targets

1. `PerPlayerManager`
   - Replace nested dictionaries with flattened count keys.
   - Make count reads snapshot-safe.
   - Batch owner/core count transitions.

2. `PerFactionManager`
   - Same flattened count model.
   - Make faction removal a batch.
   - Keep Nexus broadcasts after local write flush.

3. `PerManifestGroupManager`
   - Wrap manifest group counts.
   - Snapshot before Nexus build.

4. `LimitsNexusSync`
   - Build snapshots from snapshot arrays only.
   - Avoid reading live mutable count structures.
   - Consider a flush barrier before periodic snapshot.

5. Delayed limit validation
   - Queue the validation as game-thread work.
   - Use a group validation `CoalesceKey`.
   - Drop stale work if the group closes or the validation generation changed.

6. Speed source selection
   - Remove direct mass API calls from background ranking.
   - Cache group dry mass on the game thread, or use a background-safe ranking input.

## Full Threading Pass Phases

### Phase 1: Instrumentation

- Add game-thread id capture.
- Add `Session.IsGameThread`.
- Add throttled logging for off-thread SE API sensitive paths.
- Add queue backlog diagnostics.

### Phase 2: Shared Count State

- Add the wrapper.
- Migrate player, faction, and manifest counters.
- Convert Nexus snapshot building to snapshot arrays.
- Add batch writes for ownership and core transitions.

### Phase 3: Delayed Work

- Move delayed validation apply work to the game-thread queue.
- Add cancellation and coalescing by group id.
- Ensure unload cancels all pending work.

### Phase 4: SE API Audit

Audit background paths for calls that touch:

- `MyCubeGrid`
- `IMySlimBlock`
- `IMyCubeBlock`
- terminal properties
- physics and mass APIs
- upgrade module attachment APIs
- ownership APIs
- block removal, damage, refund, or enable state

Move unsafe calls to game-thread queued work, or replace them with cached data collected on the game thread.

### Phase 5: Snapshot Caches

For background speed, limits, and Nexus work, maintain explicit game-thread caches:

- group block count
- PCU
- dry/wet mass
- representative grid id
- active core subtype
- speed override mode and priority
- no-core/core state

Background workers should consume these caches, not live SE objects.

## Open Questions

- Should strict off-thread write violations throw in debug builds or only log?
- What is the max queued operation budget per tick?
- Do Nexus snapshots require a hard flush barrier every time, or only before periodic snapshots?
- Should queued work be exposed through chat/admin debug commands?
- Should batch failures be all-or-nothing, or should each operation validate independently?

## Working Principle

Collections can be made thread-safe. SE game objects cannot. The wrapper protects SCF state, while the threading pass must still move every game-object mutation and sensitive read back to the game thread or a game-thread-built cache.
