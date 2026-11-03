using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;

namespace VikingClimate.UI
{
    /// <summary>
    /// Vertical Column HUD Thermometer styled identically to Valheim's vertical Health bar.
    /// Features:
    /// 1. Exact 32px width and cylindrical segmented styling matching the Health bar.
    /// 2. 32x32 stylized thermal icon positioned underneath on the exact same baseline as the HP icon.
    /// 3. Large, bold 18pt temperature readout centered directly inside the bar (like the HP text).
    /// 4. Prominent ambient temperature target needle marker with sliding degree badge.
    /// 5. Automatic synchronized rightward shift of Forsaken Power and Quick Slots to prevent UI overlap.
    /// </summary>
    public class HUDThermometer : MonoBehaviour
    {
        private static HUDThermometer? _instance;
        private static Sprite? _solidWhiteSprite;
        private static Sprite? _thermalIconSprite;
        private static Sprite? _arrowSprite;
        private static Sprite? _circleSprite;

        private RectTransform? _rootRect;
        private CanvasGroup? _canvasGroup;
        private Hud? _hud;

        // Underneath 32x32 Icon
        private Image? _iconImage;

        // Bar Components
        private RectTransform? _bodyFillRect;
        private Image? _bodyFillImage;
        private Image? _proceduralBulbImage;
        private TMP_Text? _bodyText;

        // Ambient Target Pointer: [bar]<| (feels like)
        private RectTransform? _ambientMarkerRect;
        private Image? _ambientMarkerImage;
        private TMP_Text? _ambientBadgeText;

        // Layout Constants matching Valheim's Health bar and calibrated to the Norse backing board
        private const float ColumnWidth = 36f;
        private const float ColumnHeight = 152f;
        private const float CavityInset = 2.5f;
        private const float CavityWidth = ColumnWidth - (CavityInset * 2f);
        private const float CavityHeight = ColumnHeight - (CavityInset * 2f);

        // Positioning: Pinned cleanly to the left edge of the minimap frame, completely clear of status effects
        private const float IconCenterOffset = -21.8f;

        public const string AugaPluginId = "randyknapp.mods.auga";
        public const float VanillaDefaultPosX = -265.0f;
        public const float VanillaDefaultPosY = -30.0f;
        public const float AugaDefaultPosX = -345.0f;
        public const float AugaDefaultPosY = -45.0f;

        public static bool IsAugaDetected =>
            BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(AugaPluginId);

        public static Vector2 GetDefaultPosition()
        {
            if (Minimap.instance != null && Minimap.instance.m_smallRoot != null)
            {
                var mmRect = Minimap.instance.m_smallRoot.GetComponent<RectTransform>();
                if (mmRect != null && mmRect.gameObject.activeInHierarchy && mmRect.rect.width > 20f)
                {
                    // Calculate left edge in Top-Right anchored coordinates
                    // (where anchorMin = (1, 1), anchorMax = (1, 1), pivot = (1, 1))
                    float mmLeft = mmRect.anchoredPosition.x - (mmRect.rect.width * mmRect.pivot.x);
                    float mmTop = mmRect.anchoredPosition.y + (mmRect.rect.height * (1f - mmRect.pivot.y));

                    // Position thermometer column 52px to the left of the minimap left edge
                    // (allowing room for the needle pointer and badge between thermometer and map)
                    float posX = mmLeft - 52f;
                    float posY = mmTop - 4f;
                    return new Vector2(posX, posY);
                }
            }

            return IsAugaDetected
                ? new Vector2(AugaDefaultPosX, AugaDefaultPosY)
                : new Vector2(VanillaDefaultPosX, VanillaDefaultPosY);
        }

        public static Vector2 GetCurrentThermometerPosition()
        {
            if (ModConfig.UseCustomPosition != null && ModConfig.UseCustomPosition.Value)
            {
                float x = ModConfig.ThermometerPositionX != null ? ModConfig.ThermometerPositionX.Value : VanillaDefaultPosX;
                float y = ModConfig.ThermometerPositionY != null ? ModConfig.ThermometerPositionY.Value : VanillaDefaultPosY;
                return new Vector2(x, y);
            }

            return GetDefaultPosition();
        }

        public static float GetCurrentThermometerScale()
        {
            if (ModConfig.ThermometerScale == null) return 1.0f;
            return Mathf.Clamp(ModConfig.ThermometerScale.Value, 0.5f, 2.0f);
        }

        // Temperature Scale: Coincides directly with Catastrophic Hypothermia (-12°C) and Scorching (+55°C)
        // Comfort [18°C, 26°C] sits squarely in the golden center (45% to 57%).
        private const float MinTempScale = -12.0f;
        private const float MaxTempScale = 55.0f;
        private const float TempRange = MaxTempScale - MinTempScale; // 67.0f

        public static void AttachToHud(Hud hud)
        {
            if (hud == null) return;
            if (_instance != null)
            {
                if (_instance.gameObject != null)
                {
                    if (!_instance.gameObject.activeSelf)
                    {
                        _instance.gameObject.SetActive(true);
                    }
                    return;
                }
                _instance = null;
            }

            Transform? parentTransform = null;
            if (hud.m_rootObject != null)
            {
                parentTransform = hud.m_rootObject.transform;
            }
            else
            {
                parentTransform = hud.transform.Find("hudroot") ?? (hud.m_gpRoot != null ? hud.m_gpRoot.parent : hud.transform);
            }

            if (parentTransform == null) return;

            var go = new GameObject("VikingClimate_DualThermometer", typeof(RectTransform));
            go.transform.SetParent(parentTransform, false);

            int targetLayer = parentTransform.gameObject.layer;
            SetLayerRecursively(go, targetLayer);

            // Establish dedicated overlay Canvas with override sorting so Auga/custom HUD layers cannot occlude the thermometer
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            int highestSorting = 0;
            Canvas[] allCanvases = hud.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < allCanvases.Length; i++)
            {
                if (allCanvases[i] != null && allCanvases[i] != canvas && allCanvases[i].sortingOrder > highestSorting)
                {
                    highestSorting = allCanvases[i].sortingOrder;
                }
            }
            canvas.sortingOrder = Mathf.Max(highestSorting + 10, 50);
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                              AdditionalCanvasShaderChannels.Normal |
                                              AdditionalCanvasShaderChannels.Tangent;

            // Shield against Auga automatic panel restyling if Auga is installed
            if (IsAugaDetected)
            {
                var autoRestyledType = Type.GetType("AugaUnity.AutoRestyled, Auga");
                if (autoRestyledType != null && go.GetComponent(autoRestyledType) == null)
                {
                    go.AddComponent(autoRestyledType);
                }
            }

            _instance = go.AddComponent<HUDThermometer>();
            _instance.Init(hud);

            VikingClimatePlugin.Log.LogInfo(
                $"[HUDThermometer] Attached 32px HP-style column to '{parentTransform.name}' on layer {targetLayer} (Canvas sortingOrder: {canvas.sortingOrder}). " +
                $"Auga detected: {IsAugaDetected}, UseCustomPosition: {ModConfig.UseCustomPosition?.Value ?? false}, " +
                $"Position: {GetCurrentThermometerPosition()}, Scale: {GetCurrentThermometerScale():F2}.");
        }

        private void Init(Hud hud)
        {
            _hud = hud;
            _rootRect = GetComponent<RectTransform>();
            if (_rootRect == null) return;

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            var whiteSprite = GetWhiteSprite();
            var iconSprite = ClimateAssetLoader.GetIcon("se_warm") ?? GetThermalIconSprite();
            var backSprite = ClimateAssetLoader.GetUISprite("thermometer_back");
            var glassSprite = ClimateAssetLoader.GetUISprite("thermometer_glass");
            var mercurySprite = ClimateAssetLoader.GetUISprite("thermometer_mercury");
            var needleSprite = ClimateAssetLoader.GetUISprite("thermometer_needle");
            var plaqueSprite = ClimateAssetLoader.GetUISprite("thermometer_plaque");

            // 1. Root RectTransform positioned as the column frame anchored to Top-Right adjacent to minimap
            _rootRect.localRotation = Quaternion.identity;
            _rootRect.anchorMin = new Vector2(1f, 1f);
            _rootRect.anchorMax = new Vector2(1f, 1f);
            _rootRect.pivot = new Vector2(1f, 1f);
            _rootRect.sizeDelta = new Vector2(ColumnWidth, ColumnHeight);
            _rootRect.localPosition = new Vector3(_rootRect.localPosition.x, _rootRect.localPosition.y, 0f);
            ApplyLayout();
            _rootRect.SetAsLastSibling();

            SubscribeConfigEvents();

            // 2. 38x38 Icon Underneath (matching healthicon size and baseline)
            var iconGo = new GameObject("ThermalIcon", typeof(RectTransform));
            iconGo.transform.SetParent(transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.zero;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(38f, 38f);
            iconRect.anchoredPosition = new Vector2(ColumnWidth * 0.5f, IconCenterOffset);

            _iconImage = iconGo.AddComponent<Image>();
            _iconImage.sprite = iconSprite;
            _iconImage.type = Image.Type.Simple;
            _iconImage.color = new Color(0.51f, 0.78f, 0.52f);
            _iconImage.raycastTarget = false;

            bool hasCustomBack = backSprite != null;

            // 3. Master Instrument Backing Plate (Valheim bog-oak frame with forged iron end-caps & brass plaque)
            var boardGo = new GameObject("BackingPlate", typeof(RectTransform));
            boardGo.transform.SetParent(transform, false);
            var boardRect = boardGo.GetComponent<RectTransform>();
            boardRect.localScale = Vector3.one;
            boardRect.localRotation = Quaternion.identity;
            boardRect.anchorMin = Vector2.zero;
            boardRect.anchorMax = Vector2.one;
            boardRect.offsetMin = Vector2.zero;
            boardRect.offsetMax = Vector2.zero;
            boardRect.pivot = new Vector2(0.5f, 0.5f);

            var borderImg = boardGo.AddComponent<Image>();
            borderImg.sprite = backSprite ?? whiteSprite;
            borderImg.type = Image.Type.Simple;
            borderImg.color = hasCustomBack ? Color.white : new Color(0.18f, 0.16f, 0.14f, 0.95f);
            borderImg.raycastTarget = false;

            // 4. Procedural Cavity, Bulb, Capillary & Graduation Ticks (only rendered if running without custom backing art)
            if (!hasCustomBack)
            {
                var cavityGo = new GameObject("Cavity", typeof(RectTransform));
                cavityGo.transform.SetParent(transform, false);
                var cavityRect = cavityGo.GetComponent<RectTransform>();
                cavityRect.localScale = Vector3.one;
                cavityRect.localRotation = Quaternion.identity;
                cavityRect.anchorMin = Vector2.zero;
                cavityRect.anchorMax = Vector2.one;
                cavityRect.offsetMin = new Vector2(CavityInset, CavityInset);
                cavityRect.offsetMax = new Vector2(-CavityInset, -CavityInset);
                cavityRect.pivot = new Vector2(0.5f, 0.5f);

                var cavityImg = cavityGo.AddComponent<Image>();
                cavityImg.sprite = whiteSprite;
                cavityImg.type = Image.Type.Simple;
                cavityImg.color = new Color(0.06f, 0.05f, 0.05f, 0.98f);
                cavityImg.raycastTarget = false;

                // Procedural Glass Bulb at bottom
                var bulbBaseGo = new GameObject("ProceduralBulbBase", typeof(RectTransform));
                bulbBaseGo.transform.SetParent(transform, false);
                var bulbBaseRect = bulbBaseGo.GetComponent<RectTransform>();
                bulbBaseRect.localScale = Vector3.one;
                bulbBaseRect.localRotation = Quaternion.identity;
                bulbBaseRect.anchorMin = Vector2.zero;
                bulbBaseRect.anchorMax = Vector2.zero;
                bulbBaseRect.pivot = new Vector2(0.5f, 0.5f);
                bulbBaseRect.sizeDelta = new Vector2(22f, 22f);
                bulbBaseRect.anchoredPosition = new Vector2(ColumnWidth * 0.5f, 22f);

                var bulbBaseImg = bulbBaseGo.AddComponent<Image>();
                bulbBaseImg.sprite = GetCircleSprite();
                bulbBaseImg.type = Image.Type.Simple;
                bulbBaseImg.color = new Color(0.20f, 0.22f, 0.25f, 0.90f);
                bulbBaseImg.raycastTarget = false;

                // Procedural Bulb Mercury Fill
                var bulbFillGo = new GameObject("ProceduralBulbFill", typeof(RectTransform));
                bulbFillGo.transform.SetParent(transform, false);
                var bulbFillRect = bulbFillGo.GetComponent<RectTransform>();
                bulbFillRect.localScale = Vector3.one;
                bulbFillRect.localRotation = Quaternion.identity;
                bulbFillRect.anchorMin = Vector2.zero;
                bulbFillRect.anchorMax = Vector2.zero;
                bulbFillRect.pivot = new Vector2(0.5f, 0.5f);
                bulbFillRect.sizeDelta = new Vector2(17f, 17f);
                bulbFillRect.anchoredPosition = new Vector2(ColumnWidth * 0.5f, 22f);

                _proceduralBulbImage = bulbFillGo.AddComponent<Image>();
                _proceduralBulbImage.sprite = GetCircleSprite();
                _proceduralBulbImage.type = Image.Type.Simple;
                _proceduralBulbImage.color = new Color(0.51f, 0.78f, 0.52f);
                _proceduralBulbImage.raycastTarget = false;

                // Procedural Graduation Ticks carved on left side of the tube
                float tubeStartY = 28f;
                float tubeHeight = ColumnHeight - 48f;
                for (float t = -10f; t <= 50f; t += 5f)
                {
                    float ratio = Mathf.Clamp01((t - MinTempScale) / TempRange);
                    float y = tubeStartY + (ratio * tubeHeight);
                    bool isMajor = Mathf.Approximately(t % 20f, 0f) || Mathf.Approximately(t, 0f);
                    bool isMedium = !isMajor && Mathf.Approximately(t % 10f, 0f);
                    float tickLen = isMajor ? 8f : (isMedium ? 5.5f : 3.5f);
                    Color tickColor = Mathf.Approximately(t, 0f)
                        ? new Color(0.39f, 0.71f, 0.96f, 0.85f)
                        : (isMajor ? new Color(0.85f, 0.80f, 0.70f, 0.75f) : new Color(0.60f, 0.55f, 0.50f, 0.45f));

                    CreateTickMark(cavityGo, $"Tick_{t}", whiteSprite, y - CavityInset, tickLen, tickColor);
                }

                // Comfort band reference shading
                float comfortMinRatio = (18f - MinTempScale) / TempRange;
                float comfortMaxRatio = (26f - MinTempScale) / TempRange;
                CreateReferenceBand(cavityGo, "ComfortBand", whiteSprite,
                    tubeStartY + (comfortMinRatio * tubeHeight) - CavityInset,
                    (comfortMaxRatio - comfortMinRatio) * tubeHeight,
                    CavityWidth,
                    new Color(0.51f, 0.78f, 0.52f, 0.15f));
            }

            // 5. Body Warmth Liquid Fill (crisp 3D cylindrical column rising in the groove)
            var fillGo = new GameObject("BodyFill", typeof(RectTransform));
            fillGo.transform.SetParent(transform, false);
            _bodyFillRect = fillGo.GetComponent<RectTransform>();
            _bodyFillRect.localScale = Vector3.one;
            _bodyFillRect.localRotation = Quaternion.identity;
            _bodyFillRect.anchorMin = Vector2.zero;
            _bodyFillRect.anchorMax = Vector2.zero;
            _bodyFillRect.pivot = new Vector2(0.5f, 0f);

            float fillX = hasCustomBack ? (ColumnWidth * (50.0f / 104.0f)) : (ColumnWidth * 0.5f);
            float fillBottomY = hasCustomBack ? (ColumnHeight * (52.0f / 440.0f)) : 28f;
            float fillTotalHeight = hasCustomBack ? (ColumnHeight * (328.0f / 440.0f)) : (ColumnHeight - 48f);
            float fillWidth = hasCustomBack ? (ColumnWidth * (30.0f / 104.0f)) : 9f;

            _bodyFillRect.anchoredPosition = new Vector2(fillX, fillBottomY);
            _bodyFillRect.sizeDelta = new Vector2(fillWidth, fillTotalHeight);

            _bodyFillImage = fillGo.AddComponent<Image>();
            _bodyFillImage.sprite = mercurySprite ?? whiteSprite;
            _bodyFillImage.type = Image.Type.Filled;
            _bodyFillImage.fillMethod = Image.FillMethod.Vertical;
            _bodyFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            _bodyFillImage.fillAmount = 0.5f;
            _bodyFillImage.color = new Color(0.51f, 0.78f, 0.52f);
            _bodyFillImage.raycastTarget = false;

            // 5b. Glass Capillary Tube Overlay (specular gloss and soft translucent glass body)
            var glassGo = new GameObject("GlassCapillary", typeof(RectTransform));
            glassGo.transform.SetParent(transform, false);
            var glassRect = glassGo.GetComponent<RectTransform>();
            glassRect.localScale = Vector3.one;
            glassRect.localRotation = Quaternion.identity;
            glassRect.anchorMin = Vector2.zero;
            glassRect.anchorMax = Vector2.zero;
            glassRect.pivot = new Vector2(0.5f, 0f);
            glassRect.anchoredPosition = new Vector2(fillX, fillBottomY);
            glassRect.sizeDelta = new Vector2(fillWidth, fillTotalHeight);

            var glassImg = glassGo.AddComponent<Image>();
            glassImg.sprite = glassSprite ?? whiteSprite;
            glassImg.type = Image.Type.Simple;
            glassImg.color = glassSprite != null ? Color.white : new Color(0.6f, 0.8f, 1f, 0.18f);
            glassImg.raycastTarget = false;

            // 5c. Brass Plaque Overlay (renders IN FRONT of the capillary tube and liquid)
            if (plaqueSprite != null)
            {
                var plaqueGo = new GameObject("PlaqueOverlay", typeof(RectTransform));
                plaqueGo.transform.SetParent(transform, false);
                var plaqueRect = plaqueGo.GetComponent<RectTransform>();
                plaqueRect.localScale = Vector3.one;
                plaqueRect.localRotation = Quaternion.identity;
                plaqueRect.anchorMin = Vector2.zero;
                plaqueRect.anchorMax = Vector2.one;
                plaqueRect.pivot = new Vector2(0.5f, 0.5f);
                plaqueRect.offsetMin = Vector2.zero;
                plaqueRect.offsetMax = Vector2.zero;

                var plaqueImg = plaqueGo.AddComponent<Image>();
                plaqueImg.sprite = plaqueSprite;
                plaqueImg.type = Image.Type.Simple;
                plaqueImg.color = Color.white;
                plaqueImg.raycastTarget = false;
            }

            // 6. Ambient Target Pointer: [bar]<| (feels like)
            // Attaches to the right edge of the column pointing directly at the channel level
            var indicatorGo = new GameObject("AmbientIndicator", typeof(RectTransform));
            indicatorGo.transform.SetParent(transform, false);
            _ambientMarkerRect = indicatorGo.GetComponent<RectTransform>();
            _ambientMarkerRect.localScale = Vector3.one;
            _ambientMarkerRect.localRotation = Quaternion.identity;
            _ambientMarkerRect.anchorMin = Vector2.zero;
            _ambientMarkerRect.anchorMax = Vector2.zero;
            _ambientMarkerRect.pivot = new Vector2(0f, 0.5f);
            _ambientMarkerRect.sizeDelta = new Vector2(50f, 20f);
            _ambientMarkerRect.anchoredPosition = new Vector2(ColumnWidth, fillBottomY + (fillTotalHeight * 0.5f));

            // Arrow: Forged iron pointer arrow pointing left directly at the thermometer level
            var arrowGo = new GameObject("Arrow", typeof(RectTransform));
            arrowGo.transform.SetParent(indicatorGo.transform, false);
            var arrowRect = arrowGo.GetComponent<RectTransform>();
            arrowRect.localScale = Vector3.one;
            arrowRect.localRotation = Quaternion.identity;
            arrowRect.anchorMin = new Vector2(0f, 0.5f);
            arrowRect.anchorMax = new Vector2(0f, 0.5f);
            arrowRect.pivot = new Vector2(0f, 0.5f);
            arrowRect.sizeDelta = needleSprite != null ? new Vector2(20f, 13f) : new Vector2(9f, 11f);
            arrowRect.anchoredPosition = new Vector2(0f, 0f);

            _ambientMarkerImage = arrowGo.AddComponent<Image>();
            _ambientMarkerImage.sprite = needleSprite ?? GetArrowSprite();
            _ambientMarkerImage.type = Image.Type.Simple;
            _ambientMarkerImage.color = Color.white;
            _ambientMarkerImage.raycastTarget = false;

            TMP_Text? fontSource = hud.m_healthText ?? hud.m_gpName ?? hud.m_actionName;
            TMP_FontAsset? fontAsset = fontSource != null ? fontSource.font : null;
            if (fontAsset == null)
            {
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fonts != null && fonts.Length > 0)
                {
                    for (int i = 0; i < fonts.Length; i++)
                    {
                        if (fonts[i] != null && fonts[i].name.IndexOf("Valheim", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            fontAsset = fonts[i];
                            break;
                        }
                    }
                    if (fontAsset == null) fontAsset = fonts[0];
                }
            }

            // Ambient Temperature Badge (positioned cleanly after the arrow base)
            _ambientBadgeText = CreateTextElement(indicatorGo, "AmbientBadge", fontAsset, fontSource,
                new Vector2(13f, 0f), new Vector2(46f, 20f), 13f);
            _ambientBadgeText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _ambientBadgeText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _ambientBadgeText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _ambientBadgeText.rectTransform.anchoredPosition = new Vector2(13f, 0f);
            _ambientBadgeText.alignment = TextAlignmentOptions.MidlineLeft;
            _ambientBadgeText.margin = Vector4.zero;
            _ambientBadgeText.text = "17°";

            // 7. Bottom Body Warmth Text (positioned cleanly on the bottom iron plate)
            _bodyText = CreateTextElement(gameObject, "BodyText", fontAsset, fontSource,
                new Vector2(ColumnWidth * 0.5f, 9.0f),
                new Vector2(70f, 24f), 17f);
            _bodyText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _bodyText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _bodyText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _bodyText.rectTransform.anchoredPosition = new Vector2(0f, 9.0f);
            _bodyText.alignment = TextAlignmentOptions.Center;
            _bodyText.fontStyle = FontStyles.Bold;
            _bodyText.text = "22°";

            SetLayerRecursively(gameObject, gameObject.layer);
        }

        private static void CreateTickMark(GameObject parent, string name, Sprite sprite, float yPos, float width, Color color)
        {
            var tickGo = new GameObject(name, typeof(RectTransform));
            tickGo.transform.SetParent(parent.transform, false);
            var rt = tickGo.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(3f, yPos);
            rt.sizeDelta = new Vector2(width, 1.5f);

            var img = tickGo.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
        }

        private static void CreateSegmentLine(GameObject parent, string name, Sprite sprite, float yPos)
        {
            var segGo = new GameObject(name, typeof(RectTransform));
            segGo.transform.SetParent(parent.transform, false);
            var rt = segGo.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(CavityWidth, 1.5f);

            var img = segGo.AddComponent<Image>();
            img.sprite = sprite;
            img.color = new Color(0.12f, 0.11f, 0.10f, 0.65f);
            img.raycastTarget = false;
        }

        private static void CreateReferenceLine(GameObject parent, string name, Sprite sprite, float yPos, float width, float height, Color color)
        {
            var lineGo = new GameObject(name, typeof(RectTransform));
            lineGo.transform.SetParent(parent.transform, false);
            var rt = lineGo.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(width, height);

            var img = lineGo.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
        }

        private static void CreateReferenceBand(GameObject parent, string name, Sprite sprite, float yPos, float height, float width, Color color)
        {
            var bandGo = new GameObject(name, typeof(RectTransform));
            bandGo.transform.SetParent(parent.transform, false);
            var rt = bandGo.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(width, height);

            var img = bandGo.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
        }

        private static TMP_Text CreateTextElement(
            GameObject parent,
            string name,
            TMP_FontAsset? fontAsset,
            TMP_Text? fontSource,
            Vector2 centerPos,
            Vector2 size,
            float fontSize)
        {
            var textGo = new GameObject(name, typeof(RectTransform));
            textGo.transform.SetParent(parent.transform, false);

            var rt = textGo.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centerPos;
            rt.sizeDelta = size;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
            {
                tmp.font = fontAsset;
            }
            else if (fontSource != null && fontSource.font != null)
            {
                tmp.font = fontSource.font;
                tmp.fontMaterial = fontSource.fontMaterial;
            }

            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = false;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.maskable = false;
            tmp.raycastTarget = false;
            tmp.margin = Vector4.zero;
            tmp.color = Color.white;
            return tmp;
        }

        private void LateUpdate()
        {
            if (!ModConfig.ModEnabled.Value || !ModConfig.ShowThermometerHUD.Value)
            {
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                return;
            }

            var player = Player.m_localPlayer;
            var hud = Hud.instance;
            if (player == null || player.IsDead() || hud == null || !hud.IsVisible())
            {
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                return;
            }

            if (_canvasGroup != null && _canvasGroup.alpha < 1f)
            {
                _canvasGroup.alpha = 1f;
            }

            // 1. Maintain Top-Right anchored position, zero Z-depth, uniform scale, and topmost sibling order
            if (_rootRect != null)
            {
                if (_rootRect.localPosition.z != 0f)
                {
                    _rootRect.localPosition = new Vector3(_rootRect.localPosition.x, _rootRect.localPosition.y, 0f);
                }

                if (_rootRect.parent != null && _rootRect.GetSiblingIndex() < _rootRect.parent.childCount - 1)
                {
                    _rootRect.SetAsLastSibling();
                }

                Vector2 targetPos = GetCurrentThermometerPosition();
                if (_rootRect.anchoredPosition != targetPos)
                {
                    _rootRect.anchoredPosition = targetPos;
                }

                float scale = GetCurrentThermometerScale();
                if (_rootRect.localScale.x != scale || _rootRect.localScale.y != scale)
                {
                    _rootRect.localScale = new Vector3(scale, scale, 1f);
                }
            }

            var state = SurvivalManager.CurrentState;

            // 3. Update Body Warmth (Liquid Fill + Large 18pt Text + Underneath Icon)
            float bodyWarmth = state.BodyWarmth;
            Color warmthColor = GetTemperatureColor(bodyWarmth);
            float bodyRatio = Mathf.Clamp01((bodyWarmth - MinTempScale) / TempRange);

            bool isCritical = bodyWarmth < MinTempScale || bodyWarmth > MaxTempScale;
            float flashAlpha = isCritical ? (0.40f + 0.60f * Mathf.Abs(Mathf.Sin(Time.time * 6.0f))) : 1.0f;

            if (_bodyFillImage != null)
            {
                _bodyFillImage.fillAmount = bodyRatio;
                _bodyFillImage.color = new Color(warmthColor.r, warmthColor.g, warmthColor.b, flashAlpha);
            }
            if (_proceduralBulbImage != null)
            {
                _proceduralBulbImage.color = new Color(warmthColor.r, warmthColor.g, warmthColor.b, flashAlpha);
            }
            if (_bodyText != null)
            {
                _bodyText.text = $"{bodyWarmth:F0}°";
                _bodyText.color = isCritical && flashAlpha < 0.7f ? warmthColor : Color.white;
            }
            if (_iconImage != null)
            {
                string? activeTier = TemperatureSystem.EvaluateThermalStatusTier(bodyWarmth);
                if (activeTier != null)
                {
                    string iconName = activeTier switch
                    {
                        ThermalStatusEffects.ChillyId => "se_chilly",
                        ThermalStatusEffects.BitterColdId => "se_bitter_cold",
                        ThermalStatusEffects.NumbingFrostId => "se_numbing_frost",
                        ThermalStatusEffects.HypothermiaId => "se_hypothermia",
                        ThermalStatusEffects.WarmId => "se_warm",
                        ThermalStatusEffects.HeatExhaustionId => "se_heat_exhaustion",
                        ThermalStatusEffects.SevereHyperthermiaId => "se_severe_hyperthermia",
                        ThermalStatusEffects.CatastrophicScorchingId => "se_catastrophic_scorching",
                        _ => "se_warm"
                    };

                    var tierIcon = ClimateAssetLoader.GetIcon(iconName);
                    if (tierIcon != null)
                    {
                        _iconImage.sprite = tierIcon;
                        _iconImage.color = isCritical ? new Color(1f, 1f, 1f, flashAlpha) : Color.white;
                    }
                    else
                    {
                        _iconImage.color = isCritical ? new Color(warmthColor.r, warmthColor.g, warmthColor.b, flashAlpha) : warmthColor;
                    }
                }
                else
                {
                    var comfortIcon = ClimateAssetLoader.GetIcon("se_warm") ?? GetThermalIconSprite();
                    _iconImage.sprite = comfortIcon;
                    _iconImage.color = warmthColor;
                }
            }

            // 4. Update Ambient Target Marker (Needle line + sliding degree badge)
            float ambientTemp = state.AmbientTemperature;
            Color ambColor = GetTemperatureColor(ambientTemp);
            float ambientRatio = Mathf.Clamp01((ambientTemp - MinTempScale) / TempRange);

            if (_ambientMarkerRect != null)
            {
                float fillBottomY = ColumnHeight * (52.0f / 440.0f);
                float fillTotalHeight = ColumnHeight * (328.0f / 440.0f);
                float arrowY = fillBottomY + (fillTotalHeight * ambientRatio);
                _ambientMarkerRect.anchoredPosition = new Vector2(ColumnWidth, arrowY);
            }
            if (_ambientMarkerImage != null)
            {
                _ambientMarkerImage.color = ambColor;
            }
            if (_ambientBadgeText != null)
            {
                _ambientBadgeText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(ambColor)}>{FormatTemp(ambientTemp)}</color>";
            }
        }

        private static string FormatTemp(float val)
        {
            if (val > 0.4f) return $"+{val:F0}°";
            if (val < -0.4f) return $"{val:F0}°";
            return "0°";
        }

        private static Color GetTemperatureColor(float temp)
        {
            return TemperatureSystem.GetTemperatureColor(temp);
        }

        private static Sprite GetWhiteSprite()
        {
            if (_solidWhiteSprite != null) return _solidWhiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _solidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            return _solidWhiteSprite;
        }

        private static Sprite GetArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;

            int width = 8;
            int height = 10;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;

            // Sharp triangle pointing LEFT: tip at (0, 4.5), flat base at x = width - 1
            float midY = (height - 1) * 0.5f; // 4.5f
            for (int x = 0; x < width; x++)
            {
                float halfH = (x / (float)(width - 1)) * midY;
                for (int y = 0; y < height; y++)
                {
                    if (Mathf.Abs(y - midY) <= halfH + 0.35f)
                    {
                        colors[y * width + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            _arrowSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0f, 0.5f), 100f);
            return _arrowSprite;
        }

        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = (size - 2) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float edge = Mathf.Clamp01(radius - dist + 0.5f);
                        colors[y * size + x] = new Color(1f, 1f, 1f, edge);
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circleSprite;
        }

        private static Sprite GetThermalIconSprite()
        {
            if (_thermalIconSprite != null) return _thermalIconSprite;

            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = 11.5f;

            // Stylized Celtic Sun/Flame & Frost Ring Knot
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);

                    // Outer Celtic circle knot (thickness 2.2px)
                    if (dist <= radius && dist >= radius - 2.2f)
                    {
                        colors[y * size + x] = Color.white;
                    }

                    // Central flame / mercury bulb at bottom (centered at x=16, y=10, radius=3.8)
                    float bulbDist = Vector2.Distance(new Vector2(x, y), new Vector2(16f, 10f));
                    if (bulbDist <= 3.8f)
                    {
                        colors[y * size + x] = Color.white;
                    }

                    // Flame / stem rising to top
                    if (x >= 14 && x <= 17 && y >= 10 && y <= 20)
                    {
                        colors[y * size + x] = Color.white;
                    }

                    // Tapered flame crown at top (y = 20 to 23)
                    if (y >= 20 && y <= 23)
                    {
                        int halfW = 23 - y;
                        if (Mathf.Abs(x - 15.5f) <= halfW)
                        {
                            colors[y * size + x] = Color.white;
                        }
                    }

                    // 4 Celtic Ray Pips at cardinal directions
                    if (dist >= radius - 0.5f && dist <= radius + 2.0f)
                    {
                        if (Mathf.Abs(x - 16f) <= 1.2f || Mathf.Abs(y - 16f) <= 1.2f)
                        {
                            colors[y * size + x] = Color.white;
                        }
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            _thermalIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _thermalIconSprite;
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
            }
        }

        private void SubscribeConfigEvents()
        {
            if (ModConfig.UseCustomPosition != null) ModConfig.UseCustomPosition.SettingChanged += OnLayoutConfigChanged;
            if (ModConfig.ThermometerPositionX != null) ModConfig.ThermometerPositionX.SettingChanged += OnLayoutConfigChanged;
            if (ModConfig.ThermometerPositionY != null) ModConfig.ThermometerPositionY.SettingChanged += OnLayoutConfigChanged;
            if (ModConfig.ThermometerScale != null) ModConfig.ThermometerScale.SettingChanged += OnLayoutConfigChanged;
        }

        private void UnsubscribeConfigEvents()
        {
            if (ModConfig.UseCustomPosition != null) ModConfig.UseCustomPosition.SettingChanged -= OnLayoutConfigChanged;
            if (ModConfig.ThermometerPositionX != null) ModConfig.ThermometerPositionX.SettingChanged -= OnLayoutConfigChanged;
            if (ModConfig.ThermometerPositionY != null) ModConfig.ThermometerPositionY.SettingChanged -= OnLayoutConfigChanged;
            if (ModConfig.ThermometerScale != null) ModConfig.ThermometerScale.SettingChanged -= OnLayoutConfigChanged;
        }

        private void OnLayoutConfigChanged(object sender, EventArgs e)
        {
            ApplyLayout();
            VikingClimatePlugin.Log.LogInfo(
                $"[HUDThermometer] Layout updated via config change. " +
                $"Position: {GetCurrentThermometerPosition()}, Scale: {GetCurrentThermometerScale():F2}");
        }

        public void ApplyLayout()
        {
            if (_rootRect == null) return;
            _rootRect.anchoredPosition = GetCurrentThermometerPosition();
            float scale = GetCurrentThermometerScale();
            _rootRect.localScale = new Vector3(scale, scale, 1f);
            if (_rootRect.localPosition.z != 0f)
            {
                _rootRect.localPosition = new Vector3(_rootRect.localPosition.x, _rootRect.localPosition.y, 0f);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeConfigEvents();
            if (_instance == this) _instance = null;
        }
    }
}
