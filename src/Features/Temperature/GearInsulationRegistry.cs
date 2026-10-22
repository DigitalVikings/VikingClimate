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
            // Helms
            { "HelmetLeather", 0.05f },
            { "HelmetTrollLeather", 0.07f },
            { "HelmetBronze", 0.03f },
            { "HelmetIron", 0.03f },
            { "HelmetPadded", 0.12f },
            { "HelmetDrake", 0.15f },
            { "HelmetFenris", 0.12f },
            { "HelmetMage", 0.10f },
            { "HelmetCarapace", 0.08f },
            { "HelmetFlametal", 0.06f },
            { "HelmetAshlandsMedium", 0.08f },

            // Chests
            { "ArmorRags", 0.05f },
            { "ArmorLeatherChest", 0.10f },
            { "ArmorTrollLeatherChest", 0.12f },
            { "ArmorBronzeChest", 0.08f },
            { "ArmorIronChest", 0.09f },
            { "ArmorPaddedChest", 0.22f }, // Thick quilted linen gambeson
            { "ArmorWolfChest", 0.30f },   // Heavy alpine wolf pelt
            { "ArmorFenrisChest", 0.26f },
            { "ArmorMageChest", 0.18f },   // Heavy woven eitr fabric
            { "ArmorCarapaceChest", 0.12f },
            { "ArmorFlametalChest", 0.10f },
            { "ArmorAskChest", 0.14f },

            // Legs
            { "ArmorRagsLegs", 0.05f },
            { "ArmorLeatherLegs", 0.08f },
            { "ArmorTrollLeatherLegs", 0.10f },
            { "ArmorBronzeLegs", 0.06f },
            { "ArmorIronLegs", 0.07f },
            { "ArmorPaddedLegs", 0.18f },
            { "ArmorWolfLegs", 0.25f },
            { "ArmorFenrisLegs", 0.22f },
            { "ArmorMageLegs", 0.14f },
            { "ArmorCarapaceLegs", 0.10f },
            { "ArmorFlametalLegs", 0.08f },
            { "ArmorAskLegs", 0.12f },

            // Capes & Cloaks
            { "CapeDeerHide", 0.08f },
            { "CapeTrollHide", 0.10f },
            { "CapeLinen", 0.10f },
            { "CapeWolf", 0.20f },
            { "CapeLox", 0.22f },
            { "CapeFeather", 0.20f },
            { "CapeAsk", 0.22f },
            { "CapeAsh", 0.24f }
        };

        /// <summary>
        /// Calculates the total cold insulation rating of the player's equipped gear (0.0 to 0.90 cap).
        /// </summary>
        public static float CalculatePlayerInsulation(Player player, out bool hasFrostResistanceMead)
        {
            hasFrostResistanceMead = false;
            if (player == null) return 0.0f;

            float totalInsulation = 0.0f;

            var inventory = player.GetInventory();
            if (inventory != null)
            {
                var equipped = inventory.GetEquippedItems();
                for (int i = 0; i < equipped.Count; i++)
                {
                    var item = equipped[i];
                    if (item?.m_shared == null) continue;

                    string prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
                    totalInsulation += GetItemInsulation(prefabName, item.m_shared);
                }
            }

            // Check for Frost Resistance Mead or active frost-resist status effect
            var seMan = player.GetSEMan();
            if (seMan != null)
            {
                hasFrostResistanceMead = seMan.HaveStatusEffect("FrostResist".GetStableHashCode()) ||
                                         seMan.HaveStatusEffect("MeadFrostResist".GetStableHashCode());
            }

            return Math.Min(0.90f, totalInsulation);
        }

        public static float GetItemInsulation(string name, ItemDrop.ItemData.SharedData shared)
        {
            if (string.IsNullOrEmpty(name)) return 0.0f;

            if (_gearInsulation.TryGetValue(name, out float val))
            {
                return val;
            }

            // Check if item grants native Frost resistance
            if (shared.m_damageModifiers != null)
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

            // Keyword heuristics fallback for modded gear
            string lower = name.ToLowerInvariant();
            if (lower.Contains("wolf") || lower.Contains("fenris") || lower.Contains("lox")) return 0.20f;
            if (lower.Contains("padded")) return 0.16f;
            if (lower.Contains("troll") || lower.Contains("leather")) return 0.08f;
            if (lower.Contains("cape") || lower.Contains("cloak")) return 0.10f;
            if (lower.Contains("armor") || lower.Contains("helmet")) return 0.05f;

            return 0.0f;
        }
    }
}
