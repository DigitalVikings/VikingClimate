using HarmonyLib;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;
using VikingClimate.Features.Temperature.Services;

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
            if (!ModConfig.ModEnabled.Value || ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph) return;
            ThermalGraphManager.Instance.WakeCycle(15);
        }

        /// <summary>
        /// Register a newly placed piece in the structural thermal graph.
        /// </summary>
        [HarmonyPatch(typeof(Piece), nameof(Piece.OnPlaced))]
        [HarmonyPostfix]
        private static void OnPiecePlaced(Piece __instance)
        {
            if (!ModConfig.ModEnabled.Value || ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph || __instance == null) return;

            DwellingAnalyzer.InvalidateComfortCache();
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
            if (!ModConfig.ModEnabled.Value || ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph || __instance != Player.m_localPlayer) return;
            DwellingAnalyzer.InvalidateComfortCache();
            ThermalGraphManager.Instance.WakeCycle(25);
        }

        /// <summary>
        /// Unregister destroyed piece from the structural graph.
        /// </summary>
        [HarmonyPatch(typeof(WearNTear), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnWearNTearDestroyed(WearNTear __instance)
        {
            if (!ModConfig.ModEnabled.Value || ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph || __instance == null) return;

            DwellingAnalyzer.InvalidateComfortCache();
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
            if (!ModConfig.ModEnabled.Value || ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph || __instance == null) return;
            Vector3 center = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : __instance.transform.position;
            ThermalGraphManager.Instance.UpdateActiveFires(center);
            ThermalGraphManager.Instance.WakeCycle(20);
        }

        /// <summary>
        /// Ensure all structural temperatures are flushed to ZDO and memory graph is reset when the player teleports.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
        [HarmonyPostfix]
        private static void OnPlayerTeleported(Player __instance, bool __result)
        {
            if (!ModConfig.ModEnabled.Value || ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph) return;
            if (__instance != Player.m_localPlayer || !__result) return;

            ThermalGraphManager.Instance.SyncAllNodesToZdo();
            ThermalGraphManager.Instance.Clear();
        }
    }
}
