using System;
using System.Collections.Generic;

namespace VikingClimate.Features.Temperature
{
    /// <summary>
    /// Comprehensive insulation registry covering Valheim equipment, armors, cloaks, and resistance meads.
    /// Padded armor (quilted linen) is intentionally valued higher than cold conductive metals (bronze/iron).
    /// </summary>
    public static class GearInsulationRegistry
    {
        private static readonly Dictionary<string, float> _gearInsulation = new(StringComparer.OrdinalIgnoreCase)
        {
            // Helms - Prefab and Shared Tokens
            { "HelmetLeather", 0.05f },
            { "$item_helmet_leather", 0.05f },
            { "HelmetTrollLeather", 0.07f },
            { "$item_helmet_trollleather", 0.07f },
            { "HelmetBronze", 0.03f },
            { "$item_helmet_bronze", 0.03f },
            { "HelmetIron", 0.03f },
            { "$item_helmet_iron", 0.03f },
            { "HelmetPadded", 0.12f },
            { "$item_helmet_padded", 0.12f },
            { "HelmetDrake", 0.15f },
            { "$item_helmet_drake", 0.15f },
            { "HelmetFenris", 0.12f },
            { "$item_helmet_fenris", 0.12f },
            { "HelmetMage", 0.10f },
            { "$item_helmet_mage", 0.10f },
            { "HelmetCarapace", 0.08f },
            { "$item_helmet_carapace", 0.08f },
            { "HelmetFlametal", 0.06f },
            { "$item_helmet_flametal", 0.06f },
            { "HelmetAshlandsMedium", 0.08f },
            { "$item_helmet_ashlandsmedium", 0.08f },

            // Chests - Prefab and Shared Tokens
            { "ArmorRags", 0.05f },
            { "$item_chest_rags", 0.05f },
            { "$item_rags_chest", 0.05f },
            { "ArmorLeatherChest", 0.10f },
            { "$item_chest_leather", 0.10f },
            { "ArmorTrollLeatherChest", 0.12f },
            { "$item_chest_trollleather", 0.12f },
            { "ArmorBronzeChest", 0.08f },
            { "$item_chest_bronze", 0.08f },
            { "ArmorIronChest", 0.09f },
            { "$item_chest_iron", 0.09f },
            { "ArmorPaddedChest", 0.22f }, // Thick quilted linen gambeson
            { "$item_chest_padded", 0.22f },
            { "ArmorWolfChest", 0.30f },   // Heavy alpine wolf pelt
            { "$item_chest_wolf", 0.30f },
            { "ArmorFenrisChest", 0.26f },
            { "$item_chest_fenris", 0.26f },
            { "ArmorMageChest", 0.18f },   // Heavy woven eitr fabric
            { "$item_chest_mage", 0.18f },
            { "ArmorCarapaceChest", 0.12f },
            { "$item_chest_carapace", 0.12f },
            { "ArmorFlametalChest", 0.10f },
            { "$item_chest_flametal", 0.10f },
            { "ArmorAskChest", 0.14f },
            { "$item_chest_ask", 0.14f },

            // Legs - Prefab and Shared Tokens
            { "ArmorRagsLegs", 0.05f },
            { "$item_legs_rags", 0.05f },
            { "$item_rags_legs", 0.05f },
            { "ArmorLeatherLegs", 0.08f },
            { "$item_legs_leather", 0.08f },
            { "ArmorTrollLeatherLegs", 0.10f },
            { "$item_legs_trollleather", 0.10f },
            { "ArmorBronzeLegs", 0.06f },
            { "$item_legs_bronze", 0.06f },
            { "ArmorIronLegs", 0.07f },
            { "$item_legs_iron", 0.07f },
            { "ArmorPaddedLegs", 0.18f },
            { "$item_legs_padded", 0.18f },
            { "ArmorWolfLegs", 0.25f },
            { "$item_legs_wolf", 0.25f },
            { "ArmorFenrisLegs", 0.22f },
            { "$item_legs_fenris", 0.22f },
            { "ArmorMageLegs", 0.14f },
            { "$item_legs_mage", 0.14f },
            { "ArmorCarapaceLegs", 0.10f },
            { "$item_legs_carapace", 0.10f },
            { "ArmorFlametalLegs", 0.08f },
            { "$item_legs_flametal", 0.08f },
            { "ArmorAskLegs", 0.12f },
            { "$item_legs_ask", 0.12f },

            // Capes & Cloaks - Prefab and Shared Tokens
            { "CapeDeerHide", 0.08f },
            { "$item_cape_deerhide", 0.08f },
            { "CapeTrollHide", 0.10f },
            { "$item_cape_trollhide", 0.10f },
            { "CapeLinen", 0.10f },
            { "$item_cape_linen", 0.10f },
            { "CapeWolf", 0.20f },
            { "$item_cape_wolf", 0.20f },
            { "CapeLox", 0.22f },
            { "$item_cape_lox", 0.22f },
            { "CapeFeather", 0.20f },
            { "$item_cape_feather", 0.20f },
            { "CapeAsk", 0.22f },
            { "$item_cape_ask", 0.22f },
            { "CapeAsh", 0.24f },
            { "$item_cape_ash", 0.24f }
        };

        /// <summary>
        /// Calculates the total cold insulation rating of the player's equipped gear (0.0 to 0.90 cap).
        /// Gathers equipped items across both the inventory and humanoid equipment slots to ensure robustness.
        /// </summary>
        public static float CalculatePlayerInsulation(Player player, out bool hasFrostResistanceMead)
        {
            hasFrostResistanceMead = false;
            if (player == null) return 0.0f;

            float totalInsulation = 0.0f;
            var equippedSet = new HashSet<ItemDrop.ItemData>();

            var inventory = player.GetInventory();
            if (inventory != null)
            {
                var equipped = inventory.GetEquippedItems();
                for (int i = 0; i < equipped.Count; i++)
                {
                    if (equipped[i]?.m_shared != null && IsInsulatingArmorSlot(equipped[i].m_shared.m_itemType))
                    {
                        equippedSet.Add(equipped[i]);
                    }
                }
            }

            foreach (var item in equippedSet)
            {
                string cleanPrefab = item.m_dropPrefab != null ? item.m_dropPrefab.name.Replace("(Clone)", "").Trim() : string.Empty;
                string cleanShared = item.m_shared != null ? item.m_shared.m_name : string.Empty;
                string queryName = !string.IsNullOrEmpty(cleanPrefab) ? cleanPrefab : cleanShared;

                totalInsulation += GetItemInsulation(queryName, item.m_shared);
            }

            // Check for Frost Resistance Mead or active frost-resist status effect
            var seMan = player.GetSEMan();
            if (seMan != null)
            {
                if (seMan.HaveStatusEffect("Potion_frostresist".GetStableHashCode()) ||
                    seMan.HaveStatusEffect("FrostResist".GetStableHashCode()) ||
                    seMan.HaveStatusEffect("MeadFrostResist".GetStableHashCode()))
                {
                    hasFrostResistanceMead = true;
                }
                else
                {
                    // Generic fallback: check active status effects for any frost resistance modifier (modded meads/buffs)
                    var activeEffects = seMan.GetStatusEffects();
                    for (int i = 0; i < activeEffects.Count; i++)
                    {
                        if (activeEffects[i] is SE_Stats seStats && seStats.m_mods != null)
                        {
                            for (int m = 0; m < seStats.m_mods.Count; m++)
                            {
                                var mod = seStats.m_mods[m];
                                if (mod.m_type == HitData.DamageType.Frost &&
                                    (mod.m_modifier == HitData.DamageModifier.Resistant || mod.m_modifier == HitData.DamageModifier.VeryResistant))
                                {
                                    hasFrostResistanceMead = true;
                                    break;
                                }
                            }
                            if (hasFrostResistanceMead) break;
                        }
                    }
                }
            }

            return Math.Min(0.90f, totalInsulation);
        }

        public static bool IsInsulatingArmorSlot(ItemDrop.ItemData.ItemType itemType)
            => itemType == ItemDrop.ItemData.ItemType.Helmet ||
               itemType == ItemDrop.ItemData.ItemType.Chest ||
               itemType == ItemDrop.ItemData.ItemType.Legs ||
               itemType == ItemDrop.ItemData.ItemType.Shoulder;

        public static float GetItemInsulation(string name, ItemDrop.ItemData.SharedData? shared)
        {
            if (string.IsNullOrEmpty(name) && shared == null) return 0.0f;
            if (shared != null && !IsInsulatingArmorSlot(shared.m_itemType)) return 0.0f;

            // 1. Direct dictionary check with provided name
            if (!string.IsNullOrEmpty(name))
            {
                if (_gearInsulation.TryGetValue(name, out float val)) return val;

                string stripped = name.Replace("(Clone)", "").Trim();
                if (_gearInsulation.TryGetValue(stripped, out val)) return val;
            }

            // 2. Shared token and localized name check
            if (shared != null && !string.IsNullOrEmpty(shared.m_name))
            {
                if (_gearInsulation.TryGetValue(shared.m_name, out float sharedVal)) return sharedVal;

                if (Localization.instance != null)
                {
                    string localized = Localization.instance.Localize(shared.m_name);
                    if (!string.IsNullOrEmpty(localized) && _gearInsulation.TryGetValue(localized, out float locVal)) return locVal;
                }
            }

            // 3. Check if item grants native Frost resistance
            if (shared?.m_damageModifiers != null)
            {
                foreach (var mod in shared.m_damageModifiers)
                {
                    if (mod.m_type == HitData.DamageType.Frost && mod.m_modifier != HitData.DamageModifier.Normal)
                    {
                        if (mod.m_modifier == HitData.DamageModifier.Resistant || mod.m_modifier == HitData.DamageModifier.VeryResistant)
                        {
                            return 0.22f; // Auto-fallback for custom or modded frost-resistant gear
                        }
                    }
                }
            }

            // 4. Keyword heuristics fallback for modded or non-standard gear
            string combined = $"{name} {shared?.m_name ?? string.Empty}".ToLowerInvariant();
            if (Localization.instance != null && shared != null && !string.IsNullOrEmpty(shared.m_name))
            {
                combined += " " + Localization.instance.Localize(shared.m_name).ToLowerInvariant();
            }

            if (combined.Contains("wolf") || combined.Contains("fenris") || combined.Contains("lox")) return 0.20f;
            if (combined.Contains("padded")) return 0.16f;

            // Explicit iron armor heuristic coverage
            if (combined.Contains("iron"))
            {
                if (combined.Contains("chest") || combined.Contains("scale") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Chest)) return 0.09f;
                if (combined.Contains("leg") || combined.Contains("greaves") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Legs)) return 0.07f;
                if (combined.Contains("helmet") || combined.Contains("helm") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet)) return 0.03f;
                return 0.07f;
            }

            // Bronze armor heuristic coverage
            if (combined.Contains("bronze"))
            {
                if (combined.Contains("chest") || combined.Contains("cuirass") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Chest)) return 0.08f;
                if (combined.Contains("leg") || combined.Contains("leggings") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Legs)) return 0.06f;
                if (combined.Contains("helmet") || combined.Contains("helm") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet)) return 0.03f;
                return 0.05f;
            }

            // Carapace armor heuristic coverage
            if (combined.Contains("carapace"))
            {
                if (combined.Contains("chest") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Chest)) return 0.12f;
                if (combined.Contains("leg") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Legs)) return 0.10f;
                if (combined.Contains("helmet") || combined.Contains("helm") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet)) return 0.08f;
                return 0.08f;
            }

            // Flametal armor heuristic coverage
            if (combined.Contains("flametal"))
            {
                if (combined.Contains("chest") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Chest)) return 0.10f;
                if (combined.Contains("leg") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Legs)) return 0.08f;
                if (combined.Contains("helmet") || combined.Contains("helm") || (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet)) return 0.06f;
                return 0.07f;
            }

            if (combined.Contains("troll") || combined.Contains("leather")) return 0.08f;
            if (combined.Contains("cape") || combined.Contains("cloak")) return 0.10f;
            if (combined.Contains("armor") || combined.Contains("helmet") || combined.Contains("chest") || combined.Contains("legs")) return 0.05f;

            if (shared != null)
            {
                switch (shared.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.Chest: return 0.05f;
                    case ItemDrop.ItemData.ItemType.Legs: return 0.04f;
                    case ItemDrop.ItemData.ItemType.Helmet: return 0.03f;
                    case ItemDrop.ItemData.ItemType.Shoulder: return 0.06f;
                }
            }

            return 0.0f;
        }
    }
}
