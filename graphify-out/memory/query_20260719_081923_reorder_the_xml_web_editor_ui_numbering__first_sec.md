---
type: "query"
date: "2026-07-19T08:19:23.913189+00:00"
question: "Reorder the XML web editor UI numbering. First section number starts at 2 instead of 1"
contributor: "graphify"
outcome: "useful"
source_nodes: ["app.js"]
---

# Q: Reorder the XML web editor UI numbering. First section number starts at 2 instead of 1

## Answer

Expanded from original query via graph vocab: [xml, editor, editors, number]. The graph located the configurator under docs/configurator; source inspection showed the seven section headings were hard-coded in index.html as 2 through 8. Renumbered them sequentially to 1 through 7 and verified the diff with git diff --check.

## Outcome

- Signal: useful

## Source Nodes

- app.js