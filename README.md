# SE Ship Core Framework
This mod serves as an alternative to the standard block limit system in Space Engineers and some of the plugins for block limiting available through Torch. This mod is open to contribution but not distribution or uploading on the steam workshop or mod.io. Its exclusive rights belong to Blues-Hailfire and [ODB-Tech](odb-tech.com).

## GitHub Pages XML Configurator
A static configurator lives under `docs/configurator` and is deployable through GitHub Pages.

### What it does
- Always loads the bundled latest `ModConfig.XmlModels.cs` snapshot shipped with this repository.
- Lets you build reusable `BlockGroup` definitions once and reference them from multiple core `BlockLimits`.
- Supports renovating existing XML by uploading prior:
  - `ShipCoreConfig_Groups.xml`
  - `ShipCoreConfig_Manifest.xml` (read for listed references)
  - one or more `ShipCore` XML files
- Unknown/extra tags are ignored so older or custom files can still be partially loaded.
- Generates downloadable XML outputs for:
  - `ShipCoreConfig_Groups.xml`
  - `ShipCoreConfig_Manifest.xml`
  - individual `ShipCore` XML files (as a bundled text download)

### Local preview
```bash
python3 -m http.server 8080 --directory docs
```
Then open `http://localhost:8080/configurator/`.

## License

This project is licensed under the GNU General Public License v3.0 (GPL-3.0-or-later).
If you distribute modified versions, you must also provide the corresponding source code
under the same GPL terms.
