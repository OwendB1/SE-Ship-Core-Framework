---
type: "query"
date: "2026-07-11T10:51:16.862781+00:00"
question: "Can we make it so connector blacklist checks through all connected connectors by diffing the mechanically linked group against the logically linked group and then based off that decide the priority across multiple gridgroups? Come up with a plan for this. Use $ponytail and other relevant skills."
contributor: "graphify"
outcome: "useful"
source_nodes: ["GroupComponent.Connectors.cs", "GroupComponent.CoreSelection.cs", "GroupComponent"]
---

# Q: Can we make it so connector blacklist checks through all connected connectors by diffing the mechanically linked group against the logically linked group and then based off that decide the priority across multiple gridgroups? Come up with a plan for this. Use $ponytail and other relevant skills.

## Answer

Expanded from original query via vocab: [connector, connectors, blacklist, blacklisting, mechanical, logical, group, priority, selection]. Plan: retain mechanical GroupComponent nodes; discover the transitive network by BFS over actual connected IMyShipConnector pairs; make _connectedCoreGroups transitive while retaining _connectedNoCoreGroups as direct-only so CrossConnectorPunishment semantics do not change; for each target reuse DoesCoreGroupOutrankForConnectorBlacklist and CompareCoreGroupsForSelection to select the strongest reachable group that both blacklists and strictly outranks it; refresh every affected connector component after a one-frame deferred topology change. Logical-minus-mechanical may be diagnostic/optimization only because MyShipConnector omits Logical links when either connector is trading-enabled. Exact priority/block-count ties remain unpunished.

## Outcome

- Signal: useful

## Source Nodes

- GroupComponent.Connectors.cs
- GroupComponent.CoreSelection.cs
- GroupComponent