using System;
using UnityEngine;
using UnityEngine.UI;
using VikingClimate.Configuration;
using VikingClimate.Core;

namespace VikingClimate.UI
{
    /// <summary>
    /// Renders an atmospheric heat vision haze vignette and drives the native camera heat distortion
    /// when the player enters extreme heat tiers (Heat Exhaustion, Severe Hyperthermia, Scorching).
    /// Features:
    /// 1. Warm golden-amber mirage edge blur vignette on the HUD.
    /// 2. In-engine 3D heat shimmer and chromatic edge distortion via Valheim's native GameCamera.HeatDistortImageEffect.
    /// 3. Zero fill-rate and zero GPU overhead when running at normal/cool temperatures.
    /// </summary>
    public class ScreenHeatOverlay : MonoBehaviour
    {
        private static ScreenHeatOverlay? _instance;
        private static Sprite? _vignetteSprite;

        private Image? _overlayImage;
        private float _currentAlpha = 0f;
        private float _currentCameraDistort = 0f;

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

            Transform? parentTransform = hud.m_rootObject != null ? hud.m_rootObject.transform : hud.transform;
            if (parentTransform == null) return;

            var go = new GameObject("VikingClimate_ScreenHeatOverlay", typeof(RectTransform));
            go.transform.SetParent(parentTransform, false);

            int targetLayer = hud.m_rootObject != null ? hud.m_rootObject.layer : LayerMask.NameToLayer("UI");
            go.layer = targetLayer;

            _instance = go.AddComponent<ScreenHeatOverlay>();
            _instance.Init();

            VikingClimatePlugin.Log.LogInfo($"[ScreenHeatOverlay] Attached atmospheric heat haze and distortion to '{parentTransform.name}'.");
        }

        private void Init()
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) return;

            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            _overlayImage = gameObject.AddComponent<Image>();
            _overlayImage.sprite = GetHeatVignetteSprite();
            _overlayImage.type = Image.Type.Simple;
            _overlayImage.color = new Color(1.0f, 0.85f, 0.65f, 0f);
            _overlayImage.raycastTarget = false;
            _overlayImage.enabled = false;
        }

        private void Update()
        {
            bool shouldDisable = !ModConfig.ModEnabled.Value || !ModConfig.EnableScreenVignettes.Value || _overlayImage == null;
            var player = Player.m_localPlayer;
            if (shouldDisable || player == null || player.IsDead() || Hud.IsUserHidden())
            {
                ResetEffects();
                return;
            }

            float warmth = SurvivalManager.CurrentState.BodyWarmth;
            float startTemp = ModConfig.HeatVignetteStartTemp.Value;
            float maxAlpha = ModConfig.HeatVignetteMaxAlpha.Value;

            float targetAlpha = 0f;
            float targetCameraDistort = 0f;

            if (warmth > startTemp && maxAlpha > 0f)
            {
                // Full effect reached at 55°C (Catastrophic Scorching)
                float severeTemp = 55.0f;
                float t = Mathf.Clamp01((warmth - startTemp) / Mathf.Max(0.1f, severeTemp - startTemp));

                targetAlpha = Mathf.Lerp(0.015f, maxAlpha, t);
                targetCameraDistort = Mathf.Lerp(0.05f, 0.30f, t);

                // Subtle pulsating heat mirage waves
                if (warmth > 44.0f)
                {
                    float miragePulse = Mathf.Sin(Time.time * 2.0f) * (maxAlpha * 0.15f);
                    targetAlpha += miragePulse;
                    targetCameraDistort += miragePulse * 0.5f;
                }
            }

            // Smooth transitions
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, Time.deltaTime * 0.35f);
            _currentCameraDistort = Mathf.MoveTowards(_currentCameraDistort, targetCameraDistort, Time.deltaTime * 0.40f);

            // Update UI haze vignette
            if (_overlayImage != null)
            {
                if (_currentAlpha > 0.003f)
                {
                    _overlayImage.enabled = true;
                    _overlayImage.color = new Color(1.0f, 0.85f, 0.65f, _currentAlpha);
                }
                else
                {
                    _overlayImage.enabled = false;
                }
            }

            // Update native GameCamera heat distortion
            UpdateCameraDistortion(_currentCameraDistort);
        }

        private void UpdateCameraDistortion(float intensity)
        {
            var cam = GameCamera.instance;
            if (cam == null || cam.m_heatDistortImageEffect == null) return;

            var heatEffect = cam.m_heatDistortImageEffect;
            if (intensity > 0.005f)
            {
                if (!heatEffect.enabled)
                {
                    heatEffect.enabled = true;
                }
                heatEffect.m_intensity = Mathf.Clamp01(intensity);
            }
            else
            {
                heatEffect.m_intensity = 0f;
                if (heatEffect.enabled)
                {
                    heatEffect.enabled = false;
                }
            }
        }

        private void ResetEffects()
        {
            _currentAlpha = 0f;
            _currentCameraDistort = 0f;
            if (_overlayImage != null && _overlayImage.enabled)
            {
                _overlayImage.enabled = false;
            }
            UpdateCameraDistortion(0f);
        }

        private static Sprite GetHeatVignetteSprite()
        {
            if (_vignetteSprite != null) return _vignetteSprite;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (size - 1) * 0.5f;
            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float dy = (y - center) / center;
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Clear center (r < 0.82); only subtle border heat haze fading quickly towards center
                    float alpha = 0f;
                    if (dist > 0.82f)
                    {
                        float t = Mathf.Clamp01((dist - 0.82f) / 0.45f);
                        alpha = t * t;
                    }

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _vignetteSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f)
            );

            return _vignetteSprite;
        }

        private void OnDestroy()
        {
            ResetEffects();
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
