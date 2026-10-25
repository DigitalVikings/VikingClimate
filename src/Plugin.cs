using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;

namespace VikingClimate
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.digitalvikings.vikinglib", BepInDependency.DependencyFlags.HardDependency)]
    public class VikingClimatePlugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.digitalvikings.vikingclimate";
        public const string ModName = "VikingClimate";
        public const string ModVersion = "0.2.0";

        public static VikingClimatePlugin Instance { get; private set; } = null!;
        internal static ManualLogSource Log = null!;
        private Harmony _harmony = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{ModName} v{ModVersion} initializing...");

            // Initialize configuration
            ModConfig.Init(Config);

            // Initialize survival manager and temperature system
            SurvivalManager.Init();
            TemperatureSystem.Init();

            // Apply Harmony patches
            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll();

            Log.LogInfo($"{ModName} v{ModVersion} loaded successfully. Thermal simulation active.");

            // Listen for server synchronization events if Jotunn is active
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.jotunn.jotunn"))
            {
                HookJotunnSync();
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void HookJotunnSync()
        {
            Jotunn.Managers.SynchronizationManager.OnConfigurationSynchronized += (sender, args) =>
            {
                Log.LogInfo("Server configuration synchronized successfully.");
            };
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
