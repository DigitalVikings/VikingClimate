using System;
using VikingClimate.Configuration;

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

        public static event Action<Player, SurvivalState, float>? OnSurvivalTick;

        public static void Init()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            VikingClimatePlugin.Log.LogInfo("SurvivalManager initialized.");
        }

        private static readonly object WaterloggedOwnerKey = new();

        public static void OnPlayerSpawn(Player player)
        {
            if (player == null || player != Player.m_localPlayer) return;

            CurrentState.LoadFromPlayer(player);
            VikingClimatePlugin.Log.LogInfo($"Loaded survival state for '{player.GetPlayerName()}': BodyWarmth={CurrentState.BodyWarmth:F1}°C");

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
