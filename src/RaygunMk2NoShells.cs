using EFT;
using HarmonyLib;

namespace RaygunMk2
{
    /// <summary>
    /// No spent shells fly out of the Mark II (user, 2026-11-16): an energy gun. The game spawns them only when it
    /// thinks they'd be seen (Firearms.IsShellVisible), so for the Mark II's model that answer is always no.
    /// </summary>
    internal static class RaygunMk2NoShells
    {
        private static readonly AccessTools.FieldRef<Firearms, WeaponPrefab> Prefab = AccessTools.FieldRefAccess<Firearms, WeaponPrefab>("_weaponPrefab");

        public static void Enable(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Firearms), nameof(Firearms.IsShellVisible)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(RaygunMk2NoShells), nameof(Postfix))));
        }

        private static void Postfix(Firearms __instance, ref bool __result)
        {
            try
            {
                if (__result && Prefab(__instance)?.GetComponentInChildren<RaygunMk2View>(true) != null)
                    __result = false;
            }
            catch
            {
            }
        }
    }
}
