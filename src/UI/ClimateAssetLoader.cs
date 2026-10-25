using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace VikingClimate.UI
{
    /// <summary>
    /// Manages loading, caching, and fallback synthesis for VikingClimate textures and sprites.
    /// Supports both runtime disk-loading from the BepInEx plugins folder and embedded assembly resources.
    /// </summary>
    public static class ClimateAssetLoader
    {
        private static readonly Dictionary<string, Sprite> s_spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> s_textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        public static Sprite? GetIcon(string iconName)
        {
            return LoadSprite("icons/" + iconName);
        }

        public static Sprite? GetUISprite(string spriteName)
        {
            return LoadSprite("ui/" + spriteName);
        }

        public static Sprite? LoadSprite(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            if (s_spriteCache.TryGetValue(relativePath, out var cached) && cached != null)
            {
                return cached;
            }

            var tex = LoadTexture(relativePath);
            if (tex == null) return null;

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sprite.name = Path.GetFileNameWithoutExtension(relativePath);

            s_spriteCache[relativePath] = sprite;
            return sprite;
        }

        public static Texture2D? LoadTexture(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            if (s_textureCache.TryGetValue(relativePath, out var cached) && cached != null)
            {
                return cached;
            }

            byte[]? data = null;
            string normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (!normalizedPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath += ".png";
            }

            // 1. Try disk path in BepInEx plugin directory
            try
            {
                string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string diskPath = Path.Combine(pluginDir, "assets", normalizedPath);
                if (File.Exists(diskPath))
                {
                    data = File.ReadAllBytes(diskPath);
                }
                else
                {
                    // Check local project directory (for testing / development)
                    string currentDir = Directory.GetCurrentDirectory();
                    string devPath = Path.Combine(currentDir, "assets", normalizedPath);
                    if (File.Exists(devPath))
                    {
                        data = File.ReadAllBytes(devPath);
                    }
                    else
                    {
                        string climateDevPath = Path.Combine(currentDir, "VikingClimate", "assets", normalizedPath);
                        if (File.Exists(climateDevPath))
                        {
                            data = File.ReadAllBytes(climateDevPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                VikingClimatePlugin.Log.LogWarning($"[ClimateAssetLoader] Error reading disk asset '{normalizedPath}': {ex.Message}");
            }

            // 2. Try embedded manifest resource
            if (data == null)
            {
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    string resName = $"VikingClimate.assets.{relativePath.Replace('/', '.').Replace('\\', '.')}.png";
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        data = ms.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    VikingClimatePlugin.Log.LogWarning($"[ClimateAssetLoader] Error reading embedded resource '{relativePath}': {ex.Message}");
                }
            }

            if (data == null || data.Length == 0)
            {
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(data))
            {
                tex.name = Path.GetFileNameWithoutExtension(relativePath);
                s_textureCache[relativePath] = tex;
                return tex;
            }

            return null;
        }
    }
}
