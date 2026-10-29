using BepInEx.Configuration;

namespace VikingClimate.Configuration
{
    internal static class ConfigExtensions
    {
        public static ConfigEntry<T> BindSynced<T>(
            this ConfigFile config,
            string section,
            string key,
            T defaultValue,
            string description,
            int order = 0,
            AcceptableValueBase? acceptableValues = null,
            bool isAdminOnly = true)
        {
            var attributes = new ConfigurationManagerAttributes
            {
                IsAdminOnly = isAdminOnly,
                Order = order
            };

            var desc = new ConfigDescription(description, acceptableValues, attributes);
            return config.Bind(section, key, defaultValue, desc);
        }
    }
}
