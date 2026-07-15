---
type: "query"
date: "2026-07-15T16:00:40.508340+00:00"
question: "I see that SCF mod API is currently lacking the ability for other mods to request the full config through the mod API. Could you add that?"
contributor: "graphify"
outcome: "useful"
source_nodes: ["ModAPI", "ModConfigData", "ShipCoreFrameworkClient"]
---

# Q: I see that SCF mod API is currently lacking the ability for other mods to request the full config through the mod API. Could you add that?

## Answer

Expanded from original query via graph vocab: [api, config, mod, request, message, client, server, handler, send, receive, serialize, data]. ModAPI already had ModConfigData and ConvertToModConfigData for ConfigReceived; reused them for GetFullConfig_Binary method ID 40 and ShipCoreFrameworkClient.GetFullConfig(), then completed DTO coverage for current config fields.

## Outcome

- Signal: useful

## Source Nodes

- ModAPI
- ModConfigData
- ShipCoreFrameworkClient