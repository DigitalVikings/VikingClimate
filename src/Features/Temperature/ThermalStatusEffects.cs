using System;
using UnityEngine;
using VikingLib.Combat;
using VikingLib.Stats;
using VikingLib.StatusEffects;

namespace VikingClimate.Features.Temperature
{
    /// <summary>
    /// Registers and manages the 4 Cold and 4 Heat status effects on top of VikingLib.
    /// Modifiers attach declaratively to CharacterStatManager without mutating global prefabs.
    /// Tooltips are descriptive physical conditions without raw numeric degrees (degrees are shown on the HUD thermometer).
    /// </summary>
    public static class ThermalStatusEffects
    {
        // Cold Tiers
        public const string ChillyId = "SE_Chilly";
        public const string BitterColdId = "SE_BitterCold";
        public const string NumbingFrostId = "SE_NumbingFrost";
        public const string HypothermiaId = "SE_Hypothermia";

        // Heat Tiers
        public const string WarmId = "SE_Warm";
        public const string HeatExhaustionId = "SE_HeatExhaustion";
        public const string SevereHyperthermiaId = "SE_SevereHyperthermia";
        public const string CatastrophicScorchingId = "SE_CatastrophicScorching";

        public static VikingStatusEffect Chilly { get; private set; } = null!;
        public static VikingStatusEffect BitterCold { get; private set; } = null!;
        public static VikingStatusEffect NumbingFrost { get; private set; } = null!;
        public static VikingStatusEffect Hypothermia { get; private set; } = null!;

        public static VikingStatusEffect Warm { get; private set; } = null!;
        public static VikingStatusEffect HeatExhaustion { get; private set; } = null!;
        public static VikingStatusEffect SevereHyperthermia { get; private set; } = null!;
        public static VikingStatusEffect CatastrophicScorching { get; private set; } = null!;

        public static readonly string[] AllThermalIds = new[]
        {
            ChillyId, BitterColdId, NumbingFrostId, HypothermiaId,
            WarmId, HeatExhaustionId, SevereHyperthermiaId, CatastrophicScorchingId
        };

        private static bool _initialized;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // --- COLD TIERS ---
            // 1. Chilly (8°C to 18°C): Stamina regen -15%, All damage x0.90
            Chilly = StatusEffectBuilder.Create(ChillyId)
                .WithName("Chilly")
                .WithTooltip("Goosebumps and shivering chill. Stamina regeneration slightly reduced.")
                .WithIcon(CreateThermalIcon(new Color(0.6f, 0.85f, 1.0f), "C"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .WithIncreasedModifier(CharacterStats.StaminaRegen, -0.15f, "Chilly")
                .WithMultiplierModifier(CharacterStats.DamagePhysical, 0.90f, "Chilly")
                .WithMultiplierModifier(CharacterStats.DamageElemental, 0.90f, "Chilly")
                .BuildAndRegister();

            // 2. Bitter Cold (0°C to 8°C): Stamina regen -30%, All damage x0.80
            BitterCold = StatusEffectBuilder.Create(BitterColdId)
                .WithName("Bitter Cold")
                .WithTooltip("Shivering intensely. Striking power and stamina recovery significantly hindered.")
                .WithIcon(CreateThermalIcon(new Color(0.3f, 0.65f, 1.0f), "C+"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .WithIncreasedModifier(CharacterStats.StaminaRegen, -0.30f, "Bitter Cold")
                .WithMultiplierModifier(CharacterStats.DamagePhysical, 0.80f, "Bitter Cold")
                .WithMultiplierModifier(CharacterStats.DamageElemental, 0.80f, "Bitter Cold")
                .BuildAndRegister();

            // 3. Numbing Frost (-12°C to 0°C): Stamina regen -45%, All damage x0.65, Tool/Attack Speed x0.85
            NumbingFrost = StatusEffectBuilder.Create(NumbingFrostId)
                .WithName("Numbing Frost")
                .WithTooltip("Extremities stiff and numb. Weapon strikes weakened and stamina regeneration severely penalized.")
                .WithIcon(CreateThermalIcon(new Color(0.1f, 0.45f, 0.95f), "F"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .WithIncreasedModifier(CharacterStats.StaminaRegen, -0.45f, "Numbing Frost")
                .WithMultiplierModifier(CharacterStats.DamagePhysical, 0.65f, "Numbing Frost")
                .WithMultiplierModifier(CharacterStats.DamageElemental, 0.65f, "Numbing Frost")
                .WithMultiplierModifier(CharacterStats.AttackSpeed, 0.85f, "Numbing Frost")
                .BuildAndRegister();

            // 4. Catastrophic Hypothermia (< -12°C): Stamina regen -70%, All damage x0.40, Attack Speed x0.70
            Hypothermia = StatusEffectBuilder.Create(HypothermiaId)
                .WithName("Hypothermia")
                .WithTooltip("Catastrophic thermal collapse! Shivering uncontrollably and taking continuous frost damage. Seek fire immediately!")
                .WithIcon(CreateThermalIcon(new Color(0.05f, 0.25f, 0.8f), "!!"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .WithIncreasedModifier(CharacterStats.StaminaRegen, -0.70f, "Hypothermia")
                .WithMultiplierModifier(CharacterStats.DamagePhysical, 0.40f, "Hypothermia")
                .WithMultiplierModifier(CharacterStats.DamageElemental, 0.40f, "Hypothermia")
                .WithMultiplierModifier(CharacterStats.AttackSpeed, 0.70f, "Hypothermia")
                .BuildAndRegister();

            // --- HEAT TIERS ---
            // 5. Warm (26°C to 35°C): Locomotion speed x0.95, Attack speed x0.95
            Warm = StatusEffectBuilder.Create(WarmId)
                .WithName("Warm")
                .WithTooltip("Sweating in the heat. Movement and attack velocity slightly hindered.")
                .WithIcon(CreateThermalIcon(new Color(1.0f, 0.85f, 0.3f), "W"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .WithMultiplierModifier(CharacterStats.WalkSpeed, 0.95f, "Warm")
                .WithMultiplierModifier(CharacterStats.JogSpeed, 0.95f, "Warm")
                .WithMultiplierModifier(CharacterStats.RunSpeed, 0.95f, "Warm")
                .WithMultiplierModifier(CharacterStats.AttackSpeed, 0.95f, "Warm")
                .BuildAndRegister();

            // 6. Heat Exhaustion (35°C to 45°C): Locomotion speed x0.80, Attack speed x0.80
            HeatExhaustion = StatusEffectBuilder.Create(HeatExhaustionId)
                .WithName("Heat Exhaustion")
                .WithTooltip("Heavy limbs and intense sweating. Locomotion and attacks are sluggish.")
                .WithIcon(CreateThermalIcon(new Color(1.0f, 0.60f, 0.1f), "H"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .WithMultiplierModifier(CharacterStats.WalkSpeed, 0.80f, "Heat Exhaustion")
                .WithMultiplierModifier(CharacterStats.JogSpeed, 0.80f, "Heat Exhaustion")
                .WithMultiplierModifier(CharacterStats.RunSpeed, 0.80f, "Heat Exhaustion")
                .WithMultiplierModifier(CharacterStats.AttackSpeed, 0.80f, "Heat Exhaustion")
                .BuildAndRegister();

            // 7. Severe Hyperthermia (45°C to 55°C): Locomotion speed x0.65, Attack speed x0.65
            SevereHyperthermia = StatusEffectBuilder.Create(SevereHyperthermiaId)
                .WithName("Severe Hyperthermia")
                .WithTooltip("Dizziness and sweltering exhaustion. Mobility and combat effectiveness heavily reduced.")
                .WithIcon(CreateThermalIcon(new Color(0.95f, 0.35f, 0.05f), "H+"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .WithMultiplierModifier(CharacterStats.WalkSpeed, 0.65f, "Severe Hyperthermia")
                .WithMultiplierModifier(CharacterStats.JogSpeed, 0.65f, "Severe Hyperthermia")
                .WithMultiplierModifier(CharacterStats.RunSpeed, 0.65f, "Severe Hyperthermia")
                .WithMultiplierModifier(CharacterStats.AttackSpeed, 0.65f, "Severe Hyperthermia")
                .BuildAndRegister();

            // 8. Catastrophic Scorching (> 55°C): Locomotion speed x0.50, Attack speed x0.50
            CatastrophicScorching = StatusEffectBuilder.Create(CatastrophicScorchingId)
                .WithName("Catastrophic Scorching")
                .WithTooltip("Skin blistering in scorching inferno! Taking continuous burn damage. Flee the heat immediately!")
                .WithIcon(CreateThermalIcon(new Color(0.90f, 0.10f, 0.05f), "!!"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .WithMultiplierModifier(CharacterStats.WalkSpeed, 0.50f, "Catastrophic Scorching")
                .WithMultiplierModifier(CharacterStats.JogSpeed, 0.50f, "Catastrophic Scorching")
                .WithMultiplierModifier(CharacterStats.RunSpeed, 0.50f, "Catastrophic Scorching")
                .WithMultiplierModifier(CharacterStats.AttackSpeed, 0.50f, "Catastrophic Scorching")
                .BuildAndRegister();

            VikingClimatePlugin.Log.LogInfo("Thermal status effects successfully registered via VikingLib.");
        }

        private static Sprite CreateThermalIcon(Color baseColor, string label)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];

            float center = size / 2f;
            float radius = (size / 2f) - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= radius)
                    {
                        // Outer rim
                        if (dist >= radius - 3f)
                        {
                            colors[y * size + x] = Color.Lerp(baseColor, Color.white, 0.6f);
                        }
                        else
                        {
                            float t = dist / radius;
                            colors[y * size + x] = Color.Lerp(baseColor, baseColor * 0.7f, t);
                        }
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
