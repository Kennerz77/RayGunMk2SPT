using System.Reflection;
using EFT.UI.WeaponModding;
using HarmonyLib;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// The 3D view in the item window centres a gun on its saved pivot, which only fits the pose the model was saved in
    /// (the Mark II came out off to the side, user 2026-11-15). Here it is centred on what's actually drawn, every time
    /// (the Ray Gun's fix).
    /// </summary>
    internal static class RaygunMk2Preview
    {
        public static void Enable(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(WeaponPreview), nameof(WeaponPreview.PositionGameObject)),
                postfix: new HarmonyMethod(typeof(RaygunMk2Preview).GetMethod(nameof(PositionPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }

        private static void PositionPostfix(GameObject itemGameObject)
        {
            try
            {
                if (itemGameObject == null || !IsOurs(itemGameObject))
                    return;
                Bounds bounds = WeaponPreview.GetBounds(itemGameObject);
                if (bounds.extents == Vector3.zero)
                    return;
                Transform parent = itemGameObject.transform.parent;
                Vector3 centre = parent != null ? parent.position : Vector3.zero;
                itemGameObject.transform.position += centre - bounds.center;
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II preview centring skipped: {e.Message}");
            }
        }

        // Our model's weapon object (inside the container) is named weapon_rgmk2*
        private static bool IsOurs(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("weapon_rgmk2"))
                    return true;
            return false;
        }
    }
}
