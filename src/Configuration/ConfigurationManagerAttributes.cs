namespace VikingClimate.Configuration
{
    /// <summary>
    /// Custom attributes recognized by BepInEx ConfigurationManager and Jotunn SynchronizationManager.
    /// Used for categories, display ordering, and server-authoritative multiplayer synchronization.
    /// </summary>
    public class ConfigurationManagerAttributes
    {
        public bool? Browsable;
        public string? Category;
        public bool? DefaultValue;
        public bool? HideDefaultButton;
        public bool? HideSettingName;
        public string? Description;
        public int? Order;
        public bool? ReadOnly;
        public bool? IsAdvanced;

        /// <summary>
        /// If true, this setting is synchronized from server to clients in multiplayer,
        /// and can only be modified by server admins.
        /// </summary>
        public bool? IsAdminOnly;
    }
}
