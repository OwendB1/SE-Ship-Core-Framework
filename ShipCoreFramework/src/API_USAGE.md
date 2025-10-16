# Ship Core Framework - External API Documentation

The Ship Core Framework provides an external API that allows other mods to interact with the core system, check block limits, retrieve grid modifiers, and query core configurations.

## API ID

The API is broadcast using a unique identifier:
```csharp
public const long API_ID = 3217652398L;
```

## How to Receive the API

In your mod's `LoadData()` method, register a message handler:

```csharp
using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace YourModNamespace
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class YourModSession : MySessionComponentBase
    {
        private const long SCF_API_ID = 3217652398L;
        private Dictionary<string, Delegate> _scfApi;

        public override void LoadData()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(SCF_API_ID, OnApiReceived);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(SCF_API_ID, OnApiReceived);
            _scfApi = null;
        }

        private void OnApiReceived(object payload)
        {
            if (payload is Dictionary<string, Delegate> api)
            {
                _scfApi = api;
                MyAPIGateway.Utilities.ShowMessage("YourMod", "Ship Core Framework API received!");
            }
        }
    }
}
```

## Available API Methods

Once you have the API dictionary, you can call methods using delegates:

### 1. GetGridCore
Gets the active ShipCore configuration for a grid.

**Signature:** `Func<IMyCubeGrid, ShipCore>`

```csharp
var getGridCore = _scfApi["GetGridCore"] as Func<IMyCubeGrid, ShipCore>;
var core = getGridCore(myGrid);

if (core != null)
{
    MyAPIGateway.Utilities.ShowMessage("Core", $"Grid has core: {core.UniqueName}");
}
```

### 2. GetCoreBySubtypeId
Gets a specific ShipCore configuration by its SubtypeId.

**Signature:** `Func<string, ShipCore>`

```csharp
var getCoreBySubtype = _scfApi["GetCoreBySubtypeId"] as Func<string, ShipCore>;
var core = getCoreBySubtype("MyShipCoreSubtype");
```

### 3. GetAllCoreConfigs
Gets all available ShipCore configurations loaded by the framework.

**Signature:** `Func<List<ShipCore>>`

```csharp
var getAllCores = _scfApi["GetAllCoreConfigs"] as Func<List<ShipCore>>;
var allCores = getAllCores();

foreach (var core in allCores)
{
    MyAPIGateway.Utilities.ShowMessage("Core", $"Available: {core.UniqueName}");
}
```

### 4. GetBlockLimitsStatus
Gets the current block limit status for a grid.

**Signature:** `Func<IMyCubeGrid, Dictionary<string, LimitStatus>>`

**LimitStatus Structure:**
```csharp
public class LimitStatus
{
    public string Name;        // Limit name
    public double Current;     // Current count
    public double Max;         // Maximum allowed
    public bool IsOverLimit;   // Whether limit is exceeded
}
```

**Example:**
```csharp
var getLimits = _scfApi["GetBlockLimitsStatus"] as Func<IMyCubeGrid, Dictionary<string, LimitStatus>>;
var limits = getLimits(myGrid);

foreach (var kvp in limits)
{
    var status = kvp.Value;
    MyAPIGateway.Utilities.ShowMessage("Limit",
        $"{status.Name}: {status.Current}/{status.Max} {(status.IsOverLimit ? "[OVER]" : "")}");
}
```

### 5. IsBlockAllowed
Checks if adding a specific number of blocks would violate limits.

**Signature:** `Func<IMyCubeGrid, string, string, int, bool>`

**Parameters:**
- `IMyCubeGrid grid` - The grid to check
- `string typeId` - Block type ID (e.g., "MyObjectBuilder_Thrust")
- `string subtypeId` - Block subtype ID
- `int count` - Number of blocks to check

```csharp
var isAllowed = _scfApi["IsBlockAllowed"] as Func<IMyCubeGrid, string, string, int, bool>;
bool canPlace = isAllowed(myGrid, "MyObjectBuilder_Thrust", "LargeBlockLargeThrust", 5);

if (!canPlace)
{
    MyAPIGateway.Utilities.ShowMessage("Warning", "Cannot place 5 more thrusters!");
}
```

### 6. GetGridModifiers
Gets the current grid modifiers applied to a grid's core.

**Signature:** `Func<IMyCubeGrid, GridModifiers>`

**GridModifiers Structure:**
```csharp
public class GridModifiers
{
    public float AssemblerSpeed;
    public float DrillHarvestMultiplier;
    public float GyroEfficiency;
    public float GyroForce;
    public float PowerProducersOutput;
    public float RefineEfficiency;
    public float RefineSpeed;
    public float ThrusterEfficiency;
    public float ThrusterForce;
    public float MaxSpeed;
    public float MaxBoost;
    public float BoostDuration;
    public float BoostCoolDown;
}
```

**Example:**
```csharp
var getModifiers = _scfApi["GetGridModifiers"] as Func<IMyCubeGrid, GridModifiers>;
var modifiers = getModifiers(myGrid);

MyAPIGateway.Utilities.ShowMessage("Modifiers",
    $"Thruster Force: {modifiers.ThrusterForce}x, Max Speed: {modifiers.MaxSpeed}");
```

### 7. GetMaxSpeed
Gets the maximum speed allowed for a grid based on its core (accounts for boost).

**Signature:** `Func<IMyCubeGrid, float>`

```csharp
var getMaxSpeed = _scfApi["GetMaxSpeed"] as Func<IMyCubeGrid, float>;
float maxSpeed = getMaxSpeed(myGrid);

MyAPIGateway.Utilities.ShowMessage("Speed", $"Max allowed: {maxSpeed} m/s");
```

### 8. IsBoostActive
Checks if boost is currently active for a grid.

**Signature:** `Func<IMyCubeGrid, bool>`

```csharp
var isBoostActive = _scfApi["IsBoostActive"] as Func<IMyCubeGrid, bool>;
bool boosting = isBoostActive(myGrid);

if (boosting)
{
    MyAPIGateway.Utilities.ShowMessage("Boost", "BOOST ACTIVE!");
}
```

### 9. GetNoCoreConfig
Gets the currently selected NoCore configuration (applied to grids without a core).

**Signature:** `Func<ShipCore>`

```csharp
var getNoCoreConfig = _scfApi["GetNoCoreConfig"] as Func<ShipCore>;
var noCoreConfig = getNoCoreConfig();

MyAPIGateway.Utilities.ShowMessage("NoCore", $"Default config: {noCoreConfig.UniqueName}");
```

### 10. GetGroupComponent (Advanced)
Gets the internal GroupComponent for a grid.

**⚠️ WARNING:** Direct manipulation of GroupComponent can break the framework. Use with caution and only for read-only access.

**Signature:** `Func<IMyCubeGrid, GroupComponent>`

```csharp
var getGroupComponent = _scfApi["GetGroupComponent"] as Func<IMyCubeGrid, GroupComponent>;
var groupComponent = getGroupComponent(myGrid);

if (groupComponent != null)
{
    // Read-only access is safe
    int blockCount = groupComponent.GroupBlocksCount;
}
```

## Event System

The Ship Core Framework broadcasts events when significant actions occur. Other mods can subscribe to these events to react to core activation, limit enforcement, boost activation, and more.

### Available Events

The following event IDs are available for subscription:

```csharp
public const long EVENT_CORE_ACTIVATED = 3217652399L;
public const long EVENT_CORE_DEACTIVATED = 3217652400L;
public const long EVENT_LIMITS_RECALCULATED = 3217652401L;
public const long EVENT_LIMITS_ENFORCED = 3217652402L;
public const long EVENT_BOOST_ACTIVATED = 3217652403L;
public const long EVENT_BOOST_DEACTIVATED = 3217652404L;
public const long EVENT_ACTIVE_DEFENSE_ACTIVATED = 3217652405L;
public const long EVENT_ACTIVE_DEFENSE_DEACTIVATED = 3217652406L;
public const long EVENT_GRID_ADDED_TO_GROUP = 3217652407L;
public const long EVENT_GRID_REMOVED_FROM_GROUP = 3217652408L;
```

### Event Argument Classes

Each event broadcasts with specific event arguments:

**CoreActivatedEventArgs**:
```csharp
public class CoreActivatedEventArgs
{
    public IMyCubeGrid Grid;           // Grid that had a core activated
    public string CoreSubtypeId;       // SubtypeId of the activated core beacon
    public string CoreName;            // Unique name of the core configuration
    public DateTime Timestamp;         // When the event occurred
}
```

**CoreDeactivatedEventArgs**:
```csharp
public class CoreDeactivatedEventArgs
{
    public IMyCubeGrid Grid;                    // Grid that lost its core
    public string PreviousCoreSubtypeId;        // SubtypeId of the previous core
    public string PreviousCoreName;             // Name of the previous core
    public DateTime Timestamp;
}
```

**BoostEventArgs / ActiveDefenseEventArgs**:
```csharp
public class BoostEventArgs  // Also used for Active Defense events
{
    public IMyCubeGrid Grid;           // Grid that activated/deactivated boost
    public DateTime Timestamp;
}
```

**GridGroupEventArgs**:
```csharp
public class GridGroupEventArgs
{
    public IMyCubeGrid Grid;             // Grid that was added/removed
    public IMyGridGroupData GroupData;   // The grid group
    public DateTime Timestamp;
}
```

### Subscribing to Events

To subscribe to events, register a message handler for each event ID you're interested in:

```csharp
using System;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace YourModNamespace
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class YourModSession : MySessionComponentBase
    {
        private const long EVENT_CORE_ACTIVATED = 3217652399L;
        private const long EVENT_CORE_DEACTIVATED = 3217652400L;
        private const long EVENT_BOOST_ACTIVATED = 3217652403L;

        public override void LoadData()
        {
            // Subscribe to multiple events
            MyAPIGateway.Utilities.RegisterMessageHandler(EVENT_CORE_ACTIVATED, OnCoreActivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(EVENT_CORE_DEACTIVATED, OnCoreDeactivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(EVENT_BOOST_ACTIVATED, OnBoostActivated);
        }

        protected override void UnloadData()
        {
            // Unsubscribe from events
            MyAPIGateway.Utilities.UnregisterMessageHandler(EVENT_CORE_ACTIVATED, OnCoreActivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(EVENT_CORE_DEACTIVATED, OnCoreDeactivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(EVENT_BOOST_ACTIVATED, OnBoostActivated);
        }

        private void OnCoreActivated(object payload)
        {
            // Cast to the specific event args type
            var eventArgs = payload as CoreActivatedEventArgs;
            if (eventArgs == null) return;

            MyAPIGateway.Utilities.ShowMessage("Event",
                $"Core '{eventArgs.CoreName}' activated on grid '{eventArgs.Grid.CustomName}'!");
        }

        private void OnCoreDeactivated(object payload)
        {
            var eventArgs = payload as CoreDeactivatedEventArgs;
            if (eventArgs == null) return;

            MyAPIGateway.Utilities.ShowMessage("Event",
                $"Core '{eventArgs.PreviousCoreName}' deactivated on grid '{eventArgs.Grid.CustomName}'!");
        }

        private void OnBoostActivated(object payload)
        {
            var eventArgs = payload as BoostEventArgs;
            if (eventArgs == null) return;

            MyAPIGateway.Utilities.ShowMessage("Event",
                $"Boost activated on grid '{eventArgs.Grid.CustomName}'!");
        }
    }
}
```

### Event Use Cases

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

### Important Event Notes

1. **Thread Safety**: Event broadcasts occur on the game thread, so it's safe to interact with game objects in event handlers.

2. **Event Ordering**: Events are broadcast immediately when they occur, but the order of multiple events in the same tick is not guaranteed.

3. **Null Checks**: Always check if the event args can be cast to the expected type, as future versions may add new event types.

4. **Performance**: Event handlers should be lightweight. Heavy operations should be queued for later processing.

5. **Unsubscribe**: Always unsubscribe from events in `UnloadData()` to prevent memory leaks.

## Complete Example

Here's a complete example mod that uses both the Ship Core Framework API and Events:

```csharp
using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace ExampleMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class ExampleModSession : MySessionComponentBase
    {
        private const long SCF_API_ID = 3217652398L;
        private Dictionary<string, Delegate> _scfApi;
        private bool _apiReady = false;

        public override void LoadData()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(SCF_API_ID, OnApiReceived);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(SCF_API_ID, OnApiReceived);
            _scfApi = null;
            _apiReady = false;
        }

        private void OnApiReceived(object payload)
        {
            if (payload is Dictionary<string, Delegate> api)
            {
                _scfApi = api;
                _apiReady = true;
                MyAPIGateway.Utilities.ShowMessage("ExampleMod", "SCF API received!");
            }
        }

        // Example: Check if a player's grid can place more thrusters
        public bool CanPlaceThrusters(IMyCubeGrid grid, int count)
        {
            if (!_apiReady) return true; // If API not ready, allow by default

            var isAllowed = _scfApi["IsBlockAllowed"] as Func<IMyCubeGrid, string, string, int, bool>;
            if (isAllowed == null) return true;

            return isAllowed(grid, "MyObjectBuilder_Thrust", "any", count);
        }

        // Example: Get all block limits for a grid and display them
        public void ShowGridLimits(IMyCubeGrid grid)
        {
            if (!_apiReady) return;

            var getLimits = _scfApi["GetBlockLimitsStatus"] as Func<IMyCubeGrid, Dictionary<string, LimitStatus>>;
            if (getLimits == null) return;

            var limits = getLimits(grid);

            MyAPIGateway.Utilities.ShowMessage("Limits", "Block limits for grid:");
            foreach (var kvp in limits)
            {
                var status = kvp.Value;
                MyAPIGateway.Utilities.ShowMessage("", status.ToString());
            }
        }

        // Example: Get the core name for a grid
        public string GetCoreNameForGrid(IMyCubeGrid grid)
        {
            if (!_apiReady) return "Unknown";

            var getCore = _scfApi["GetGridCore"] as Func<IMyCubeGrid, ShipCore>;
            if (getCore == null) return "Unknown";

            var core = getCore(grid);
            return core?.UniqueName ?? "No Core";
        }
    }
}
```

## Important Notes

1. **API Availability**: The API is broadcast during `LoadData()`, so it should be available by the time your mod's `BeforeStart()` is called. Always check if the API is null before using it.

2. **Thread Safety**: All API methods include proper error handling and thread safety. However, be aware that grid groups can change at any time due to pistons, rotors, connectors, etc.

3. **Null Checks**: Always check if the returned delegates are null before calling them, as the API dictionary might not contain all expected methods if the version differs.

4. **NoCore Config**: Grids without a Ship Core beacon will use the "NoCore" configuration. You can check which config is active using `GetGridCore()` or `GetNoCoreConfig()`.

5. **Version Compatibility**: This API is designed for Ship Core Framework v3. Future versions may add or change methods, so always include null checks for delegates.

## Preventing Drift

As mentioned in your requirements, the API prevents drift between what the framework considers a grid's core and what your mod thinks it is. **Always use `GetGridCore()` to determine a grid's active core** instead of trying to find core beacons manually.

Example:
```csharp
// ❌ DON'T DO THIS - can cause drift
var beacons = new List<IMyBeacon>();
grid.GetBlocksOfType(beacons, b => b.BlockDefinition.SubtypeId.Contains("Core"));

// ✅ DO THIS - always matches the framework's state
var getCore = _scfApi["GetGridCore"] as Func<IMyCubeGrid, ShipCore>;
var actualCore = getCore(grid);
```

## Support

For questions or issues with the API, please contact:
- Blues-Hailfire
- ODB-Tech

Or open an issue in the mod's development repository.