---
type: "query"
date: "2026-07-11T10:08:09.104372+00:00"
question: "We have a core activation bug and blacklist issue by the looks of it. Describe what you think that the issue is based on the player report and codebase analysis: Basically, you build a structure all as one grid, attach the connectors to eachother and then grind the 2 blocks in between. The beacon shows no core but the panel shows not punished and the limited blocks still work. By the sound of it the split into multiple gridgroups does not happen correctly."
contributor: "graphify"
outcome: "useful"
source_nodes: ["GroupComponent", "RebuildConnectorPunishmentLinks", "MyCubeGridOnBlocksChangeFinishedGlobally", "RefreshLimitedBlockPunishmentState"]
---

# Q: We have a core activation bug and blacklist issue by the looks of it. Describe what you think that the issue is based on the player report and codebase analysis: Basically, you build a structure all as one grid, attach the connectors to eachother and then grind the 2 blocks in between. The beacon shows no core but the panel shows not punished and the limited blocks still work. By the sound of it the split into multiple gridgroups does not happen correctly.

## Answer

Expanded from graph vocabulary: group, grid, physical, connector, blacklist, removal, rebuild, beacon, punishment, limited, blocks. Diagnosis: the split likely succeeds because the detached side resolves to a no-core mechanical GroupComponent. The race is in immediate split reconciliation. Space Engineers raises OnBlocksChangeFinishedGlobally during MoveBlocks before the new split grid is added to scene and receives Mechanical group nodes. Session.Events immediately queries both grids, usually finds only the original group, and rebuilds it. Its connector scan cannot resolve the not-yet-created peer GroupComponent and stores no link. The new no-core group is created later, but group creation never refreshes connected peer groups; connector status never changed, so no connector event heals the stale core-side cache. CrossConnectorPunishment then omits external blocks. Manifest blacklist is separately core-to-core only because TryGetConnectedBlacklistingGroup rejects no-core groups. The panel Limited Blocks flag does not represent CrossConnectorPunishment overflow, so No is expected there and is misleading evidence.

## Outcome

- Signal: useful

## Source Nodes

- GroupComponent
- RebuildConnectorPunishmentLinks
- MyCubeGridOnBlocksChangeFinishedGlobally
- RefreshLimitedBlockPunishmentState