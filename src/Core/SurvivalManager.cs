using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Features.Temperature;

namespace VikingClimate.Core
{
    /// <summary>
    /// Central manager orchestrating player survival state updates, environmental evaluations,
    /// periodic survival ticks, and VikingLib stat engine integration.
    /// </summary>
    public static class SurvivalManager
    {
        private static float _tickTimer = 0.0f;
        private const float TickInterval = 1.0f; // 1-second discrete survival tick interval

        public static SurvivalState CurrentState { get; } = new SurvivalState();
        public static bool IsInitialized { get; private set; }
        public static bool PendingDeathRespawn { get; set; }

        public static event Action<Player, SurvivalState, float>? OnSurvivalTick;

        public static void Init()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            VikingClimatePlugin.Log.LogInfo("SurvivalManager initialized.");
        }

        private static readonly object WaterloggedOwnerKey = new();

        public static void OnPlayerDeath(Player player)
        {
            if (player == null || player != Player.m_localPlayer) return;
            PendingDeathRespawn = true;
            if (player.m_customData != null)
            {
                player.m_customData[SurvivalState.KeyDeathReset] = "1";
            }
            VikingClimatePlugin.Log.LogInfo($"Player '{player.GetPlayerName()}' died. Flagged for temperature reset on respawn.");
        }

        public static void ResetTemperatureOnRespawn(Player player)
        {
            Vector3 spawnPos = player.transform.position;
            bool inDungeon = player.InInterior();
            float outdoorAmb = AmbientTemperatureCalculator.CalculateOutdoorAmbient(spawnPos, inDungeon);

            // Scan spatial octree for nearby claimed bed matching player's current spawn point
            var nearbyPieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(spawnPos, 8.0f, nearbyPieces);

            Bed? currentBed = null;
            for (int i = 0; i < nearbyPieces.Count; i++)
            {
                var piece = nearbyPieces[i];
                if (piece == null) continue;
                var bed = piece.GetComponent<Bed>();
                if (bed != null && bed.IsCurrent())
                {
                    currentBed = bed;
                    break;
                }
            }

            float targetWarmth;
            if (currentBed != null)
            {
                Vector3 bedPos = currentBed.transform.position;
                if (ModConfig.ThermalMassSimulationMode.Value == ThermalMassMode.StructuralGraph)
                {
                    ThermalGraphManager.Instance.ScanNearbyPieces(bedPos, outdoorAmb);
                    float bedAreaTemp = ThermalGraphManager.Instance.GetAverageTemperatureAroundPoint(bedPos, 8.0f);
                    targetWarmth = !float.IsNaN(bedAreaTemp) ? bedAreaTemp : outdoorAmb;
                }
                else
                {
                    targetWarmth = outdoorAmb;
                }
                VikingClimatePlugin.Log.LogInfo($"Player spawned at claimed bed. Resolved surrounding structure temperature: {targetWarmth:F1}°C");
            }
            else
            {
                targetWarmth = outdoorAmb;
                VikingClimatePlugin.Log.LogInfo($"Player spawned at sacrificial stones / world spawn. Resolved outdoor ambient: {targetWarmth:F1}°C");
            }

            TemperatureSystem.OnPlayerRespawnReset(player, targetWarmth);
        }

        public static void OnPlayerSpawn(Player player)
        {
            if (player == null || player != Player.m_localPlayer) return;

            bool isDeathRespawn = PendingDeathRespawn;
            if (!isDeathRespawn && player.m_customData != null &&
                player.m_customData.TryGetValue(SurvivalState.KeyDeathReset, out string val) && val == "1")
            {
                isDeathRespawn = true;
            }

            if (isDeathRespawn)
            {
                PendingDeathRespawn = false;
                if (player.m_customData != null)
                {
                    player.m_customData.Remove(SurvivalState.KeyDeathReset);
                }
                ResetTemperatureOnRespawn(player);
            }
            else
            {
                CurrentState.LoadFromPlayer(player);
                VikingClimatePlugin.Log.LogInfo($"Loaded survival state for '{player.GetPlayerName()}': BodyWarmth={CurrentState.BodyWarmth:F1}°C");
            }

            RegisterWaterloggedModifiers(player);
        }

        private static void RegisterWaterloggedModifiers(Player player)
        {
            // Waterlogged gear imposes -5% movement speed penalty while wet
            VikingLib.Stats.CharacterStatManager.AddIncreased(
                player,
                VikingLib.Stats.CharacterStats.JogSpeed,
                -0.05f,
                "Waterlogged",
                c => c.GetSEMan()?.HaveStatusEffect(SEMan.s_statusEffectWet) == true,
                WaterloggedOwnerKey
            );
            VikingLib.Stats.CharacterStatManager.AddIncreased(
                player,
                VikingLib.Stats.CharacterStats.RunSpeed,
                -0.05f,
                "Waterlogged",
                c => c.GetSEMan()?.HaveStatusEffect(SEMan.s_statusEffectWet) == true,
                WaterloggedOwnerKey
            );
        }

        public static void OnPlayerSave(Player player)
        {
            if (player == null || player != Player.m_localPlayer) return;

            CurrentState.SaveToPlayer(player);
        }

        public static void Update(Player player, float dt)
        {
            if (!ModConfig.ModEnabled.Value || player == null || player != Player.m_localPlayer) return;
            if (player.IsDead() || player.IsTeleporting()) return;

            _tickTimer += dt;
            if (_tickTimer >= TickInterval)
            {
                float elapsed = _tickTimer;
                _tickTimer = 0.0f;

                Tick(player, elapsed);
            }
        }

        private static void Tick(Player player, float dt)
        {
            try
            {
                OnSurvivalTick?.Invoke(player, CurrentState, dt);
            }
            catch (Exception ex)
            {
                VikingClimatePlugin.Log.LogError($"Error in survival tick handler: {ex}");
            }

            OnPlayerSave(player);
        }
    }
}
