using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VikingClimate.Configuration;
using VikingClimate.Core;

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

        private RectTransform? _rootRect;
        private CanvasGroup? _canvasGroup;
        private Hud? _hud;

        // Underneath 32x32 Icon
        private Image? _iconImage;

        // Bar Components
        private RectTransform? _bodyFillRect;
        private Image? _bodyFillImage;
        private TMP_Text? _bodyText;

        // Ambient Target Pointer: [bar]<| (feels like)
        private RectTransform? _ambientMarkerRect;
        private Image? _ambientMarkerImage;
        private TMP_Text? _ambientBadgeText;

        // Layout Constants matching Valheim's Health bar
        private const float ColumnWidth = 32f;
        private const float ColumnHeight = 110f;
        private const float CavityInset = 2f;
        private const float CavityWidth = ColumnWidth - (CavityInset * 2f); // 28px
        private const float CavityHeight = ColumnHeight - (CavityInset * 2f); // 106px

        // Positioning: Column starts at X = 106 (Center X = 122), Y = 95.8f (Health base)
        // Perfectly matches the ~14-17px gap between Food and Health columns
        private const float BaseX = 106f;
        private const float BaseY = 95.8f;
        private const float IconCenterY = 74f; // Matches healthicon Y = 74f

        private const float VanillaGpX = 209f;
        private const float VanillaGpY = 86f;

        private Vector2? _vanillaQuickSlotsPos;

        // Temperature Scale: -25°C to +50°C (75°C total span)
        private const float MinTempScale = -25f;
        private const float MaxTempScale = 50f;
        private const float TempRange = MaxTempScale - MinTempScale;

        public static void AttachToHud(Hud hud)
        {
            if (hud == null) return;
            if (_instance != null && _instance.gameObject != null)
            {
                if (!_instance.gameObject.activeSelf)
                {
                    _instance.gameObject.SetActive(true);
                }
                return;
            }

            Transform? parentTransform = hud.m_rootObject != null ? hud.m_rootObject.transform : (hud.m_gpRoot != null ? hud.m_gpRoot.parent : null);
            if (parentTransform == null) return;

            var go = new GameObject("VikingClimate_DualThermometer", typeof(RectTransform));
            go.transform.SetParent(parentTransform, false);

            int targetLayer = hud.m_rootObject != null ? hud.m_rootObject.layer : LayerMask.NameToLayer("UI");
            SetLayerRecursively(go, targetLayer);

            _instance = go.AddComponent<HUDThermometer>();
            _instance.Init(hud);

            VikingClimatePlugin.Log.LogInfo($"[HUDThermometer] Attached 32px HP-style column to '{parentTransform.name}' on layer {targetLayer}.");
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
            var iconSprite = GetThermalIconSprite();

            // 1. Root RectTransform positioned as the column frame
            _rootRect.localScale = Vector3.one;
            _rootRect.localRotation = Quaternion.identity;
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.zero;
            _rootRect.pivot = Vector2.zero;
            _rootRect.sizeDelta = new Vector2(ColumnWidth, ColumnHeight);
            _rootRect.anchoredPosition = new Vector2(BaseX + ModConfig.ThermometerOffsetX.Value, BaseY + ModConfig.ThermometerOffsetY.Value);
            _rootRect.SetAsLastSibling();

            // 2. 32x32 Icon Underneath (matching healthicon size and baseline)
            var iconGo = new GameObject("ThermalIcon", typeof(RectTransform));
            iconGo.transform.SetParent(transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.zero;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(32f, 32f);
            // Relative to root base (Y = 95.8), icon center is at Y = 74 -> delta = -21.8f
            iconRect.anchoredPosition = new Vector2(ColumnWidth * 0.5f, IconCenterY - BaseY);

            _iconImage = iconGo.AddComponent<Image>();
            _iconImage.sprite = iconSprite;
            _iconImage.type = Image.Type.Simple;
            _iconImage.color = new Color(0.51f, 0.78f, 0.52f);
            _iconImage.raycastTarget = false;

            // 3. Column Outer Border (Valheim cylindrical border trim)
            var borderImg = gameObject.AddComponent<Image>();
            borderImg.sprite = whiteSprite;
            borderImg.type = Image.Type.Simple;
            borderImg.color = new Color(0.18f, 0.16f, 0.14f, 0.95f);
            borderImg.raycastTarget = false;

            // 4. Inner Dark Cavity
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
            cavityImg.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);
            cavityImg.raycastTarget = false;

            // 5. Cylindrical Segment Dividers (matching the HP bar segments)
            CreateSegmentLine(cavityGo, "Segment1", whiteSprite, CavityHeight * 0.25f);
            CreateSegmentLine(cavityGo, "Segment2", whiteSprite, CavityHeight * 0.50f);
            CreateSegmentLine(cavityGo, "Segment3", whiteSprite, CavityHeight * 0.75f);

            // Reference Freezing Marker (0°C = 33.3%)
            float freezeRatio = (0f - MinTempScale) / TempRange;
            CreateReferenceLine(cavityGo, "FreezingLine", whiteSprite,
                freezeRatio * CavityHeight, CavityWidth, 1.5f,
                new Color(0.39f, 0.71f, 0.96f, 0.50f));

            // Reference Comfort Band (18°C to 26°C = 57.3% to 68.0%)
            float comfortMinRatio = (18f - MinTempScale) / TempRange;
            float comfortMaxRatio = (26f - MinTempScale) / TempRange;
            CreateReferenceBand(cavityGo, "ComfortBand", whiteSprite,
                comfortMinRatio * CavityHeight, (comfortMaxRatio - comfortMinRatio) * CavityHeight, CavityWidth,
                new Color(0.51f, 0.78f, 0.52f, 0.16f));

            // 6. Body Warmth Liquid Fill (rises vertically from bottom)
            var fillGo = new GameObject("BodyFill", typeof(RectTransform));
            fillGo.transform.SetParent(cavityGo.transform, false);
            _bodyFillRect = fillGo.GetComponent<RectTransform>();
            _bodyFillRect.localScale = Vector3.one;
            _bodyFillRect.localRotation = Quaternion.identity;
            _bodyFillRect.anchorMin = Vector2.zero;
            _bodyFillRect.anchorMax = new Vector2(1f, 0f);
            _bodyFillRect.pivot = new Vector2(0.5f, 0f);
            _bodyFillRect.anchoredPosition = Vector2.zero;
            _bodyFillRect.sizeDelta = new Vector2(0f, 0f);

            _bodyFillImage = fillGo.AddComponent<Image>();
            _bodyFillImage.sprite = whiteSprite;
            _bodyFillImage.type = Image.Type.Simple;
            _bodyFillImage.color = new Color(0.51f, 0.78f, 0.52f);
            _bodyFillImage.raycastTarget = false;

            // 7. Ambient Target Pointer: [bar]<| (feels like)
            // Attaches to the right edge of the column without intersecting the cavity or numbers
            var indicatorGo = new GameObject("AmbientIndicator", typeof(RectTransform));
            indicatorGo.transform.SetParent(transform, false);
            _ambientMarkerRect = indicatorGo.GetComponent<RectTransform>();
            _ambientMarkerRect.localScale = Vector3.one;
            _ambientMarkerRect.localRotation = Quaternion.identity;
            _ambientMarkerRect.anchorMin = Vector2.zero;
            _ambientMarkerRect.anchorMax = Vector2.zero;
            _ambientMarkerRect.pivot = new Vector2(0f, 0.5f);
            _ambientMarkerRect.sizeDelta = new Vector2(40f, 16f);
            _ambientMarkerRect.anchoredPosition = new Vector2(ColumnWidth, CavityInset + (CavityHeight * 0.5f));

            // Arrow: <| pointing left directly at the thermometer level
            var arrowGo = new GameObject("Arrow", typeof(RectTransform));
            arrowGo.transform.SetParent(indicatorGo.transform, false);
            var arrowRect = arrowGo.GetComponent<RectTransform>();
            arrowRect.localScale = Vector3.one;
            arrowRect.localRotation = Quaternion.identity;
            arrowRect.anchorMin = new Vector2(0f, 0.5f);
            arrowRect.anchorMax = new Vector2(0f, 0.5f);
            arrowRect.pivot = new Vector2(0f, 0.5f);
            arrowRect.sizeDelta = new Vector2(7f, 9f);
            arrowRect.anchoredPosition = new Vector2(0f, 0f);

            _ambientMarkerImage = arrowGo.AddComponent<Image>();
            _ambientMarkerImage.sprite = GetArrowSprite();
            _ambientMarkerImage.type = Image.Type.Simple;
            _ambientMarkerImage.color = Color.white;
            _ambientMarkerImage.raycastTarget = false;

            TMP_Text? fontSource = hud.m_healthText ?? hud.m_gpName ?? hud.m_actionName;

            // Ambient Temperature Badge (positioned cleanly after the arrow base, vertically centered with the arrow)
            _ambientBadgeText = CreateTextElement(indicatorGo, "AmbientBadge", fontSource,
                new Vector2(9.5f, 0f), new Vector2(32f, 16f), 10.5f);
            _ambientBadgeText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _ambientBadgeText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _ambientBadgeText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _ambientBadgeText.rectTransform.anchoredPosition = new Vector2(9.5f, 0f);
            _ambientBadgeText.alignment = TextAlignmentOptions.MidlineLeft;
            _ambientBadgeText.margin = Vector4.zero;
            _ambientBadgeText.text = "17°";

            // 8. Large Centered Body Warmth Text (exact same 18pt font and style as HP bar)
            _bodyText = CreateTextElement(gameObject, "BodyText", fontSource,
                new Vector2(ColumnWidth * 0.5f, ColumnHeight * 0.5f),
                new Vector2(ColumnWidth + 10f, 24f), 18f);
            _bodyText.alignment = TextAlignmentOptions.Center;
            _bodyText.fontStyle = FontStyles.Bold;
            _bodyText.text = "22°";

            SetLayerRecursively(gameObject, gameObject.layer);
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
            TMP_Text? fontSource,
            Vector2 centerPos,
            Vector2 size,
            float fontSize)
        {
            GameObject textGo;
            if (fontSource != null && fontSource.gameObject != null)
            {
                textGo = UnityEngine.Object.Instantiate(fontSource.gameObject, parent.transform, false);
                textGo.name = name;
            }
            else
            {
                textGo = new GameObject(name, typeof(RectTransform));
                textGo.transform.SetParent(parent.transform, false);
            }

            var rt = textGo.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centerPos;
            rt.sizeDelta = size;

            var tmp = textGo.GetComponent<TMP_Text>();
            if (tmp == null)
            {
                tmp = textGo.AddComponent<TextMeshProUGUI>();
                if (fontSource != null && fontSource.font != null)
                {
                    tmp.font = fontSource.font;
                }
            }

            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
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

            // 1. Maintain LowerLeft anchored position with config offsets
            if (_rootRect != null)
            {
                _rootRect.anchoredPosition = new Vector2(BaseX + ModConfig.ThermometerOffsetX.Value, BaseY + ModConfig.ThermometerOffsetY.Value);
            }

            // 2. Synchronized Rightward Shift of Forsaken Power & Quick Slots
            float gpShift = ModConfig.ForsakenPowerOffsetX.Value;
            if (hud.m_gpRoot != null)
            {
                hud.m_gpRoot.anchoredPosition = new Vector2(VanillaGpX + gpShift, VanillaGpY);
            }

            // Also shift QuickSlotsHotkeyBar so Forsaken Power never collides with it
            if (hud.m_rootObject != null)
            {
                var qsTransform = hud.m_rootObject.transform.Find("QuickSlotsHotkeyBar") as RectTransform;
                if (qsTransform != null)
                {
                    if (!_vanillaQuickSlotsPos.HasValue)
                    {
                        _vanillaQuickSlotsPos = qsTransform.anchoredPosition;
                    }
                    qsTransform.anchoredPosition = new Vector2(_vanillaQuickSlotsPos.Value.x + gpShift, _vanillaQuickSlotsPos.Value.y);
                }
            }

            var state = SurvivalManager.CurrentState;

            // 3. Update Body Warmth (Liquid Fill + Large 18pt Text + Underneath Icon)
            float bodyWarmth = state.BodyWarmth;
            Color warmthColor = GetTemperatureColor(bodyWarmth);
            float bodyRatio = Mathf.Clamp01((bodyWarmth - MinTempScale) / TempRange);

            if (_bodyFillRect != null)
            {
                _bodyFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CavityHeight * bodyRatio);
            }
            if (_bodyFillImage != null)
            {
                _bodyFillImage.color = warmthColor;
            }
            if (_bodyText != null)
            {
                _bodyText.text = $"{bodyWarmth:F0}°";
                _bodyText.color = Color.white;
            }
            if (_iconImage != null)
            {
                _iconImage.color = warmthColor;
            }

            // 4. Update Ambient Target Marker (Needle line + sliding degree badge)
            float ambientTemp = state.AmbientTemperature;
            Color ambColor = GetTemperatureColor(ambientTemp);
            float ambientRatio = Mathf.Clamp01((ambientTemp - MinTempScale) / TempRange);

            if (_ambientMarkerRect != null)
            {
                float arrowY = CavityInset + (CavityHeight * ambientRatio);
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
            if (temp < 0.0f) return new Color(0.39f, 0.71f, 0.96f);  // Ice Blue
            if (temp < 18.0f) return new Color(0.51f, 0.83f, 0.98f); // Cyan Chilly
            if (temp <= 26.0f) return new Color(0.51f, 0.78f, 0.52f);// Comfort Green
            if (temp <= 45.0f) return new Color(1.0f, 0.72f, 0.30f); // Warm Amber
            return new Color(0.90f, 0.45f, 0.45f);                   // Scorching Red
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

        private void OnDestroy()
        {
            if (_hud != null && _hud.m_gpRoot != null)
            {
                _hud.m_gpRoot.anchoredPosition = new Vector2(VanillaGpX, VanillaGpY);
            }
            if (_hud != null && _hud.m_rootObject != null && _vanillaQuickSlotsPos.HasValue)
            {
                var qsTransform = _hud.m_rootObject.transform.Find("QuickSlotsHotkeyBar") as RectTransform;
                if (qsTransform != null)
                {
                    qsTransform.anchoredPosition = _vanillaQuickSlotsPos.Value;
                }
            }

            if (_instance == this) _instance = null;
        }
    }
}
