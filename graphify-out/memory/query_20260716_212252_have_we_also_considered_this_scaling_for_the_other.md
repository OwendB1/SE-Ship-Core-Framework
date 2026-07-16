---
type: "query"
date: "2026-07-16T21:22:52.325443+00:00"
question: "Have we also considered this scaling for the other LCD sizes? It should also be done there."
contributor: "graphify"
outcome: "useful"
source_nodes: ["Session", "CoreTypeLCDScript"]
---

# Q: Have we also considered this scaling for the other LCD sizes? It should also be done there.

## Answer

Expanded from original query via vocab: [lcd, text, definition, render, scale, session, load, block, script]. CoreTypeLCDScript already derives layout scale for every standalone LCD from ScreenArea metadata. Generalized Session high-resolution definition handling from the LargeLCDPanel3x3 subtype to every large-grid MyTextPanelDefinition spanning at least three block cells. Keen definitions selected are LargeLCDPanel3x3, LargeLCDPanel5x3, and LargeLCDPanel5x5; modded large multi-cell LCDs are included. Original per-definition and per-screen resolutions are retained and restored on unload.

## Outcome

- Signal: useful

## Source Nodes

- Session
- CoreTypeLCDScript