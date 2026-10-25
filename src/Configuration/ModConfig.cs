using BepInEx.Configuration;
using UnityEngine;

namespace VikingClimate.Configuration
{
    public static class ModConfig
    {
        // 1 - General
        public static ConfigEntry<bool> ModEnabled { get; private set; } = null!;
        public static ConfigEntry<bool> EnableLogging { get; private set; } = null!;

        // 2 - Player Comfort & Thresholds
        public static ConfigEntry<float> ComfortMinTemp { get; private set; } = null!;
        public static ConfigEntry<float> ComfortMaxTemp { get; private set; } = null!;
        public static ConfigEntry<bool> EnableStructureComfort { get; private set; } = null!;
        public static ConfigEntry<bool> EnableThermalResting { get; private set; } = null!;
        public static ConfigEntry<bool> RequireWarmthToSleep { get; private set; } = null!;
        public static ConfigEntry<float> RestingMinTemp { get; private set; } = null!;
        public static ConfigEntry<float> RestingMaxTemp { get; private set; } = null!;
        public static ConfigEntry<float> RestingActivationDelay { get; private set; } = null!;
        public static ConfigEntry<float> RestedDelaySeconds { get; private set; } = null!;

        // 3 - Player Body Thermodynamics
        public static ConfigEntry<float> BaseCoolingRate { get; private set; } = null!;
        public static ConfigEntry<float> ThermalGradientElasticity { get; private set; } = null!;
        public static ConfigEntry<float> ReversionSpeedMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> WetCoolingMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> SwimCoolingMultiplier { get; private set; } = null!;

        // 4 - Environmental Scanning & Shelter
        public static ConfigEntry<float> ThermalSamplingInterval { get; private set; } = null!;
        public static ConfigEntry<float> EnclosureShelterThreshold { get; private set; } = null!;
        public static ConfigEntry<float> InsulationUnroofedFactor { get; private set; } = null!;

        // 5 - Heat Sources (Watts)
        public static ConfigEntry<float> FireOutputCampfire { get; private set; } = null!;
        public static ConfigEntry<float> FireOutputHearth { get; private set; } = null!;
        public static ConfigEntry<float> FireOutputBrazier { get; private set; } = null!;
        public static ConfigEntry<float> FireOutputBonfire { get; private set; } = null!;
        public static ConfigEntry<float> FireOutputTorch { get; private set; } = null!;
        public static ConfigEntry<float> FireOutputSmelter { get; private set; } = null!;
        public static ConfigEntry<float> FireOutputKiln { get; private set; } = null!;
        public static ConfigEntry<float> FireOutputBlastFurnace { get; private set; } = null!;

        // 6 - Structural Thermal Graph
        public static ConfigEntry<ThermalMassMode> ThermalMassSimulationMode { get; private set; } = null!;
        public static ConfigEntry<float> FireRayRange { get; private set; } = null!;
        public static ConfigEntry<float> DirectFlameWarmthCoeff { get; private set; } = null!;
        public static ConfigEntry<float> DirectFlameWarmthMax { get; private set; } = null!;
        public static ConfigEntry<float> StructuralRelaxationRate { get; private set; } = null!;

        // 7 - Material Physical Defaults
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

        // 8 - Consequence & Damage
        public static ConfigEntry<float> HypothermiaDamageRate { get; private set; } = null!;
        public static ConfigEntry<float> HyperthermiaDamageRate { get; private set; } = null!;

        // 9 - UI & Thermometer HUD
        public static ConfigEntry<bool> ShowThermometerHUD { get; private set; } = null!;
        public static ConfigEntry<float> ThermometerOffsetX { get; private set; } = null!;
        public static ConfigEntry<float> ThermometerOffsetY { get; private set; } = null!;
        public static ConfigEntry<float> ForsakenPowerOffsetX { get; private set; } = null!;

        // 10 - Inspection & Debugging
        public static ConfigEntry<bool> DebugFreezeAmbientTemp { get; private set; } = null!;
        public static ConfigEntry<float> DebugForcedAmbientTemp { get; private set; } = null!;
        public static ConfigEntry<bool> DebugFreezeStructureTemps { get; private set; } = null!;
        public static ConfigEntry<float> DebugForcedStructureTemp { get; private set; } = null!;
        public static ConfigEntry<bool> DebugInstantConduction { get; private set; } = null!;

        public static ConfigEntry<bool> ShowPieceHoverDebug { get; private set; } = null!;
        public static ConfigEntry<bool> HighlightConnectedPieces { get; private set; } = null!;
        public static ConfigEntry<KeyCode> TogglePieceHoverHotkey { get; private set; } = null!;
        public static ConfigEntry<float> PieceHoverOffsetX { get; private set; } = null!;
        public static ConfigEntry<float> PieceHoverOffsetY { get; private set; } = null!;

        private static readonly System.Collections.Generic.HashSet<ConfigDefinition> s_boundDefinitions = new System.Collections.Generic.HashSet<ConfigDefinition>();

        public static void Init(ConfigFile config)
        {
            // 1 - General
            ModEnabled = Bind(
                config,
                "1 - General",
                "Enabled",
                true,
                "Master switch to enable or disable all features of VikingClimate.",
                order: 100,
                isAdminOnly: true
            );

            EnableLogging = Bind(
                config,
                "1 - General",
                "EnableLogging",
                false,
                "Enable verbose diagnostic logging for temperature calculations.",
                order: 95,
                isAdminOnly: false
            );

            // 2 - Player Comfort & Thresholds
            ComfortMinTemp = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "ComfortMinTemp",
                18.0f,
                "Lower bound in Celsius for the player's thermal comfort equilibrium.",
                order: 90,
                new AcceptableValueRange<float>(-50.0f, 100.0f),
                isAdminOnly: true
            );

            ComfortMaxTemp = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "ComfortMaxTemp",
                26.0f,
                "Upper bound in Celsius for the player's thermal comfort equilibrium.",
                order: 85,
                new AcceptableValueRange<float>(-50.0f, 100.0f),
                isAdminOnly: true
            );

            EnableStructureComfort = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "EnableStructureComfort",
                true,
                "When enabled, comfort items (beds, chairs, rugs, banners, hearths) propagate across the entire connected building structure instead of a 10m sphere.",
                order: 84,
                isAdminOnly: true
            );

            EnableThermalResting = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "EnableThermalResting",
                true,
                "When enabled, entering and maintaining the Resting state requires a comfortable dwelling temperature [RestingMinTemp, RestingMaxTemp] instead of vanilla's binary campfire proximity.",
                order: 83,
                isAdminOnly: true
            );

            RequireWarmthToSleep = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "RequireWarmthToSleep",
                false,
                "When enabled, sleeping in a bed is blocked if the player is cold (body temperature below ComfortMinTemp). Disabled by default.",
                order: 82,
                isAdminOnly: true
            );

            RestingMinTemp = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "RestingMinTemp",
                18.0f,
                "Minimum ambient temperature in Celsius required to enter and maintain the Resting state inside dwellings without a direct campfire.",
                order: 81,
                new AcceptableValueRange<float>(-50.0f, 100.0f),
                isAdminOnly: true
            );

            RestingMaxTemp = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "RestingMaxTemp",
                28.0f,
                "Maximum ambient temperature in Celsius before excessive heat disrupts the Resting state.",
                order: 80,
                new AcceptableValueRange<float>(-50.0f, 100.0f),
                isAdminOnly: true
            );

            RestingActivationDelay = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "RestingActivationDelay",
                2.5f,
                "Delay in seconds of continuously staying in comfortable dwelling temperature before the Resting state begins. Prevents Resting from flickering when running through doorways.",
                order: 80,
                new AcceptableValueRange<float>(0.0f, 60.0f),
                isAdminOnly: true
            );

            RestedDelaySeconds = Bind(
                config,
                "2 - Player Comfort & Thresholds",
                "RestedDelaySeconds",
                10.0f,
                "Duration in seconds the player must remain in the Resting state before the Rested status effect is granted or refreshed.",
                order: 79,
                new AcceptableValueRange<float>(0.0f, 120.0f),
                isAdminOnly: true
            );

            // 3 - Player Body Thermodynamics
            BaseCoolingRate = Bind(
                config,
                "3 - Player Body Thermodynamics",
                "BaseCoolingRate",
                0.005f,
                "Base rate per second at which player body warmth conducts towards ambient temperature.",
                order: 80,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true
            );

            ThermalGradientElasticity = Bind(
                config,
                "3 - Player Body Thermodynamics",
                "ThermalGradientElasticity",
                1.0f,
                "Exponent (POW) in the conduction equation: dT/dt = Rate * |T_amb - T|^POW. Default 1.0 represents standard linear Newton conduction.",
                order: 78,
                new AcceptableValueRange<float>(0.1f, 5.0f),
                isAdminOnly: true
            );

            ReversionSpeedMultiplier = Bind(
                config,
                "3 - Player Body Thermodynamics",
                "ReversionSpeedMultiplier",
                10.0f,
                "Multiplier accelerating heat conduction when body temperature is reverting towards comfort equilibrium (22°C) while resting or sleeping.",
                order: 76,
                new AcceptableValueRange<float>(0.1f, 100.0f),
                isAdminOnly: true
            );

            WetCoolingMultiplier = Bind(
                config,
                "3 - Player Body Thermodynamics",
                "WetCoolingMultiplier",
                2.0f,
                "Conductivity multiplier applied to cooling when player is wet from rain or splash.",
                order: 65,
                new AcceptableValueRange<float>(1.0f, 20.0f),
                isAdminOnly: true
            );

            SwimCoolingMultiplier = Bind(
                config,
                "3 - Player Body Thermodynamics",
                "SwimCoolingMultiplier",
                3.0f,
                "Conductivity multiplier applied to cooling when player is submerged and swimming.",
                order: 60,
                new AcceptableValueRange<float>(1.0f, 20.0f),
                isAdminOnly: true
            );

            // 4 - Environmental Scanning & Shelter
            ThermalSamplingInterval = Bind(
                config,
                "4 - Environmental Scanning & Shelter",
                "ThermalSamplingInterval",
                4.0f,
                "Interval in seconds between full 64-ray environmental, shelter, and thermal mass scans around the player. Continuous interpolation keeps ambient and body temperatures smooth between scans.",
                order: 84,
                new AcceptableValueRange<float>(0.1f, 60.0f),
                isAdminOnly: true
            );

            EnclosureShelterThreshold = Bind(
                config,
                "4 - Environmental Scanning & Shelter",
                "EnclosureShelterThreshold",
                0.80f,
                "Fraction of the 64-ray surrounding sphere that must be shielded/enclosed by structural pieces or roof for the player to be considered inside an enclosed compound/dwelling.",
                order: 83,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true
            );

            InsulationUnroofedFactor = Bind(
                config,
                "4 - Environmental Scanning & Shelter",
                "InsulationUnroofedFactor",
                0.40f,
                "Insulation factor multiplier applied to pieces that have walls around them but lack a direct roof overhead.",
                order: 82,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true
            );

            // 5 - Heat Sources (Watts)
            FireOutputCampfire = Bind(
                config,
                "5 - Heat Sources (Watts)",
                "FireOutput_Campfire",
                800.0f,
                "Thermal energy output (Watts) of standard campfires. Radiates direct heat to line-of-sight pieces and nearby players.",
                order: 58,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true
            );

            FireOutputHearth = Bind(
                config,
                "5 - Heat Sources (Watts)",
                "FireOutput_Hearth",
                3200.0f,
                "Thermal energy output (Watts) of stone hearths. High thermal capacity hearths effectively heat surrounding stones and dwellings.",
                order: 57,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true
            );

            FireOutputBrazier = Bind(
                config,
                "5 - Heat Sources (Watts)",
                "FireOutput_Brazier",
                400.0f,
                "Thermal energy output (Watts) of standing and hanging braziers.",
                order: 56,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true
            );

            FireOutputBonfire = Bind(
                config,
                "5 - Heat Sources (Watts)",
                "FireOutput_Bonfire",
                1600.0f,
                "Thermal energy output (Watts) of large outdoor bonfires.",
                order: 55,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true
            );

            FireOutputTorch = Bind(
                config,
                "5 - Heat Sources (Watts)",
                "FireOutput_Torch",
                50.0f,
                "Thermal energy output (Watts) of standing wood/iron torches and wall sconces.",
                order: 54,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true
            );

            FireOutputSmelter = Bind(
                config,
                "5 - Heat Sources (Watts)",
                "FireOutput_Smelter",
                2400.0f,
                "Thermal energy output (Watts) of metallurgical smelters while actively smelting ore.",
                order: 53,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true
            );

            FireOutputKiln = Bind(
                config,
                "5 - Heat Sources (Watts)",
                "FireOutput_CharcoalKiln",
                2400.0f,
                "Thermal energy output (Watts) of charcoal kilns while actively burning wood into coal.",
                order: 52,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true
            );

            FireOutputBlastFurnace = Bind(
                config,
                "5 - Heat Sources (Watts)",
                "FireOutput_BlastFurnace",
                4000.0f,
                "Thermal energy output (Watts) of high-temperature blast furnaces.",
                order: 51,
                new AcceptableValueRange<float>(0.0f, 100000.0f),
                isAdminOnly: true
            );

            // 6 - Structural Thermal Graph
            ThermalMassSimulationMode = Bind(
                config,
                "6 - Structural Thermal Graph",
                "ThermalMassSimulationMode",
                ThermalMassMode.StructuralGraph,
                "Dwelling heat simulation engine. StructuralGraph uses forward 128-ray emission + structural graph conduction across snapped pieces.",
                order: 70,
                isAdminOnly: true
            );

            FireRayRange = Bind(
                config,
                "6 - Structural Thermal Graph",
                "FireRayRange",
                16.0f,
                "Maximum radiant sightline range in meters across which heat rays propagate from active fire sources to building pieces.",
                order: 68,
                new AcceptableValueRange<float>(1.0f, 64.0f),
                isAdminOnly: true
            );

            DirectFlameWarmthCoeff = Bind(
                config,
                "6 - Structural Thermal Graph",
                "DirectFlameWarmthCoeff",
                0.008f,
                "Scaling factor determining how much direct infrared skin warmth the player feels per Watt from visible burning flames at distance.",
                order: 66,
                new AcceptableValueRange<float>(0.0f, 0.50f),
                isAdminOnly: true
            );

            DirectFlameWarmthMax = Bind(
                config,
                "6 - Structural Thermal Graph",
                "DirectFlameWarmthMax",
                18.0f,
                "Maximum perceived warmth bonus in Celsius granted by direct line-of-sight flame exposure.",
                order: 64,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true
            );

            StructuralRelaxationRate = Bind(
                config,
                "6 - Structural Thermal Graph",
                "StructuralRelaxationRate",
                1.0f,
                "Simulation time step (dt) multiplier in seconds for structural relaxation iterations. Higher values accelerate heat diffusion.",
                order: 62,
                new AcceptableValueRange<float>(0.01f, 10.0f),
                isAdminOnly: true
            );

            // 7 - Material Physical Defaults
            // Stone
            MatStoneHeatCapacity = Bind(
                config,
                "7 - Material Physical Defaults",
                "Stone_HeatCapacity",
                800.0f,
                "Thermal mass capacity (Joules/°C) for stone masonry, rock, grausten, and marble. Higher values retain heat longer.",
                order: 50,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true
            );

            MatStoneAirLoss = Bind(
                config,
                "7 - Material Physical Defaults",
                "Stone_AirLoss",
                1.0f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for stone pieces when exposed.",
                order: 49,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true
            );

            MatStoneConductivity = Bind(
                config,
                "7 - Material Physical Defaults",
                "Stone_Conductivity",
                75.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped stone pieces.",
                order: 48,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true
            );

            MatStoneAbsorption = Bind(
                config,
                "7 - Material Physical Defaults",
                "Stone_Absorption",
                1.0f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by stone surfaces.",
                order: 47,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true
            );

            // Wood
            MatWoodHeatCapacity = Bind(
                config,
                "7 - Material Physical Defaults",
                "Wood_HeatCapacity",
                200.0f,
                "Thermal mass capacity (Joules/°C) for standard wooden planks, thatch roofs, beams, and poles.",
                order: 45,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true
            );

            MatWoodAirLoss = Bind(
                config,
                "7 - Material Physical Defaults",
                "Wood_AirLoss",
                2.0f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for wood pieces when exposed.",
                order: 44,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true
            );

            MatWoodConductivity = Bind(
                config,
                "7 - Material Physical Defaults",
                "Wood_Conductivity",
                40.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped wood pieces.",
                order: 43,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true
            );

            MatWoodAbsorption = Bind(
                config,
                "7 - Material Physical Defaults",
                "Wood_Absorption",
                1.0f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by wood surfaces.",
                order: 42,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true
            );

            // CoreWood
            MatCoreWoodHeatCapacity = Bind(
                config,
                "7 - Material Physical Defaults",
                "CoreWood_HeatCapacity",
                400.0f,
                "Thermal mass capacity (Joules/°C) for heavy timber, corewood log poles, and roundlog beams.",
                order: 40,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true
            );

            MatCoreWoodAirLoss = Bind(
                config,
                "7 - Material Physical Defaults",
                "CoreWood_AirLoss",
                2.0f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for corewood pieces when exposed.",
                order: 39,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true
            );

            MatCoreWoodConductivity = Bind(
                config,
                "7 - Material Physical Defaults",
                "CoreWood_Conductivity",
                55.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped corewood pieces.",
                order: 38,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true
            );

            MatCoreWoodAbsorption = Bind(
                config,
                "7 - Material Physical Defaults",
                "CoreWood_Absorption",
                1.0f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by corewood surfaces.",
                order: 37,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true
            );

            // Metal
            MatMetalHeatCapacity = Bind(
                config,
                "7 - Material Physical Defaults",
                "Metal_HeatCapacity",
                500.0f,
                "Thermal mass capacity (Joules/°C) for iron grates, iron cage beams, and darkmetal structures.",
                order: 35,
                new AcceptableValueRange<float>(1.0f, 50000.0f),
                isAdminOnly: true
            );

            MatMetalAirLoss = Bind(
                config,
                "7 - Material Physical Defaults",
                "Metal_AirLoss",
                1.0f,
                "Base convective heat dissipation rate (Watts/°C) to open atmosphere for metal pieces when exposed.",
                order: 34,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true
            );

            MatMetalConductivity = Bind(
                config,
                "7 - Material Physical Defaults",
                "Metal_Conductivity",
                120.0f,
                "Inter-piece contact joint conductivity (Watts/°C) between adjacent snapped metal pieces.",
                order: 33,
                new AcceptableValueRange<float>(0.0f, 1000.0f),
                isAdminOnly: true
            );

            MatMetalAbsorption = Bind(
                config,
                "7 - Material Physical Defaults",
                "Metal_Absorption",
                1.0f,
                "Fraction (0.0 to 1.0) of direct radiant flame energy captured by metal surfaces.",
                order: 32,
                new AcceptableValueRange<float>(0.0f, 1.0f),
                isAdminOnly: true
            );

            // 8 - Consequence & Damage
            HypothermiaDamageRate = Bind(
                config,
                "8 - Consequence & Damage",
                "HypothermiaDamageRate",
                1.5f,
                "Frost damage per second dealt during catastrophic hypothermia (< -12°C).",
                order: 30,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true
            );

            HyperthermiaDamageRate = Bind(
                config,
                "8 - Consequence & Damage",
                "HyperthermiaDamageRate",
                2.0f,
                "Fire damage per second dealt during catastrophic scorching (> 55°C).",
                order: 25,
                new AcceptableValueRange<float>(0.0f, 100.0f),
                isAdminOnly: true
            );

            // 9 - UI & Thermometer HUD
            ShowThermometerHUD = Bind(
                config,
                "9 - UI & Thermometer HUD",
                "ShowThermometerHUD",
                true,
                "Display the ambient and body warmth thermometer widget on the HUD.",
                order: 30,
                isAdminOnly: false
            );

            ThermometerOffsetX = Bind(
                config,
                "9 - UI & Thermometer HUD",
                "ThermometerOffsetX",
                0.0f,
                "Horizontal pixel offset of the thermometer relative to its calculated position.",
                order: 25,
                new AcceptableValueRange<float>(-2000.0f, 2000.0f),
                isAdminOnly: false
            );

            ThermometerOffsetY = Bind(
                config,
                "9 - UI & Thermometer HUD",
                "ThermometerOffsetY",
                0.0f,
                "Vertical pixel offset of the thermometer relative to its calculated position.",
                order: 20,
                new AcceptableValueRange<float>(-2000.0f, 2000.0f),
                isAdminOnly: false
            );

            ForsakenPowerOffsetX = Bind(
                config,
                "9 - UI & Thermometer HUD",
                "ForsakenPowerOffsetX",
                54.0f,
                "Horizontal pixel shift applied to Forsaken Power and Quick Slots to accommodate the thermometer bar.",
                order: 15,
                new AcceptableValueRange<float>(-500.0f, 500.0f),
                isAdminOnly: false
            );

            // 10 - Inspection & Debugging
            DebugFreezeAmbientTemp = Bind(
                config,
                "10 - Inspection & Debugging",
                "DebugFreezeAmbientTemp",
                false,
                "Freeze the ambient outdoor temperature everywhere in the world to DebugForcedAmbientTemp for testing and calibration.",
                order: 40,
                isAdminOnly: false
            );

            DebugForcedAmbientTemp = Bind(
                config,
                "10 - Inspection & Debugging",
                "DebugForcedAmbientTemp",
                15.0f,
                "Target outdoor ambient temperature in Celsius when DebugFreezeAmbientTemp is enabled.",
                order: 38,
                new AcceptableValueRange<float>(-50.0f, 50.0f),
                isAdminOnly: false
            );

            DebugFreezeStructureTemps = Bind(
                config,
                "10 - Inspection & Debugging",
                "DebugFreezeStructureTemps",
                false,
                "Force all structural building pieces to DebugForcedStructureTemp. When toggled off, building temperatures start evolving naturally.",
                order: 36,
                isAdminOnly: false
            );

            DebugForcedStructureTemp = Bind(
                config,
                "10 - Inspection & Debugging",
                "DebugForcedStructureTemp",
                15.0f,
                "Target temperature in Celsius for all structural building pieces when DebugFreezeStructureTemps is enabled.",
                order: 34,
                new AcceptableValueRange<float>(-50.0f, 120.0f),
                isAdminOnly: false
            );

            DebugInstantConduction = Bind(
                config,
                "10 - Inspection & Debugging",
                "DebugInstantConduction",
                false,
                "When enabled, instantaneously equalizes temperature across all physically connected building pieces (infinite joint conductivity). Perfect for isolating and tuning environmental air loss and radiant fire power without internal conduction lag.",
                order: 31,
                isAdminOnly: false
            );

            ShowPieceHoverDebug = Bind(
                config,
                "10 - Inspection & Debugging",
                "ShowPieceHoverDebug",
                false,
                "Show detailed thermodynamic information in HUD when looking at a building piece with the crosshair.",
                order: 30,
                isAdminOnly: false
            );

            HighlightConnectedPieces = Bind(
                config,
                "10 - Inspection & Debugging",
                "HighlightConnectedPieces",
                false,
                "Highlight structurally connected neighbor pieces in the world when inspecting a piece in the crosshair.",
                order: 28,
                isAdminOnly: false
            );

            TogglePieceHoverHotkey = Bind(
                config,
                "10 - Inspection & Debugging",
                "TogglePieceHoverHotkey",
                KeyCode.F7,
                "Hotkey to toggle crosshair piece thermal inspection display and connected highlights on or off.",
                order: 26,
                isAdminOnly: false
            );

            PieceHoverOffsetX = Bind(
                config,
                "10 - Inspection & Debugging",
                "PieceHoverOffsetX",
                0.0f,
                "Horizontal pixel offset of the piece inspection panel relative to the lower-right corner of the screen.",
                order: 24,
                new AcceptableValueRange<float>(-3000.0f, 3000.0f),
                isAdminOnly: false
            );

            PieceHoverOffsetY = Bind(
                config,
                "10 - Inspection & Debugging",
                "PieceHoverOffsetY",
                0.0f,
                "Vertical pixel offset of the piece inspection panel relative to the lower-right corner of the screen.",
                order: 22,
                new AcceptableValueRange<float>(-3000.0f, 3000.0f),
                isAdminOnly: false
            );

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
            bool isAdminOnly = false)
        {
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
    }

    public enum ThermalMassMode
    {
        StructuralGraph,
        Fallback
    }
}
