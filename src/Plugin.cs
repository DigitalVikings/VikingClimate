using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;
using VikingClimate.UI;
using VikingLib.UI.Hub;

namespace VikingClimate
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.digitalvikings.vikinglib", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("randyknapp.mods.auga", BepInDependency.DependencyFlags.SoftDependency)]
    public class VikingClimatePlugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.digitalvikings.vikingclimate";
        public const string ModName = "VikingClimate";
        public const string ModVersion = "0.4.0";

        public static VikingClimatePlugin Instance { get; private set; } = null!;
        internal static ManualLogSource Log = null!;
        private Harmony _harmony = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{ModName} v{ModVersion} initializing...");

            // Exclude VikingClimate from Project Auga's AutoRestyle to protect custom UI graphics
            ExcludeFromAugaRestyle();

            // Initialize configuration
            ModConfig.Init(Config);

            // Initialize survival manager and temperature system
            SurvivalManager.Init();
            TemperatureSystem.Init();

            // Register Climate tab with VikingHub
            VikingHubRegistry.RegisterTab(new ClimateHubTab());

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

        private static void ExcludeFromAugaRestyle()
        {
            try
            {
                if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("randyknapp.mods.auga"))
                {
                    System.Type? augaApiType = System.Type.GetType("Auga.API, Auga");
                    if (augaApiType != null)
                    {
                        var method = augaApiType.GetMethod("AutoRestyle_Exclude", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        method?.Invoke(null, new object[] { ModGuid });
                        Log.LogInfo($"[{ModName}] Successfully registered AutoRestyle exclusion with Project Auga.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"[{ModName}] Could not register Auga AutoRestyle exclusion: {ex.Message}");
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void HookJotunnSync()
        {
            Jotunn.Managers.SynchronizationManager.OnConfigurationSynchronized += (sender, args) =>
            {
                Log.LogInfo("Server configuration synchronized successfully.");
                ThermalStatusEffects.ReapplyConfigModifiers();
            };
        }

        private void OnDestroy()
        {
            VikingHubRegistry.UnregisterTab(ClimateHubTab.TabId);
            _harmony?.UnpatchSelf();
        }
    }
}
