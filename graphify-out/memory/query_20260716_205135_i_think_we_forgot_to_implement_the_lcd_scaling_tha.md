---
type: "query"
date: "2026-07-16T20:51:35.341308+00:00"
question: "I think we forgot to implement the LCD scaling that we were planning to add. The 3x3 massively zooms in because the relative scale of the layout/pixels is not kept."
contributor: "graphify"
outcome: "useful"
source_nodes: ["CoreTypeLCDScript", ".Draw()"]
---

# Q: I think we forgot to implement the LCD scaling that we were planning to add. The 3x3 massively zooms in because the relative scale of the layout/pixels is not kept.

## Answer

Expanded from graph vocab: [lcd, scale, render, draw, sprite, position, script, core, type]. CoreTypeLCDScript applied physical RenderScale only to font-scale-derived constants, leaving its canvas, width-derived layout, clipping, scrolling, and cursor in 512-space. Fixed PanelGroupLayout to apply RenderScale to the full virtual coordinate system and based reference density on the texture's shortest dimension. A 3x3 512 surface now renders a 1536 virtual canvas at 0.333 scale.

## Outcome

- Signal: useful

## Source Nodes

- CoreTypeLCDScript
- .Draw()