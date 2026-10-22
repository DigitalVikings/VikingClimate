# VikingClimate

VikingClimate replaces Valheim's binary environmental triggers with a physically grounded thermodynamic simulation. Internal body warmth, 16-point microclimatic ambient blending, convective wind chill, evaporative wetness, and gear insulation govern player exposure across all biomes, accompanied by non-intrusive sensory immersion and HUD telemetry.

![Thermometer HUD](https://raw.githubusercontent.com/DigitalVikings/VikingClimate/main/thermometer_hud.png)
![Thermal Piece Inspection](https://raw.githubusercontent.com/DigitalVikings/VikingClimate/main/thermal_inspection_debug.png)

> ⚠️ **Work in Progress (Pre-Release v0.1.0)**: This mod is in active development and has not been tested extensively. Features, balance, and mechanics may change, and unintended bugs or edge cases may occur. Backup your character and world saves before testing.

> ⚡ **Powered by VikingLib**: This mod requires [VikingLib](https://valheim.thunderstore.io/package/DigitalVikings/VikingLib/). 
> VikingLib replaces Valheim's native combat calculation and stat engines. Not all mods may be compatible / work as expected with this.

---

## How It Works

Your character maintains an internal body warmth (comfort equilibrium at 22°C) that continually conducts towards the ambient "feels-like" temperature of your surroundings:

- **Ambient Microclimates**: Local temperature blends dynamically from seven environmental inputs: 16-point spatial biome sampling, solar elevation, elevation lapse rate (-1°C per 35m), precipitation, wind chill, and raycast-occluded fire radiance. Handheld torches radiate personal warmth (+3°C).
- **Insulation & Exposure**: Cold gear armor (wolf, lox, fenris, padded) mitigates environmental heat loss by up to 90%. Wet clothing, cold wind, and swimming accelerate heat loss. Sheltered resting and sleeping stabilize body warmth rapidly.
- **Sensory Immersion**: Cold breath vapor puffs emit from your character in freezing environments (scaling with exertion), while a subtle frosted screen vignette outlines severe hypothermia without obscuring your crosshair.
- **HUD Telemetry**: A stylized vertical thermometer column attaches beside your health bar, displaying live body temperature, rate-of-change arrows, and the local feels-like ambient target.

<details>
<summary><b>Click to expand Thermal Conduction Modifiers</b></summary>

| Condition | Rate Multiplier | Mechanical Effect |
| :--- | :--- | :--- |
| **Base Cooling Rate** | $0.005\text{ /s}$ | Baseline conduction rate towards ambient temperature. |
| **Wetness (`Wet`)** | $1.8\times$ | Increases thermal conduction by +80% and applies -5% movement speed via waterlogged gear. |
| **Swimming** | $3.0\times$ | Submersion in water triples conductive heat loss. |
| **Wind Exposure** | Up to $2.2\times$ | Open wind accelerates heat loss based on live wind speed (fully blocked by shelter). |
| **Cold Gear Insulation** | Down to $0.10\times$ | Insulated armor pieces mitigate cold heat loss by up to 90%. Inactive when warming up. |
| **Resting / Sleeping** | $10.0\times$ | Sitting sheltered near fire triggers rapid metabolic stabilization toward 22°C. |

</details>

<details>
<summary><b>Click to expand Thermal Consequences (8-Tier Progressive System)</b></summary>

Native binary Cold and Freezing effects are replaced with 8 progressive stages scaling through VikingLib:

### Cold Progression (Hypothermia)
| Stage | Threshold | Mechanics & Modifiers |
| :--- | :--- | :--- |
| **Comfort** | 18°C to 26°C | Baseline physiological equilibrium. No penalties. |
| **Chilly** | 8°C to 18°C | Stamina Regen -15%, All Damage × 0.90. |
| **Bitter Cold** | 0°C to 8°C | Stamina Regen -30%, All Damage × 0.80. |
| **Numbing Frost** | -12°C to 0°C | Stamina Regen -45%, All Damage × 0.65, Attack/Tool Speed × 0.85. |
| **Hypothermia** | < -12°C | Stamina Regen -60%, All Damage × 0.50, Attack/Tool Speed × 0.70, periodic frost damage. |

### Heat Progression (Hyperthermia)
| Stage | Threshold | Mechanics & Modifiers |
| :--- | :--- | :--- |
| **Warm** | 26°C to 35°C | Mild heat. Stamina Regen -10%. |
| **Heat Exhaustion** | 35°C to 45°C | Stamina Regen -25%, All Damage × 0.85, Run Stamina Drain +20%. |
| **Severe Hyperthermia** | 45°C to 55°C | Stamina Regen -40%, All Damage × 0.70, Run Stamina Drain +40%, Jump Stamina +30%. |
| **Catastrophic Scorching** | > 55°C | Severe debuffs, Run Stamina Drain +60%, periodic fire damage. |

</details>
