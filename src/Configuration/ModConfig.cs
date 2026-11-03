using BepInEx.Configuration;
using UnityEngine;

namespace VikingClimate.Configuration
{
    public static class ModConfig
    {
        // 01 - General
        public static ConfigEntry<bool> ModEnabled { get; private set; } = null!;
        public static ConfigEntry<bool> EnableLogging { get; private set; } = null!;

        // 02 - Comfort & Resting
        public static ConfigEntry<float> ComfortMinTemp { get; private set; } = null!;
        public static ConfigEntry<float> ComfortMaxTemp { get; private set; } = null!;
        public static ConfigEntry<bool> EnableStructureComfort { get; private set; } = null!;
        public static ConfigEntry<bool> EnableThermalResting { get; private set; } = null!;
        public static ConfigEntry<bool> RequireWarmthToSleep { get; private set; } = null!;
        public static ConfigEntry<float> RestingMinTemp { get; private set; } = null!;
        public static ConfigEntry<float> RestingMaxTemp { get; private set; } = null!;
        public static ConfigEntry<float> RestingActivationDelay { get; private set; } = null!;
        public static ConfigEntry<float> RestedDelaySeconds { get; private set; } = null!;

        // 03 - Body Thermodynamics
        public static ConfigEntry<float> BaseCoolingRate { get; private set; } = null!;
        public static ConfigEntry<float> ThermalGradientElasticity { get; private set; } = null!;
        public static ConfigEntry<float> ReversionSpeedMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> WetCoolingMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> SwimCoolingMultiplier { get; private set; } = null!;

        // 04 - Cold Stages & Debuffs
        public static ConfigEntry<float> ChillyStaminaRegenModifier { get; private set; } = null!;
        public static ConfigEntry<float> ChillyDamageMultiplier { get; private set; } = null!;

        public static ConfigEntry<float> BitterColdStaminaRegenModifier { get; private set; } = null!;
        public static ConfigEntry<float> BitterColdDamageMultiplier { get; private set; } = null!;

        public static ConfigEntry<float> NumbingFrostStaminaRegenModifier { get; private set; } = null!;
        public static ConfigEntry<float> NumbingFrostDamageMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> NumbingFrostAttackSpeedMultiplier { get; private set; } = null!;

        public static ConfigEntry<float> HypothermiaStaminaRegenModifier { get; private set; } = null!;
        public static ConfigEntry<float> HypothermiaDamageMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> HypothermiaAttackSpeedMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> HypothermiaHealthRegenModifier { get; private set; } = null!;
        public static ConfigEntry<float> HypothermiaEitrRegenModifier { get; private set; } = null!;

        // 05 - Heat Stages & Debuffs
        public static ConfigEntry<float> WarmSpeedMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> WarmAttackSpeedMultiplier { get; private set; } = null!;

        public static ConfigEntry<float> HeatExhaustionSpeedMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> HeatExhaustionAttackSpeedMultiplier { get; private set; } = null!;

        public static ConfigEntry<float> SevereHyperthermiaSpeedMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> SevereHyperthermiaAttackSpeedMultiplier { get; private set; } = null!;

        public static ConfigEntry<float> CatastrophicScorchingSpeedMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> CatastrophicScorchingAttackSpeedMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> CatastrophicScorchingStaminaRegenModifier { get; private set; } = null!;

        // 06 - Consequences & Damage
        public static ConfigEntry<float> HypothermiaDamageRate { get; private set; } = null!;
        public static ConfigEntry<float> HyperthermiaDamageRate { get; private set; } = null!;

        // 07 - Environmental & Shelter
        public static ConfigEntry<float> ThermalSamplingInterval { get; private set; } = null!;
        public static ConfigEntry<float> EnclosureShelterThreshold { get; private set; } = null!;
        public static ConfigEntry<float> InsulationUnroofedFactor { get; private set; } = null!;

        // 08 - Structural Thermal Graph
        public static ConfigEntry<ThermalMassMode> ThermalMassSimulationMode { get; private set; } = null!;
        public static ConfigEntry<float> DirectFlameWarmthCoeff { get; private set; } = null!;
        public static ConfigEntry<float> DirectFlameWarmthMax { get; private set; } = null!;
        public static ConfigEntry<float> ShelterAirLossBeta { get; private set; } = null!;
        public static ConfigEntry<OfflineThermalMode> OfflineThermalBehavior { get; private set; } = null!;
        public static ConfigEntry<float> StructuralRelaxationRate { get; private set; } = null!;

        // 09 - Heat Raycasting
        public static ConfigEntry<bool> EnableHeatRaycasting { get; private set; } = null!;
        public static ConfigEntry<float> FireRayRange { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerCampfire { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerHearth { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerStandingBrazier { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerHangingBrazier { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerBonfire { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerTorch { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerSmelter { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerKiln { get; private set; } = null!;
        public static ConfigEntry<float> RaycastPowerBlastFurnace { get; private set; } = null!;

        // Backward compatibility accessors
        public static ConfigEntry<float> FireOutputCampfire => RaycastPowerCampfire;
        public static ConfigEntry<float> FireOutputHearth => RaycastPowerHearth;
        public static ConfigEntry<float> FireOutputBrazier => RaycastPowerStandingBrazier;
        public static ConfigEntry<float> FireOutputBonfire => RaycastPowerBonfire;
        public static ConfigEntry<float> FireOutputTorch => RaycastPowerTorch;
        public static ConfigEntry<float> FireOutputSmelter => RaycastPowerSmelter;
        public static ConfigEntry<float> FireOutputKiln => RaycastPowerKiln;
        public static ConfigEntry<float> FireOutputBlastFurnace => RaycastPowerBlastFurnace;

        // 10 - Heat Convection
        public static ConfigEntry<bool> EnableHeatConvection { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveCampfireTotalWatts { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveCampfireMaxWattsPerPiece { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveCampfireRadius { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveHearthTotalWatts { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveHearthMaxWattsPerPiece { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveHearthRadius { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveStandingBrazierTotalWatts { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveStandingBrazierMaxWattsPerPiece { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveStandingBrazierRadius { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveHangingBrazierTotalWatts { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveHangingBrazierMaxWattsPerPiece { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveHangingBrazierRadius { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveBonfireTotalWatts { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveBonfireMaxWattsPerPiece { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveBonfireRadius { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveIndustrialTotalWatts { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveIndustrialMaxWattsPerPiece { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveIndustrialRadius { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveTorchTotalWatts { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveTorchMaxWattsPerPiece { get; private set; } = null!;
        public static ConfigEntry<float> ConvectiveTorchRadius { get; private set; } = null!;

        // 11 - Material Physical Properties
        // Stone
        public static ConfigEntry<float> MatStoneHeatCapacity { get; private set; } = null!;
        public static ConfigEntry<float> MatStoneAirLoss { get; private set; } = null!;
        public static ConfigEntry<float> MatStoneConductivity { get; private set; } = null!;
        public static ConfigEntry<float> MatStoneAbsorption { get; private set; } = null!;

        // Wood
        public static ConfigEntry<float> MatWoodHeatCapacity { get; private set; } = null!;
        public static ConfigEntry<float> MatWoodAirLoss { get; private set; } = null!;
        public static ConfigEntry<float> MatWoodConductivity { get; private set; } = null!;
        public static ConfigEntry<float> MatWoodAbsorption { get; private set; } = null!;

        // CoreWood
        public static ConfigEntry<float> MatCoreWoodHeatCapacity { get; private set; } = null!;
        public static ConfigEntry<float> MatCoreWoodAirLoss { get; private set; } = null!;
        public static ConfigEntry<float> MatCoreWoodConductivity { get; private set; } = null!;
        public static ConfigEntry<float> MatCoreWoodAbsorption { get; private set; } = null!;

        // Metal
        public static ConfigEntry<float> MatMetalHeatCapacity { get; private set; } = null!;
        public static ConfigEntry<float> MatMetalAirLoss { get; private set; } = null!;
        public static ConfigEntry<float> MatMetalConductivity { get; private set; } = null!;
        public static ConfigEntry<float> MatMetalAbsorption { get; private set; } = null!;

        // Crystal / Glass
        public static ConfigEntry<float> MatCrystalHeatCapacity { get; private set; } = null!;
        public static ConfigEntry<float> MatCrystalAirLoss { get; private set; } = null!;
        public static ConfigEntry<float> MatCrystalConductivity { get; private set; } = null!;
        public static ConfigEntry<float> MatCrystalAbsorption { get; private set; } = null!;

        // 12 - UI & Thermometer HUD
        public static ConfigEntry<bool> ShowThermometerHUD { get; private set; } = null!;
        public static ConfigEntry<bool> UseCustomPosition { get; private set; } = null!;
        public static ConfigEntry<float> ThermometerPositionX { get; private set; } = null!;
        public static ConfigEntry<float> ThermometerPositionY { get; private set; } = null!;
        public static ConfigEntry<float> ThermometerScale { get; private set; } = null!;
        public static ConfigEntry<bool> EnableScreenVignettes { get; private set; } = null!;
        public static ConfigEntry<float> FrostVignetteStartTemp { get; private set; } = null!;
        public static ConfigEntry<float> FrostVignetteMaxAlpha { get; private set; } = null!;
        public static ConfigEntry<float> HeatVignetteStartTemp { get; private set; } = null!;
        public static ConfigEntry<float> HeatVignetteMaxAlpha { get; private set; } = null!;

        // 13 - Compatibility
        public static ConfigEntry<bool> EnableSeasonIntegration { get; private set; } = null!;

        // 14 - Inspection & Debugging
        public static ConfigEntry<bool> HighlightPieceInsulation { get; private set; } = null!;
        public static ConfigEntry<bool> InstantGraphEquilibrium { get; private set; } = null!;
        public static ConfigEntry<bool> DebugFreezeAmbientTemp { get; private set; } = null!;
        public static ConfigEntry<float> DebugForcedAmbientTemp { get; private set; } = null!;
        public static ConfigEntry<bool> DebugFreezeStructureTemps { get; private set; } = null!;
        public static ConfigEntry<float> DebugForcedStructureTemp { get; private set; } = null!;
        public static ConfigEntry<bool> DebugInstantConduction { get; private set; } = null!;

        public static ConfigEntry<bool> ShowPieceHoverDebug { get; private set; } = null!;
        public static ConfigEntry<bool> HighlightConnectedPieces { get; private set; } = null!;
        public static ConfigEntry<bool> InspectAggregatedGraph { get; private set; } = null!;
        public static ConfigEntry<KeyCode> TogglePieceHoverHotkey { get; private set; } = null!;
        public static ConfigEntry<KeyCode> ToggleInspectionModeHotkey { get; private set; } = null!;
        public static ConfigEntry<KeyCode> LockHeatSourceHotkey { get; private set; } = null!;
        public static ConfigEntry<float> PieceHoverOffsetX { get; private set; } = null!;
        public static ConfigEntry<float> PieceHoverOffsetY { get; private set; } = null!;

        private static readonly System.Collections.Generic.HashSet<ConfigDefinition> s_boundDefinitions = new System.Collections.Generic.HashSet<ConfigDefinition>();

        public static void Init(ConfigFile config)
        {
            // 01 - General
            ModEnabled = Bind(
                config,
                "01 - General",
                "Enabled",
                true,
                "Master switch to enable or disable all features of VikingClimate.",
                order: 100,
                isAdminOnly: true,
                legacySection: "1 - General"
            );

            EnableLogging = Bind(
                config,
                "01 - General",
                "EnableLogging",
                false,
                "Enable verbose diagnostic logging for temperature calculations.",
                order: 90,
                isAdminOnly: false,
                legacySection: "1 - General"
            );

            // 02 - Comfort & Resting
            ComfortMinTemp = Bind(
                config,
                "02 - Comfort & Resting",
                "ComfortMinTemp",
                18.0f,
                "Lower bound in Celsius for the player's thermal comfort equilibrium.",
                order: 90,
                new AcceptableValueRange<float>(-50.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            ComfortMaxTemp = Bind(
                config,
                "02 - Comfort & Resting",
                "ComfortMaxTemp",
                26.0f,
                "Upper bound in Celsius for the player's thermal comfort equilibrium.",
                order: 89,
                new AcceptableValueRange<float>(-50.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            EnableStructureComfort = Bind(
                config,
                "02 - Comfort & Resting",
                "EnableStructureComfort",
                true,
                "When enabled, comfort items (beds, chairs, rugs, banners, hearths) propagate across the entire connected building structure instead of a 10m sphere.",
                order: 80,
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            EnableThermalResting = Bind(
                config,
                "02 - Comfort & Resting",
                "EnableThermalResting",
                true,
                "When enabled, entering and maintaining the Resting state requires a comfortable dwelling temperature [RestingMinTemp, RestingMaxTemp] instead of vanilla's binary campfire proximity.",
                order: 79,
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            RequireWarmthToSleep = Bind(
                config,
                "02 - Comfort & Resting",
                "RequireWarmthToSleep",
                false,
                "When enabled, sleeping in a bed is blocked if the player is cold (body temperature below ComfortMinTemp). Disabled by default.",
                order: 78,
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            RestingMinTemp = Bind(
                config,
                "02 - Comfort & Resting",
                "RestingMinTemp",
                18.0f,
                "Minimum ambient temperature in Celsius required to enter and maintain the Resting state inside dwellings without a direct campfire.",
                order: 70,
                new AcceptableValueRange<float>(-50.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            RestingMaxTemp = Bind(
                config,
                "02 - Comfort & Resting",
                "RestingMaxTemp",
                28.0f,
                "Maximum ambient temperature in Celsius before excessive heat disrupts the Resting state.",
                order: 69,
                new AcceptableValueRange<float>(-50.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            RestingActivationDelay = Bind(
                config,
                "02 - Comfort & Resting",
                "RestingActivationDelay",
                2.5f,
                "Delay in seconds of continuously staying in comfortable dwelling temperature before the Resting state begins. Prevents Resting from flickering when running through doorways.",
                order: 60,
                new AcceptableValueRange<float>(0.0f, 60.0f),
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            RestedDelaySeconds = Bind(
                config,
                "02 - Comfort & Resting",
                "RestedDelaySeconds",
                10.0f,
                "Duration in seconds the player must remain in the Resting state before the Rested status effect is granted or refreshed.",
                order: 59,
                new AcceptableValueRange<float>(0.0f, 120.0f),
                isAdminOnly: true,
                legacySection: "2 - Player Comfort & Thresholds"
            );

            // 03 - Body Thermodynamics
            BaseCoolingRate = Bind(
                config,
                "03 - Body Thermodynamics",
                "BaseCoolingRate",
                0.005f,
                "Base rate per second at which player body warmth conducts towards ambient temperature.",
                order: 80,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "3 - Player Body Thermodynamics"
            );

            ThermalGradientElasticity = Bind(
                config,
                "03 - Body Thermodynamics",
                "ThermalGradientElasticity",
                1.0f,
                "Exponent (POW) in the conduction equation: dT/dt = Rate * |T_amb - T|^POW. Default 1.0 represents standard linear Newton conduction.",
                order: 78,
                new AcceptableValueRange<float>(0.1f, 5.0f),
                isAdminOnly: true,
                legacySection: "3 - Player Body Thermodynamics"
            );

            ReversionSpeedMultiplier = Bind(
                config,
                "03 - Body Thermodynamics",
                "ReversionSpeedMultiplier",
                25.0f,
                "Multiplier accelerating heat conduction when body temperature is reverting towards comfort equilibrium (22°C) while resting or sleeping.",
                order: 76,
                new AcceptableValueRange<float>(0.1f, 100.0f),
                isAdminOnly: true,
                legacySection: "3 - Player Body Thermodynamics"
            );

            WetCoolingMultiplier = Bind(
                config,
                "03 - Body Thermodynamics",
                "WetCoolingMultiplier",
                2.0f,
                "Conductivity multiplier applied to cooling when player is wet from rain or splash.",
                order: 65,
                new AcceptableValueRange<float>(1.0f, 20.0f),
                isAdminOnly: true,
                legacySection: "3 - Player Body Thermodynamics"
            );

            SwimCoolingMultiplier = Bind(
                config,
                "03 - Body Thermodynamics",
                "SwimCoolingMultiplier",
                3.0f,
                "Conductivity multiplier applied to cooling when player is submerged and swimming.",
                order: 60,
                new AcceptableValueRange<float>(1.0f, 20.0f),
                isAdminOnly: true,
                legacySection: "3 - Player Body Thermodynamics"
            );

            // 04 - Cold Stages & Debuffs
            ChillyStaminaRegenModifier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "ChillyStaminaRegenModifier",
                -0.1f,
                "Additive stamina regeneration modifier while Chilly (-0.15 = -15%).",
                order: 50,
                new AcceptableValueRange<float>(-1.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            ChillyDamageMultiplier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "ChillyDamageMultiplier",
                0.90f,
                "Physical and elemental damage multiplier while Chilly (0.90 = 90% normal damage).",
                order: 49,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            BitterColdStaminaRegenModifier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "BitterColdStaminaRegenModifier",
                -0.30f,
                "Additive stamina regeneration modifier while experiencing Bitter Cold (-0.30 = -30%).",
                order: 40,
                new AcceptableValueRange<float>(-1.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            BitterColdDamageMultiplier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "BitterColdDamageMultiplier",
                0.80f,
                "Physical and elemental damage multiplier while experiencing Bitter Cold (0.80 = 80% normal damage).",
                order: 39,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            NumbingFrostStaminaRegenModifier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "NumbingFrostStaminaRegenModifier",
                -0.45f,
                "Additive stamina regeneration modifier while suffering from Numbing Frost (-0.45 = -45%).",
                order: 30,
                new AcceptableValueRange<float>(-1.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            NumbingFrostDamageMultiplier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "NumbingFrostDamageMultiplier",
                0.65f,
                "Physical and elemental damage multiplier while suffering from Numbing Frost (0.65 = 65% normal damage).",
                order: 29,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            NumbingFrostAttackSpeedMultiplier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "NumbingFrostAttackSpeedMultiplier",
                0.85f,
                "Attack and weapon swing speed multiplier while suffering from Numbing Frost (0.85 = 85% normal speed).",
                order: 28,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            HypothermiaStaminaRegenModifier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "HypothermiaStaminaRegenModifier",
                -0.70f,
                "Additive stamina regeneration modifier while in catastrophic Hypothermia (-0.70 = -70%).",
                order: 20,
                new AcceptableValueRange<float>(-1.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            HypothermiaDamageMultiplier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "HypothermiaDamageMultiplier",
                0.40f,
                "Physical and elemental damage multiplier while in catastrophic Hypothermia (0.40 = 40% normal damage).",
                order: 19,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            HypothermiaAttackSpeedMultiplier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "HypothermiaAttackSpeedMultiplier",
                0.70f,
                "Attack and weapon swing speed multiplier while in catastrophic Hypothermia (0.70 = 70% normal speed).",
                order: 18,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            HypothermiaHealthRegenModifier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "HypothermiaHealthRegenModifier",
                0.0f,
                "Additive health regeneration modifier while in catastrophic Hypothermia (0.0 = vanilla baseline / unmodified, -1.0 = stops natural health regeneration).",
                order: 17,
                new AcceptableValueRange<float>(-1.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            HypothermiaEitrRegenModifier = Bind(
                config,
                "04 - Cold Stages & Debuffs",
                "HypothermiaEitrRegenModifier",
                0.0f,
                "Additive Eitr regeneration modifier while in catastrophic Hypothermia (0.0 = vanilla baseline / unmodified, -0.5 = -50%).",
                order: 16,
                new AcceptableValueRange<float>(-1.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "Cold Debuffs"
            );

            // 05 - Heat Stages & Debuffs
            WarmSpeedMultiplier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "WarmSpeedMultiplier",
                0.95f,
                "Locomotion movement speed multiplier (walk, jog, run) while Warm (0.95 = 95% normal speed).",
                order: 40,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            WarmAttackSpeedMultiplier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "WarmAttackSpeedMultiplier",
                0.95f,
                "Attack and tool swing speed multiplier while Warm (0.95 = 95% normal speed).",
                order: 39,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            HeatExhaustionSpeedMultiplier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "HeatExhaustionSpeedMultiplier",
                0.80f,
                "Locomotion movement speed multiplier (walk, jog, run) while experiencing Heat Exhaustion (0.80 = 80% normal speed).",
                order: 30,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            HeatExhaustionAttackSpeedMultiplier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "HeatExhaustionAttackSpeedMultiplier",
                0.80f,
                "Attack and tool swing speed multiplier while experiencing Heat Exhaustion (0.80 = 80% normal speed).",
                order: 29,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            SevereHyperthermiaSpeedMultiplier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "SevereHyperthermiaSpeedMultiplier",
                0.65f,
                "Locomotion movement speed multiplier (walk, jog, run) while suffering from Severe Hyperthermia (0.65 = 65% normal speed).",
                order: 20,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            SevereHyperthermiaAttackSpeedMultiplier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "SevereHyperthermiaAttackSpeedMultiplier",
                0.65f,
                "Attack and tool swing speed multiplier while suffering from Severe Hyperthermia (0.65 = 65% normal speed).",
                order: 19,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            CatastrophicScorchingSpeedMultiplier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "CatastrophicScorchingSpeedMultiplier",
                0.50f,
                "Locomotion movement speed multiplier (walk, jog, run) while in Catastrophic Scorching (0.50 = 50% normal speed).",
                order: 10,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            CatastrophicScorchingAttackSpeedMultiplier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "CatastrophicScorchingAttackSpeedMultiplier",
                0.50f,
                "Attack and tool swing speed multiplier while in Catastrophic Scorching (0.50 = 50% normal speed).",
                order: 9,
                new AcceptableValueRange<float>(0.0f, 2.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            CatastrophicScorchingStaminaRegenModifier = Bind(
                config,
                "05 - Heat Stages & Debuffs",
                "CatastrophicScorchingStaminaRegenModifier",
                0.0f,
                "Additive stamina regeneration modifier during Catastrophic Scorching (0.0 = unmodified, -0.50 = -50%).",
                order: 8,
                new AcceptableValueRange<float>(-1.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "Heat Debuffs"
            );

            // 06 - Consequences & Damage
            HypothermiaDamageRate = Bind(
                config,
                "06 - Consequences & Damage",
                "HypothermiaDamageRate",
                1.5f,
                "Frost damage per second dealt during catastrophic hypothermia (< -12°C).",
                order: 20,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "8 - Consequence & Damage"
            );

            HyperthermiaDamageRate = Bind(
                config,
                "06 - Consequences & Damage",
                "HyperthermiaDamageRate",
                2.0f,
                "Fire damage per second dealt during catastrophic scorching (> 55°C).",
                order: 18,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "8 - Consequence & Damage"
            );

            // 07 - Environmental & Shelter
            ThermalSamplingInterval = Bind(
                config,
                "07 - Environmental & Shelter",
                "ThermalSamplingInterval",
                4.0f,
                "Interval in seconds between full 64-ray environmental, shelter, and thermal mass scans around the player. Continuous interpolation keeps ambient and body temperatures smooth between scans.",
                order: 30,
                new AcceptableValueRange<float>(0.1f, 60.0f),
                isAdminOnly: true,
                legacySection: "4 - Environmental Scanning & Shelter"
            );

            EnclosureShelterThreshold = Bind(
                config,
                "07 - Environmental & Shelter",
                "EnclosureShelterThreshold",
                0.80f,
                "Fraction of the 64-ray surrounding sphere that must be shielded/enclosed by structural pieces or roof for the player to be considered inside an enclosed compound/dwelling.",
                order: 20,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "4 - Environmental Scanning & Shelter"
            );

            InsulationUnroofedFactor = Bind(
                config,
                "07 - Environmental & Shelter",
                "InsulationUnroofedFactor",
                0.40f,
                "Insulation factor multiplier applied to pieces that have walls around them but lack a direct roof overhead.",
                order: 10,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "4 - Environmental Scanning & Shelter"
            );

            // 08 - Structural Thermal Graph
            ThermalMassSimulationMode = Bind(
                config,
                "08 - Structural Thermal Graph",
                "ThermalMassSimulationMode",
                ThermalMassMode.StructuralGraph,
                "Dwelling heat simulation engine. StructuralGraph uses forward thermal propagation and structural graph conduction across snapped pieces.",
                order: 100,
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacySection2: "6 - Structural Thermal Graph"
            );

            DirectFlameWarmthCoeff = Bind(
                config,
                "08 - Structural Thermal Graph",
                "DirectFlameWarmthCoeff",
                0.008f,
                "Scaling factor determining how much direct infrared skin warmth the player feels per Watt from visible burning flames at distance.",
                order: 80,
                new AcceptableValueRange<float>(0.0f, 0.50f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacySection2: "6 - Structural Thermal Graph"
            );

            DirectFlameWarmthMax = Bind(
                config,
                "08 - Structural Thermal Graph",
                "DirectFlameWarmthMax",
                18.0f,
                "Maximum perceived warmth bonus in Celsius granted by direct line-of-sight flame exposure.",
                order: 75,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacySection2: "6 - Structural Thermal Graph"
            );

            ShelterAirLossBeta = Bind(
                config,
                "08 - Structural Thermal Graph",
                "ShelterAirLossBeta",
                1.0f,
                "Scale coefficient (beta) for the shelter air loss reduction: loss = max(0, 1 - shelter / beta). Default 1.0 represents standard linear scaling from 100% loss down to 0% at full shelter.",
                order: 70,
                new AcceptableValueRange<float>(0.10f, 2.0f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacySection2: "6 - Structural Thermal Graph"
            );

            OfflineThermalBehavior = Bind(
                config,
                "08 - Structural Thermal Graph",
                "OfflineThermalBehavior",
                OfflineThermalMode.Freeze,
                "Determines how building temperatures evolve when no player is nearby (Freeze preserves temperatures; DecayWhenUnlit cools unheated buildings; AlwaysDecay constantly decays).",
                order: 65,
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacySection2: "6 - Structural Thermal Graph"
            );

            StructuralRelaxationRate = Bind(
                config,
                "08 - Structural Thermal Graph",
                "StructuralRelaxationRate",
                1.0f,
                "Simulation time step (dt) multiplier in seconds for structural relaxation iterations. Higher values accelerate heat diffusion.",
                order: 60,
                new AcceptableValueRange<float>(0.01f, 10.0f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacySection2: "6 - Structural Thermal Graph"
            );

            // 09 - Heat Raycasting
            EnableHeatRaycasting = Bind(
                config,
                "09 - Heat Raycasting",
                "EnableHeatRaycasting",
                false,
                "Master switch to enable or disable the legacy 64-direction Fibonacci heat raycasting engine. Can run independently or simultaneously with heat convection.",
                order: 100,
                isAdminOnly: true
            );

            FireRayRange = Bind(
                config,
                "09 - Heat Raycasting",
                "FireRayRange",
                16.0f,
                "Maximum radiant sightline range in meters across which heat rays propagate from active fire sources to building pieces.",
                order: 95,
                new AcceptableValueRange<float>(1.0f, 64.0f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacyKey: "FireRayRange",
                legacySection2: "6 - Structural Thermal Graph"
            );

            RaycastPowerCampfire = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_Campfire",
                800.0f,
                "Radiant thermal energy output (Watts) emitted by standard campfires under the raycasting engine.",
                order: 80,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Campfire",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            RaycastPowerHearth = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_Hearth",
                3200.0f,
                "Radiant thermal energy output (Watts) emitted by stone hearths under the raycasting engine.",
                order: 75,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Hearth",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            RaycastPowerStandingBrazier = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_StandingBrazier",
                400.0f,
                "Radiant thermal energy output (Watts) emitted by standing floor braziers under the raycasting engine.",
                order: 70,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Brazier",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            RaycastPowerHangingBrazier = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_HangingBrazier",
                400.0f,
                "Radiant thermal energy output (Watts) emitted by ceiling-hanging braziers under the raycasting engine.",
                order: 65,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Brazier",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            RaycastPowerBonfire = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_Bonfire",
                1600.0f,
                "Radiant thermal energy output (Watts) emitted by large outdoor bonfires under the raycasting engine.",
                order: 60,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Bonfire",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            RaycastPowerTorch = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_Torch",
                50.0f,
                "Radiant thermal energy output (Watts) emitted by standing wood/iron torches and wall sconces under the raycasting engine.",
                order: 55,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Torch",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            RaycastPowerSmelter = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_Smelter",
                2400.0f,
                "Radiant thermal energy output (Watts) emitted by metallurgical smelters while actively smelting ore under the raycasting engine.",
                order: 50,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Smelter",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            RaycastPowerKiln = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_CharcoalKiln",
                2400.0f,
                "Radiant thermal energy output (Watts) emitted by charcoal kilns while burning wood under the raycasting engine.",
                order: 45,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_CharcoalKiln",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            RaycastPowerBlastFurnace = Bind(
                config,
                "09 - Heat Raycasting",
                "RaycastPower_BlastFurnace",
                4000.0f,
                "Radiant thermal energy output (Watts) emitted by high-temperature blast furnaces under the raycasting engine.",
                order: 40,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_BlastFurnace",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            // 10 - Heat Convection
            EnableHeatConvection = Bind(
                config,
                "10 - Heat Convection",
                "EnableHeatConvection",
                true,
                "Master switch to enable or disable the proximity-based convective heat propagation engine. Distributes heat evenly to nearby unoccluded building pieces with per-piece absorption limits.",
                order: 100,
                isAdminOnly: true
            );

            // Campfire
            ConvectiveCampfireTotalWatts = Bind(
                config,
                "10 - Heat Convection",
                "Campfire_TotalWatts",
                800.0f,
                "Total convective thermal output (Watts) budget of standard campfires.",
                order: 90,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Campfire",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            ConvectiveCampfireMaxWattsPerPiece = Bind(
                config,
                "10 - Heat Convection",
                "Campfire_MaxWattsPerPiece",
                25.0f,
                "Maximum thermal energy (Watts) any single piece can absorb from a standard campfire, preventing small huts from instantly boiling.",
                order: 89,
                new AcceptableValueRange<float>(1.0f, 500.0f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacyKey: "ConvectiveMaxWattsPerPiece"
            );

            ConvectiveCampfireRadius = Bind(
                config,
                "10 - Heat Convection",
                "Campfire_Radius",
                9.0f,
                "Convective heat reach radius in meters for standard campfires.",
                order: 88,
                new AcceptableValueRange<float>(1.0f, 30.0f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacyKey: "ConvectiveRadiusCampfire"
            );

            // Hearth
            ConvectiveHearthTotalWatts = Bind(
                config,
                "10 - Heat Convection",
                "Hearth_TotalWatts",
                3200.0f,
                "Total convective thermal output (Watts) budget of large stone hearths.",
                order: 80,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Hearth",
                legacySection2: "5 - Heat Sources (Watts)"
            );

            ConvectiveHearthMaxWattsPerPiece = Bind(
                config,
                "10 - Heat Convection",
                "Hearth_MaxWattsPerPiece",
                50.0f,
                "Maximum thermal energy (Watts) any single piece can absorb from a stone hearth.",
                order: 79,
                new AcceptableValueRange<float>(1.0f, 500.0f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacyKey: "ConvectiveHearthMaxWattsPerPiece"
            );

            ConvectiveHearthRadius = Bind(
                config,
                "10 - Heat Convection",
                "Hearth_Radius",
                15.0f,
                "Convective heat reach radius in meters for stone hearths.",
                order: 78,
                new AcceptableValueRange<float>(1.0f, 50.0f),
                isAdminOnly: true,
                legacySection: "09 - Structural Thermal Graph",
                legacyKey: "ConvectiveRadiusHearth"
            );

            // Standing Brazier
            ConvectiveStandingBrazierTotalWatts = Bind(
                config,
                "10 - Heat Convection",
                "StandingBrazier_TotalWatts",
                600.0f,
                "Total convective thermal output (Watts) budget of standing floor braziers.",
                order: 70,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Brazier"
            );

            ConvectiveStandingBrazierMaxWattsPerPiece = Bind(
                config,
                "10 - Heat Convection",
                "StandingBrazier_MaxWattsPerPiece",
                30.0f,
                "Maximum thermal energy (Watts) any single piece can absorb from a standing brazier.",
                order: 69,
                new AcceptableValueRange<float>(1.0f, 500.0f),
                isAdminOnly: true
            );

            ConvectiveStandingBrazierRadius = Bind(
                config,
                "10 - Heat Convection",
                "StandingBrazier_Radius",
                7.0f,
                "Convective heat reach radius in meters for standing braziers.",
                order: 68,
                new AcceptableValueRange<float>(1.0f, 30.0f),
                isAdminOnly: true
            );

            // Hanging Brazier
            ConvectiveHangingBrazierTotalWatts = Bind(
                config,
                "10 - Heat Convection",
                "HangingBrazier_TotalWatts",
                600.0f,
                "Total convective thermal output (Watts) budget of ceiling-hanging braziers.",
                order: 60,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Brazier"
            );

            ConvectiveHangingBrazierMaxWattsPerPiece = Bind(
                config,
                "10 - Heat Convection",
                "HangingBrazier_MaxWattsPerPiece",
                30.0f,
                "Maximum thermal energy (Watts) any single piece can absorb from a hanging brazier.",
                order: 59,
                new AcceptableValueRange<float>(1.0f, 500.0f),
                isAdminOnly: true
            );

            ConvectiveHangingBrazierRadius = Bind(
                config,
                "10 - Heat Convection",
                "HangingBrazier_Radius",
                7.0f,
                "Convective heat reach radius in meters for hanging braziers.",
                order: 58,
                new AcceptableValueRange<float>(1.0f, 30.0f),
                isAdminOnly: true
            );

            // Bonfire
            ConvectiveBonfireTotalWatts = Bind(
                config,
                "10 - Heat Convection",
                "Bonfire_TotalWatts",
                2000.0f,
                "Total convective thermal output (Watts) budget of large outdoor bonfires.",
                order: 50,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Bonfire"
            );

            ConvectiveBonfireMaxWattsPerPiece = Bind(
                config,
                "10 - Heat Convection",
                "Bonfire_MaxWattsPerPiece",
                60.0f,
                "Maximum thermal energy (Watts) any single piece can absorb from a bonfire.",
                order: 49,
                new AcceptableValueRange<float>(1.0f, 500.0f),
                isAdminOnly: true
            );

            ConvectiveBonfireRadius = Bind(
                config,
                "10 - Heat Convection",
                "Bonfire_Radius",
                14.0f,
                "Convective heat reach radius in meters for outdoor bonfires.",
                order: 48,
                new AcceptableValueRange<float>(1.0f, 50.0f),
                isAdminOnly: true
            );

            // Industrial (Smelter, Kiln, Blast Furnace)
            ConvectiveIndustrialTotalWatts = Bind(
                config,
                "10 - Heat Convection",
                "Industrial_TotalWatts",
                3000.0f,
                "Total convective thermal output (Watts) budget of active smelters, kilns, and blast furnaces.",
                order: 40,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Smelter"
            );

            ConvectiveIndustrialMaxWattsPerPiece = Bind(
                config,
                "10 - Heat Convection",
                "Industrial_MaxWattsPerPiece",
                60.0f,
                "Maximum thermal energy (Watts) any single piece can absorb from industrial furnaces.",
                order: 39,
                new AcceptableValueRange<float>(1.0f, 500.0f),
                isAdminOnly: true
            );

            ConvectiveIndustrialRadius = Bind(
                config,
                "10 - Heat Convection",
                "Industrial_Radius",
                12.0f,
                "Convective heat reach radius in meters for industrial smelters, kilns, and blast furnaces.",
                order: 38,
                new AcceptableValueRange<float>(1.0f, 50.0f),
                isAdminOnly: true
            );

            // Torch
            ConvectiveTorchTotalWatts = Bind(
                config,
                "10 - Heat Convection",
                "Torch_TotalWatts",
                50.0f,
                "Total convective thermal output (Watts) budget of standing torches and wall sconces.",
                order: 30,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true,
                legacySection: "08 - Heat Sources (Watts)",
                legacyKey: "FireOutput_Torch"
            );

            ConvectiveTorchMaxWattsPerPiece = Bind(
                config,
                "10 - Heat Convection",
                "Torch_MaxWattsPerPiece",
                10.0f,
                "Maximum thermal energy (Watts) any single piece can absorb from a torch or sconce.",
                order: 29,
                new AcceptableValueRange<float>(1.0f, 500.0f),
                isAdminOnly: true
            );

            ConvectiveTorchRadius = Bind(
                config,
                "10 - Heat Convection",
                "Torch_Radius",
                3.0f,
                "Convective heat reach radius in meters for torches and sconces.",
                order: 28,
                new AcceptableValueRange<float>(0.5f, 20.0f),
                isAdminOnly: true
            );

            // 11 - Material Physical Properties
            // Stone
            MatStoneHeatCapacity = Bind(
                config,
                "11 - Material Physical Properties",
                "Stone_HeatCapacity",
                800.0f,
                "Thermal mass capacity (Joules/°C) for stone masonry, rock, grausten, and marble. Higher values retain heat longer.",
                order: 80,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatStoneAirLoss = Bind(
                config,
                "11 - Material Physical Properties",
                "Stone_AirLoss",
                1.0f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for stone pieces when exposed.",
                order: 79,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatStoneConductivity = Bind(
                config,
                "11 - Material Physical Properties",
                "Stone_Conductivity",
                75.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped stone pieces.",
                order: 78,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatStoneAbsorption = Bind(
                config,
                "11 - Material Physical Properties",
                "Stone_Absorption",
                1.0f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by stone surfaces.",
                order: 77,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            // Wood
            MatWoodHeatCapacity = Bind(
                config,
                "11 - Material Physical Properties",
                "Wood_HeatCapacity",
                200.0f,
                "Thermal mass capacity (Joules/°C) for standard wooden planks, thatch roofs, beams, and poles.",
                order: 60,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatWoodAirLoss = Bind(
                config,
                "11 - Material Physical Properties",
                "Wood_AirLoss",
                2.0f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for wood pieces when exposed.",
                order: 59,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatWoodConductivity = Bind(
                config,
                "11 - Material Physical Properties",
                "Wood_Conductivity",
                40.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped wood pieces.",
                order: 58,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatWoodAbsorption = Bind(
                config,
                "11 - Material Physical Properties",
                "Wood_Absorption",
                1.0f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by wood surfaces.",
                order: 57,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            // CoreWood
            MatCoreWoodHeatCapacity = Bind(
                config,
                "11 - Material Physical Properties",
                "CoreWood_HeatCapacity",
                400.0f,
                "Thermal mass capacity (Joules/°C) for heavy timber, corewood log poles, and roundlog beams.",
                order: 40,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatCoreWoodAirLoss = Bind(
                config,
                "11 - Material Physical Properties",
                "CoreWood_AirLoss",
                2.0f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for corewood pieces when exposed.",
                order: 39,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatCoreWoodConductivity = Bind(
                config,
                "11 - Material Physical Properties",
                "CoreWood_Conductivity",
                55.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped corewood pieces.",
                order: 38,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatCoreWoodAbsorption = Bind(
                config,
                "11 - Material Physical Properties",
                "CoreWood_Absorption",
                1.0f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by corewood surfaces.",
                order: 37,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            // Metal
            MatMetalHeatCapacity = Bind(
                config,
                "11 - Material Physical Properties",
                "Metal_HeatCapacity",
                500.0f,
                "Thermal mass capacity (Joules/°C) for iron grates, iron cage beams, and darkmetal structures.",
                order: 20,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatMetalAirLoss = Bind(
                config,
                "11 - Material Physical Properties",
                "Metal_AirLoss",
                1.0f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for metal pieces when exposed.",
                order: 19,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatMetalConductivity = Bind(
                config,
                "11 - Material Physical Properties",
                "Metal_Conductivity",
                120.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped metal pieces.",
                order: 18,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            MatMetalAbsorption = Bind(
                config,
                "11 - Material Physical Properties",
                "Metal_Absorption",
                1.0f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by metal surfaces.",
                order: 17,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true,
                legacySection: "10 - Material Physical Properties",
                legacySection2: "7 - Material Physical Defaults"
            );

            // Crystal / Glass
            MatCrystalHeatCapacity = Bind(
                config,
                "11 - Material Physical Properties",
                "Crystal_HeatCapacity",
                60.0f,
                "Thermal mass capacity (Joules/°C) for crystal and glass wall pieces.",
                order: 16,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true
            );

            MatCrystalAirLoss = Bind(
                config,
                "11 - Material Physical Properties",
                "Crystal_AirLoss",
                3.5f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for crystal/glass panes when exposed.",
                order: 15,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true
            );

            MatCrystalConductivity = Bind(
                config,
                "11 - Material Physical Properties",
                "Crystal_Conductivity",
                50.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped crystal pieces.",
                order: 14,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true
            );

            MatCrystalAbsorption = Bind(
                config,
                "11 - Material Physical Properties",
                "Crystal_Absorption",
                0.85f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by crystal surfaces.",
                order: 13,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true
            );

            // 12 - UI & Thermometer HUD
            ShowThermometerHUD = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "ShowThermometerHUD",
                true,
                "Display the ambient and body warmth thermometer widget on the HUD.",
                order: 50,
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            UseCustomPosition = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "UseCustomPosition",
                false,
                "When disabled (false), automatically positions the thermometer HUD adjacent to the minimap (pinned to the left edge of the minimap frame in both Auga and vanilla layouts, safely clear of status effect lists). When enabled (true), uses the manual ThermometerPositionX and ThermometerPositionY coordinates below.",
                order: 40,
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            ThermometerPositionX = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "ThermometerPositionX",
                -265.0f,
                "Manual horizontal pixel position (X) of the thermometer. Anchored to top-right corner (negative values move leftwards from the right screen edge). Only active when UseCustomPosition is true.",
                order: 39,
                new AcceptableValueRange<float>(-3000.0f, 3000.0f),
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            ThermometerPositionY = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "ThermometerPositionY",
                -30.0f,
                "Manual vertical pixel position (Y) of the thermometer. Anchored to top-right corner (negative values move downwards from the top screen edge). Only active when UseCustomPosition is true.",
                order: 38,
                new AcceptableValueRange<float>(-3000.0f, 3000.0f),
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            ThermometerScale = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "ThermometerScale",
                1.0f,
                "Uniform scale multiplier for the thermometer HUD widget (0.5 to 2.0).",
                order: 37,
                new AcceptableValueRange<float>(0.5f, 2.0f),
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            EnableScreenVignettes = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "EnableScreenVignettes",
                true,
                "Enable atmospheric screen-edge vignettes for extreme cold (frost clouding) and heat (mirage blur/distortion).",
                order: 20,
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            FrostVignetteStartTemp = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "FrostVignetteStartTemp",
                0.0f,
                "Player body warmth in Celsius below which the screen-edge frost vignette begins to appear.",
                order: 19,
                new AcceptableValueRange<float>(-25.0f, 15.0f),
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            FrostVignetteMaxAlpha = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "FrostVignetteMaxAlpha",
                0.10f,
                "Maximum opacity of the screen-edge frost vignette during severe hypothermia.",
                order: 18,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            HeatVignetteStartTemp = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "HeatVignetteStartTemp",
                38.0f,
                "Player body warmth in Celsius above which screen-edge heat blur and camera shimmer distortion begin to appear.",
                order: 17,
                new AcceptableValueRange<float>(26.0f, 60.0f),
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            HeatVignetteMaxAlpha = Bind(
                config,
                "12 - UI & Thermometer HUD",
                "HeatVignetteMaxAlpha",
                0.10f,
                "Maximum opacity of the screen-edge heat blur vignette during extreme heat.",
                order: 16,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: false,
                legacySection: "11 - UI & Thermometer HUD",
                legacySection2: "9 - UI & Thermometer HUD"
            );

            // 13 - Compatibility
            EnableSeasonIntegration = Bind(
                config,
                "13 - Compatibility",
                "EnableSeasonIntegration",
                false,
                "Integrate with third-party season mods (such as Seasonality by RustyMods or Seasons by shudnal) to apply biome-scaled seasonal temperature shifts. When disabled, seasonal shifts are completely ignored, preserving native climate calculations.",
                order: 10,
                isAdminOnly: true,
                legacySection: "12 - Compatibility",
                legacySection2: "Compatibility"
            );

            // 14 - Inspection & Debugging
            HighlightPieceInsulation = Bind(
                config,
                "14 - Inspection & Debugging",
                "HighlightPieceInsulation",
                false,
                "Highlight nearby building pieces with color-coded shaders based on their thermal insulation / shelter percentage (Green = fully insulated / zero heat leak, Yellow = partial, Red = uninsulated / high heat leak). Live toggled.",
                order: 90,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            InstantGraphEquilibrium = Bind(
                config,
                "14 - Inspection & Debugging",
                "InstantGraphEquilibrium",
                false,
                "When enabled, immediately equalizes all structural pieces within each connected dwelling cluster to the cluster average temperature, allowing instant macro equilibrium testing without joint conduction lag.",
                order: 85,
                isAdminOnly: false
            );

            DebugFreezeAmbientTemp = Bind(
                config,
                "14 - Inspection & Debugging",
                "DebugFreezeAmbientTemp",
                false,
                "Freeze the ambient outdoor temperature everywhere in the world to DebugForcedAmbientTemp for testing and calibration.",
                order: 40,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            DebugForcedAmbientTemp = Bind(
                config,
                "14 - Inspection & Debugging",
                "DebugForcedAmbientTemp",
                15.0f,
                "Target outdoor ambient temperature in Celsius when DebugFreezeAmbientTemp is enabled.",
                order: 38,
                new AcceptableValueRange<float>(-60.0f, 100.0f),
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            DebugFreezeStructureTemps = Bind(
                config,
                "14 - Inspection & Debugging",
                "DebugFreezeStructureTemps",
                false,
                "Force all structural building pieces to DebugForcedStructureTemp. When toggled off, building temperatures start evolving naturally.",
                order: 36,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            DebugForcedStructureTemp = Bind(
                config,
                "14 - Inspection & Debugging",
                "DebugForcedStructureTemp",
                15.0f,
                "Target temperature in Celsius for all structural building pieces when DebugFreezeStructureTemps is enabled.",
                order: 34,
                new AcceptableValueRange<float>(-50.0f, 120.0f),
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            DebugInstantConduction = Bind(
                config,
                "14 - Inspection & Debugging",
                "DebugInstantConduction",
                false,
                "When enabled, instantaneously equalizes temperature across all physically connected building pieces (infinite joint conductivity). Perfect for isolating and tuning environmental air loss and radiant fire power without internal conduction lag.",
                order: 31,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            ShowPieceHoverDebug = Bind(
                config,
                "14 - Inspection & Debugging",
                "ShowPieceHoverDebug",
                true,
                "Show detailed thermodynamic information in HUD when looking at a building piece with the crosshair.",
                order: 30,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            HighlightConnectedPieces = Bind(
                config,
                "14 - Inspection & Debugging",
                "HighlightConnectedPieces",
                true,
                "Highlight structurally connected neighbor pieces in the world when inspecting a piece in the crosshair.",
                order: 28,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            TogglePieceHoverHotkey = Bind(
                config,
                "14 - Inspection & Debugging",
                "TogglePieceHoverHotkey",
                KeyCode.F7,
                "Hotkey to toggle crosshair piece thermal inspection display and connected highlights on or off.",
                order: 26,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            LockHeatSourceHotkey = Bind(
                config,
                "14 - Inspection & Debugging",
                "LockHeatSourceHotkey",
                KeyCode.L,
                "Hotkey to lock heat source highlights and inspection display so you can walk around without aiming at the fire.",
                order: 25,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            ToggleInspectionModeHotkey = Bind(
                config,
                "14 - Inspection & Debugging",
                "ToggleInspectionModeHotkey",
                KeyCode.F8,
                "Hotkey to toggle between single piece thermal inspection and full connected dwelling graph inspection.",
                order: 24,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            InspectAggregatedGraph = Bind(
                config,
                "14 - Inspection & Debugging",
                "InspectAggregatedGraph",
                false,
                "When enabled, thermal inspection shows the aggregated stats and highlights for the entire connected graph instead of a single piece.",
                order: 23,
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            PieceHoverOffsetX = Bind(
                config,
                "14 - Inspection & Debugging",
                "PieceHoverOffsetX",
                0.0f,
                "Horizontal pixel offset of the piece inspection panel relative to the lower-right corner of the screen.",
                order: 22,
                new AcceptableValueRange<float>(-3000.0f, 3000.0f),
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            PieceHoverOffsetY = Bind(
                config,
                "14 - Inspection & Debugging",
                "PieceHoverOffsetY",
                0.0f,
                "Vertical pixel offset of the piece inspection panel relative to the lower-right corner of the screen.",
                order: 21,
                new AcceptableValueRange<float>(-3000.0f, 3000.0f),
                isAdminOnly: false,
                legacySection: "13 - Inspection & Debugging",
                legacySection2: "10 - Inspection & Debugging"
            );

            // Hook live configuration changes to reload thermal status effect modifiers
            HookStatusEffectConfigEvents();

            PurgeHistoricalKeys(config);
        }

        private static ConfigEntry<T> Bind<T>(
            ConfigFile config,
            string section,
            string key,
            T defaultValue,
            string description,
            int order = 0,
            AcceptableValueBase? acceptableValues = null,
            bool isAdminOnly = false,
            string? legacySection = null,
            string? legacyKey = null,
            string? legacySection2 = null,
            string? legacyKey2 = null)
        {
            if (!string.IsNullOrEmpty(legacySection))
            {
                var legacyDef = new ConfigDefinition(legacySection, legacyKey ?? key);
                if (config.TryGetEntry<T>(legacyDef, out var legacyEntry))
                {
                    defaultValue = legacyEntry.Value;
                    config.Remove(legacyDef);
                }
                else if (!string.IsNullOrEmpty(legacySection2))
                {
                    var legacyDef2 = new ConfigDefinition(legacySection2, legacyKey2 ?? legacyKey ?? key);
                    if (config.TryGetEntry<T>(legacyDef2, out var legacyEntry2))
                    {
                        defaultValue = legacyEntry2.Value;
                        config.Remove(legacyDef2);
                    }
                }
            }

            var tags = new ConfigurationManagerAttributes
            {
                Order = order,
                Category = section,
                IsAdminOnly = isAdminOnly
            };
            var configDesc = new ConfigDescription(description, acceptableValues, tags);
            var entry = config.Bind(section, key, defaultValue, configDesc);
            s_boundDefinitions.Add(entry.Definition);
            return entry;
        }

        private static void PurgeHistoricalKeys(ConfigFile config)
        {
            var obsoleteDefs = new System.Collections.Generic.List<ConfigDefinition>();
            foreach (var def in config.Keys)
            {
                if (!s_boundDefinitions.Contains(def))
                {
                    obsoleteDefs.Add(def);
                }
            }

            foreach (var def in obsoleteDefs)
            {
                config.Remove(def);
            }

            if (obsoleteDefs.Count > 0)
            {
                config.Save();
                VikingClimatePlugin.Log.LogInfo($"[ModConfig] Cleaned {obsoleteDefs.Count} historical/orphaned config keys from configuration file.");
            }
        }

        private static void HookStatusEffectConfigEvents()
        {
            System.Action onSettingChanged = () => Features.Temperature.ThermalStatusEffects.ReapplyConfigModifiers();

            ChillyStaminaRegenModifier.SettingChanged += (s, e) => onSettingChanged();
            ChillyDamageMultiplier.SettingChanged += (s, e) => onSettingChanged();
            BitterColdStaminaRegenModifier.SettingChanged += (s, e) => onSettingChanged();
            BitterColdDamageMultiplier.SettingChanged += (s, e) => onSettingChanged();
            NumbingFrostStaminaRegenModifier.SettingChanged += (s, e) => onSettingChanged();
            NumbingFrostDamageMultiplier.SettingChanged += (s, e) => onSettingChanged();
            NumbingFrostAttackSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            HypothermiaStaminaRegenModifier.SettingChanged += (s, e) => onSettingChanged();
            HypothermiaDamageMultiplier.SettingChanged += (s, e) => onSettingChanged();
            HypothermiaAttackSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            HypothermiaHealthRegenModifier.SettingChanged += (s, e) => onSettingChanged();
            HypothermiaEitrRegenModifier.SettingChanged += (s, e) => onSettingChanged();

            WarmSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            WarmAttackSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            HeatExhaustionSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            HeatExhaustionAttackSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            SevereHyperthermiaSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            SevereHyperthermiaAttackSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            CatastrophicScorchingSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            CatastrophicScorchingAttackSpeedMultiplier.SettingChanged += (s, e) => onSettingChanged();
            CatastrophicScorchingStaminaRegenModifier.SettingChanged += (s, e) => onSettingChanged();
        }
    }

    public enum HeatPropagationModel
    {
        Raycast,
        ConvectiveProximity
    }

    public enum ThermalMassMode
    {
        StructuralGraph,
        Fallback
    }

    public enum OfflineThermalMode
    {
        Freeze,
        DecayWhenUnlit,
        AlwaysDecay
    }
}
