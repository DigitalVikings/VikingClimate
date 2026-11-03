using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.UI;
using VikingLib.Combat;
using VikingLib.Stats;
using VikingLib.StatusEffects;

namespace VikingClimate.Features.Temperature
{
    /// <summary>
    /// Registers and manages the 4 Cold and 4 Heat status effects on top of VikingLib.
    /// Modifiers attach declaratively to CharacterStatManager without mutating global prefabs.
    /// Debuff magnitudes are dynamically synchronized with ModConfig entries at runtime.
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
            // 1. Chilly (8°C to 18°C)
            Chilly = StatusEffectBuilder.Create(ChillyId)
                .WithName("Chilly")
                .WithTooltip("Goosebumps and shivering chill. Stamina regeneration slightly reduced.")
                .WithIcon(ResolveIcon("se_chilly", new Color(0.6f, 0.85f, 1.0f), "C"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .BuildAndRegister();

            // 2. Bitter Cold (0°C to 8°C)
            BitterCold = StatusEffectBuilder.Create(BitterColdId)
                .WithName("Bitter Cold")
                .WithTooltip("Shivering intensely. Striking power and stamina recovery significantly hindered.")
                .WithIcon(ResolveIcon("se_bitter_cold", new Color(0.3f, 0.65f, 1.0f), "C+"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .BuildAndRegister();

            // 3. Numbing Frost (-12°C to 0°C)
            NumbingFrost = StatusEffectBuilder.Create(NumbingFrostId)
                .WithName("Numbing Frost")
                .WithTooltip("Extremities stiff and numb. Weapon strikes weakened and stamina regeneration severely penalized.")
                .WithIcon(ResolveIcon("se_numbing_frost", new Color(0.1f, 0.45f, 0.95f), "F"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .BuildAndRegister();

            // 4. Catastrophic Hypothermia (< -12°C)
            Hypothermia = StatusEffectBuilder.Create(HypothermiaId)
                .WithName("Hypothermia")
                .WithTooltip("Catastrophic thermal collapse! Shivering uncontrollably and taking continuous frost damage. Seek fire immediately!")
                .WithIcon(ResolveIcon("se_hypothermia", new Color(0.05f, 0.25f, 0.8f), "!!"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .BuildAndRegister();

            // --- HEAT TIERS ---
            // 5. Warm (26°C to 35°C)
            Warm = StatusEffectBuilder.Create(WarmId)
                .WithName("Warm")
                .WithTooltip("Sweating in the heat. Movement and attack velocity slightly hindered.")
                .WithIcon(ResolveIcon("se_warm", new Color(1.0f, 0.85f, 0.3f), "W"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .BuildAndRegister();

            // 6. Heat Exhaustion (35°C to 45°C)
            HeatExhaustion = StatusEffectBuilder.Create(HeatExhaustionId)
                .WithName("Heat Exhaustion")
                .WithTooltip("Heavy limbs and intense sweating. Locomotion and attacks are sluggish.")
                .WithIcon(ResolveIcon("se_heat_exhaustion", new Color(1.0f, 0.60f, 0.1f), "H"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .BuildAndRegister();

            // 7. Severe Hyperthermia (45°C to 55°C)
            SevereHyperthermia = StatusEffectBuilder.Create(SevereHyperthermiaId)
                .WithName("Severe Hyperthermia")
                .WithTooltip("Dizziness and sweltering exhaustion. Mobility and combat effectiveness heavily reduced.")
                .WithIcon(ResolveIcon("se_severe_hyperthermia", new Color(0.95f, 0.35f, 0.05f), "H+"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .BuildAndRegister();

            // 8. Catastrophic Scorching (> 55°C)
            CatastrophicScorching = StatusEffectBuilder.Create(CatastrophicScorchingId)
                .WithName("Catastrophic Scorching")
                .WithTooltip("Skin blistering in scorching inferno! Taking continuous burn damage. Flee the heat immediately!")
                .WithIcon(ResolveIcon("se_catastrophic_scorching", new Color(0.90f, 0.10f, 0.05f), "!!"))
                .WithDuration(0f)
                .WithMaxStacks(1, StackPolicy.RefreshDurationOnly)
                .BuildAndRegister();

            // Populate status effect modifiers dynamically from configuration
            ReapplyConfigModifiers();

            VikingClimatePlugin.Log.LogInfo("Thermal status effects successfully registered via VikingLib with dynamic configuration.");
        }

        /// <summary>
        /// Dynamically updates all thermal status effect blueprints from ModConfig and refreshes active local player modifiers.
        /// </summary>
        public static void ReapplyConfigModifiers()
        {
            if (!_initialized) return;

            ConfigureDefinitionModifiers(Chilly.Definition,
                ModConfig.ChillyStaminaRegenModifier.Value,
                ModConfig.ChillyDamageMultiplier.Value,
                1.0f,
                1.0f,
                0f,
                0f,
                0f,
                "Chilly");

            ConfigureDefinitionModifiers(BitterCold.Definition,
                ModConfig.BitterColdStaminaRegenModifier.Value,
                ModConfig.BitterColdDamageMultiplier.Value,
                1.0f,
                1.0f,
                0f,
                0f,
                0f,
                "Bitter Cold");

            ConfigureDefinitionModifiers(NumbingFrost.Definition,
                ModConfig.NumbingFrostStaminaRegenModifier.Value,
                ModConfig.NumbingFrostDamageMultiplier.Value,
                1.0f,
                ModConfig.NumbingFrostAttackSpeedMultiplier.Value,
                0f,
                0f,
                0f,
                "Numbing Frost");

            ConfigureDefinitionModifiers(Hypothermia.Definition,
                ModConfig.HypothermiaStaminaRegenModifier.Value,
                ModConfig.HypothermiaDamageMultiplier.Value,
                1.0f,
                ModConfig.HypothermiaAttackSpeedMultiplier.Value,
                ModConfig.HypothermiaHealthRegenModifier.Value,
                ModConfig.HypothermiaEitrRegenModifier.Value,
                0f,
                "Hypothermia");

            ConfigureDefinitionModifiers(Warm.Definition,
                0f,
                1.0f,
                ModConfig.WarmSpeedMultiplier.Value,
                ModConfig.WarmAttackSpeedMultiplier.Value,
                0f,
                0f,
                0f,
                "Warm");

            ConfigureDefinitionModifiers(HeatExhaustion.Definition,
                0f,
                1.0f,
                ModConfig.HeatExhaustionSpeedMultiplier.Value,
                ModConfig.HeatExhaustionAttackSpeedMultiplier.Value,
                0f,
                0f,
                0f,
                "Heat Exhaustion");

            ConfigureDefinitionModifiers(SevereHyperthermia.Definition,
                0f,
                1.0f,
                ModConfig.SevereHyperthermiaSpeedMultiplier.Value,
                ModConfig.SevereHyperthermiaAttackSpeedMultiplier.Value,
                0f,
                0f,
                0f,
                "Severe Hyperthermia");

            ConfigureDefinitionModifiers(CatastrophicScorching.Definition,
                0f,
                1.0f,
                ModConfig.CatastrophicScorchingSpeedMultiplier.Value,
                ModConfig.CatastrophicScorchingAttackSpeedMultiplier.Value,
                0f,
                0f,
                ModConfig.CatastrophicScorchingStaminaRegenModifier.Value,
                "Catastrophic Scorching");

            // Refresh active local player modifiers if currently under a thermal status effect
            if (Player.m_localPlayer != null)
            {
                var seMan = Player.m_localPlayer.GetSEMan();
                if (seMan != null)
                {
                    var active = seMan.GetStatusEffects();
                    for (int i = 0; i < active.Count; i++)
                    {
                        if (active[i] is VikingStatusEffect vse && vse.Definition != null && IsThermalStatusEffect(vse.Definition.Name))
                        {
                            var newMods = new List<StatModifier>();
                            for (int m = 0; m < vse.Definition.Modifiers.Count; m++)
                            {
                                var bp = vse.Definition.Modifiers[m];
                                if (bp.StatType.HasValue)
                                {
                                    newMods.Add(new StatModifier(bp.StatType.Value, bp.Tier, bp.ValuePerStack, bp.Source ?? vse.name, bp.Condition, vse));
                                }
                            }
                            CharacterStatManager.ReplaceModifiersByOwner(Player.m_localPlayer, vse, newMods);
                        }
                    }
                }
            }
        }

        private static void ConfigureDefinitionModifiers(
            VikingStatusEffectDefinition def,
            float staminaRegen,
            float damageMult,
            float speedMult,
            float attackSpeedMult,
            float healthRegen,
            float eitrRegen,
            float heatStaminaRegen,
            string label)
        {
            def.Modifiers.Clear();

            // Stamina regen modifier (Tier 2 Increased / Additive Percentage)
            float totalStaminaRegen = staminaRegen + heatStaminaRegen;
            if (Mathf.Abs(totalStaminaRegen) > 0.0001f)
            {
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.StaminaRegen.Type, ModifierTier.IncreasedPercent, totalStaminaRegen, label));
            }

            // Damage multiplier (Tier 3 Multiplier)
            if (Mathf.Abs(damageMult - 1.0f) > 0.0001f)
            {
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.DamagePhysical.Type, ModifierTier.Multiplier, damageMult, label));
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.DamageElemental.Type, ModifierTier.Multiplier, damageMult, label));
            }

            // Locomotion speed multiplier (Tier 3 Multiplier)
            if (Mathf.Abs(speedMult - 1.0f) > 0.0001f)
            {
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.WalkSpeed.Type, ModifierTier.Multiplier, speedMult, label));
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.JogSpeed.Type, ModifierTier.Multiplier, speedMult, label));
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.RunSpeed.Type, ModifierTier.Multiplier, speedMult, label));
            }

            // Attack & tool speed multiplier (Tier 3 Multiplier)
            if (Mathf.Abs(attackSpeedMult - 1.0f) > 0.0001f)
            {
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.AttackSpeed.Type, ModifierTier.Multiplier, attackSpeedMult, label));
            }

            // Health regen modifier (Tier 2 Increased / Additive Percentage)
            if (Mathf.Abs(healthRegen) > 0.0001f)
            {
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.HealthRegen.Type, ModifierTier.IncreasedPercent, healthRegen, label));
            }

            // Eitr regen modifier (Tier 2 Increased / Additive Percentage)
            if (Mathf.Abs(eitrRegen) > 0.0001f)
            {
                def.Modifiers.Add(new StatModifierBlueprint(CharacterStats.EitrRegen.Type, ModifierTier.IncreasedPercent, eitrRegen, label));
            }
        }

        public static bool IsThermalStatusEffect(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < AllThermalIds.Length; i++)
            {
                if (string.Equals(AllThermalIds[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static Sprite ResolveIcon(string iconName, Color fallbackColor, string fallbackLabel)
        {
            var sprite = ClimateAssetLoader.GetIcon(iconName);
            if (sprite != null) return sprite;
            return CreateThermalIcon(fallbackColor, fallbackLabel);
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
