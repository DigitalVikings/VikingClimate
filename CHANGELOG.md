# Changelog

All notable changes to **VikingClimate** will be documented in this file.

## [0.3.0] - 2026-11-03

### Added
- **Convective Proximity Heat Propagation**: Introduced a convective heat propagation model alongside raycasting with independent per-source wattage and range settings.
- **Instant Graph Thermal Equilibrium**: Debug and optimization solver that accelerates connected structures to thermal equilibrium without frame drops.
- **Piece Insulation Visualizer**: Real-time color-coded visualizer highlighting structural insulation and thermal shelter quality.
- **Crystal Thermal Conductivity**: Crystal walls and windows now conduct heat, absorb fire warmth, and contribute to indoor structural climate envelopes.

### Changed
- **Sectioned Configuration Architecture**: Restructured all settings into sequential, numbered sections with automated legacy setting migration.
- **Non-Structural Occlusion Bypass**: Furniture, crafting tables, and decorations no longer block convective heat rays inside buildings.
- **Thermal Inspection HUD**: Added high-contrast dark backdrop panel behind inspection text for crystal-clear readability in blizzards.

## [0.2.2] - 2026-10-29

### Added
- **Steady-State Thermodynamic Equilibrium**: Instant equilibrium solver analytically calculates cluster heat balance and smoothly converges spatial conduction gradients without frame spikes.
- **Crystal & Glass Thermal Simulation**: Crystal walls and windows are fully registered as heatable structural pieces with configured thermal mass, conductivity, air loss, and absorption.
- **Decoupled Heat Propagation Engines**: Raycasting and convection now operate independently or simultaneously via separate switches (`[09 - Heat Raycasting]` and `[10 - Heat Convection]`).
- **Granular Heat Source Tuning**: Individual configuration options for total wattage, per-piece wattage caps, and reach radii across campfires, hearths, braziers, bonfires, industrial smelters, and torches.
- **Dwelling & Thermal HUD Inspector**: Hotkey toggle between single piece and connected dwelling views, with radiant heat source absorption and energy flux tallies.
- **Piece Insulation Visualizer**: Live debug toggle highlighting nearby building pieces with color-coded shaders based on thermal insulation percentage.

### Changed
- **Equilibrium Relaxation Performance**: Replaced multi-pass convergence with single-pass accelerated relaxation and gentle 15% cluster bias, eliminating periodic stutter.
- **Convective Heat Propagation Model**: Proximity heat distribution engine with distance-weighted power budgeting, solving small hut overheating and large hall freeze.
- **Metabolic Reversion Rate**: Increased default recovery speed multiplier from 10x to 25x while resting or sleeping.
- **Configuration Hierarchy**: Reorganized settings into clean, sequentially numbered categories (01â€“14) with lossless automatic migration of existing custom configurations.
- **Thermal Inspection HUD Styling**: Added high-contrast semi-transparent dark background panels with clean border framing for legibility against bright snow.

### Fixed
- **Fireplace NullReference Crash**: Hardened heat area iteration and state queries against uninitialized network views during sector load and unload.
- **Transparent Heat Propagation**: Non-heatable pieces (workbenches, crafting stations, chests, furniture) no longer block convective heat rays or cast artificial cold shadows.
- **Hearth Alcove Heat Occlusion**: Graph-connected building pieces (hearth lips, framing beams, alcove walls) no longer block convective heat transfer to the rest of the dwelling.
- **Building Thermal Retention**: Preserved building heat across portals and zone unloads with direct ZDO persistence and offline thermal retention.
- **Dwelling Envelope Calculations**: Corrected structural cover detection and ambient differential air loss reporting in connected dwelling graphs.
- **Fire Output Stability**: Expanded heat source discovery radius to 64m and stabilized active fire state against transient smoke accumulation blips.

## [0.2.1] - 2026-10-27

### Added
- **Dynamic Screen Heat Vignette**: Subtle visual heat distortion and warmth overlay for intense thermal environments.
- **HUD Thermometer Plaque**: Wood and copper mounting frame anchoring the skeuomorphic thermometer HUD.

### Changed
- **Thermal Sensor Calibration**: Smoothed ambient temperature transitions and cold breath particle cadence.

## [0.2.0] - 2026-10-25

### Added
- **Skeuomorphic Mercurial Thermometer HUD**: Weathered bog-oak backing, blown glass capillary tube, and fluid mercury level indicator with critical temperature flashing alerts.
- **Directional Thermal Radiation**: Raycast-based radiant warmth occlusion from campfires, hearths, and smelters.
- **Progressive Thermal Exposure**: Seamless 9-tier temperature model with gear insulation and accelerated resting recovery.

## [0.1.0] - 2026-10-25

### Added
- **Thermodynamic Climate Engine**: Dynamic microclimates driven by altitude gradients, sun angle, weather patterns, and wind chill.
- **Directional Thermal Radiation**: Campfires, hearths, and smelters project realistic radiant warmth with raycast shadow occlusion behind walls and obstacles.
- **Skeuomorphic Mercurial Thermometer**: Handcrafted HUD temperature instrument with fluid mercury display, comfort indicator, and critical temperature flash warnings.
- **Progressive Thermal Afflictions**: Distinct 9-tier exposure progression from soothing warmth up to dangerous hypothermia and heat exhaustion.
- **Gear Insulation & Resting Equilibrium**: Armor, fur cloaks, and sheltered dwellings shield against extreme elements and accelerate body temperature recovery while resting.
