---
type: "query"
date: "2026-07-16T20:58:49.685689+00:00"
question: "Still not scaling"
contributor: "graphify"
outcome: "corrected"
correction: "Use the LCD ScreenArea definition as the authoritative scaling source; geometry is only a fallback."
source_nodes: ["CoreTypeLCDScript"]
---

# Q: Still not scaling

## Answer

Expanded from original query via vocab: [lcd, scale, render, physical, cube, block, core, script]. CoreTypeLCDScript kept scale 1 when TryGetSurfaceLayout failed or model geometry reported normalized dimensions. The corrected path reads MyFunctionalBlockDefinition.ScreenAreas for standalone LCDs and derives render scale from TextureResolution, ScreenWidth, and ScreenHeight before geometry fallback. LargeLCDPanel3x3 therefore maps a 1536x1536 virtual layout to its 512x512 surface at scale 1/3.

## Outcome

- Signal: corrected
- Correction: Use the LCD ScreenArea definition as the authoritative scaling source; geometry is only a fallback.

## Source Nodes

- CoreTypeLCDScript