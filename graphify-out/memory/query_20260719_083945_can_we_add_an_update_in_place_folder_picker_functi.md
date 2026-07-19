---
type: "query"
date: "2026-07-19T08:39:45.821064+00:00"
question: "Can we add an update in place folder picker function to the XML web config? We do the same for the viewer /home/owendb/Documents/GitHub/viewer/"
contributor: "graphify"
outcome: "useful"
source_nodes: ["app.js", "download"]
---

# Q: Can we add an update in place folder picker function to the XML web config? We do the same for the viewer /home/owendb/Documents/GitHub/viewer/

## Answer

Expanded from graph vocabulary via [directory, download, files, folder, generated, handle, save, upload, write, xml]. The graph identified app.js generation and download paths. The viewer reference established the File System Access API pattern. Implemented a readwrite directory picker that accepts a mod root or Data folder, loads its XML/SBC files, retains the handle for the page session, and writes the shared generated XML entry set in place. Ordinary uploads clear the writable handle, unsupported browsers retain upload/download fallback, and generated paths are validated against folder escape. Added Node tests for path mapping, traversal rejection, and writes.

## Outcome

- Signal: useful

## Source Nodes

- app.js
- download