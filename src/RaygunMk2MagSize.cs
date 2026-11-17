using EFT.InventoryLogic;
using HarmonyLib;

namespace RaygunMk2
{
    /// <summary>
    /// The Mark II battery clip's size as it reads: 21/21 (a plain gun's), 42/42 only while it sits in a Pack-a-Punched gun (user,
    /// 2026-11-16). The item itself has room for the upgraded count (its template's), so a Pack-a-Punch top-up fits; a
    /// magazine taken out still holding more than a plain gun's reads at the upgraded size until it runs down.
    /// </summary>
    internal static class RaygunMk2MagSize
    {
        public static void Enable(Harmony harmony)
        {
            harmony.Patch(AccessTools.PropertyGetter(typeof(Magazine), nameof(Magazine.MaxCount)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(RaygunMk2MagSize), nameof(MaxCountPostfix))));
        }

        private static readonly System.Reflection.FieldInfo SlotMax = AccessTools.Field(typeof(StackSlot), "MaxCount");

        private static void MaxCountPostfix(Magazine __instance, ref int __result)
        {
            if (!RaygunMk2Ids.IsClip(__instance))
                return;
            Item holder = __instance.CurrentAddress?.Container?.ParentItem;
            bool upgraded = holder is Weapon gun && RaygunMk2Ids.Is(gun) ? RaygunMk2PaP.IsInfused(gun) : __instance.Count > RaygunMk2Rounds.Clip;
            __result = upgraded ? RaygunMk2Rounds.UpgradedClip : RaygunMk2Rounds.Clip;
            // The cartridge slot itself holds that many: the item has room for the Pack-a-Punched count, so 64 rounds
            // could be loaded into a mag reading /32 (user, 2026-11-16)
            if (SlotMax != null && __instance.Cartridges != null && __instance.Cartridges.MaxCount != __result)
                SlotMax.SetValue(__instance.Cartridges, __result);
        }
    }
}
