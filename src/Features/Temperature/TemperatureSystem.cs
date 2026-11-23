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
        private static bool _initialized = false;
        private static string? _currentActiveStatusId = null;
        private static float _damageTickTimer = 0f;
        private const float DamageTickInterval = 1.0f; // 1 second between damage ticks

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            VikingWardsBridge.Init();
            SurvivalManager.OnSurvivalTick += HandleSurvivalTick;
            ThermalStatusEffects.Init();

            // Hook into VikingLib's Character Stats compendium tab (Hugin's Compendium & Auga Player Sheet)
            CharacterStatsGlossaryPanel.OnFormatCustomSections += FormatGlossarySection;
        }

        private static void HandleSurvivalTick(Player player, SurvivalState state, float dt)
        {
            if (!ModConfig.ModEnabled.Value || player == null || player != Player.m_localPlayer) return;

            // 1. Calculate Ambient Feels-Like Temperature and breakdown
            float targetAmbient = AmbientTemperatureCalculator.Calculate(player, dt, out var breakdown);
            bool thorWeatherProtected = !breakdown.IsSubterranean && VikingWardsBridge.IsPlayerProtectedByThorAegis(player);
            state.AmbientTemperature = targetAmbient;
            state.Breakdown = breakdown;
            state.WindChill = breakdown.WindChillDelta;
            state.FireWarmth = breakdown.FireWarmthDelta;
            state.IsInShelter = breakdown.IsSheltered;
            state.IsNearFire = breakdown.FireWarmthDelta > 0.01f;
            state.IsThorAegisProtected = thorWeatherProtected;

            // 2. Active Sleeping in Bed: Equate body temperature directly to bed ambient
            if (player.IsSleeping())
            {
                state.AmbientTemperature = targetAmbient;
                state.BodyWarmth = targetAmbient;
                state.TemperatureVelocity = 0.0f;
                state.ConductionRate = 0.0f;
                state.IsResting = true;
                state.IsReverting = false;
                UpdateThermalStatusTier(player, targetAmbient, dt, preventFreezing: thorWeatherProtected);
                return;
            }

            // 3. Calculate Gear Cold Insulation Rating
            float gearInsulation = GearInsulationRegistry.CalculatePlayerInsulation(player, out bool hasFrostMead);
            state.InsulationRating = gearInsulation;

            // 4. Principled Non-Linear Differential Conduction: dT/dt = Rate * sgn(T_amb - T) * |T_amb - T|^POW
            float ambientFeelsLike = state.AmbientTemperature;
            float currentWarmth = state.BodyWarmth;
            bool isWet = player.GetSEMan() != null && player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectWet);
            bool isSwimming = player.IsSwimming();

            float comfortMin = ModConfig.ComfortMinTemp.Value; // default 18°C
            float comfortMax = ModConfig.ComfortMaxTemp.Value; // default 26°C
            float comfortMid = (comfortMin + comfortMax) * 0.5f; // 22°C

            // A. Base Conduction Rate
            float baseRate = ModConfig.BaseCoolingRate.Value;

            // B. Wetness Multiplier (Thor Tier 3 Stormcaller's Aegis prevents weather wet accumulation & wet cooling unless in water)
            float wetMult = 1.0f;
            if (isSwimming)
            {
                wetMult = ModConfig.SwimCoolingMultiplier.Value;
            }
            else if (isWet)
            {
                if (thorWeatherProtected && !player.InWater())
                {
                    player.GetSEMan()?.RemoveStatusEffect(SEMan.s_statusEffectWet, quiet: true);
                    wetMult = 1.0f;
                }
                else
                {
                    wetMult = ModConfig.WetCoolingMultiplier.Value;
                }
            }

            // C. Wind Speed & Shelter Multiplier (suppressed by shelter, subterranean cover, or Thor Tier 3 Stormcaller's Aegis)
            float windMult = 1.0f;
            if (!state.IsInShelter && !state.Breakdown.IsSubterranean && !thorWeatherProtected && EnvMan.instance != null)
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

            // E. Non-Linear Temperature Delta & Conduction Direction
            float deltaT = ambientFeelsLike - currentWarmth;

            // F. Metabolic Stabilization Multiplier (Fast reversion ONLY when resting; never in the wild)
            bool isResting = (player.GetSEMan() != null && player.GetSEMan().HaveStatusEffect("Resting".GetStableHashCode())) || player.IsSleeping();
            bool movingTowardsComfort;
            if (currentWarmth < comfortMin)
            {
                movingTowardsComfort = deltaT > 0f;
            }
            else if (currentWarmth > comfortMax)
            {
                movingTowardsComfort = deltaT < 0f;
            }
            else
            {
                movingTowardsComfort = (ambientFeelsLike >= comfortMin && ambientFeelsLike <= comfortMax)
                    || Mathf.Abs((currentWarmth + deltaT) - comfortMid) < Mathf.Abs(currentWarmth - comfortMid);
            }

            bool isReverting = isResting && movingTowardsComfort;
            float metabolicMult = isReverting ? ModConfig.ReversionSpeedMultiplier.Value : 1.0f;

            // G. Unit-Interval Clamped Conduction Rate
            float rawRate = baseRate * wetMult * windMult * insulationMult * metabolicMult;
            float rate = Mathf.Clamp01(rawRate);

            // H. Non-Linear Differential Step Integration
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
            UpdateThermalStatusTier(player, currentWarmth, dt, preventFreezing: thorWeatherProtected);
        }

        public static string? EvaluateThermalStatusTier(float warmth)
        {
            float minComfort = ModConfig.ComfortMinTemp.Value;
            float maxComfort = ModConfig.ComfortMaxTemp.Value;

            if (warmth < minComfort)
            {
                if (warmth < -12.0f) return ThermalStatusEffects.HypothermiaId;
                if (warmth < 0.0f) return ThermalStatusEffects.NumbingFrostId;
                if (warmth < 8.0f) return ThermalStatusEffects.BitterColdId;
                return ThermalStatusEffects.ChillyId;
            }

            if (warmth > maxComfort)
            {
                if (warmth > 55.0f) return ThermalStatusEffects.CatastrophicScorchingId;
                if (warmth > 45.0f) return ThermalStatusEffects.SevereHyperthermiaId;
                if (warmth > 35.0f) return ThermalStatusEffects.HeatExhaustionId;
                return ThermalStatusEffects.WarmId;
            }

            return null; // Comfort zone
        }

        private static void UpdateThermalStatusTier(Player player, float warmth, float dt, bool preventFreezing = false)
        {
            string? targetStatusId = EvaluateThermalStatusTier(warmth);
            if (preventFreezing &&
                (targetStatusId == ThermalStatusEffects.HypothermiaId || targetStatusId == ThermalStatusEffects.NumbingFrostId))
            {
                targetStatusId = ThermalStatusEffects.BitterColdId;
            }

            // Apply periodic ticking extreme damage if in catastrophic tiers (every 1 second)
            // Uses direct neutral damage to prevent triggering vanilla SE_Frost/SE_Burning debuffs on top of our custom status effects
            if (targetStatusId == ThermalStatusEffects.HypothermiaId)
            {
                if (ModConfig.HypothermiaDamageRate.Value > 0f && player.GetHealth() > 1.0f)
                {
                    _damageTickTimer += dt;
                    if (_damageTickTimer >= DamageTickInterval)
                    {
                        _damageTickTimer = 0f;
                        float damageAmount = ModConfig.HypothermiaDamageRate.Value * DamageTickInterval;
                        HitData hit = new HitData
                        {
                            m_damage = { m_damage = damageAmount },
                            m_point = player.transform.position,
                            m_dir = Vector3.down,
                            m_hitType = HitData.HitType.PlayerHit
                        };
                        player.ApplyDamage(hit, showDamageText: true, triggerEffects: false, HitData.DamageModifier.Normal);
                    }
                }
            }
            else if (targetStatusId == ThermalStatusEffects.CatastrophicScorchingId)
            {
                if (ModConfig.HyperthermiaDamageRate.Value > 0f && player.GetHealth() > 1.0f)
                {
                    _damageTickTimer += dt;
                    if (_damageTickTimer >= DamageTickInterval)
                    {
                        _damageTickTimer = 0f;
                        float damageAmount = ModConfig.HyperthermiaDamageRate.Value * DamageTickInterval;
                        HitData hit = new HitData
                        {
                            m_damage = { m_damage = damageAmount },
                            m_point = player.transform.position,
                            m_dir = Vector3.down,
                            m_hitType = HitData.HitType.PlayerHit
                        };
                        player.ApplyDamage(hit, showDamageText: true, triggerEffects: false, HitData.DamageModifier.Normal);
                    }
                }
            }
            else
            {
                _damageTickTimer = 0f;
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

            SurvivalManager.CurrentState.ActiveThermalTierId = _currentActiveStatusId;
        }

        public static void ResetStatusOnDeath(Player player)
        {
            _damageTickTimer = 0f;
            if (!string.IsNullOrEmpty(_currentActiveStatusId))
            {
                StatusEffectRegistry.Remove(player, _currentActiveStatusId);
                _currentActiveStatusId = null;
            }
            SurvivalManager.CurrentState.ActiveThermalTierId = null;
        }

        public static void OnPlayerRespawnReset(Player player, float targetWarmth)
        {
            ResetStatusOnDeath(player);

            var state = SurvivalManager.CurrentState;
            state.BodyWarmth = targetWarmth;
            state.AmbientTemperature = targetWarmth;
            state.SaveToPlayer(player);

            VikingClimatePlugin.Log.LogInfo($"[TemperatureSystem] Reset player '{player.GetPlayerName()}' temperature to {targetWarmth:F1}°C upon respawn.");
        }

        private static void FormatGlossarySection(Player player, StringBuilder sb)
        {
            if (!ModConfig.ModEnabled.Value || player == null || player != Player.m_localPlayer) return;

            var state = SurvivalManager.CurrentState;
            var bd = state.Breakdown;
            bool thorProtected = state.IsThorAegisProtected;

            sb.AppendLine("<color=orange><b>[ THERMAL & CLIMATE ]</b></color>");

            // 1. Body Warmth & Active Thermal Tier
            string warmthColor = GetTemperatureColorHex(state.BodyWarmth);
            string activeTierName = GetActiveThermalTierLabel(state.ActiveThermalTierId, state.BodyWarmth);
            bool freezingWarded = thorProtected && state.BodyWarmth < 0.0f &&
                                  state.ActiveThermalTierId == ThermalStatusEffects.BitterColdId;
            string tierSuffix = freezingWarded ? " <color=#82F5FF>(Freezing Warded)</color>" : string.Empty;
            sb.AppendLine($"• <b>Body Warmth</b>: <color={warmthColor}>{FormatTemp(state.BodyWarmth)}</color> <i>({activeTierName})</i>{tierSuffix}");

            // 1b. Stormcaller's Aegis (VikingWards Thor T3 soft-bridge — sparse-pruned when inactive)
            if (thorProtected)
            {
                sb.AppendLine("• <b>Stormcaller's Aegis</b>: <color=#82F5FF>Active</color> <i>(Storms, wind chill, rain wetness & freezing warded)</i>");
            }

            // 2. Heat Transfer Rate & Tendency (in °C/min with contributing factors underneath)
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
            else if (thorProtected)
            {
                wetDesc = "<color=#82F5FF>1.0x (Dry — Rain Warded)</color>";
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
            else if (thorProtected)
            {
                windDesc = "<color=#82F5FF>1.0x (Blocked by Stormcaller's Aegis)</color>";
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
                    insulDesc = $"<color=#81C784>-{reduction:F0}% Rate ({state.InsulationRating * 100.0f:F0}% Gear)</color>";
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

            // 3. Gear Warmth & Resistance (reads cached state directly — zero inventory scan allocation)
            float insulPct = state.InsulationRating * 100.0f;
            string insulNote = state.HasFrostMead
                ? " <color=#81D4FA>(+80% Frost Mead, Cap 95%)</color>"
                : (insulPct > 0.5f ? string.Empty : " <i>(Unprotected)</i>");
            sb.AppendLine($"• <b>Gear Warmth & Resistance</b>: <color=yellow>{insulPct:F0}% Cold Insulation</color>{insulNote}");

            // 4. Structural Shelter & Insulation Rating
            float enclosurePct = bd.EnclosureRatio * 100.0f;
            if (bd.IsSubterranean)
            {
                sb.AppendLine("• <b>Structural Shelter</b>: <color=#81C784>Subterranean Cover</color> <i>(Wind & Weather Isolated)</i>");
            }
            else if (state.IsInShelter)
            {
                sb.AppendLine($"• <b>Structural Shelter</b>: <color=#81C784>Enclosed Dwelling ({enclosurePct:F0}% Enclosure)</color>");
                if (bd.IsDwellingHeatActive)
                {
                    sb.AppendLine($"    <color=#A0A0A0>├─ Structural Thermal Mass: {FormatTemp(bd.AverageStructureTemp)} ({FormatTemp(bd.StructuralWarmth)} vs outdoor)</color>");
                    string hearthSuffix = bd.ActiveFiresInLos > 0 ? $" ({bd.ActiveFiresInLos} active in sight)" : string.Empty;
                    sb.AppendLine($"    <color=#A0A0A0>└─ Direct Hearth Warmth: {FormatTemp(bd.DirectFlameWarmth)}{hearthSuffix}</color>");
                }
            }
            else if (enclosurePct >= 1.0f)
            {
                sb.AppendLine($"• <b>Structural Shelter</b>: <color=#FFB74D>Exposed ({enclosurePct:F0}% Partial Enclosure)</color>");
            }
            else
            {
                sb.AppendLine("• <b>Structural Shelter</b>: <color=#A0A0A0>Exposed Outdoors (0% Enclosure)</color>");
            }

            // 5. Ambient Feels-Like with complete environmental delta breakdown
            string feelsColor = GetTemperatureColorHex(state.AmbientTemperature);
            string biomeName = string.IsNullOrEmpty(bd.BiomeName) ? "Unknown" : bd.BiomeName;
            string weatherName = string.IsNullOrEmpty(bd.WeatherName) ? "Clear" : bd.WeatherName;
            sb.AppendLine($"• <b>Ambient Feels-Like</b>: <color={feelsColor}>{FormatTemp(state.AmbientTemperature)}</color>");
            sb.AppendLine($"    <color=#A0A0A0>├─ Biome Base ({biomeName}): {FormatTemp(bd.BiomeBaseTemp)}</color>");

            if (bd.IsSubterranean)
            {
                sb.AppendLine($"    <color=#A0A0A0>├─ Subterranean Ground Offset: {FormatTemp(bd.SubterraneanOffset)}</color>");
                sb.AppendLine($"    <color=#A0A0A0>├─ Surface Altitude (50% Dampened): {FormatTemp(bd.AltitudeDelta)}</color>");
                sb.AppendLine("    <color=#A0A0A0>├─ Time of Day (Subterranean): +0.0°C</color>");
                sb.AppendLine($"    <color=#A0A0A0>├─ Weather ({weatherName}, Subterranean): +0.0°C</color>");
                sb.AppendLine("    <color=#A0A0A0>├─ Wind Chill (Subterranean): +0.0°C</color>");
                if (!string.IsNullOrEmpty(bd.SeasonName) && bd.SeasonName != "None" && bd.SeasonName != "Default")
                {
                    sb.AppendLine($"    <color=#A0A0A0>├─ Season ({bd.SeasonName}, 50% Dampened): {FormatTemp(bd.SeasonDelta)}</color>");
                }
            }
            else
            {
                sb.AppendLine($"    <color=#A0A0A0>├─ Altitude: {FormatTemp(bd.AltitudeDelta)}</color>");

                string todPhase = bd.TimeOfDayDelta > 0.5f ? "Day" : (bd.TimeOfDayDelta < -0.5f ? "Night" : "Twilight");
                sb.AppendLine($"    <color=#A0A0A0>├─ Time of Day ({todPhase}): {FormatTemp(bd.TimeOfDayDelta)}</color>");

                string weatherSuffix = thorProtected ? " — Stormcaller's Aegis" : string.Empty;
                sb.AppendLine($"    <color=#A0A0A0>├─ Weather ({weatherName}{weatherSuffix}): {FormatTemp(bd.WeatherDelta)}</color>");

                // Wind Chill / Convective Wind Exposure line
                if (state.IsInShelter)
                {
                    sb.AppendLine("    <color=#A0A0A0>├─ Wind Chill: +0.0°C (Sheltered)</color>");
                }
                else if (thorProtected)
                {
                    sb.AppendLine("    <color=#A0A0A0>├─ Wind Chill: +0.0°C (Warded by Stormcaller's Aegis)</color>");
                }
                else if (Mathf.Abs(bd.WindChillDelta) > 0.05f)
                {
                    sb.AppendLine($"    <color=#A0A0A0>├─ Wind Chill ({bd.RawWindSpeedKmh:F0} km/h): {FormatTemp(bd.WindChillDelta)} ({state.WindMultiplier:F1}x cooling)</color>");
                }
                else if (state.WindMultiplier > 1.05f)
                {
                    sb.AppendLine($"    <color=#A0A0A0>├─ Wind Chill ({bd.RawWindSpeedKmh:F0} km/h): {state.WindMultiplier:F1}x convective cooling</color>");
                }
                else
                {
                    sb.AppendLine("    <color=#A0A0A0>├─ Wind Chill: +0.0°C (Calm)</color>");
                }

                if (!string.IsNullOrEmpty(bd.SeasonName) && bd.SeasonName != "None" && bd.SeasonName != "Default")
                {
                    sb.AppendLine($"    <color=#A0A0A0>├─ Season ({bd.SeasonName}): {FormatTemp(bd.SeasonDelta)}</color>");
                }
            }

            // Hearth Warmth, Radiance & Dwelling Thermal Mass
            if (bd.FireSources != null && bd.FireSources.Count > 0)
            {
                if (bd.FireSources.Count == 1)
                {
                    string firstSource = bd.FireSources[0];
                    int colonIdx = firstSource.IndexOf(':');
                    sb.Append("    <color=#A0A0A0>└─ Hearth & Fire Warmth: ");
                    sb.Append(FormatTemp(bd.FireWarmthDelta));
                    sb.Append(" (");
                    if (colonIdx > 0)
                    {
                        sb.Append(firstSource, 0, colonIdx);
                    }
                    else
                    {
                        sb.Append(firstSource);
                    }
                    sb.AppendLine(")</color>");
                }
                else
                {
                    sb.AppendLine($"    <color=#A0A0A0>└─ Hearth & Fire Warmth: {FormatTemp(bd.FireWarmthDelta)}</color>");
                    for (int i = 0; i < bd.FireSources.Count; i++)
                    {
                        string branch = (i == bd.FireSources.Count - 1) ? "└─ " : "├─ ";
                        sb.AppendLine($"        <color=#80CBC4>{branch}{bd.FireSources[i]}</color>");
                    }
                }
            }
            else
            {
                sb.AppendLine("    <color=#A0A0A0>└─ Hearth & Fire Warmth: +0.0°C</color>");
            }

            // 6. Dwelling Comfort & Rested Breakdown (reads cached LastComfortInfo — zero BFS/scene queries)
            if (ModConfig.EnableStructureComfort.Value && !bd.IsSubterranean && state.IsInShelter)
            {
                var comfortInfo = ThermalGraphManager.Instance.LastComfortInfo;
                if (comfortInfo != null && comfortInfo.InShelter && comfortInfo.ContributingItems.Count > 0)
                {
                    int comfort = comfortInfo.TotalComfort;
                    sb.AppendLine($"• <b>Dwelling Comfort</b>: <color=yellow>Level {comfort}</color>");
                    sb.AppendLine("    <color=#A0A0A0>├─ Base Comfort: +1</color>");
                    sb.AppendLine($"    <color=#A0A0A0>├─ Enclosed Shelter: +1 ({enclosurePct:F0}% Enclosure)</color>");

                    var items = comfortInfo.ContributingItems;
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        bool isLast = (i == items.Count - 1) && (comfortInfo.RedundantCount == 0);
                        string branch = isLast ? "└─ " : "├─ ";
                        string grp = !string.IsNullOrEmpty(item.Group) && item.Group != "None" ? $" (Group: {item.Group})" : string.Empty;
                        sb.AppendLine($"    <color=#80CBC4>{branch}{item.Name}: +{item.Comfort}{grp}</color>");
                    }

                    if (comfortInfo.RedundantCount > 0)
                    {
                        sb.AppendLine($"    <color=#78909C>└─ <i>({comfortInfo.RedundantCount} redundant items omitted by group priority)</i></color>");
                    }
                }
                else
                {
                    int wildComfort = (comfortInfo != null && comfortInfo.TotalComfort > 0) ? comfortInfo.TotalComfort : 2;
                    sb.AppendLine($"• <b>Dwelling Comfort</b>: <color=#A0A0A0>Level {wildComfort} (Sheltered, {enclosurePct:F0}% Enclosure)</color>");
                }
            }

            sb.AppendLine();
        }

        private static string GetActiveThermalTierLabel(string? statusId, float warmth)
        {
            if (string.IsNullOrEmpty(statusId))
            {
                float minComfort = ModConfig.ComfortMinTemp.Value;
                float maxComfort = ModConfig.ComfortMaxTemp.Value;
                if (warmth >= minComfort && warmth <= maxComfort)
                {
                    return "Comfort Equilibrium";
                }
                return GetWarmthConditionDescriptor(warmth);
            }

            switch (statusId)
            {
                case ThermalStatusEffects.ChillyId: return "Chilly";
                case ThermalStatusEffects.BitterColdId: return "Bitter Cold";
                case ThermalStatusEffects.NumbingFrostId: return "Numbing Frost";
                case ThermalStatusEffects.HypothermiaId: return "Hypothermia";
                case ThermalStatusEffects.WarmId: return "Warm";
                case ThermalStatusEffects.HeatExhaustionId: return "Heat Exhaustion";
                case ThermalStatusEffects.SevereHyperthermiaId: return "Severe Hyperthermia";
                case ThermalStatusEffects.CatastrophicScorchingId: return "Catastrophic Scorching";
                default: return GetWarmthConditionDescriptor(warmth);
            }
        }

        private static string FormatTemp(float val)
        {
            return val >= 0.0f ? $"+{val:F1}°C" : $"{val:F1}°C";
        }

        public static Color GetTemperatureColor(float warmth)
        {
            float minComfort = ModConfig.ComfortMinTemp.Value;
            float maxComfort = ModConfig.ComfortMaxTemp.Value;

            if (warmth < minComfort)
            {
                if (warmth < -12.0f) return new Color(0.25f, 0.32f, 0.71f); // Hypothermia: Deep Indigo
                if (warmth < 0.0f) return new Color(0.26f, 0.65f, 0.96f);   // Numbing Frost: Ice Blue
                if (warmth < 8.0f) return new Color(0.30f, 0.82f, 0.88f);   // Bitter Cold: Cyan
                return new Color(0.50f, 0.87f, 0.92f);                      // Chilly: Soft Sky Cyan
            }

            if (warmth > maxComfort)
            {
                if (warmth > 55.0f) return new Color(0.94f, 0.33f, 0.31f);  // Catastrophic Scorching: Crimson
                if (warmth > 45.0f) return new Color(1.00f, 0.44f, 0.26f);  // Severe Hyperthermia: Deep Orange-Red
                if (warmth > 35.0f) return new Color(1.00f, 0.65f, 0.15f);  // Heat Exhaustion: Vivid Orange
                return new Color(1.00f, 0.93f, 0.35f);                      // Warm: Warm Amber-Yellow
            }

            return new Color(0.51f, 0.78f, 0.52f);                          // Comfort: Pleasant Green
        }

        public static string GetTemperatureColorHex(float warmth)
        {
            float minComfort = ModConfig.ComfortMinTemp.Value;
            float maxComfort = ModConfig.ComfortMaxTemp.Value;

            if (warmth < minComfort)
            {
                if (warmth < -12.0f) return "#3F51B5"; // Hypothermia
                if (warmth < 0.0f) return "#42A5F5";   // Numbing Frost
                if (warmth < 8.0f) return "#4DD0E1";   // Bitter Cold
                return "#80DEEA";                      // Chilly
            }

            if (warmth > maxComfort)
            {
                if (warmth > 55.0f) return "#EF5350";  // Catastrophic Scorching
                if (warmth > 45.0f) return "#FF7043";  // Severe Hyperthermia
                if (warmth > 35.0f) return "#FFA726";  // Heat Exhaustion
                return "#FFEE58";                      // Warm
            }

            return "#81C784";                          // Comfort
        }

        private static string GetWarmthConditionDescriptor(float warmth)
        {
            float minComfort = ModConfig.ComfortMinTemp.Value;
            float maxComfort = ModConfig.ComfortMaxTemp.Value;

            if (warmth < -12.0f) return "Catastrophic Hypothermia";
            if (warmth < 0.0f) return "Numbing Frost";
            if (warmth < 8.0f) return "Bitter Cold";
            if (warmth < minComfort) return "Chilly Discomfort";
            if (warmth <= maxComfort) return "Comfort Equilibrium";
            if (warmth <= 35.0f) return "Warm";
            if (warmth <= 45.0f) return "Heat Exhaustion";
            if (warmth <= 55.0f) return "Severe Hyperthermia";
            return "Catastrophic Scorching";
        }
    }
}
