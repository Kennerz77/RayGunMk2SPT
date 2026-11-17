using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// The inventory pictures. The game's icon studio washes these models out white, so the cells get pictures
    /// rendered at build time instead (icons/ beside the plugin): the Mark II, the Pack-a-Punched one, and the
    /// battery clip. Each is fitted into the sprite the studio made, so it sits in the cell like any icon.
    /// </summary>
    internal static class RaygunMk2Icons
    {
        private static readonly string Dir = Path.Combine(Path.GetDirectoryName(typeof(RaygunMk2Icons).Assembly.Location)!, "icons");
        private static readonly Dictionary<string, Texture2D> Images = new Dictionary<string, Texture2D>();

        // Bumped whenever the pictures change, so the game's disk cache of older ones (the white studio photos) is retired
        private const int Salt = 0x4D4B0002;

        public static void Enable(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(ItemIconCreator), nameof(ItemIconCreator.RenderModel)),
                postfix: new HarmonyMethod(typeof(RaygunMk2Icons).GetMethod(nameof(RenderModelPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }

        /// <summary>Mixed into the icon hash of every Mark II item (RaygunMk2Models adds the PaP look).</summary>
        public static int Salted(int hash) => hash * 31 + Salt;

        public static string ImageFor(Item item)
        {
            if (RaygunMk2Ids.Is(item))
            {
                string look = RaygunMk2PaP.IsInfused(item) ? (RaygunMk2PaP.IsOrigins(item) ? "raygunmk2_pap_origins" : "raygunmk2_pap") : "raygunmk2";
                // No magazine in it: its own picture, the clip left off (user, 2026-11-16)
                return item is Weapon gun && gun.GetCurrentMagazine() == null ? look + "_nomag" : look;
            }
            if (RaygunMk2Ids.IsClip(item))
                return "clip";
            return null;
        }

        private static void RenderModelPostfix(Item item, ref Task<IconCreatorBase<Item, ItemIcon>.RenderModelResult> __result)
        {
            string image = ImageFor(item);
            if (image != null && __result != null)
                __result = WithImage(__result, image);
        }

        private static async Task<IconCreatorBase<Item, ItemIcon>.RenderModelResult> WithImage(Task<IconCreatorBase<Item, ItemIcon>.RenderModelResult> task, string name)
        {
            var result = await task;
            try
            {
                Texture2D image = Load(name);
                if (result.sprite != null && image != null)
                    result.sprite = Fit(result.sprite, image);
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogError($"RaygunMk2 icon {name} failed, keeping the studio's: {e.Message}");
            }
            return result;
        }

        private static Texture2D Load(string name)
        {
            if (Images.TryGetValue(name, out Texture2D cached) && cached != null)
                return cached;
            string path = Path.Combine(Dir, name + ".png");
            if (!File.Exists(path))
            {
                RaygunMk2Plugin.Log.LogWarning($"RaygunMk2 icon missing: {path}");
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "mk2_icon_" + name, wrapMode = TextureWrapMode.Clamp };
            tex.LoadImage(File.ReadAllBytes(path), false); // stays readable: Fit samples it on the CPU
            return Images[name] = tex;
        }

        /// <summary>
        /// The picture scaled to fit the studio's sprite (same size, pivot and pixels per unit), centred, keeping its
        /// shape. On the CPU: a GPU blit from the studio's callbacks picks up its render state and washed icons out.
        /// </summary>
        private static Sprite Fit(Sprite like, Texture2D image)
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(like.rect.width)), h = Mathf.Max(1, Mathf.RoundToInt(like.rect.height));
            // Slight margin, as the studio leaves round its models
            float scale = Mathf.Min((float)w / image.width, (float)h / image.height) * 0.92f;
            float iw = image.width * scale, ih = image.height * scale;
            float x0 = (w - iw) / 2f, y0 = (h - ih) / 2f;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f - x0) / iw, v = (y + 0.5f - y0) / ih;
                    pixels[y * w + x] = u < 0f || u > 1f || v < 0f || v > 1f ? Color.clear : image.GetPixelBilinear(u, v);
                }
            }
            var fitted = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = image.name, wrapMode = TextureWrapMode.Clamp };
            fitted.SetPixels(pixels);
            fitted.Apply(false, false); // readable: the game reads icons back to cache them
            var pivot = new Vector2(like.pivot.x / like.rect.width, like.pivot.y / like.rect.height);
            return Sprite.Create(fitted, new Rect(0, 0, w, h), pivot, like.pixelsPerUnit);
        }
    }
}
