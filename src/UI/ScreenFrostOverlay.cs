using System;
using UnityEngine;
using UnityEngine.UI;
using VikingClimate.Configuration;
using VikingClimate.Core;

namespace VikingClimate.UI
{
    /// <summary>
    /// Renders a subtle, atmospheric screen-edge frost vignette when the player enters cold tiers.
    /// Uses a procedural radial vignette texture with zero asset bundle dependencies and zero fill-rate overhead when warm.
    /// </summary>
    public class ScreenFrostOverlay : MonoBehaviour
    {
        private static ScreenFrostOverlay? _instance;
        private static Sprite? _vignetteSprite;

        private Image? _overlayImage;
        private float _currentAlpha = 0f;

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

            var go = new GameObject("VikingClimate_ScreenFrostOverlay", typeof(RectTransform));
            go.transform.SetParent(parentTransform, false);

            int targetLayer = hud.m_rootObject != null ? hud.m_rootObject.layer : LayerMask.NameToLayer("UI");
            go.layer = targetLayer;

            _instance = go.AddComponent<ScreenFrostOverlay>();
            _instance.Init();

            VikingClimatePlugin.Log.LogInfo($"[ScreenFrostOverlay] Attached atmospheric frost vignette to '{parentTransform.name}'.");
        }

        private void Init()
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) return;

            // Fullscreen stretch
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling(); // Render behind HUD elements like bars and icons

            _overlayImage = gameObject.AddComponent<Image>();
            _overlayImage.sprite = GetFrostVignetteSprite();
            _overlayImage.type = Image.Type.Simple;
            _overlayImage.color = new Color(0.80f, 0.92f, 1.0f, 0f);
            _overlayImage.raycastTarget = false;
            _overlayImage.enabled = false;
        }

        private void Update()
        {
            if (!ModConfig.ModEnabled.Value || _overlayImage == null)
            {
                if (_overlayImage != null && _overlayImage.enabled)
                {
                    _overlayImage.enabled = false;
                }
                return;
            }

            var player = Player.m_localPlayer;
            if (player == null || player.IsDead() || Hud.IsUserHidden())
            {
                if (_overlayImage.enabled)
                {
                    _overlayImage.enabled = false;
                }
                return;
            }

            float warmth = SurvivalManager.CurrentState.BodyWarmth;
            float targetAlpha = 0f;

            // Compute target frost vignette alpha based on cold severity:
            // Comfort / Chilly (>= 8°C): 0%
            // Bitter Cold (0°C to 8°C): 0% -> 12%
            // Numbing Frost (-12°C to 0°C): 12% -> 28%
            // Hypothermia (< -12°C): 28% -> 50%
            if (warmth < 0f && warmth >= -12f)
            {
                float t = Mathf.Clamp01(-warmth / 12f);
                targetAlpha = Mathf.Lerp(0.12f, 0.28f, t);
            }
            else if (warmth < -12f)
            {
                float t = Mathf.Clamp01((-warmth - 12f) / 13f);
                targetAlpha = Mathf.Lerp(0.28f, 0.50f, t);
                // Subtle shivering pulse in hypothermia
                targetAlpha += Mathf.Sin(Time.time * 2.2f) * 0.03f;
            }
            else if (warmth < 8f)
            {
                float t = Mathf.Clamp01((8f - warmth) / 8f);
                targetAlpha = Mathf.Lerp(0f, 0.12f, t);
            }

            // Smooth transition
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, Time.deltaTime * 0.35f);

            if (_currentAlpha > 0.005f)
            {
                _overlayImage.enabled = true;
                _overlayImage.color = new Color(0.80f, 0.92f, 1.0f, _currentAlpha);
            }
            else
            {
                _overlayImage.enabled = false;
            }
        }

        private static Sprite GetFrostVignetteSprite()
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
                    // Normalized distance from screen center (0 at center, 1 at edge, 1.41 at corners)
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Transparent center oval (r < 0.60); soft frost vignette ramping up to the edges
                    float alpha = 0f;
                    if (dist > 0.60f)
                    {
                        float t = Mathf.Clamp01((dist - 0.60f) / 0.55f);
                        alpha = t * t * (3f - 2f * t); // Smoothstep curve
                    }

                    // Frosted ice-blue tint
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
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
