using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;
using VikingClimate.UI;

namespace VikingClimate.Patches
{
    [HarmonyPatch]
    public static class PlayerPatches
    {
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void OnPlayerSpawned(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            SurvivalManager.OnPlayerSpawn(__instance);

            if (__instance.GetComponent<ColdBreathEmitter>() == null)
            {
                __instance.gameObject.AddComponent<ColdBreathEmitter>();
            }

            if (__instance.GetComponent<ThermalPieceInspector>() == null)
            {
                __instance.gameObject.AddComponent<ThermalPieceInspector>();
            }

            if (Hud.instance != null)
            {
                HUDThermometer.AttachToHud(Hud.instance);
                ScreenFrostOverlay.AttachToHud(Hud.instance);
                ScreenHeatOverlay.AttachToHud(Hud.instance);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Save), typeof(ZPackage))]
        [HarmonyPrefix]
        private static void OnPlayerSave(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            SurvivalManager.OnPlayerSave(__instance);
            ThermalGraphManager.Instance.SyncAllNodesToZdo();
        }

        [HarmonyPatch(typeof(Player), "Update")]
        [HarmonyPostfix]
        private static void OnPlayerUpdate(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            SurvivalManager.Update(__instance, Time.deltaTime);
        }

        /// <summary>
        /// Prevents native binary Cold and Freezing status effects from being added to the local player at the SEMan level.
        /// Returning false prevents StatusEffect.Setup from executing, suppressing the "You are cold" screen notification,
        /// tutorial prompt, and audio cue, leaving VikingClimate's multi-tiered thermal system with full authority.
        /// </summary>
        [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short))]
        [HarmonyPrefix]
        private static bool SuppressNativeColdAdd(SEMan __instance, Character ___m_character, int nameHash, ref StatusEffect? __result)
        {
            if (!ModConfig.ModEnabled.Value) return true;
            if (___m_character != Player.m_localPlayer) return true;

            if (nameHash == SEMan.s_statusEffectCold || nameHash == SEMan.s_statusEffectFreezing)
            {
                __result = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Overload handling StatusEffect instance additions directly.
        /// </summary>
        [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short))]
        [HarmonyPrefix]
        private static bool SuppressNativeColdAddObj(SEMan __instance, Character ___m_character, StatusEffect statusEffect, ref StatusEffect? __result)
        {
            if (!ModConfig.ModEnabled.Value || statusEffect == null) return true;
            if (___m_character != Player.m_localPlayer) return true;

            int hash = statusEffect.NameHash();
            if (hash == SEMan.s_statusEffectCold || hash == SEMan.s_statusEffectFreezing)
            {
                __result = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Intercepts native environmental status updates.
        /// Transpiles the Resting condition to evaluate thermodynamic dwelling warmth and enclosure,
        /// completely decoupling Resting from the Campfire buff so the Fire buff functions purely for actual fires and wet drying.
        /// In the Postfix, cleanly purges lingering cold/freezing effects.
        /// </summary>
        [HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects")]
        public static class Player_UpdateEnvStatusEffects_Patch
        {
            private static float s_comfortZoneTimer = 0.0f;

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);

                // Match location: right before s_statusEffectResting is referenced
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(SEMan), nameof(SEMan.s_statusEffectResting)))
                );

                if (!matcher.IsValid)
                {
                    VikingClimatePlugin.Log.LogError("Failed to locate s_statusEffectResting in Player.UpdateEnvStatusEffects transpiler!");
                    return matcher.InstructionEnumeration();
                }

                // Backtrack to the 'and' instruction that combines vanillaBaseCondition with native nearFire
                matcher.MatchBack(false,
                    new CodeMatch(OpCodes.And)
                );

                if (!matcher.IsValid)
                {
                    VikingClimatePlugin.Log.LogError("Failed to locate OpCodes.And in Player.UpdateEnvStatusEffects transpiler!");
                    return matcher.InstructionEnumeration();
                }

                // Step back to the instruction loading nearFire (ldloc.1)
                matcher.Advance(-1);

                // Replace in-place with Set/SetAndAdvance to strictly preserve incoming branch labels (e.g. Label #19):
                // [ldloc.1] -> ldarg.0 (Player)
                // [and]     -> call EvaluateRestingCondition(bool, Player)
                matcher.SetAndAdvance(OpCodes.Ldarg_0, null);
                matcher.Set(
                    OpCodes.Call,
                    AccessTools.Method(typeof(Player_UpdateEnvStatusEffects_Patch), nameof(EvaluateRestingCondition))
                );

                return matcher.InstructionEnumeration();
            }

            public static bool EvaluateRestingCondition(bool vanillaBaseCondition, Player player)
            {
                if (!ModConfig.ModEnabled.Value || !ModConfig.EnableThermalResting.Value || player != Player.m_localPlayer)
                {
                    // Native fallback: use native campfire buff / heat area proximity
                    return vanillaBaseCondition && (player.GetSEMan()?.HaveStatusEffect(SEMan.s_statusEffectCampFire) == true || EffectArea.IsPointInsideArea(player.transform.position, EffectArea.Type.Heat, 1.0f));
                }

                var seMan = player.GetSEMan();
                if (seMan == null) return false;

                // Disqualifiers: in combat / sensed, burning, or wet without heat
                if (player.IsSensed() || seMan.HaveStatusEffect(SEMan.s_statusEffectBurning))
                {
                    s_comfortZoneTimer = 0.0f;
                    return false;
                }

                if (seMan.HaveStatusEffect(SEMan.s_statusEffectWet))
                {
                    bool inHeat = EffectArea.IsPointInsideArea(player.transform.position, EffectArea.Type.Heat, 1.0f);
                    if (!inHeat)
                    {
                        s_comfortZoneTimer = 0.0f;
                        return false;
                    }
                }

                // Core condition: warm dwelling or campfire/heat area, with shelter or sitting
                bool isInside = ThermalGraphManager.IsInsideCompound(player) || player.InShelter();
                bool nearFire = seMan.HaveStatusEffect(SEMan.s_statusEffectCampFire) || EffectArea.IsPointInsideArea(player.transform.position, EffectArea.Type.Heat, 1.0f);

                // Dwelling warmth: if inside without a direct campfire, the indoor ambient must meet the comfort/resting threshold
                float ambientTemp = SurvivalManager.CurrentState.AmbientTemperature;
                float minRestingTemp = Mathf.Min(ModConfig.RestingMinTemp.Value, ModConfig.ComfortMinTemp.Value);
                float maxRestingTemp = Mathf.Max(ModConfig.RestingMaxTemp.Value, ModConfig.ComfortMaxTemp.Value);
                bool isDwellingComfortable = isInside && (ambientTemp >= minRestingTemp && ambientTemp <= maxRestingTemp);

                bool environmentWarm = nearFire || isDwellingComfortable;
                bool physicalRestingPosture = isInside || (nearFire && player.IsSitting());

                bool canRest = physicalRestingPosture && environmentWarm;

                if (canRest)
                {
                    s_comfortZoneTimer += Time.deltaTime;
                    return s_comfortZoneTimer >= ModConfig.RestingActivationDelay.Value;
                }
                else
                {
                    s_comfortZoneTimer = 0.0f;
                    return false;
                }
            }

            [HarmonyPostfix]
            private static void Postfix(Player __instance, float dt)
            {
                if (!ModConfig.ModEnabled.Value || __instance != Player.m_localPlayer) return;

                var seMan = __instance.GetSEMan();
                if (seMan != null)
                {
                    if (seMan.HaveStatusEffect(SEMan.s_statusEffectCold))
                    {
                        seMan.RemoveStatusEffect(SEMan.s_statusEffectCold, quiet: true);
                    }
                    if (seMan.HaveStatusEffect(SEMan.s_statusEffectFreezing))
                    {
                        seMan.RemoveStatusEffect(SEMan.s_statusEffectFreezing, quiet: true);
                    }
                }
            }
        }

        /// <summary>
        /// Injects the HUDThermometer adjacent to the health bar upon HUD initialization.
        /// </summary>
        [HarmonyPatch(typeof(Hud), "Awake")]
        [HarmonyPostfix]
        private static void OnHudAwake(Hud __instance)
        {
            HUDThermometer.AttachToHud(__instance);
            ScreenFrostOverlay.AttachToHud(__instance);
            ScreenHeatOverlay.AttachToHud(__instance);
        }

        /// <summary>
        /// When the player sleeps in a bed or wakes up in the morning, evaluates the ambient temperature
        /// of the bed and sets the player's body temperature directly to that value.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.SetSleeping))]
        [HarmonyPostfix]
        private static void OnPlayerSetSleeping(Player __instance, bool sleep)
        {
            if (!ModConfig.ModEnabled.Value || __instance != Player.m_localPlayer) return;

            float bedAmbient = AmbientTemperatureCalculator.Calculate(__instance, out var breakdown);
            var state = SurvivalManager.CurrentState;
            state.AmbientTemperature = bedAmbient;
            state.Breakdown = breakdown;
            state.BodyWarmth = bedAmbient;
            state.TemperatureVelocity = 0.0f;
            state.ConductionRate = 0.0f;
            state.IsResting = true;
            state.SaveToPlayer(__instance);
        }
    }
}
