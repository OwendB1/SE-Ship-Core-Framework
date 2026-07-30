---
type: "query"
date: "2026-07-29T23:59:53.185600+00:00"
question: "Allow BlockGroups inside BlockLimits to define a comma-separated Directions attribute using DirectionType names or integers, overriding the limit-wide AllowedDirections."
contributor: "graphify"
outcome: "useful"
source_nodes: ["BlockLimit", "DirectionType", "GridComponent", "GroupComponent", "LimitEvaluation"]
---

# Q: Allow BlockGroups inside BlockLimits to define a comma-separated Directions attribute using DirectionType names or integers, overriding the limit-wide AllowedDirections.

## Answer

Expanded query: block, groups, limit, limits, direction, directions, allowed, primary, resolve, config, xml, override. Implemented BlockGroupReference as XML text plus optional Directions attribute. Config normalization parses case-insensitive enum names and defined integer values. BlockLimit.GetAllowedDirections resolves the matched included group override first, then falls back to limit-wide AllowedDirections. Grid placement, existing-block punishment, and client limit evaluation all use this shared precedence. Configurator and README support round-trip editing. Build and all contract tests pass.

## Outcome

- Signal: useful

## Source Nodes

- BlockLimit
- DirectionType
- GridComponent
- GroupComponent
- LimitEvaluation