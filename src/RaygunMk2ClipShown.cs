using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// The magazine is part of the gun model (the original's reloads animate it), so a gun with no magazine fitted
    /// still showed one: in your hands, on your back, on the ground and in the item window. Hidden whenever the gun has
    /// no magazine (user, 2026-11-16), except in your hands mid-reload, where the animation brings the new one in.
    /// </summary>
    internal static class RaygunMk2ClipShown
    {
        private const string ClipBone = "tag_clip";

        /// <summary>The clip's mesh objects on a model (not the magazine slots' own items, which ride the clip's bones).</summary>
        public static List<GameObject> Parts(GameObject model)
        {
            var parts = new List<GameObject>();
            Transform clip = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == ClipBone)
                {
                    clip = t;
                    break;
                }
            if (clip == null)
                return parts;
            foreach (Renderer r in clip.GetComponentsInChildren<Renderer>(true))
            {
                bool slot = false;
                for (Transform t = r.transform; t != null && t != clip; t = t.parent)
                    if (t.name.StartsWith("mod_") || t.name.StartsWith("patron_"))
                        slot = true;
                if (!slot && !r.name.Contains("upgraded_materials"))
                    parts.Add(r.gameObject);
            }
            return parts;
        }

        /// <summary>Whether this gun's model should show its clip now.</summary>
        public static bool Wanted(Weapon gun, WeaponPrefab prefab)
        {
            if (gun == null || gun.GetCurrentMagazine() != null)
                return true;
            Player me = LocalPlayer.Get();
            if (prefab != null && me?.HandsController is Player.FirearmController fc && fc._weaponPrefab == prefab)
            {
                string op = fc.CurrentOperation?.GetType().Name ?? "";
                return op.IndexOf("Reload", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        public static void Show(List<GameObject> parts, bool show)
        {
            foreach (GameObject go in parts)
                if (go != null && go.activeSelf != show)
                    go.SetActive(show);
        }

        /// <summary>A model just built for this gun: its clip shown or not straight away (the item window frames it at once).</summary>
        public static void Apply(GameObject model, Item item)
        {
            if (model != null && item is Weapon gun)
                Show(Parts(model), gun.GetCurrentMagazine() != null);
        }
    }
}
