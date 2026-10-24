# Changelog

## [0.1.1] - 2026-10-24
### Fixed
- Fixed dungeons (Burial Chambers, Troll Caves, Sunken Crypts, Frost Caves, Infested Mines) experiencing extreme `-120°C` altitude cold due to Valheim spawning dungeon interiors at `y = 5000m`. Dungeons now evaluate a subterranean ground temperature model anchored to true surface terrain elevation, isolated from surface wind, precipitation, and diurnal swings.
- Fixed indoor dwelling temperature and Hugin's Compendium telemetry persisting after stepping outside. Exiting shelter now immediately reverts to the outdoor ambient and campfire/torch radiance formula with zero latency.
- Fixed `RestedRites` duration scaling compatibility by modifying `SE_Cozy` activation delay in-place instead of intercepting `SEMan.AddStatusEffect`.
- Disabled the thermal inspection overlay camera by default unless debug mode is explicitly enabled.

## [0.1.0] - 2026-10-22
### Added
- Initial release of VikingClimate: thermodynamic heat conduction, 64-ray Fibonacci structural enclosure and thermal mass simulation, 17-point Gaussian biome blending, gear cold insulation, and progressive hypothermia/hyperthermia status tiers.
