---
type: "query"
date: "2026-07-29T22:44:48.459600+00:00"
question: "Verify prior merge analysis against ShipCore and implement only verified fixes"
contributor: "graphify"
outcome: "useful"
source_nodes: ["GroupComponent", "GridComponent"]
---

# Q: Verify prior merge analysis against ShipCore and implement only verified fixes

## Answer

Expanded from original query via vocab: [grid, group, block, component, core, event, lifecycle, limit, limits, physical, rebuild, state]. Direct source verification found ShipCore enforces block and hard limits inside per-block transfer callbacks while MyCubeGrid.IsBlockTrasferInProgress is true, and admin Ignore PCU bypass returned before core tracking. Fixed local transfer punishment deferral, next-tick core and limit reconciliation, stale core pruning, and admin bypass tracking. Skipped speculative merge-block projected-limit interceptor because codebase has no exact projected evaluator.

## Outcome

- Signal: useful

## Source Nodes

- GroupComponent
- GridComponent