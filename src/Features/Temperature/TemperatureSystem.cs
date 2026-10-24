using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VikingLib.StatusEffects;
using VikingLib.UI;
using VikingClimate.Configuration;
using VikingClimate.Core;

namespace VikingClimate.Features.Temperature
{
    /// <summary>
    /// Coordinates environmental feels-like assessment, directional conduction,
    /// warm-blooded metabolic recovery, physiological status tier transitions, and glossary formatting.
    /// </summary>
    public static class TemperatureSystem
    {
        private static string? _currentActiveStatusId = null;

        public static void Init()
        {
            SurvivalManager.OnSurvivalTick += HandleSurvivalTick;
            ThermalStatusEffects.Init();

            // Hook into VikingLib's Character Stats compendium tab
            CharacterStatsGlossaryPanel.OnFormatCustomSections += FormatGlossarySection;
        }

        private static void HandleSurvivalTick(Player player, SurvivalState state, float dt)
        {
            if (!ModConfig.ModEnabled.Value || player == null || player != Player.m_localPlayer) return;

            // 1. Calculate Ambient Feels-Like Temperature and breakdown
            float targetAmbient = AmbientTemperatureCalculator.Calculate(player, dt, out var breakdown);
            state.AmbientTemperature = targetAmbient;
            state.Breakdown = breakdown;
            state.WindChill = breakdown.WindChillDelta;
            state.FireWarmth = breakdown.FireWarmthDelta;
            state.IsInShelter = breakdown.IsSheltered;
            state.IsNearFire = breakdown.FireWarmthDelta > 0.01f;

            // 2. Active Sleeping in Bed: Equate body temperature directly to bed ambient
            if (player.IsSleeping())
            {
                state.AmbientTemperature = targetAmbient;
                state.BodyWarmth = targetAmbient;
                state.TemperatureVelocity = 0.0f;
                state.ConductionRate = 0.0f;
                state.IsResting = true;
                state.IsReverting = false;
                UpdateThermalStatusTier(player, targetAmbient, dt);
                return;
            }

            // 3. Calculate Gear Cold Insulation Rating
            float gearInsulation = GearInsulationRegistry.CalculatePlayerInsulation(player, out bool hasFrostMead);
            state.InsulationRating = gearInsulation;

            // 4. Principled Non-Linear Differential Conduction: dT/dt = Rate * sgn(T_amb - T) * |T_amb - T|^POW
            float ambientFeelsLike = state.AmbientTemperature;
            float currentWarmth = state.BodyWarmth;
            bool isWet = player.GetSEMan() != null && player.GetSEMan().HaveStatusEffect("Wet".GetStableHashCode());
            bool isSwimming = player.IsSwimming();

            float comfortMin = ModConfig.ComfortMinTemp.Value; // default 18°C
            float comfortMax = ModConfig.ComfortMaxTemp.Value; // default 26°C
            float comfortMid = (comfortMin + comfortMax) * 0.5f; // 22°C

            // A. Base Conduction Rate
            float baseRate = ModConfig.BaseCoolingRate.Value;

            // B. Wetness Multiplier
            float wetMult = 1.0f;
            if (isSwimming) wetMult = ModConfig.SwimCoolingMultiplier.Value;
            else if (isWet) wetMult = ModConfig.WetCoolingMultiplier.Value;

            // C. Wind Speed & Shelter Multiplier
            float windMult = 1.0f;
            if (!state.IsInShelter && !state.Breakdown.IsSubterranean && EnvMan.instance != null)
            {
                windMult = 1.0f + (1.2f * EnvMan.instance.GetWindIntensity());
            }

            // D. Gear Cold Insulation Multiplier (ONLY active when cooling down)
            float insulationMult = 1.0f;
            if (ambientFeelsLike < currentWarmth)
            {
                float effectiveInsulation = gearInsulation;
                if (hasFrostMead) effectiveInsulation = Mathf.Min(0.95f, effectiveInsulation + 0.80f);
                insulationMult = Mathf.Max(0.05f, 1.0f - effectiveInsulation);
            }

            // E. Metabolic Stabilization Multiplier (Fast reversion ONLY when resting; never in the wild)
            bool isResting = (player.GetSEMan() != null && player.GetSEMan().HaveStatusEffect("Resting".GetStableHashCode())) || player.IsSleeping();
            bool movingTowardsComfort = Mathf.Abs(ambientFeelsLike - comfortMid) < Mathf.Abs(currentWarmth - comfortMid);
            bool isReverting = isResting && movingTowardsComfort;
            float metabolicMult = isReverting ? ModConfig.ReversionSpeedMultiplier.Value : 1.0f;

            // F. Unit-Interval Clamped Conduction Rate
            float rawRate = baseRate * wetMult * windMult * insulationMult * metabolicMult;
            float rate = Mathf.Clamp01(rawRate);

            // G. Non-Linear Differential Step Integration
            float deltaT = ambientFeelsLike - currentWarmth;
            float absDelta = Mathf.Abs(deltaT);
            float velocity = 0.0f;

            if (absDelta < 0.05f)
            {
                // Snap to equilibrium to prevent micro-oscillations near target
                currentWarmth = ambientFeelsLike;
                velocity = 0.0f;
            }
            else
            {
                float alpha = ModConfig.ThermalGradientElasticity.Value;
                float gradient = Mathf.Approximately(alpha, 1.0f) ? absDelta : Mathf.Pow(absDelta, alpha);
                velocity = rate * gradient * Mathf.Sign(deltaT);
                float step = velocity * dt;

                // Guard against mathematical overshoot
                if (deltaT > 0f)
                {
                    currentWarmth = Mathf.Min(ambientFeelsLike, currentWarmth + step);
                }
                else
                {
                    currentWarmth = Mathf.Max(ambientFeelsLike, currentWarmth + step);
                }
            }

            state.BodyWarmth = currentWarmth;
            state.ConductionRate = rate;
            state.TemperatureVelocity = velocity;
            state.IsResting = isResting;
            state.IsReverting = isReverting;
            state.WetMultiplier = wetMult;
            state.WindMultiplier = windMult;
            state.InsulationMultiplier = insulationMult;
            state.HasFrostMead = hasFrostMead;

            // 5. Evaluate and Apply Physiological Status Tiers with Hysteresis
            UpdateThermalStatusTier(player, currentWarmth, dt);
        }

        private static void UpdateThermalStatusTier(Player player, float warmth, float dt)
        {
            string? targetStatusId = null;

            // Hysteresis buffers (prevents rapid on/off fluttering when near tier boundaries)
            if (_currentActiveStatusId == ThermalStatusEffects.HypothermiaId)
            {
                if (warmth < -11.0f) targetStatusId = ThermalStatusEffects.HypothermiaId;
                else if (warmth < 0.5f) targetStatusId = ThermalStatusEffects.NumbingFrostId;
                else if (warmth < 8.5f) targetStatusId = ThermalStatusEffects.BitterColdId;
                else if (warmth < 18.5f) targetStatusId = ThermalStatusEffects.ChillyId;
            }
            else if (_currentActiveStatusId == ThermalStatusEffects.NumbingFrostId)
            {
                if (warmth < -12.5f) targetStatusId = ThermalStatusEffects.HypothermiaId;
                else if (warmth < 0.5f) targetStatusId = ThermalStatusEffects.NumbingFrostId;
                else if (warmth < 8.5f) targetStatusId = ThermalStatusEffects.BitterColdId;
                else if (warmth < 18.5f) targetStatusId = ThermalStatusEffects.ChillyId;
            }
            else if (_currentActiveStatusId == ThermalStatusEffects.BitterColdId)
            {
                if (warmth < -12.5f) targetStatusId = ThermalStatusEffects.HypothermiaId;
                else if (warmth < -0.5f) targetStatusId = ThermalStatusEffects.NumbingFrostId;
                else if (warmth < 8.5f) targetStatusId = ThermalStatusEffects.BitterColdId;
                else if (warmth < 18.5f) targetStatusId = ThermalStatusEffects.ChillyId;
            }
            else if (_currentActiveStatusId == ThermalStatusEffects.ChillyId)
            {
                if (warmth < -12.5f) targetStatusId = ThermalStatusEffects.HypothermiaId;
                else if (warmth < -0.5f) targetStatusId = ThermalStatusEffects.NumbingFrostId;
                else if (warmth < 7.5f) targetStatusId = ThermalStatusEffects.BitterColdId;
                else if (warmth < 18.5f) targetStatusId = ThermalStatusEffects.ChillyId;
            }
            else if (_currentActiveStatusId == ThermalStatusEffects.CatastrophicScorchingId)
            {
                if (warmth > 54.0f) targetStatusId = ThermalStatusEffects.CatastrophicScorchingId;
                else if (warmth > 44.5f) targetStatusId = ThermalStatusEffects.SevereHyperthermiaId;
                else if (warmth > 34.5f) targetStatusId = ThermalStatusEffects.HeatExhaustionId;
                else if (warmth > 25.5f) targetStatusId = ThermalStatusEffects.WarmId;
            }
            else if (_currentActiveStatusId == ThermalStatusEffects.SevereHyperthermiaId)
            {
                if (warmth > 55.5f) targetStatusId = ThermalStatusEffects.CatastrophicScorchingId;
                else if (warmth > 44.5f) targetStatusId = ThermalStatusEffects.SevereHyperthermiaId;
                else if (warmth > 34.5f) targetStatusId = ThermalStatusEffects.HeatExhaustionId;
                else if (warmth > 25.5f) targetStatusId = ThermalStatusEffects.WarmId;
            }
            else if (_currentActiveStatusId == ThermalStatusEffects.HeatExhaustionId)
            {
                if (warmth > 55.5f) targetStatusId = ThermalStatusEffects.CatastrophicScorchingId;
                else if (warmth > 45.5f) targetStatusId = ThermalStatusEffects.SevereHyperthermiaId;
                else if (warmth > 34.5f) targetStatusId = ThermalStatusEffects.HeatExhaustionId;
                else if (warmth > 25.5f) targetStatusId = ThermalStatusEffects.WarmId;
            }
            else if (_currentActiveStatusId == ThermalStatusEffects.WarmId)
            {
                if (warmth > 55.5f) targetStatusId = ThermalStatusEffects.CatastrophicScorchingId;
                else if (warmth > 45.5f) targetStatusId = ThermalStatusEffects.SevereHyperthermiaId;
                else if (warmth > 35.5f) targetStatusId = ThermalStatusEffects.HeatExhaustionId;
                else if (warmth > 25.5f) targetStatusId = ThermalStatusEffects.WarmId;
            }
            else
            {
                // Baseline entering cold or heat
                if (warmth < 17.5f)
                {
                    if (warmth >= 7.5f) targetStatusId = ThermalStatusEffects.ChillyId;
                    else if (warmth >= -0.5f) targetStatusId = ThermalStatusEffects.BitterColdId;
                    else if (warmth >= -12.5f) targetStatusId = ThermalStatusEffects.NumbingFrostId;
                    else targetStatusId = ThermalStatusEffects.HypothermiaId;
                }
                else if (warmth > 26.5f)
                {
                    if (warmth <= 35.5f) targetStatusId = ThermalStatusEffects.WarmId;
                    else if (warmth <= 45.5f) targetStatusId = ThermalStatusEffects.HeatExhaustionId;
                    else if (warmth <= 55.5f) targetStatusId = ThermalStatusEffects.SevereHyperthermiaId;
                    else targetStatusId = ThermalStatusEffects.CatastrophicScorchingId;
                }
            }

            // Apply periodic ticking extreme damage if in catastrophic tiers
            if (targetStatusId == ThermalStatusEffects.HypothermiaId)
            {
                if (ModConfig.HypothermiaDamageRate.Value > 0f && player.GetHealth() > 1.0f)
                {
                    HitData hit = new HitData
                    {
                        m_damage = { m_frost = ModConfig.HypothermiaDamageRate.Value * dt },
                        m_point = player.transform.position,
                        m_dir = Vector3.down
                    };
                    player.Damage(hit);
                }
            }
            else if (targetStatusId == ThermalStatusEffects.CatastrophicScorchingId)
            {
                if (ModConfig.HyperthermiaDamageRate.Value > 0f && player.GetHealth() > 1.0f)
                {
                    HitData hit = new HitData
                    {
                        m_damage = { m_fire = ModConfig.HyperthermiaDamageRate.Value * dt },
                        m_point = player.transform.position,
                        m_dir = Vector3.down
                    };
                    player.Damage(hit);
                }
            }

            // Transition status effect if tier changed
            if (_currentActiveStatusId != targetStatusId)
            {
                // Remove previous thermal effect
                if (!string.IsNullOrEmpty(_currentActiveStatusId))
                {
                    StatusEffectRegistry.Remove(player, _currentActiveStatusId);
                }

                // Apply new thermal effect
                if (!string.IsNullOrEmpty(targetStatusId))
                {
                    StatusEffectRegistry.Apply(player, targetStatusId);
                }

                _currentActiveStatusId = targetStatusId;
            }
        }

        private static void FormatGlossarySection(Player player, StringBuilder sb)
        {
            if (player == null || player != Player.m_localPlayer) return;

            var state = SurvivalManager.CurrentState;
            var bd = state.Breakdown;

            sb.AppendLine("<color=orange><b>[ SURVIVAL & ENVIRONMENT ]</b></color>");

            // 1. Body Warmth (Primary vital state)
            string warmthColor = GetTemperatureColorHex(state.BodyWarmth);
            string conditionDesc = GetWarmthConditionDescriptor(state.BodyWarmth);
            sb.AppendLine($"• <b>Body Warmth</b>: <color={warmthColor}>{FormatTemp(state.BodyWarmth)}</color> <i>({conditionDesc})</i>");

            // 2. Heat Transfer Rate & Tendency (Accumulated rate in °C/min with contributing factors underneath)
            float velPerMin = state.TemperatureVelocity * 60.0f;
            string tendColor;
            string tendText;
            if (Mathf.Abs(velPerMin) < 0.05f || Mathf.Abs(state.AmbientTemperature - state.BodyWarmth) < 0.05f)
            {
                tendColor = "#81C784";
                tendText = "Stable at Target (0.0°C/min)";
            }
            else if (velPerMin > 0.0f)
            {
                tendColor = "#FFB74D";
                tendText = $"Warming (+{velPerMin:F1}°C/min)";
            }
            else
            {
                tendColor = "#64B5F6";
                tendText = $"Cooling ({velPerMin:F1}°C/min)";
            }

            sb.AppendLine($"• <b>Heat Transfer Rate</b>: <color={tendColor}>{tendText}</color>");

            // Rate conditions listed underneath
            string metabolicDesc;
            if (state.IsResting)
            {
                metabolicDesc = state.IsReverting
                    ? $"<color=#81C784>{ModConfig.ReversionSpeedMultiplier.Value:F1}x (Resting - Fast Reversion)</color>"
                    : "<color=#A0A0A0>1.0x (Resting - Diverging)</color>";
            }
            else
            {
                metabolicDesc = "<color=#A0A0A0>1.0x (Active / In the Wild)</color>";
            }
            sb.AppendLine($"    <color=#A0A0A0>├─ Metabolic State: {metabolicDesc}</color>");

            string wetDesc;
            if (player.IsSwimming())
            {
                wetDesc = $"<color=#64B5F6>{state.WetMultiplier:F1}x (Submerged)</color>";
            }
            else if (state.WetMultiplier > 1.05f)
            {
                wetDesc = $"<color=#64B5F6>{state.WetMultiplier:F1}x (Wet)</color>";
            }
            else
            {
                wetDesc = "<color=#A0A0A0>1.0x (Dry)</color>";
            }
            sb.AppendLine($"    <color=#A0A0A0>├─ Moisture / Wetness: {wetDesc}</color>");

            string windDesc;
            if (bd.IsSubterranean)
            {
                windDesc = "<color=#81C784>1.0x (Blocked by Subterranean Cover)</color>";
            }
            else if (state.IsInShelter)
            {
                windDesc = "<color=#81C784>1.0x (Blocked by Shelter)</color>";
            }
            else if (state.WindMultiplier > 1.05f)
            {
                windDesc = $"<color=#FFB74D>{state.WindMultiplier:F1}x (Exposed, {bd.RawWindSpeedKmh:F0} km/h wind)</color>";
            }
            else
            {
                windDesc = "<color=#A0A0A0>1.0x (Calm, Unshielded)</color>";
            }
            sb.AppendLine($"    <color=#A0A0A0>├─ Wind Exposure: {windDesc}</color>");

            string insulDesc;
            if (state.AmbientTemperature < state.BodyWarmth)
            {
                float reduction = (1.0f - state.InsulationMultiplier) * 100.0f;
                if (reduction > 0.5f)
                {
                    insulDesc = $"<color=#81C784>-{reduction:F0}% Rate ({state.InsulationRating * 100.0f:F0}% Insulation)</color>";
                }
                else
                {
                    insulDesc = "<color=#A0A0A0>1.0x (Unprotected)</color>";
                }
            }
            else
            {
                insulDesc = "<color=#A0A0A0>1.0x (Heating Up, Cold Insulation Inactive)</color>";
            }
            sb.AppendLine($"    <color=#A0A0A0>└─ Gear Insulation: {insulDesc}</color>");

            // 3. Gear Cold Insulation
            float insulPct = state.InsulationRating * 100.0f;
            string insulNote = state.HasFrostMead ? " <color=#81D4FA>(+80% Frost Mead)</color>" : (insulPct > 0.5f ? "" : " <i>(Unprotected)</i>");
            sb.AppendLine($"• <b>Gear Cold Insulation</b>: <color=yellow>{insulPct:F0}%</color>{insulNote}");

            // 4. Ambient Feels-Like with complete hierarchical breakdown (4 spaces indented per Rule 9)
            string feelsColor = GetTemperatureColorHex(state.AmbientTemperature);
            sb.AppendLine($"• <b>Ambient Feels-Like</b>: <color={feelsColor}>{FormatTemp(state.AmbientTemperature)}</color>");
            sb.AppendLine($"    <color=#A0A0A0>├─ Biome Base ({bd.BiomeName}): {FormatTemp(bd.BiomeBaseTemp)}</color>");

            if (bd.IsSubterranean)
            {
                sb.AppendLine($"    <color=#A0A0A0>├─ Subterranean Ground Offset: {FormatTemp(bd.SubterraneanOffset)}</color>");
                sb.AppendLine($"    <color=#A0A0A0>├─ Surface Altitude (50% Dampened): {FormatTemp(bd.AltitudeDelta)}</color>");
                sb.AppendLine($"    <color=#A0A0A0>├─ Time of Day (Subterranean): +0.0°C</color>");
                sb.AppendLine($"    <color=#A0A0A0>├─ Weather ({bd.WeatherName}, Subterranean): +0.0°C</color>");
                if (bd.SeasonName != "None" && bd.SeasonName != "Default")
                {
                    sb.AppendLine($"    <color=#A0A0A0>├─ Season ({bd.SeasonName}, 50% Dampened): {FormatTemp(bd.SeasonDelta)}</color>");
                }
            }
            else
            {
                // Altitude Delta only
                sb.AppendLine($"    <color=#A0A0A0>├─ Altitude: {FormatTemp(bd.AltitudeDelta)}</color>");

                // Diurnal Time-of-Day Phase
                string todPhase = bd.TimeOfDayDelta > 0.5f ? "Day" : (bd.TimeOfDayDelta < -0.5f ? "Night" : "Twilight");
                sb.AppendLine($"    <color=#A0A0A0>├─ Time of Day ({todPhase}): {FormatTemp(bd.TimeOfDayDelta)}</color>");

                // Weather Precipitation
                sb.AppendLine($"    <color=#A0A0A0>├─ Weather ({bd.WeatherName}): {FormatTemp(bd.WeatherDelta)}</color>");

                // Biome-Scaled Seasonality (only if season mod present)
                if (bd.SeasonName != "None" && bd.SeasonName != "Default")
                {
                    sb.AppendLine($"    <color=#A0A0A0>├─ Season ({bd.SeasonName}): {FormatTemp(bd.SeasonDelta)}</color>");
                }
            }

            // Heat Radiance & Dwelling Thermal Mass
            if (bd.FireSources != null && bd.FireSources.Count > 0)
            {
                if (bd.FireSources.Count == 1)
                {
                    string singleDesc = bd.FireSources[0].Split(':')[0];
                    sb.AppendLine($"    <color=#A0A0A0>└─ Radiance & Fire: {FormatTemp(bd.FireWarmthDelta)} ({singleDesc})</color>");
                }
                else
                {
                    sb.AppendLine($"    <color=#A0A0A0>└─ Radiance & Fire: {FormatTemp(bd.FireWarmthDelta)}</color>");
                    for (int i = 0; i < bd.FireSources.Count; i++)
                    {
                        string branch = (i == bd.FireSources.Count - 1) ? "└─ " : "├─ ";
                        sb.AppendLine($"        <color=#80CBC4>{branch}{bd.FireSources[i]}</color>");
                    }
                }
            }
            else
            {
                sb.AppendLine("    <color=#A0A0A0>└─ Radiance & Fire: 0.0°C</color>");
            }

            // 5. Dwelling Comfort & Rested Breakdown (shown strictly when sheltered inside a surface dwelling)
            if (ModConfig.EnableStructureComfort.Value)
            {
                bool isInside = player != null && !player.InInterior() && (player.InShelter() || ThermalGraphManager.IsInsideCompound(player));
                if (player != null && isInside)
                {
                    var comfortInfo = ThermalGraphManager.Instance.LastComfortInfo;
                    if (comfortInfo.ContributingItems.Count == 0)
                    {
                        ThermalGraphManager.Instance.CalculateDwellingComfort(player, true, player.transform.position);
                    }

                    if (comfortInfo != null && comfortInfo.InShelter)
                    {
                        int comfort = comfortInfo.TotalComfort;
                        sb.AppendLine($"• <b>Dwelling Comfort</b>: <color=yellow>Level {comfort}</color>");
                        float encPct = ThermalGraphManager.Instance.EnclosureRatio * 100f;
                        sb.AppendLine($"    <color=#A0A0A0>├─ Base Comfort: +1</color>");
                        sb.AppendLine($"    <color=#A0A0A0>├─ Enclosed Shelter: +1 ({encPct:F0}% Enclosure)</color>");

                        var items = comfortInfo.ContributingItems;
                        for (int i = 0; i < items.Count; i++)
                        {
                            var item = items[i];
                            bool isLast = (i == items.Count - 1) && (comfortInfo.RedundantCount == 0);
                            string branch = isLast ? "└─ " : "├─ ";
                            string grp = !string.IsNullOrEmpty(item.Group) && item.Group != "None" ? $" (Group: {item.Group})" : "";
                            sb.AppendLine($"    <color=#80CBC4>{branch}{item.Name}: +{item.Comfort}{grp}</color>");
                        }

                        if (comfortInfo.RedundantCount > 0)
                        {
                            sb.AppendLine($"    <color=#78909C>└─ <i>({comfortInfo.RedundantCount} redundant items omitted by group priority)</i></color>");
                        }
                    }
                    else
                    {
                        float encPct = ThermalGraphManager.Instance.EnclosureRatio * 100f;
                        int wildComfort = (comfortInfo != null && comfortInfo.TotalComfort > 0) ? comfortInfo.TotalComfort : 2;
                        sb.AppendLine($"• <b>Dwelling Comfort</b>: <color=#A0A0A0>Level {wildComfort} (Sheltered, {encPct:F0}% Enclosure, No Comfort Pieces)</color>");
                    }
                }
            }
            sb.AppendLine();
        }

        private static string FormatTemp(float val)
        {
            return val >= 0.0f ? $"+{val:F1}°C" : $"{val:F1}°C";
        }

        private static string GetTemperatureColorHex(float temp)
        {
            if (temp < 0.0f) return "#64B5F6";  // Ice blue
            if (temp < 18.0f) return "#81D4FA"; // Chilly light cyan
            if (temp <= 26.0f) return "#81C784";// Comfort green
            if (temp <= 45.0f) return "#FFB74D";// Warm amber
            return "#E57373";                   // Scorching red
        }

        private static string GetWarmthConditionDescriptor(float warmth)
        {
            if (warmth < -12.0f) return "Catastrophic Hypothermia";
            if (warmth < 0.0f) return "Numbing Frost";
            if (warmth < 8.0f) return "Bitter Cold";
            if (warmth < 18.0f) return "Chilly Discomfort";
            if (warmth <= 26.0f) return "Comfort Equilibrium";
            if (warmth <= 35.0f) return "Warm";
            if (warmth <= 45.0f) return "Heat Exhaustion";
            if (warmth <= 55.0f) return "Severe Hyperthermia";
            return "Catastrophic Scorching";
        }
    }
}
