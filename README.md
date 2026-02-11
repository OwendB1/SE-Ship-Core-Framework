# SE Ship Core Framework
This mod serves as an alternative to the standard block limit system in Space Engineers and some of the plugins for block limiting available through Torch. This mod is open to contribution but not distribution or uploading on the steam workshop or mod.io. Its exclusive rights belong to Blues-Hailfire and [ODB-Tech](odb-tech.com).

## GitHub Pages XML Configurator
A static configurator now lives under `docs/configurator` and is deployable through GitHub Pages.

### What it does
- Loads and parses `ModConfig.cs` (bundled copy or uploaded file) to expose XML-annotated classes.
- Lets you build reusable `BlockGroup` definitions once and reference them from multiple core `BlockLimits`.
- Generates downloadable XML outputs for:
  - `ShipCoreConfig_Groups.xml`
  - `ShipCoreConfig_Manifest.xml`
  - individual `ShipCore` XML files (as a bundled text download)

### Local preview
```bash
python3 -m http.server 8080 --directory docs
```
Then open `http://localhost:8080/configurator/`.
