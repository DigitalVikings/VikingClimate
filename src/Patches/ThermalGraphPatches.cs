using HarmonyLib;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;

namespace VikingClimate.Patches
{
    [HarmonyPatch]
    public static class ThermalGraphPatches
    {
        /// <summary>
        /// Notify structural graph when a door opens or closes.
        /// </summary>
        [HarmonyPatch(typeof(Door), "SetState")]
        [HarmonyPostfix]
        private static void OnDoorStateChanged()
        {
            if (!ModConfig.ModEnabled.Value) return;
            ThermalGraphManager.Instance.WakeCycle(15);
        }

        /// <summary>
        /// Register a newly placed piece in the structural thermal graph.
        /// </summary>
        [HarmonyPatch(typeof(Piece), nameof(Piece.OnPlaced))]
        [HarmonyPostfix]
        private static void OnPiecePlaced(Piece __instance)
        {
            if (!ModConfig.ModEnabled.Value || __instance == null) return;

            float amb = SurvivalManager.CurrentState.AmbientTemperature;
            ThermalGraphManager.Instance.RegisterPiece(__instance, amb, wakeCycle: true);
        }

        /// <summary>
        /// Fallback hook for Player.PlacePiece to ensure player build cycles wake the graph.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        [HarmonyPostfix]
        private static void OnPlayerPlacePiece(Player __instance)
        {
            if (!ModConfig.ModEnabled.Value || __instance != Player.m_localPlayer) return;
            ThermalGraphManager.Instance.WakeCycle(25);
        }

        /// <summary>
        /// Unregister destroyed piece from the structural graph.
        /// </summary>
        [HarmonyPatch(typeof(WearNTear), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnWearNTearDestroyed(WearNTear __instance)
        {
            if (!ModConfig.ModEnabled.Value || __instance == null) return;

            Piece piece = __instance.GetComponent<Piece>();
            if (piece != null)
            {
                ThermalGraphManager.Instance.UnregisterPiece(piece);
            }
            else
            {
                ThermalGraphManager.Instance.WakeCycle(20);
            }
        }

        /// <summary>
        /// Wake up structural relaxation when a fireplace changes state (ignited, fueled, or extinguished).
        /// </summary>
        [HarmonyPatch(typeof(Fireplace), "UpdateState")]
        [HarmonyPostfix]
        private static void OnFireplaceStateChanged(Fireplace __instance)
        {
            if (!ModConfig.ModEnabled.Value || __instance == null) return;
            ThermalGraphManager.Instance.WakeCycle(20);
        }
    }
}
