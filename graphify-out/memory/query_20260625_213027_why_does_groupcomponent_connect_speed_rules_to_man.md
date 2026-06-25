---
type: "query"
date: "2026-06-25T21:30:27.460509+00:00"
question: "Why does GroupComponent connect Speed Rules to many runtime rule communities?"
contributor: "graphify"
outcome: "useful"
source_nodes: ["GroupComponent", "SpeedEnforcement", "NoFlyZoneEnforcement", "GridComponent"]
---

# Q: Why does GroupComponent connect Speed Rules to many runtime rule communities?

## Answer

Expanded from original query via graph vocab: [group, component, speed, rules, runtime, boost, defense, limit, limits, friction, core, grid]. Answer: GroupComponent is the per-ship-group runtime coordinator. Its partials own punishment gates, main-core selection, limit state, speed cache invalidation, boost/defense timers, grid membership effects, and helper calls into speed/no-fly/limit systems. Speed rules depend on those same state transitions: punishment flags toggle PunishSpeed and invalidate speed state, boost changes BoostEnabled and invalidates speed cache, block add/remove and limit snapshots mark speed clusters dirty, SpeedEnforcement consumes GroupComponent as its context, and NoFlyZoneEnforcement uses GroupComponent state plus ShipCore.BlockLimits to apply punishments. So the graph bridge is real: GroupComponent is where runtime rules converge before speed enforcement runs.

## Outcome

- Signal: useful

## Source Nodes

- GroupComponent
- SpeedEnforcement
- NoFlyZoneEnforcement
- GridComponent