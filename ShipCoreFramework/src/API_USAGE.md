# Ship Core Framework - External API Documentation

The Ship Core Framework provides an external API that allows other mods to interact with the core system, check block limits, retrieve grid modifiers, and query core configurations.

## Getting Started (Recommended)

Use the **provided sample client** (`ShipCoreFrameworkClient`) instead of re-implementing message handlers or copying raw API delegate signatures into your mod.

### 1) Copy the client + DTOs into your mod

Copy these two files into your mod project (keep them in the same namespace):
- `ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/API/ApiData.cs` (constants + DTOs + event args)
- `ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/API/SCF_ModAPIClient.cs` (the client wrapper)

This keeps your mod strongly-typed without referencing the Ship Core Framework assembly.

### 2) Register / Unregister once per session

Create a single `ShipCoreFrameworkClient`, call `Register()` in `LoadData()`, and `Unregister()` in `UnloadData()`:

Note: Ship Core Framework broadcasts its API payload in `BeforeStart()`, so `IsReady` may still be false during your `LoadData()`. Use `IsReady` checks in `BeforeStart()`/`Update...()` before calling API methods.

```csharp
using System;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using ShipCoreFramework; // After copying ApiData.cs to your project

namespace YourModNamespace
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class YourModSession : MySessionComponentBase
    {
        private readonly ShipCoreFrameworkClient _scf = new ShipCoreFrameworkClient();

        public override void LoadData()
        {
            _scf.Register();
        }

        protected override void UnloadData()
        {
            _scf.Unregister();
        }

        public override void UpdateBeforeSimulation()
        {
            if (!_scf.IsReady)
                return;

            // Safe to call API methods here (or from any point after IsReady == true).
        }
    }
}
```

### 3) Version compatibility

The client requires a matching **major** API version. Minor version differences are treated as compatible, so backwards-compatible additions do not block connection.

If you want to use API members added in a newer minor version, re-copy `ApiData.cs` and `SCF_ModAPIClient.cs` into your mod first.

## Using the API (through the client)

Once `IsReady` is true, call the methods on `ShipCoreFrameworkClient`. Prefer the `long cubeGridEntityId` overloads when you are responding to events (event args already contain entity IDs).

The full, up-to-date method surface is intentionally maintained in the sample client:
- `ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/API/SCF_ModAPIClient.cs`

Common calls include:
- Core/config: `GetGridCore(...)`, `GetCoreBySubtypeId(...)`, `GetAllCoreConfigs()`, `GetNoCoreConfig()`
- Limits: `GetBlockLimitsStatus(...)`, `IsBlockAllowed(...)`
- Modifiers/speed: `GetGridModifiers(...)`, `GetMaxSpeed(...)`, `IsBoostActive(...)`, `GetSpeedModifiers(...)`
- Boost tuning: `GetBoostResistance(...)`, `GetBaseMaxSpeed(...)`, `GetBoostDuration(...)`, `GetBoostCooldown(...)`
- Friction (group-scoped): `SetFrictionEnabledForGroup(...)`, `GetFrictionEnabledForGroup(...)`, and related friction settings

## Events (through the client)

The Ship Core Framework broadcasts events when significant actions occur. The sample client automatically registers all event message handlers and deserializes payloads into strongly typed event args.

Subscribe to the client events you care about (instead of registering message handlers yourself):
- Core lifecycle: `CoreActivated`, `CoreDeactivated`
- Limits: `LimitsRecalculated`, `LimitsEnforced`
- Boost: `BoostActivated`, `BoostDeactivated`
- Active defense: `ActiveDefenseActivated`, `ActiveDefenseDeactivated`
- Logical groups: `GridAddedToGroup`, `GridRemovedFromGroup`

For convenience, the client also exposes “resolved” variants that attempt to resolve `IMyCubeGrid` and `IMyGridGroupData` for you (resolution can fail if the entity isn’t available).

### Event use cases

**Core Activated/Deactivated Events**: Use these to react when a grid gains or loses its main core. This is triggered when:
- A core beacon is placed and becomes the main core
- All core beacons are destroyed (CoreDeactivated)
- The main core failover occurs to a different beacon

**Boost Events**: React to boost activation/deactivation for:
- Visual effects (engine trails, particle effects)
- Sound effects
- Speed limit adjustments in your mod

**Active Defense Events**: Monitor when active defense is engaged/disengaged for:
- Shield visual effects
- HUD notifications
- Combat logging

**Grid Group Events**: Track when grids are added/removed from groups due to:
- Connectors connecting/disconnecting
- Pistons/rotors creating sub-grids
- Grid splits from damage

**Limits Events**: Monitor when block limits are recalculated or enforced (advanced usage).

### Important notes

1. **Thread Safety**: Event broadcasts occur on the game thread, so it's safe to interact with game objects in event handlers.

2. **Event Ordering**: Events are broadcast immediately when they occur, but the order of multiple events in the same tick is not guaranteed.

3. **Null Checks**: Always assume grids may not be resolved (use the `...Resolved` events if you want best-effort resolution).

4. **Performance**: Event handlers should be lightweight. Heavy operations should be queued for later processing.

5. **Unsubscribe**: Always unsubscribe from events in `UnloadData()` to prevent memory leaks.

## Best practices

- **Wait for readiness**: only call methods after `IsReady == true`.
- **Avoid “core drift”**: don’t scan for core beacons yourself; use `GetGridCore(...)`/`GetGridCoreSubtypeId(...)` (or react to `CoreActivated`/`CoreDeactivated`) so your mod stays consistent with the framework.
- **No-core handling**: grids without a core use the NoCore config; use `GetNoCoreConfig()` if you need the default values.
- **Group volatility**: logical groups can change due to connectors/rotors/pistons/splits; prefer entityId-based overloads and event-driven updates.

## Support

For questions or issues with the API, please contact:
- Blues-Hailfire
- OwendB (OB / ODB-Tech)

Or open an issue in the mod's development repository.
