---
type: "query"
date: "2026-07-17T06:52:34.684880+00:00"
question: "We got a crash now on multiplayer: NullReferenceException at Session.ApplyHighResolutionLcdDefinitions during LoadData"
contributor: "graphify"
outcome: "corrected"
correction: "Do not enumerate generic definition subtype collections during multiplayer LoadData; use direct cube-definition ID lookups and fail closed."
source_nodes: ["Session"]
---

# Q: We got a crash now on multiplayer: NullReferenceException at Session.ApplyHighResolutionLcdDefinitions during LoadData

## Answer

Expanded from original query via vocab: [lcd, definition, session, load, client, multiplayer, render, scale, block]. The generalized GetAllDefinitions<MyTextPanelDefinition>() path dereferences an internal subtype collection that is not guaranteed initialized during multiplayer client LoadData. Replaced it with direct TryGetCubeBlockDefinition lookups through the populated ID dictionary for LargeLCDPanel3x3, LargeLCDPanel5x3, and LargeLCDPanel5x5. Moved setup after config loading, isolated each optional definition lookup, and wrapped cosmetic high-resolution setup so it can never abort session loading.

## Outcome

- Signal: corrected
- Correction: Do not enumerate generic definition subtype collections during multiplayer LoadData; use direct cube-definition ID lookups and fail closed.

## Source Nodes

- Session