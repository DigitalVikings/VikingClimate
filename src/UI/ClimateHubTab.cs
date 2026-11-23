#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;
using VikingLib.UI.Hub;
using VikingLib.UI.Hub.Core;
using VikingLib.UI.Hub.Rendering;

namespace VikingClimate.UI
{
    /// <summary>
    /// Live Climate &amp; Thermal Simulation dashboard hosted inside <see cref="VikingHubDialog"/>.
    /// </summary>
    public sealed class ClimateHubTab : IStructuredHubTab
    {
        public const string TabId = "climate";
        private const string IconKey = "vc_hub_climate";

        public string Id => TabId;
        public string DisplayName => "Climate";
        public int SortOrder => 40;

        public Sprite? HubIcon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _iconLoaded = true;
                    _hubIcon = VikingHubTheme.LoadEmbeddedSprite(typeof(ClimateHubTab).Assembly, "hub_climate.png");
                    if (_hubIcon != null)
                    {
                        HubSpriteAtlas.RegisterExternalSprite(IconKey, _hubIcon);
                    }
                }
                return _hubIcon;
            }
        }

        private static Sprite? _hubIcon;
        private static bool _iconLoaded;

        public HubPortalSummary GetPortalSummary()
        {
            Player? player = Player.m_localPlayer;
            if (player == null)
            {
                return new HubPortalSummary(
                    statusLine: "No active character",
                    subtitle: string.Empty);
            }

            SurvivalState state = SurvivalManager.CurrentState;
            return new HubPortalSummary(
                statusLine: $"Body {state.BodyWarmth:F1}°C • Ambient {state.AmbientTemperature:F1}°C • Conduction {state.ConductionRate:P0}",
                alertBadge: GetTierDisplayName(state.ActiveThermalTierId),
                subtitle: string.Empty);
        }

        public HubView BuildView(HubViewContext ctx)
        {
            var view = new HubView
            {
                ViewTitle = "Climate",
                ShowBackToHub = true
            };

            Player? player = Player.m_localPlayer;
            if (player == null)
            {
                view.Root = new TextNode("No active character.", TextStyle.Muted, TextAlign.Center);
                return view;
            }

            SurvivalState state = SurvivalManager.CurrentState;
            AmbientThermalBreakdown bd = state.Breakdown;

            var scroll = new ScrollColumnNode { Id = "ClimateScroll", ItemGap = 12f };
            var bento = new BentoGridNode { Id = "ClimateBento", Columns = 2 };

            CardNode bodyCard = BuildBodyAndInsulationCard(player, state, ctx);
            bodyCard.PreferredColumn = 0;
            bento.Items.Add(bodyCard);

            CardNode effectsCard = BuildActiveClimateEffectsCard(player, state);
            effectsCard.PreferredColumn = 0;
            bento.Items.Add(effectsCard);

            CardNode ambientCard = BuildAmbientBreakdownCard(state, bd, ctx);
            ambientCard.PreferredColumn = 1;
            bento.Items.Add(ambientCard);

            scroll.Children.Add(bento);
            view.Root = scroll;

            return view;
        }

        // Legacy IVikingHubTab fallback (unused by VikingHubDialog for IStructuredHubTab)
        public void BuildContent(RectTransform container) { }
        public void OnShow() { }
        public void OnHide() { }
        public void OnRefresh() { }
        public bool HandleBack() => false;

        private static CardNode BuildBodyAndInsulationCard(
            Player player,
            SurvivalState state,
            HubViewContext ctx)
        {
            var card = new CardNode
            {
                Id = "ClimateBodyCard",
                Title = "Body & Insulation"
            };

            card.Children.Add(new StatRowNode
            {
                Id = "Climate_BodyWarmth",
                Label = "Body Warmth",
                Value = $"{state.BodyWarmth:F1}°C",
                Highlight = true,
                ToneKey = state.BodyWarmth < 18f || state.BodyWarmth > 26f ? "negative" : "positive"
            });

            string velTone = state.TemperatureVelocity > 0.001f ? "positive" : (state.TemperatureVelocity < -0.001f ? "negative" : "flat");
            card.Children.Add(new StatRowNode
            {
                Id = "Climate_Velocity",
                Label = "Temperature Velocity",
                Value = $"{state.TemperatureVelocity:+0.00;-0.00;0.00}°C/s",
                ToneKey = velTone
            });

            card.Children.Add(new StatRowNode
            {
                Id = "Climate_Conduction",
                Label = "Conduction Rate",
                Value = $"{state.ConductionRate:P0}",
                ToneKey = "flat"
            });

            card.Children.Add(new StatRowNode
            {
                Id = "Climate_WetMult",
                Label = "Wet Multiplier",
                Value = $"{state.WetMultiplier:F2}x",
                ToneKey = state.WetMultiplier > 1.05f ? "negative" : "flat"
            });

            card.Children.Add(new StatRowNode
            {
                Id = "Climate_WindMult",
                Label = "Wind Multiplier",
                Value = $"{state.WindMultiplier:F2}x",
                ToneKey = state.WindMultiplier > 1.05f ? "negative" : "flat"
            });

            card.Children.Add(new StatRowNode
            {
                Id = "Climate_InsulMult",
                Label = "Insulation Multiplier",
                Value = $"{state.InsulationMultiplier:F2}x",
                ToneKey = state.InsulationMultiplier < 0.95f ? "positive" : "flat"
            });

            if (state.IsThorAegisProtected)
            {
                card.Children.Add(new StatRowNode
                {
                    Id = "Climate_ThorAegis",
                    Label = "Thor's Aegis",
                    Value = "Weather Immune",
                    ToneKey = "positive"
                });
            }

            StatBreakdown insulBreakdown = BuildInsulationBreakdown(player, state);
            card.Children.Add(new StatRowNode
            {
                Id = "Climate_GearInsulation",
                Label = "Gear Insulation",
                Value = $"{state.InsulationRating:P0}",
                IsExpanded = ctx.IsExpanded("Climate_GearInsulation"),
                Breakdown = insulBreakdown
            });

            return card;
        }

        private static StatBreakdown BuildInsulationBreakdown(Player player, SurvivalState state)
        {
            var bd = new StatBreakdown();
            var entries = new List<StatBreakdownEntry>();

            var inv = player.GetInventory();
            if (inv != null)
            {
                var equipped = inv.GetEquippedItems();
                for (int i = 0; i < equipped.Count; i++)
                {
                    ItemDrop.ItemData item = equipped[i];
                    if (item?.m_shared == null || !GearInsulationRegistry.IsInsulatingArmorSlot(item.m_shared.m_itemType)) continue;

                    string cleanPrefab = item.m_dropPrefab != null ? item.m_dropPrefab.name.Replace("(Clone)", "").Trim() : string.Empty;
                    string cleanShared = item.m_shared.m_name ?? string.Empty;
                    string queryName = !string.IsNullOrEmpty(cleanPrefab) ? cleanPrefab : cleanShared;

                    float insul = GearInsulationRegistry.GetItemInsulation(queryName, item.m_shared);
                    if (insul > 0.001f)
                    {
                        string display = Localize(cleanShared);
                        entries.Add(new StatBreakdownEntry(display, $"+{insul * 100f:F0}%", "positive"));
                    }
                }
            }

            entries.Sort((a, b) => string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase));
            bd.Entries.AddRange(entries);

            if (state.HasFrostMead)
            {
                bd.Entries.Add(new StatBreakdownEntry("Frost Resistance Mead", "Active", "positive"));
            }

            if (bd.Entries.Count == 0)
            {
                bd.Entries.Add(new StatBreakdownEntry("No insulated gear equipped", "0%", "flat"));
            }

            return bd;
        }

        private static CardNode BuildActiveClimateEffectsCard(Player player, SurvivalState state)
        {
            var card = new CardNode
            {
                Id = "ClimateActiveEffectsCard",
                Title = "Active Climate Effects"
            };

            string? tierName = GetTierDisplayName(state.ActiveThermalTierId);
            if (tierName == null)
            {
                card.Children.Add(new StatRowNode
                {
                    Id = "Effect_ThermalState",
                    Label = "Thermal State",
                    Value = "Comfort Equilibrium",
                    Highlight = true,
                    ToneKey = "positive"
                });
                card.Children.Add(new StatRowNode
                {
                    Id = "Effect_StatImpact",
                    Label = "Stat Impact",
                    Value = "None",
                    ToneKey = "flat"
                });
            }
            else
            {
                card.Children.Add(new StatRowNode
                {
                    Id = "Effect_ThermalState",
                    Label = "Thermal State",
                    Value = tierName,
                    Highlight = true,
                    ToneKey = "negative"
                });

                AppendActiveTierStats(card, state.ActiveThermalTierId!);
            }

            if (player.GetSEMan()?.HaveStatusEffect(SEMan.s_statusEffectWet) == true)
            {
                card.Children.Add(new StatRowNode
                {
                    Id = "Effect_Waterlogged",
                    Label = "Waterlogged (Wet)",
                    Value = "-5% Jog & Run Speed",
                    ToneKey = "negative"
                });
            }

            return card;
        }

        private static void AppendActiveTierStats(CardNode card, string tierId)
        {
            switch (tierId)
            {
                case ThermalStatusEffects.ChillyId:
                    AddAdditivePercentRow(card, "Eff_Stam", "Stamina Regen", ModConfig.ChillyStaminaRegenModifier.Value);
                    AddMultiplierRow(card, "Eff_Dmg", "Damage Output", ModConfig.ChillyDamageMultiplier.Value);
                    break;

                case ThermalStatusEffects.BitterColdId:
                    AddAdditivePercentRow(card, "Eff_Stam", "Stamina Regen", ModConfig.BitterColdStaminaRegenModifier.Value);
                    AddMultiplierRow(card, "Eff_Dmg", "Damage Output", ModConfig.BitterColdDamageMultiplier.Value);
                    break;

                case ThermalStatusEffects.NumbingFrostId:
                    AddAdditivePercentRow(card, "Eff_Stam", "Stamina Regen", ModConfig.NumbingFrostStaminaRegenModifier.Value);
                    AddMultiplierRow(card, "Eff_Dmg", "Damage Output", ModConfig.NumbingFrostDamageMultiplier.Value);
                    AddMultiplierRow(card, "Eff_Atk", "Attack Speed", ModConfig.NumbingFrostAttackSpeedMultiplier.Value);
                    break;

                case ThermalStatusEffects.HypothermiaId:
                    AddAdditivePercentRow(card, "Eff_Stam", "Stamina Regen", ModConfig.HypothermiaStaminaRegenModifier.Value);
                    AddMultiplierRow(card, "Eff_Dmg", "Damage Output", ModConfig.HypothermiaDamageMultiplier.Value);
                    AddMultiplierRow(card, "Eff_Atk", "Attack Speed", ModConfig.HypothermiaAttackSpeedMultiplier.Value);
                    AddAdditivePercentRow(card, "Eff_Hp", "Health Regen", ModConfig.HypothermiaHealthRegenModifier.Value);
                    AddAdditivePercentRow(card, "Eff_Eitr", "Eitr Regen", ModConfig.HypothermiaEitrRegenModifier.Value);
                    if (ModConfig.HypothermiaDamageRate.Value > 0.01f)
                    {
                        card.Children.Add(new StatRowNode
                        {
                            Id = "Eff_Drain",
                            Label = "Freeze Drain",
                            Value = $"{ModConfig.HypothermiaDamageRate.Value:F1}/s",
                            ToneKey = "negative"
                        });
                    }
                    break;

                case ThermalStatusEffects.WarmId:
                    AddMultiplierRow(card, "Eff_Move", "Move Speed", ModConfig.WarmSpeedMultiplier.Value);
                    AddMultiplierRow(card, "Eff_Atk", "Attack Speed", ModConfig.WarmAttackSpeedMultiplier.Value);
                    break;

                case ThermalStatusEffects.HeatExhaustionId:
                    AddMultiplierRow(card, "Eff_Move", "Move Speed", ModConfig.HeatExhaustionSpeedMultiplier.Value);
                    AddMultiplierRow(card, "Eff_Atk", "Attack Speed", ModConfig.HeatExhaustionAttackSpeedMultiplier.Value);
                    break;

                case ThermalStatusEffects.SevereHyperthermiaId:
                    AddMultiplierRow(card, "Eff_Move", "Move Speed", ModConfig.SevereHyperthermiaSpeedMultiplier.Value);
                    AddMultiplierRow(card, "Eff_Atk", "Attack Speed", ModConfig.SevereHyperthermiaAttackSpeedMultiplier.Value);
                    break;

                case ThermalStatusEffects.CatastrophicScorchingId:
                    AddMultiplierRow(card, "Eff_Move", "Move Speed", ModConfig.CatastrophicScorchingSpeedMultiplier.Value);
                    AddMultiplierRow(card, "Eff_Atk", "Attack Speed", ModConfig.CatastrophicScorchingAttackSpeedMultiplier.Value);
                    AddAdditivePercentRow(card, "Eff_Stam", "Stamina Regen", ModConfig.CatastrophicScorchingStaminaRegenModifier.Value);
                    if (ModConfig.HyperthermiaDamageRate.Value > 0.01f)
                    {
                        card.Children.Add(new StatRowNode
                        {
                            Id = "Eff_Drain",
                            Label = "Scorch Drain",
                            Value = $"{ModConfig.HyperthermiaDamageRate.Value:F1}/s",
                            ToneKey = "negative"
                        });
                    }
                    break;
            }
        }

        private static void AddAdditivePercentRow(CardNode card, string id, string label, float mod)
        {
            if (Mathf.Abs(mod) <= 0.001f) return;
            card.Children.Add(new StatRowNode
            {
                Id = id,
                Label = label,
                Value = $"{mod * 100f:+0;-0;0}%",
                ToneKey = mod > 0f ? "positive" : "negative"
            });
        }

        private static void AddMultiplierRow(CardNode card, string id, string label, float mult)
        {
            if (Mathf.Abs(mult - 1f) <= 0.001f) return;
            card.Children.Add(new StatRowNode
            {
                Id = id,
                Label = label,
                Value = $"{mult:F2}x",
                ToneKey = mult > 1f ? "positive" : "negative"
            });
        }

        private static CardNode BuildAmbientBreakdownCard(SurvivalState state, AmbientThermalBreakdown bd, HubViewContext ctx)
        {
            var card = new CardNode
            {
                Id = "ClimateAmbientCard",
                Title = "Ambient Breakdown"
            };

            string biomeName = string.IsNullOrEmpty(bd.BiomeName) ? "Unknown" : bd.BiomeName;
            card.Children.Add(new StatRowNode
            {
                Id = "Amb_Biome",
                Label = $"Biome Base ({biomeName})",
                Value = $"{bd.BiomeBaseTemp:F1}°C",
                ToneKey = bd.BiomeBaseTemp < 8f ? "negative" : (bd.BiomeBaseTemp > 26f ? "negative" : "flat")
            });

            AddDeltaRow(card, "Amb_Altitude", "Altitude", bd.AltitudeDelta);
            AddDeltaRow(card, "Amb_Time", "Time of Day", bd.TimeOfDayDelta);

            string weatherName = string.IsNullOrEmpty(bd.WeatherName) ? "Clear" : bd.WeatherName;
            AddDeltaRow(card, "Amb_Weather", $"Weather ({weatherName})", bd.WeatherDelta);

            if (Mathf.Abs(bd.SeasonDelta) > 0.05f && !string.IsNullOrEmpty(bd.SeasonName))
            {
                AddDeltaRow(card, "Amb_Season", $"Season ({bd.SeasonName})", bd.SeasonDelta);
            }

            if (Mathf.Abs(bd.WindChillDelta) > 0.05f)
            {
                AddDeltaRow(card, "Amb_Wind", $"Wind Chill ({bd.RawWindSpeedKmh:F0} km/h)", bd.WindChillDelta);
            }

            string fireDesc = string.IsNullOrEmpty(bd.FireSourceDescription) ? "None" : bd.FireSourceDescription;
            StatBreakdown? fireBreakdown = null;
            if (bd.IsDwellingHeatActive && (Mathf.Abs(bd.DirectFlameWarmth) > 0.05f || Mathf.Abs(bd.StructuralWarmth) > 0.05f))
            {
                fireBreakdown = new StatBreakdown();
                fireBreakdown.Entries.Add(new StatBreakdownEntry(
                    "Direct Flame",
                    $"{(bd.DirectFlameWarmth > 0.05f ? "+" : "")}{bd.DirectFlameWarmth:F1}°C",
                    bd.DirectFlameWarmth > 0.05f ? "positive" : "flat"));
                fireBreakdown.Entries.Add(new StatBreakdownEntry(
                    "Structural Warmth",
                    $"{(bd.StructuralWarmth > 0.05f ? "+" : "")}{bd.StructuralWarmth:F1}°C",
                    bd.StructuralWarmth > 0.05f ? "positive" : "flat"));
            }

            string fireSign = bd.FireWarmthDelta > 0.05f ? "+" : "";
            string fireTone = bd.FireWarmthDelta > 0.05f ? "positive" : (bd.FireWarmthDelta < -0.05f ? "negative" : "flat");
            card.Children.Add(new StatRowNode
            {
                Id = "Amb_Fire",
                Label = $"Fire & Hearth ({fireDesc})",
                Value = $"{fireSign}{bd.FireWarmthDelta:F1}°C",
                ToneKey = fireTone,
                IsExpanded = fireBreakdown != null && ctx.IsExpanded("Amb_Fire"),
                Breakdown = fireBreakdown
            });

            if (bd.IsSubterranean)
            {
                AddDeltaRow(card, "Amb_Subterranean", "Subterranean", bd.SubterraneanOffset);
            }

            card.Children.Add(new StatRowNode
            {
                Id = "Amb_Net",
                Label = "Net Feels-Like Ambient",
                Value = $"{state.AmbientTemperature:F1}°C",
                Highlight = true,
                ToneKey = state.AmbientTemperature < 8f ? "negative" : (state.AmbientTemperature > 35f ? "negative" : "positive")
            });

            return card;
        }

        private static void AddDeltaRow(CardNode card, string id, string label, float delta)
        {
            string sign = delta > 0.05f ? "+" : "";
            string tone = delta > 0.05f ? "positive" : (delta < -0.05f ? "negative" : "flat");
            card.Children.Add(new StatRowNode
            {
                Id = id,
                Label = label,
                Value = $"{sign}{delta:F1}°C",
                ToneKey = tone
            });
        }

        private static string? GetTierDisplayName(string? tierId) => tierId switch
        {
            ThermalStatusEffects.ChillyId => "Chilly",
            ThermalStatusEffects.BitterColdId => "Bitter Cold",
            ThermalStatusEffects.NumbingFrostId => "Numbing Frost",
            ThermalStatusEffects.HypothermiaId => "Hypothermia",
            ThermalStatusEffects.WarmId => "Warm",
            ThermalStatusEffects.HeatExhaustionId => "Heat Exhaustion",
            ThermalStatusEffects.SevereHyperthermiaId => "Severe Hyperthermia",
            ThermalStatusEffects.CatastrophicScorchingId => "Catastrophic Scorching",
            _ => null
        };

        private static string Localize(string raw)
        {
            if (string.IsNullOrEmpty(raw) || Localization.instance == null) return raw;
            return Localization.instance.Localize(raw);
        }
    }
}
