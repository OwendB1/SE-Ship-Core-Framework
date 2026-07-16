---
type: "query"
date: "2026-07-16T21:17:09.799058+00:00"
question: "Add proper true extra sharpness"
contributor: "graphify"
outcome: "useful"
source_nodes: ["Session", "CoreTypeLCDScript"]
---

# Q: Add proper true extra sharpness

## Answer

Expanded from original query via vocab: [lcd, text, definition, render, scale, session, load, script]. Session.LoadData now raises LargeLCDPanel3x3 and all of its ScreenArea TextureResolution values from 512 to at least 1024 on rendering clients before block entities initialize. Session unload restores prior definition values. CoreTypeLCDScript compensates for the 2x resolution so the 3x3 keeps a 1024x1024 logical canvas and current physical UI size while receiving 4x raster pixels.

## Outcome

- Signal: useful

## Source Nodes

- Session
- CoreTypeLCDScript