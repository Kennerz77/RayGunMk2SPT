using System;
using System.Linq;
using EFT.InventoryLogic;
using EFT.Trading;
using HarmonyLib;

namespace RaygunMk2
{
    /// <summary>
    /// What traders pay for the Ray Gun Mark II's items (the server pays the price the client offers). The gun always
    /// sells for what the wall charged, plus the Pack-a-Punch fee when infused; a battery clip for what the ammo box
    /// charged, less for the shots spent, and nothing once empty.
    /// </summary>
    internal static class RaygunMk2SellPrice
    {
        public const int GunRub = 950_000;
        public const int CartridgeRub = 15_000;

        public static void Enable(Harmony harmony)
        {
            // Last, so PaP's own postfix (which adds its fee to the vanilla price) has already run and is replaced
            harmony.Patch(AccessTools.Method(typeof(Trader), nameof(Trader.GetUserItemPrice)),
                postfix: new HarmonyMethod(typeof(RaygunMk2SellPrice).GetMethod(nameof(PricePostfix),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)) { priority = Priority.Last });
        }

        private static void PricePostfix(Trader __instance, Item item, ref Trader.ItemPrice? __result)
        {
            try
            {
                // No price means this trader doesn't buy it; that stays so
                if (__result == null || item == null)
                    return;
                long rub;
                if (RaygunMk2Ids.Is(item))
                    rub = GunRub + (RaygunMk2PaP.IsInfused(item) ? RaygunMk2PaP.InfusedFeeRub : 0);
                else if (RaygunMk2Ids.IsClip(item))
                    rub = CartridgeValue(item);
                else
                    return;
                Trader.ItemPrice price = __result.Value;
                double course = price.CurrencyId.HasValue && __instance.CurrencyCourses != null
                                && __instance.CurrencyCourses.TryGetValue(price.CurrencyId.Value.ToString(), out double c) && c > 0 ? c : 1.0;
                __result = new Trader.ItemPrice(price.CurrencyId, (int)Math.Min((long)Math.Floor(rub / course), int.MaxValue));
            }
            catch (Exception e)
            {
                RaygunMk2Plugin.Log.LogError($"Sell price failed: {e.Message}");
            }
        }

        /// <summary>A battery clip: what the ammo box charged for a full one, by the shots it still holds.</summary>
        private static long CartridgeValue(Item cartridge)
        {
            int charges = (int)cartridge.GetAllItems().Where(i => i.TemplateId.ToString() == RaygunMk2Ids.Charge).Sum(i => i.StackObjectsCount);
            if (charges <= 0)
                return 0;
            return (long)CartridgeRub * Math.Min(charges, RaygunMk2Rounds.Clip) / RaygunMk2Rounds.Clip;
        }
    }
}
