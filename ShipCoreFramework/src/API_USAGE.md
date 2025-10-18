# Ship Core Framework - External API Documentation

The Ship Core Framework provides an external API that allows other mods to interact with the core system, check block limits, retrieve grid modifiers, and query core configurations.

## Getting Started

**IMPORTANT**: To use this API, copy the `ApiData.cs` file from the Ship Core Framework mod to your own mod project. This file contains all the data structures (structs and event args classes) that the API uses. This approach ensures you have all necessary types without needing to reference the Ship Core Framework assembly.

## API ID

The API constants are defined in `ApiConstants` (available in `ApiData.cs`):
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
using ShipCoreFramework; // After copying ApiData.cs to your project

namespace YourModNamespace
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class YourModSession : MySessionComponentBase
    {
        private Dictionary<string, Delegate> _scfApi;

        public override void LoadData()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.API_ID, OnApiReceived);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.API_ID, OnApiReceived);
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

Once you have the API dictionary, you can call methods using delegates. All return types are structs defined in `ApiData.cs`.

### 1. GetGridCore
Gets the active ShipCore configuration for a grid.

**Signature:** `Func<IMyCubeGrid, ShipCoreData>`

```csharp
var getGridCore = _scfApi["GetGridCore"] as Func<IMyCubeGrid, ShipCoreData>;
var core = getGridCore(myGrid);

MyAPIGateway.Utilities.ShowMessage("Core", $"Grid has core: {core.UniqueName}");
```

### 2. GetCoreBySubtypeId
Gets a specific ShipCore configuration by its SubtypeId.

**Signature:** `Func<string, ShipCoreData>`

```csharp
var getCoreBySubtype = _scfApi["GetCoreBySubtypeId"] as Func<string, ShipCoreData>;
var core = getCoreBySubtype("MyShipCoreSubtype");
```

### 3. GetAllCoreConfigs
Gets all available ShipCore configurations loaded by the framework.

**Signature:** `Func<List<ShipCoreData>>`

```csharp
var getAllCores = _scfApi["GetAllCoreConfigs"] as Func<List<ShipCoreData>>;
var allCores = getAllCores();

foreach (var core in allCores)
{
    MyAPIGateway.Utilities.ShowMessage("Core", $"Available: {core.UniqueName}");
}
```

### 4. GetBlockLimitsStatus
Gets the current block limit status for a grid.

**Signature:** `Func<IMyCubeGrid, Dictionary<string, LimitStatusData>>`

**LimitStatusData Structure** (defined in `ApiData.cs`):
```csharp
public struct LimitStatusData
{
    public string Name;        // Limit name
    public double Current;     // Current count
    public double Max;         // Maximum allowed
    public bool IsOverLimit;   // Whether limit is exceeded
}
```

**Example:**
```csharp
var getLimits = _scfApi["GetBlockLimitsStatus"] as Func<IMyCubeGrid, Dictionary<string, LimitStatusData>>;
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

**Signature:** `Func<IMyCubeGrid, GridModifiersData>`

**GridModifiersData Structure** (defined in `ApiData.cs`):
```csharp
public struct GridModifiersData
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
var getModifiers = _scfApi["GetGridModifiers"] as Func<IMyCubeGrid, GridModifiersData>;
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

**Signature:** `Func<ShipCoreData>`

```csharp
var getNoCoreConfig = _scfApi["GetNoCoreConfig"] as Func<ShipCoreData>;
var noCoreConfig = getNoCoreConfig();

MyAPIGateway.Utilities.ShowMessage("NoCore", $"Default config: {noCoreConfig.UniqueName}");
```

## Data Structures

All data structures are defined in `ApiData.cs`. Copy this file to your mod project to access them.

### ShipCoreData
```csharp
public struct ShipCoreData
{
    public string SubtypeId;
    public string UniqueName;
    public bool ForceBroadCast;
    public float ForceBroadCastRange;
    public bool LargeGridStatic;
    public bool LargeGridMobile;
    public int MaxBlocks;
    public float MaxMass;
    public int MaxPCU;
    public int MaxPerFaction;
    public int MaxPerPlayer;
    public int MinPlayers;
    public GridModifiersData Modifiers;
    public GridDefenseModifiersData PassiveDefenseModifiers;
    public bool SpeedBoostEnabled;
    public bool EnableActiveDefenseModifiers;
    public GridDefenseModifiersData ActiveDefenseModifiers;
}
```

### GridDefenseModifiersData
```csharp
public struct GridDefenseModifiersData
{
    public float Bullet;
    public float PostShield;
    public float Duration;
    public float Cooldown;
    public float Rocket;
    public float Explosion;
    public float Environment;
    public float Energy;
    public float Kinetic;
}
```

## Event System

The Ship Core Framework broadcasts events when significant actions occur. Other mods can subscribe to these events to react to core activation, limit enforcement, boost activation, and more.

### Available Events

The following event IDs are available for subscription (defined in `ApiConstants` in `ApiData.cs`):

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

Each event broadcasts with specific event arguments (all defined in `ApiData.cs`):

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

**LimitsRecalculatedEventArgs**:
```csharp
public class LimitsRecalculatedEventArgs
{
    public IMyGridGroupData GroupData;
    public DateTime Timestamp;
}
```

**LimitsEnforcedEventArgs**:
```csharp
public class LimitsEnforcedEventArgs
{
    public IMyGridGroupData GroupData;
    public int BlocksPunished;
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

public class ActiveDefenseEventArgs
{
    public IMyCubeGrid Grid;
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
using ShipCoreFramework; // After copying ApiData.cs

namespace YourModNamespace
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class YourModSession : MySessionComponentBase
    {
        public override void LoadData()
        {
            // Subscribe to multiple events
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_CORE_ACTIVATED, OnCoreActivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_CORE_DEACTIVATED, OnCoreDeactivated);
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.EVENT_BOOST_ACTIVATED, OnBoostActivated);
        }

        protected override void UnloadData()
        {
            // Unsubscribe from events
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_CORE_ACTIVATED, OnCoreActivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_CORE_DEACTIVATED, OnCoreDeactivated);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.EVENT_BOOST_ACTIVATED, OnBoostActivated);
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
using ShipCoreFramework; // After copying ApiData.cs to your project

namespace ExampleMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class ExampleModSession : MySessionComponentBase
    {
        private Dictionary<string, Delegate> _scfApi;
        private bool _apiReady = false;

        public override void LoadData()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiConstants.API_ID, OnApiReceived);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiConstants.API_ID, OnApiReceived);
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

            var getLimits = _scfApi["GetBlockLimitsStatus"] as Func<IMyCubeGrid, Dictionary<string, LimitStatusData>>;
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

            var getCore = _scfApi["GetGridCore"] as Func<IMyCubeGrid, ShipCoreData>;
            if (getCore == null) return "Unknown";

            var core = getCore(grid);
            return core.UniqueName ?? "No Core";
        }
    }
}
```

## Important Notes

1. **Copy ApiData.cs**: Always copy `ApiData.cs` from the Ship Core Framework to your mod project. This file contains all data structures used by the API.

2. **API Availability**: The API is broadcast during `LoadData()`, so it should be available by the time your mod's `BeforeStart()` is called. Always check if the API is null before using it.

3. **Thread Safety**: All API methods include proper error handling and thread safety. However, be aware that grid groups can change at any time due to pistons, rotors, connectors, etc.

4. **Null Checks**: Always check if the returned delegates are null before calling them, as the API dictionary might not contain all expected methods if the version differs.

5. **NoCore Config**: Grids without a Ship Core beacon will use the "NoCore" configuration. You can check which config is active using `GetGridCore()` or `GetNoCoreConfig()`.

6. **Version Compatibility**: This API is designed for Ship Core Framework v3. Future versions may add or change methods, so always include null checks for delegates.

## Preventing Drift

The API prevents drift between what the framework considers a grid's core and what your mod thinks it is. **Always use `GetGridCore()` to determine a grid's active core** instead of trying to find core beacons manually.

Example:
```csharp
// ❌ DON'T DO THIS - can cause drift
var beacons = new List<IMyBeacon>();
grid.GetBlocksOfType(beacons, b => b.BlockDefinition.SubtypeId.Contains("Core"));

// ✅ DO THIS - always matches the framework's state
var getCore = _scfApi["GetGridCore"] as Func<IMyCubeGrid, ShipCoreData>;
var actualCore = getCore(grid);
```

## Support

For questions or issues with the API, please contact:
- Blues-Hailfire
- OwendB (OB / ODB-Tech)

Or open an issue in the mod's development repository.