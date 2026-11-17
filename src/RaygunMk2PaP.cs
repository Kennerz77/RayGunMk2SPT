using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using EFT;
using EFT.InventoryLogic;
using PaPHideoutModule;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// Everything the Mark II asks of Pack-a-Punch. PaP is optional: callers go through <see cref="RaygunMk2PaP"/>,
    /// which only touches this class (and so only loads PaPHideoutModule.dll) when PaP is installed.
    /// </summary>
    internal static class RaygunMk2PaPBridge
    {
        public static bool IsInfused(Item item) => PaPApi.IsInfused(item) && !InMachine(item);

        public static bool IsOrigins(Item item) => IsInfused(item) && PaPApi.IsOriginsGun(item);

        /// <summary>What the machine added to the gun's worth: its fee, which depends on the mode picked in PaP.</summary>
        public static int InfusedFeeRub => PaPApi.BonusesActive ? 500_000 : 5_000;

        // Weapons infused when the set last changed (profile ids), so a change can tell which ones were just stripped
        private static HashSet<string> _infused = new HashSet<string>();

        // Guns the machine has just infused (profile id -> when): the server says yes before the gun goes in, but
        // like PaP's camo, the upgraded look waits until it comes back out (WeaponUpgraded). The timeout only
        // covers a sequence that never finishes (leaving the hideout mid-way); the machine takes about 4 s
        private static readonly Dictionary<string, float> Upgrading = new Dictionary<string, float>();
        private const float MachineTimeout = 15f;

        private static bool InMachine(Item item)
        {
            if (Upgrading.Count == 0)
                return false;
            string id = PaPApi.OriginalId(item.Id);
            if (!Upgrading.TryGetValue(id, out float since))
                return false;
            if (Time.time - since < MachineTimeout)
                return true;
            Upgrading.Remove(id);
            return false;
        }

        public static void Setup()
        {
            PaPApi.RegisterName(RaygunMk2Ids.Weapon, RaygunMk2Ids.UpgradedName);
            // Its own shot stays as it is (the original's Pack-a-Punched shot replaces it, registered by the view)
            PaPApi.NoShotTail(RaygunMk2Ids.Weapon);
            // The bare metal (split off by the original's camo map) and the glowing cells keep their look in the Origins
            // machine too (its look paints cut-out materials unless told not to)
            PaPApi.KeepOriginsPlain("_metal");
            PaPApi.KeepOriginsPlain("mk2_body_power");
            // The camo drawn smaller (user, 2026-11-15); where it goes is split at build time by the original's own
            // camo map (bare metal keeps its look). PaP 2.1.2+ only: an older PaP just keeps its size
            try
            {
                SmallerCamo();
            }
            catch (System.MissingMethodException)
            {
            }
            PaPApi.WeaponUpgraded += OnUpgraded;
            PaPApi.InfusedChanged += OnInfusedChanged;
            _infused = new HashSet<string>(PaPApi.InfusedIds);
        }

        public const float CamoTiling = 2.5f;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void SmallerCamo()
        {
            try
            {
                OwnLooks();
            }
            catch (System.MissingMethodException)
            {
                // PaP before 2.2.1: one size for every look
                PaPApi.SetCamoTiling("mk2_body", CamoTiling);
            }
        }

        /// <summary>
        /// The default camo small; the Origins crystal's dark splotches big and bold (bigger pattern, less glow over
        /// it: user, 2026-11-15). PaP 2.2.1+.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void OwnLooks()
        {
            PaPApi.SetCamoTiling("mk2_body", CamoTiling, OriginsTiling);
            PaPApi.SetOriginsGlow("mk2_body", OriginsGlow);
        }

        public const float OriginsTiling = 0.55f;
        public const float OriginsGlow = 0.4f;

        // Out of the machine: the red power cells and the camo show
        private static void OnUpgraded(Item item)
        {
            if (item != null && Upgrading.Remove(item.Id))
                RefreshAll();
        }

        /// <summary>
        /// The infused set changed (an upgrade, a strip, a profile sync): every live Mark II model redoes its power
        /// cells and camo at once, rather than when the gun is next drawn.
        /// </summary>
        private static void OnInfusedChanged()
        {
            var now = new HashSet<string>(PaPApi.InfusedIds);
            // One gun newly infused while in the hideout is the machine at work (a profile sync happens at the menu,
            // with no player, and can bring several at once)
            List<string> added = now.Where(id => !_infused.Contains(id)).ToList();
            if (added.Count == 1 && LocalPlayer.Get() is HideoutPlayer)
                Upgrading[added[0]] = Time.time;
            foreach (string id in _infused.Where(id => !now.Contains(id)))
                Upgrading.Remove(id);
            _infused = now;
            RefreshAll();
        }

        private static void RefreshAll()
        {
            foreach (RaygunMk2View view in RaygunMk2View.Live.ToList())
                view?.Refresh();
        }

        public static string OriginalId(string itemId) => PaPApi.OriginalId(itemId);

        public static void ApplyCamo(GameObject model, Item item) => PaPApi.ApplyCamo(model, item);

        public static void KeepOwnShot() => PaPApi.KeepOwnShotSound(RaygunMk2Ids.Weapon);
    }

    /// <summary>Pack-a-Punch hooks that are safe to call with or without PaP installed.</summary>
    internal static class RaygunMk2PaP
    {
        // PaP 2.0.0 or newer: older ones lack the API these features call (an older one is treated as not installed)
        public static readonly bool Installed =
            Chainloader.PluginInfos.TryGetValue("com.kennerz.paphideoutmodule", out BepInEx.PluginInfo pap) && pap.Metadata.Version >= new System.Version(2, 0, 0);
        private static bool _shotRegistered;

        public static void Setup()
        {
            if (Installed)
                RaygunMk2PaPBridge.Setup();
        }

        public static bool IsInfused(Item item) => Installed && item != null && RaygunMk2PaPBridge.IsInfused(item);

        /// <summary>Upgraded in PaP's Origins machine.</summary>
        public static bool IsOrigins(Item item) => Installed && item != null && RaygunMk2PaPBridge.IsOrigins(item);

        public static int InfusedFeeRub => Installed ? RaygunMk2PaPBridge.InfusedFeeRub : 0;

        /// <summary>The profile id behind a hideout weapons-mode copy (the id itself without PaP).</summary>
        public static string OriginalId(string itemId) => Installed ? RaygunMk2PaPBridge.OriginalId(itemId) : itemId;

        /// <summary>PaP's camo on (or off) a model, by the gun's current state. Nothing without PaP.</summary>
        public static void ApplyCamo(GameObject model, Item item)
        {
            if (Installed && model != null && item != null)
                RaygunMk2PaPBridge.ApplyCamo(model, item);
        }

        /// <summary>The original's Pack-a-Punched shot (in the weapon bundle) becomes the upgraded Mark II's sound.</summary>
        /// <summary>The gun plays its own Pack-a-Punched fire (the view swaps it in): Pack-a-Punch adds nothing on top.</summary>
        public static void KeepOwnShot()
        {
            if (!Installed || _shotRegistered)
                return;
            RaygunMk2PaPBridge.KeepOwnShot();
            _shotRegistered = true;
        }
    }

    internal static class RaygunMk2Ids
    {
        public const string Weapon = "6a7f2b4e8d11530000000001";
        public const string Clip = "6a7f2b4e8d11530000000002";
        public const string Charge = "6a7f2b4e8d11530000000003";
        public const string Name = "Ray Gun Mark II";
        public const string UpgradedName = "Porter's Mark II Ray Gun";

        public static bool Is(Item item) => item != null && item.TemplateId.ToString() == Weapon;

        public static bool IsClip(Item item) => item != null && item.TemplateId.ToString() == Clip;

        public static bool IsCharge(Item item) => item != null && item.TemplateId.ToString() == Charge;
    }
}
