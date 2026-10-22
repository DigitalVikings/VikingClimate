using System;
using System.Collections.Generic;
using System.Globalization;

namespace VikingClimate.Core
{
    /// <summary>
    /// Holds detailed ambient delta components for telemetry and glossary presentation.
    /// </summary>
    public struct AmbientThermalBreakdown
    {
        public string BiomeName;
        public float BiomeBaseTemp;
        public float AltitudeDelta;
        public float TimeOfDayDelta;
        public string WeatherName;
        public float WeatherDelta;
        public string SeasonName;
        public float SeasonDelta;
        public float WindChillDelta;
        public float OutdoorAmbient;
        public float GroundTemperature;
        public float FireWarmthDelta;
        public string FireSourceDescription;
        public List<string>? FireSources;
        public bool IsSheltered;
        public float RawWindSpeedKmh;
        public bool IsDwellingHeatActive;
        public VikingClimate.Features.Temperature.DwellingThermalProfile? DwellingProfile;
    }

    /// <summary>
    /// Represents the serialized survival vitals of a Player, persisted directly in Player.m_customData.
    /// </summary>
    public class SurvivalState
    {
        public const string KeyPrefix = "VikingClimate_";
        public const string KeyBodyWarmth = KeyPrefix + "BodyWarmth";
        public const string LegacyKeyBodyWarmth = "VikingSurvival_BodyWarmth";

        /// <summary>
        /// Peripheral / thermal equilibrium warmth in Celsius.
        /// 22.0°C = Comfort equilibrium; < 0°C = freezing / hypothermia; > 45°C = scorching.
        /// Persisted in Player.m_customData.
        /// </summary>
        public float BodyWarmth { get; set; } = 22.0f;

        /// <summary>
        /// Net ambient feels-like temperature in Celsius (including weather, wind chill, and fire convection).
        /// </summary>
        public float AmbientTemperature { get; set; } = 20.0f;

        /// <summary>
        /// Current gear cold insulation rating (0.0 to 0.90).
        /// </summary>
        public float InsulationRating { get; set; } = 0.0f;

        /// <summary>
        /// Active wind chill penalty in Celsius (0.0 if sheltered).
        /// </summary>
        public float WindChill { get; set; } = 0.0f;

        /// <summary>
        /// Active fire radiance / enclosed convection in Celsius.
        /// </summary>
        public float FireWarmth { get; set; } = 0.0f;

        /// <summary>
        /// Whether the player is currently under roof or in shelter.
        /// </summary>
        public bool IsInShelter { get; set; } = false;

        /// <summary>
        /// Whether the player is currently near an active fireplace/hearth.
        /// </summary>
        public bool IsNearFire { get; set; } = false;

        /// <summary>
        /// Current rate of body temperature change in °C/s (signed: positive for warming, negative for cooling).
        /// </summary>
        public float TemperatureVelocity { get; set; } = 0.0f;

        /// <summary>
        /// Dynamic conduction rate in [0, 1] resulting from wetness, wind, insulation, and metabolic stabilization.
        /// </summary>
        public float ConductionRate { get; set; } = 0.0f;

        /// <summary>
        /// True if the player is currently resting (sitting, sheltered near fire, or sleeping).
        /// </summary>
        public bool IsResting { get; set; } = false;

        /// <summary>
        /// True if the body temperature is currently reverting towards comfort equilibrium (22°C).
        /// </summary>
        public bool IsReverting { get; set; } = false;

        /// <summary>
        /// Active multiplier from wetness or swimming (e.g. 1.8x wet, 3.0x swimming).
        /// </summary>
        public float WetMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Active multiplier from wind exposure when outside shelter (1.0x to 2.2x).
        /// </summary>
        public float WindMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Active multiplier from cold gear insulation when cooling down (e.g. 0.35 for 65% insulation).
        /// </summary>
        public float InsulationMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Whether frost resistance mead is currently active.
        /// </summary>
        public bool HasFrostMead { get; set; } = false;

        /// <summary>
        /// Detailed itemization of all positive and negative ambient contributors.
        /// </summary>
        public AmbientThermalBreakdown Breakdown;

        public void LoadFromPlayer(Player player)
        {
            if (player?.m_customData == null) return;

            if ((player.m_customData.TryGetValue(KeyBodyWarmth, out string? warmthStr) ||
                 player.m_customData.TryGetValue(LegacyKeyBodyWarmth, out warmthStr)) &&
                float.TryParse(warmthStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float warmth))
            {
                BodyWarmth = warmth;
            }
        }

        public void SaveToPlayer(Player player)
        {
            if (player?.m_customData == null) return;

            player.m_customData[KeyBodyWarmth] = BodyWarmth.ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
