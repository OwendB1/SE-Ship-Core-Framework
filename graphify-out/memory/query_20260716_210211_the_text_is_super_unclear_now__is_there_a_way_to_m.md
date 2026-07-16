---
type: "query"
date: "2026-07-16T21:02:11.920934+00:00"
question: "The text is super unclear now. Is there a way to make it sharper? I also think that 1/3 render scale is a bit too much"
contributor: "graphify"
outcome: "useful"
source_nodes: ["CoreTypeLCDScript", "TextUtils"]
---

# Q: The text is super unclear now. Is there a way to make it sharper? I also think that 1/3 render scale is a bit too much

## Answer

Expanded from original query via vocab: [lcd, text, sprite, scale, render, physical, core, script]. CoreTypeLCDScript renders into the LCD fixed 512x512 texture, so one-third scale reduces glyph raster detail. Raised MinPhysicalRenderScale to 0.5, making the 3x3 use a 1024x1024 virtual canvas at half scale. This retains layout scaling while increasing rendered text size and sharpness.

## Outcome

- Signal: useful

## Source Nodes

- CoreTypeLCDScript
- TextUtils