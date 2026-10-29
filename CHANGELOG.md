# Changelog

All notable changes to **VikingClimate** will be documented in this file.

## [0.2.2] - 2026-10-29

### Added
- **Dwelling Graph & Thermal Inspector**: Hotkey toggle (`F8`) between single piece and connected dwelling graph views with signed Fire, Joint, and Air energy flux tallies.
- **Heat Source Absorption HUD**: Detailed breakdown of radiant energy into structures, ground, and air with lockable piece temperature gradients.
- **Polynomial Shelter Air Loss**: Configurable exponent (`ShelterAirLossAlpha`) tuning how enclosure curves reduce convective atmospheric heat dissipation.

### Fixed
- **Building Thermal Retention**: Preserved building heat across portals and zone unloads with direct ZDO persistence and offline thermal retention.
- **Dwelling Air Loss Arithmetic**: Corrected connected graph air loss reporting to compute against ambient differential rather than absolute Celsius.
- **Dwelling Envelope Cover Detection**: Initialized cover and roof state for structural nodes upon registration, correctly classifying roofs as overhead protection.
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
