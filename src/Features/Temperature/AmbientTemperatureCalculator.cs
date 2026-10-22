using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Core;

namespace VikingClimate.Features.Temperature
{
    /// <summary>
    /// Calculates the ambient 'feels-like' temperature experienced by the local player.
    /// Incorporates a 17-point dual-ring Gaussian biome kernel for seamless spatial blending,
    /// subtle elevation lapse, diurnal solar curve, weather precipitation, biome-scaled seasonality,
    /// wind chill (blocked by shelter), and direct/convective heat source dispersion.
    /// </summary>
    public static class AmbientTemperatureCalculator
    {
        private static readonly Vector2[] s_innerOffsets = new Vector2[8];
        private static readonly Vector2[] s_outerOffsets = new Vector2[8];

        static AmbientTemperatureCalculator()
        {
            // Inner ring: 8 points at radius 25m
            float rInner = 25.0f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (Mathf.PI * 2.0f / 8.0f);
                s_innerOffsets[i] = new Vector2(Mathf.Cos(angle) * rInner, Mathf.Sin(angle) * rInner);
            }

            // Outer ring: 8 points at radius 50m (rotated by 22.5 degrees for uniform angular coverage)
            float rOuter = 50.0f;
            float rotOffset = Mathf.PI / 8.0f;
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * (Mathf.PI * 2.0f / 8.0f)) + rotOffset;
                s_outerOffsets[i] = new Vector2(Mathf.Cos(angle) * rOuter, Mathf.Sin(angle) * rOuter);
            }
        }

        public static float Calculate(Player player, out AmbientThermalBreakdown breakdown)
        {
            return Calculate(player, 0.5f, out breakdown);
        }

        public static float Calculate(Player player, float dt, out AmbientThermalBreakdown breakdown)
        {
            breakdown = default;

            if (player == null || EnvMan.instance == null)
            {
                breakdown.BiomeName = "None";
                breakdown.WeatherName = "None";
                breakdown.SeasonName = "None";
                return 20.0f;
            }

            Vector3 pos = player.transform.position;
            bool inShelter = player.InShelter();
            breakdown.IsSheltered = inShelter;

            // 1. Biome Baseline Maximum Temperature (17-point Gaussian kernel blend)
            float baseTemp = SampleBlendedBiomeBaseTemp(pos, out Heightmap.Biome primaryBiome);
            breakdown.BiomeName = primaryBiome.ToString();
            breakdown.BiomeBaseTemp = baseTemp;

            // 2. Subtle Altitude Lapse Rate (-1°C per 35m elevation above sea level y=30)
            float altitudeDelta = 0.0f;
            if (pos.y > 30.0f)
            {
                altitudeDelta = -Mathf.Max(0.0f, (pos.y - 30.0f) / 35.0f);
            }
            breakdown.AltitudeDelta = altitudeDelta;

            // 3. Diurnal Time-of-Day Curve (Peak afternoon +4°C, night trough -4°C)
            float dayFraction = EnvMan.instance.GetDayFraction();
            float timeOfDayDelta = 4.0f * Mathf.Sin((dayFraction - 0.25f) * Mathf.PI * 2.0f);
            breakdown.TimeOfDayDelta = timeOfDayDelta;

            // 4. Weather Precipitation & Environment Delta
            EnvSetup env = EnvMan.instance.GetCurrentEnvironment();
            string weatherName = env != null ? env.m_name : "Clear";
            breakdown.WeatherName = weatherName;
            float weatherDelta = GetWeatherDelta(env);
            breakdown.WeatherDelta = weatherDelta;

            // 5. Biome-Scaled Seasonality Mod Synergy
            string seasonName;
            float seasonDelta = GetSeasonDelta(primaryBiome, out seasonName);
            breakdown.SeasonName = seasonName;
            breakdown.SeasonDelta = seasonDelta;

            // 6. Wind Speed (Recorded for convective heat transfer rate; does not alter ambient air temperature)
            float windIntensity = EnvMan.instance.GetWindIntensity();
            breakdown.RawWindSpeedKmh = windIntensity * 50.0f;
            breakdown.WindChillDelta = 0.0f;

            // Net Outdoor Ambient Temperature (without dwelling/fire modifications)
            float outdoorAmbient = baseTemp + altitudeDelta + timeOfDayDelta + weatherDelta + seasonDelta;

            // Debug World Override: Force ambient outdoor temperature everywhere to a fixed value
            if (VikingClimate.Configuration.ModConfig.DebugFreezeAmbientTemp != null &&
                VikingClimate.Configuration.ModConfig.DebugFreezeAmbientTemp.Value)
            {
                float forcedAmb = VikingClimate.Configuration.ModConfig.DebugForcedAmbientTemp != null
                    ? VikingClimate.Configuration.ModConfig.DebugForcedAmbientTemp.Value
                    : 15.0f;
                outdoorAmbient = forcedAmb;
                baseTemp = forcedAmb;
                altitudeDelta = 0.0f;
                timeOfDayDelta = 0.0f;
                weatherDelta = 0.0f;
                seasonDelta = 0.0f;
                breakdown.BiomeBaseTemp = forcedAmb;
                breakdown.AltitudeDelta = 0.0f;
                breakdown.TimeOfDayDelta = 0.0f;
                breakdown.WeatherDelta = 0.0f;
                breakdown.SeasonDelta = 0.0f;
            }

            breakdown.OutdoorAmbient = outdoorAmbient;

            // 7. Enclosed Structure & Fireplace Heat Convection / Thermal Mass System
            if (VikingClimate.Configuration.ModConfig.ThermalMassSimulationMode.Value == VikingClimate.Configuration.ThermalMassMode.StructuralGraph)
            {
                // Formalized Ground Temperature:
                // High thermal inertia, dampened altitude & season, no diurnal swing, biome ground offset
                float groundBiomeOffset = GetGroundBiomeOffset(baseTemp);
                float groundAltitudeDelta = altitudeDelta * 0.50f;
                float groundSeasonDelta = seasonDelta * 0.50f;
                float groundTemp = (VikingClimate.Configuration.ModConfig.DebugFreezeAmbientTemp != null &&
                                    VikingClimate.Configuration.ModConfig.DebugFreezeAmbientTemp.Value)
                    ? outdoorAmbient
                    : (baseTemp + groundBiomeOffset + groundAltitudeDelta + groundSeasonDelta);
                breakdown.GroundTemperature = groundTemp;

                ThermalGraphManager.Instance.Update(dt, player, outdoorAmbient);

                float perceivedTemp = ThermalGraphManager.Instance.GetPlayerWarmth(
                    player,
                    pos,
                    outdoorAmbient,
                    groundTemp,
                    dt,
                    out float directWarmth,
                    out float structureWarmth,
                    out var graphTelemetry
                );

                bool isSheltered = ThermalGraphManager.Instance.IsPlayerSheltered;
                breakdown.IsSheltered = isSheltered;

                float netDelta = perceivedTemp - outdoorAmbient;
                breakdown.IsDwellingHeatActive = graphTelemetry.IsActive;
                breakdown.FireWarmthDelta = netDelta;
                breakdown.FireSourceDescription = isSheltered ? "Dwelling Thermal Mass" : "Environmental Field";
                breakdown.FireSources = FormatGraphFireSources(perceivedTemp, directWarmth, structureWarmth, graphTelemetry);

                return perceivedTemp;
            }

            // Outside shelter with no structural heat, or Fallback mode
            float fireWarmthDelta = CalculateFireWarmth(player, pos, inShelter, out string fireDesc, out List<string> fireSources);
            breakdown.IsDwellingHeatActive = false;
            breakdown.FireWarmthDelta = fireWarmthDelta;
            breakdown.FireSourceDescription = fireDesc;
            breakdown.FireSources = fireSources;

            float finalFeelsLike = outdoorAmbient + fireWarmthDelta;
            return finalFeelsLike;
        }

        /// <summary>
        /// Samples a 17-point Gaussian disk around the given position to produce a smooth,
        /// continuous spatial transition across biome borders.
        /// </summary>
        private static float SampleBlendedBiomeBaseTemp(Vector3 pos, out Heightmap.Biome primaryBiome)
        {
            primaryBiome = GetBiomeAt(pos);
            float totalTemp = 4.0f * GetBiomeBaseTemp(primaryBiome);
            float totalWeight = 4.0f;

            // Inner ring (weight 1.0 each)
            for (int i = 0; i < 8; i++)
            {
                var samplePos = new Vector3(pos.x + s_innerOffsets[i].x, pos.y, pos.z + s_innerOffsets[i].y);
                Heightmap.Biome b = GetBiomeAt(samplePos);
                totalTemp += 1.0f * GetBiomeBaseTemp(b);
                totalWeight += 1.0f;
            }

            // Outer ring (weight 0.5 each)
            for (int i = 0; i < 8; i++)
            {
                var samplePos = new Vector3(pos.x + s_outerOffsets[i].x, pos.y, pos.z + s_outerOffsets[i].y);
                Heightmap.Biome b = GetBiomeAt(samplePos);
                totalTemp += 0.5f * GetBiomeBaseTemp(b);
                totalWeight += 0.5f;
            }

            return totalTemp / totalWeight;
        }

        private static Heightmap.Biome GetBiomeAt(Vector3 point)
        {
            if (WorldGenerator.instance != null)
            {
                return WorldGenerator.instance.GetBiome(point);
            }
            return Heightmap.FindBiome(point);
        }

        private static float GetBiomeBaseTemp(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.Meadows:
                    return 20.0f;
                case Heightmap.Biome.BlackForest:
                    return 14.0f;
                case Heightmap.Biome.Swamp:
                    return 16.0f;
                case Heightmap.Biome.Plains:
                    return 26.0f;
                case Heightmap.Biome.Ocean:
                    return 16.0f;
                case Heightmap.Biome.Mistlands:
                    return 12.0f;
                case Heightmap.Biome.Mountain:
                    return -10.0f;
                case Heightmap.Biome.DeepNorth:
                    return -23.0f;
                case Heightmap.Biome.AshLands:
                    return 48.0f;
                default:
                    return 18.0f;
            }
        }

        private static float GetWeatherDelta(EnvSetup? env)
        {
            if (env == null) return 0.0f;

            string name = env.m_name.ToLowerInvariant();

            if (name.Contains("blizzard")) return -4.0f;
            if (name.Contains("snow")) return -2.5f;
            if (name.Contains("thunder")) return -2.0f;
            if (name.Contains("rain")) return -1.5f;
            if (name.Contains("fog") || name.Contains("mist")) return -0.5f;

            if (env.m_isFreezing) return -3.0f;
            if (env.m_isCold) return -1.5f;
            if (env.m_isWet) return -1.0f;

            return 0.0f;
        }

        private static bool _checkedSeasonMod = false;
        private static bool _seasonModPresent = false;

        /// <summary>
        /// Checks whether a dedicated Seasons mod (e.g. Seasonality, Seasons by shudnal) is loaded in BepInEx.
        /// Prevents stale global keys persisted in the world save from permanently forcing winter in vanilla worlds.
        /// </summary>
        public static bool IsSeasonModInstalled()
        {
            if (!_checkedSeasonMod && BepInEx.Bootstrap.Chainloader.PluginInfos != null)
            {
                _checkedSeasonMod = true;
                foreach (var kvp in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    string id = kvp.Key.ToLowerInvariant();
                    if (id.Contains("season"))
                    {
                        _seasonModPresent = true;
                        break;
                    }
                }
            }
            return _seasonModPresent;
        }

        /// <summary>
        /// Scales seasonal temperature deltas according to biome harshness.
        /// Meadows is gentle (±3°C) so early-game players aren't overwhelmed before crafting winter gear.
        /// </summary>
        private static float GetSeasonDelta(Heightmap.Biome biome, out string seasonName)
        {
            seasonName = "Default";
            float seasonFactor = 0.0f;

            if (IsSeasonModInstalled() && ZoneSystem.instance != null)
            {
                if (ZoneSystem.instance.GetGlobalKey("season_winter"))
                {
                    seasonName = "Winter";
                    seasonFactor = -1.0f;
                }
                else if (ZoneSystem.instance.GetGlobalKey("season_fall") || ZoneSystem.instance.GetGlobalKey("season_autumn"))
                {
                    seasonName = "Autumn";
                    seasonFactor = -0.4f;
                }
                else if (ZoneSystem.instance.GetGlobalKey("season_summer"))
                {
                    seasonName = "Summer";
                    seasonFactor = 0.6f;
                }
                else if (ZoneSystem.instance.GetGlobalKey("season_spring"))
                {
                    seasonName = "Spring";
                    seasonFactor = 0.0f;
                }
            }

            if (Mathf.Abs(seasonFactor) < 0.001f) return 0.0f;

            float biomeScale;
            switch (biome)
            {
                case Heightmap.Biome.Meadows:
                    biomeScale = 3.0f;
                    break;
                case Heightmap.Biome.BlackForest:
                case Heightmap.Biome.Swamp:
                    biomeScale = 5.0f;
                    break;
                default:
                    biomeScale = 8.0f;
                    break;
            }

            return seasonFactor * biomeScale;
        }

        /// <summary>
        /// Calculates direct line-of-sight radiance and enclosed shelter room convection
        /// with strictly non-negative quadratic falloff and diminishing returns across multiple fires.
        /// Includes equipped handheld torches (+3°C) as personal mobile fire sources.
        /// </summary>
        private struct DiscoveredFire
        {
            public string Name;
            public float Warmth;
            public bool IsConvection;
        }

        private static float CalculateFireWarmth(Player player, Vector3 playerPos, bool inShelter, out string fireDesc, out List<string> fireSources)
        {
            fireSources = new List<string>();
            var discoveredFires = new List<DiscoveredFire>();

            // 1. Equipped Handheld Torch (Personal mobile fire source: exactly +3°C)
            // Active ONLY when not sheltered or when ThermalMassSimulationMode is Fallback
            bool holdingTorch = IsHoldingLitTorch(player);
            bool allowTorch = !inShelter || VikingClimate.Configuration.ModConfig.ThermalMassSimulationMode.Value == VikingClimate.Configuration.ThermalMassMode.Fallback;
            if (holdingTorch && allowTorch)
            {
                discoveredFires.Add(new DiscoveredFire
                {
                    Name = "Equipped Torch",
                    Warmth = 3.0f,
                    IsConvection = false
                });
            }

            // 2. Discover world fireplaces and effect areas
            var areas = EffectArea.GetAllAreas();
            if (areas != null && areas.Count > 0)
            {
                Vector3 eyePos = playerPos + Vector3.up * 1.5f;
                int occlusionMask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");

                for (int i = 0; i < areas.Count; i++)
                {
                    var area = areas[i];
                    if (area == null) continue;

                    // Filter for thermal/fire effect areas
                    if ((area.m_type & EffectArea.Type.Heat) == 0 && (area.m_type & EffectArea.Type.Fire) == 0)
                    {
                        continue;
                    }

                    // Crucial: Skip player's own equipped torch/gear to avoid duplicate counting and misclassification
                    if (area.GetComponentInParent<Player>() != null || area.transform.IsChildOf(player.transform))
                    {
                        continue;
                    }

                    // Verify fireplace is fueled/burning if a Fireplace component is present
                    var fireplace = area.GetComponentInParent<Fireplace>();
                    if (fireplace != null && !fireplace.IsBurning())
                    {
                        continue;
                    }

                    // Verify smelter is fueled/active if a Smelter component is present
                    var smelter = area.GetComponentInParent<Smelter>();
                    if (smelter != null && (smelter.m_windmill != null || !smelter.IsActive()))
                    {
                        continue;
                    }

                    // Determine archetype and friendly name
                    float baseWarmth = 16.0f;
                    float radRadius = 7.0f;
                    string friendlyName = "Campfire";

                    string objName = "";
                    if (fireplace != null)
                    {
                        objName = fireplace.gameObject.name.ToLowerInvariant();
                    }
                    else if (smelter != null)
                    {
                        objName = (smelter.m_name ?? smelter.gameObject.name).ToLowerInvariant();
                    }
                    else
                    {
                        var piece = area.GetComponentInParent<Piece>();
                        objName = (piece != null ? piece.gameObject.name : area.gameObject.name).ToLowerInvariant();
                    }

                    if (objName.Contains("blastfurnace"))
                    {
                        baseWarmth = 28.0f;
                        radRadius = 10.0f;
                        friendlyName = "Blast Furnace";
                    }
                    else if (objName.Contains("smelter"))
                    {
                        baseWarmth = 24.0f;
                        radRadius = 9.0f;
                        friendlyName = "Smelter";
                    }
                    else if (objName.Contains("kiln"))
                    {
                        baseWarmth = 22.0f;
                        radRadius = 8.5f;
                        friendlyName = "Charcoal Kiln";
                    }
                    else if (objName.Contains("hearth"))
                    {
                        baseWarmth = 24.0f;
                        radRadius = 9.0f;
                        friendlyName = "Hearth";
                    }
                    else if (objName.Contains("bonfire"))
                    {
                        baseWarmth = 28.0f;
                        radRadius = 11.0f;
                        friendlyName = "Bonfire";
                    }
                    else if (objName.Contains("brazier"))
                    {
                        baseWarmth = 6.0f;
                        radRadius = 5.0f;
                        friendlyName = "Brazier";
                    }
                    else if (objName.Contains("torch") || objName.Contains("sconce"))
                    {
                        baseWarmth = 3.0f; // Low base heat (+3°C) for standing torches and wall sconces
                        radRadius = 4.0f;
                        friendlyName = "Standing Torch";
                    }
                    else if (objName.Contains("fire") || objName.Contains("camp"))
                    {
                        baseWarmth = 16.0f;
                        radRadius = 7.0f;
                        friendlyName = "Campfire";
                    }

                    Vector3 firePos = area.transform.position;
                    Vector3 fireTarget = firePos + Vector3.up * 0.35f;
                    Vector3 toFire = fireTarget - eyePos;
                    float dist = toFire.magnitude;

                    // Direct line-of-sight radiance calculation
                    float directRadiance = 0.0f;
                    if (dist <= radRadius)
                    {
                        bool hasLos = true;
                        if (Physics.Raycast(eyePos, toFire.normalized, out RaycastHit hit, dist, occlusionMask))
                        {
                            var hitFireplace = hit.collider.GetComponentInParent<Fireplace>();
                            bool hitSelf = (hitFireplace != null && hitFireplace == fireplace) ||
                                           (area.transform.root == hit.collider.transform.root) ||
                                           (hit.distance >= dist - 0.4f);
                            if (!hitSelf)
                            {
                                hasLos = false;
                            }
                        }

                        if (hasLos)
                        {
                            float normDist = Mathf.Clamp01(dist / radRadius);
                            directRadiance = baseWarmth * Mathf.Pow(1.0f - normDist, 2f);
                        }
                    }

                    // Enclosed room convection calculation
                    float roomWarmth = 0.0f;
                    if (inShelter && dist <= 16.0f)
                    {
                        float convNorm = Mathf.Clamp01(dist / 16.0f);
                        roomWarmth = baseWarmth * 0.70f * (1.0f - convNorm);
                    }

                    float sourceWarmth = Mathf.Max(directRadiance, roomWarmth);
                    if (sourceWarmth <= 0.01f) continue;

                    bool isConv = roomWarmth > directRadiance;
                    discoveredFires.Add(new DiscoveredFire
                    {
                        Name = isConv ? $"{friendlyName} Convection" : $"{friendlyName} Radiance",
                        Warmth = sourceWarmth,
                        IsConvection = isConv
                    });
                }
            }

            // 3. Include any active Smelter / Kiln heat sources tracked by ThermalGraphManager
            // (e.g. Charcoal Kilns that lack native EffectArea components)
            var trackedSources = ThermalGraphManager.Instance.TrackedHeatSources;
            if (trackedSources != null && trackedSources.Count > 0)
            {
                Vector3 eyePos = playerPos + Vector3.up * 1.5f;
                int occlusionMask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");

                for (int i = 0; i < trackedSources.Count; i++)
                {
                    var source = trackedSources[i];
                    if (source == null || !source.IsActive || source.SmelterRef == null || source.GameObject == null)
                    {
                        continue;
                    }

                    // Check if already accounted for via EffectArea
                    bool alreadyFound = false;
                    for (int d = 0; d < discoveredFires.Count; d++)
                    {
                        if (discoveredFires[d].Name.Contains(source.DisplayName))
                        {
                            alreadyFound = true;
                            break;
                        }
                    }
                    if (alreadyFound) continue;

                    string smName = source.DisplayName;
                    float baseWarmth = 22.0f;
                    float radRadius = 8.5f;
                    string lower = smName.ToLowerInvariant();
                    if (lower.Contains("blast"))
                    {
                        baseWarmth = 28.0f;
                        radRadius = 10.0f;
                    }
                    else if (lower.Contains("smelter"))
                    {
                        baseWarmth = 24.0f;
                        radRadius = 9.0f;
                    }

                    Vector3 fireTarget = source.Position;
                    Vector3 toFire = fireTarget - eyePos;
                    float dist = toFire.magnitude;

                    float directRadiance = 0.0f;
                    if (dist <= radRadius)
                    {
                        bool hasLos = true;
                        if (Physics.Raycast(eyePos, toFire.normalized, out RaycastHit hit, dist, occlusionMask))
                        {
                            if (!hit.collider.transform.IsChildOf(source.Transform) && hit.distance < dist - 0.4f)
                            {
                                hasLos = false;
                            }
                        }

                        if (hasLos)
                        {
                            float normDist = Mathf.Clamp01(dist / radRadius);
                            directRadiance = baseWarmth * Mathf.Pow(1.0f - normDist, 2f);
                        }
                    }

                    float roomWarmth = 0.0f;
                    if (inShelter && dist <= 16.0f)
                    {
                        float convNorm = Mathf.Clamp01(dist / 16.0f);
                        roomWarmth = baseWarmth * 0.70f * (1.0f - convNorm);
                    }

                    float sourceWarmth = Mathf.Max(directRadiance, roomWarmth);
                    if (sourceWarmth <= 0.01f) continue;

                    bool isConv = roomWarmth > directRadiance;
                    discoveredFires.Add(new DiscoveredFire
                    {
                        Name = isConv ? $"{smName} Convection" : $"{smName} Radiance",
                        Warmth = sourceWarmth,
                        IsConvection = isConv
                    });
                }
            }

            if (discoveredFires.Count == 0)
            {
                fireDesc = "";
                return 0.0f;
            }

            // Sort discovered fire sources descending by raw warmth
            discoveredFires.Sort((a, b) => b.Warmth.CompareTo(a.Warmth));

            // Apply diminishing returns stacking across multiple fires:
            // 1st: 100%, 2nd: 50%, 3rd: 25%, 4th+: 10%
            float totalWarmth = 0.0f;
            for (int i = 0; i < discoveredFires.Count; i++)
            {
                var fire = discoveredFires[i];
                float weight = i switch
                {
                    0 => 1.0f,
                    1 => 0.50f,
                    2 => 0.25f,
                    _ => 0.10f
                };

                float netContribution = fire.Warmth * weight;
                totalWarmth += netContribution;

                string plusSign = netContribution >= 0 ? "+" : "";
                fireSources.Add($"{fire.Name}: {plusSign}{netContribution:F1}°C");
            }

            float finalWarmth = Mathf.Min(40.0f, totalWarmth);

            if (discoveredFires.Count == 1)
            {
                fireDesc = discoveredFires[0].Name;
            }
            else
            {
                fireDesc = $"{discoveredFires.Count} Fire Sources";
            }

            return finalWarmth;
        }

        private static bool IsHoldingLitTorch(Player player)
        {
            if (player == null || player.IsSwimming()) return false;
            var right = player.RightItem;
            if (right != null && (right.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch || right.m_shared.m_name.ToLowerInvariant().Contains("torch")))
            {
                return true;
            }
            var left = player.LeftItem;
            if (left != null && (left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch || left.m_shared.m_name.ToLowerInvariant().Contains("torch")))
            {
                return true;
            }
            return false;
        }

        private static List<string> FormatGraphFireSources(float perceivedTemp, float directWarmth, float structureWarmth, ThermalGraphTelemetry telemetry)
        {
            var sources = new List<string>();
            float totalDelta = directWarmth + structureWarmth;
            string deltaSign = totalDelta >= 0 ? "+" : "";
            sources.Add($"Perceived Dwelling Warmth: {perceivedTemp:F1}°C ({deltaSign}{totalDelta:F1}°C)");

            if (directWarmth > 0.05f)
            {
                sources.Add($"Direct Radiant Flame: +{directWarmth:F1}°C ({telemetry.ActiveFiresCount} in line of sight)");
            }

            sources.Add($"Surrounding Field (64 Rays): {telemetry.PieceRaysHit} Pieces, {telemetry.GroundRaysHit} Ground, {telemetry.AirRaysHit} Open Air");

            if (telemetry.PieceRaysHit > 0)
            {
                sources.Add($"Visible Building Mass: Avg {telemetry.AveragePieceSurfaceTemp:F1}°C ({telemetry.PieceRaysHit}/64 field of view)");
            }

            if (telemetry.GroundRaysHit > 0)
            {
                sources.Add($"Ground / Terrain Footing: {telemetry.GroundTemp:F1}°C ({telemetry.GroundRaysHit}/64 field of view)");
            }

            if (telemetry.AirRaysHit > 0)
            {
                sources.Add($"Outdoor Open Air: {telemetry.OutdoorAirTemp:F1}°C ({telemetry.AirRaysHit}/64 field of view)");
            }

            string cycleState = telemetry.IsCycleActive ? $"Relaxing (step {telemetry.CycleStepsRemaining}, ΔT={telemetry.MaxDeltaT:F2}°C)" : "Equilibrium Reached (Sleeping)";
            sources.Add($"Building Skeleton: {telemetry.TotalNodes} total pieces in area ({cycleState})");

            return sources;
        }

        private static float GetGroundBiomeOffset(float baseTemp)
        {
            // Cold biomes (Mountain <= -5°C, Deep North): ground is geothermal/snow insulated, warmer than air (+3.5°C)
            // Temperate & warm biomes (Meadows >= 10°C, Plains, Swamp): ground is cooler than daytime air (-2.5°C)
            if (baseTemp <= -5.0f)
            {
                return 3.5f;
            }
            if (baseTemp >= 10.0f)
            {
                return -2.5f;
            }

            float t = (baseTemp - (-5.0f)) / 15.0f; // 0.0 at -5°C to 1.0 at 10°C
            return Mathf.Lerp(3.5f, -2.5f, t);
        }
    }
}
