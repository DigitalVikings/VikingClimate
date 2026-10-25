using System;
using HarmonyLib;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Features.Temperature;

namespace VikingClimate.Patches
{
    /// <summary>
    /// Harmony patches integrating dwelling-wide comfort propagation across the structural graph
    /// and temperature-gated resting requirements for beds.
    /// </summary>
    public static class ComfortPatches
    {
        /// <summary>
        /// Intercepts Player comfort calculations (called by Player.UpdateBaseValue every 2 seconds).
        /// Replaces the vanilla 10m sphere check with the connected structural graph dwelling pool.
        /// </summary>
        [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.CalculateComfortLevel), typeof(Player))]
        public static class SE_Rested_CalculateComfortLevel_Player_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Player player, ref int __result)
            {
                if (!ModConfig.ModEnabled.Value || !ModConfig.EnableStructureComfort.Value || player == null)
                {
                    return true;
                }

                bool isInside = player.InShelter() || ThermalGraphManager.IsInsideCompound(player);
                __result = ThermalGraphManager.Instance.CalculateDwellingComfort(player, isInside, player.transform.position);
                return false;
            }
        }

        /// <summary>
        /// Intercepts position-based comfort calculations as fallback.
        /// </summary>
        [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.CalculateComfortLevel), typeof(bool), typeof(Vector3))]
        public static class SE_Rested_CalculateComfortLevel_Pos_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(bool inShelter, Vector3 position, ref int __result)
            {
                if (!ModConfig.ModEnabled.Value || !ModConfig.EnableStructureComfort.Value)
                {
                    return true;
                }

                if (Player.m_localPlayer != null && Vector3.Distance(position, Player.m_localPlayer.transform.position) < 3.0f)
                {
                    bool isInside = inShelter || ThermalGraphManager.IsInsideCompound(Player.m_localPlayer);
                    __result = ThermalGraphManager.Instance.CalculateDwellingComfort(Player.m_localPlayer, isInside, position);
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Replaces the hardcoded campfire proximity check for sleeping in beds
        /// with an evaluation of the player's thermal comfort and shelter.
        /// </summary>
        [HarmonyPatch(typeof(Bed), "CheckFire")]
        public static class Bed_CheckFire_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Bed __instance, Player human, ref bool __result)
            {
                if (!ModConfig.ModEnabled.Value || !ModConfig.EnableThermalResting.Value || human == null)
                {
                    return true;
                }

                float playerTemp = VikingClimate.Core.SurvivalManager.CurrentState.BodyWarmth;
                float minTemp = ModConfig.ComfortMinTemp.Value;
                float maxTemp = ModConfig.ComfortMaxTemp.Value;

                bool isInside = ThermalGraphManager.IsInsideCompound(human) || human.InShelter();

                if (!isInside)
                {
                    human.Message(MessageHud.MessageType.Center, "$msg_bedneedroof");
                    __result = false;
                    return false;
                }

                if (ModConfig.RequireWarmthToSleep.Value && playerTemp < minTemp)
                {
                    human.Message(MessageHud.MessageType.Center, $"You are too cold to sleep ({playerTemp:F0}°C / Need {minTemp:F0}°C)");
                    __result = false;
                    return false;
                }

                if (playerTemp > maxTemp)
                {
                    human.Message(MessageHud.MessageType.Center, $"You are too hot to sleep ({playerTemp:F0}°C)");
                    __result = false;
                    return false;
                }

                __result = true;
                return false;
            }
        }

        /// <summary>
        /// Configures the countdown timer required on SE_Cozy before the Rested buff is granted or refreshed.
        /// Requires staying in the good zone for at least 10s.
        /// </summary>
        [HarmonyPatch(typeof(SE_Cozy), nameof(SE_Cozy.Setup))]
        public static class SE_Cozy_Setup_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(SE_Cozy __instance)
            {
                if (ModConfig.ModEnabled.Value && ModConfig.EnableThermalResting.Value)
                {
                    __instance.m_delay = Mathf.Max(5.0f, ModConfig.RestedDelaySeconds.Value);
                }
            }
        }
    }
}
