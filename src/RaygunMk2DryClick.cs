using EFT;
using HarmonyLib;

namespace RaygunMk2
{
    /// <summary>
    /// Empty, the Mark II clicks once per trigger pull, not on every cycle while the trigger is held (user, 2026-11-16:
    /// holding the trigger spammed the click). The click is the gun animation's dry-fire sound event; while your Mark II's
    /// trigger stays down after one click, the next ones are dropped.
    /// </summary>
    internal static class RaygunMk2DryClick
    {
        private static bool _clicked;

        public static void Enable(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(BaseSoundPlayer), nameof(BaseSoundPlayer.SoundEventHandler)),
                new HarmonyMethod(AccessTools.Method(typeof(RaygunMk2DryClick), nameof(SoundPrefix))));
        }

        /// <summary>
        /// The trigger let go: the next pull may click again. The game's own trigger state drops between dry clicks
        /// while the button is still held (the click spammed on, user 2026-11-16), so the fire button itself must be up too.
        /// </summary>
        public static void Tick(Player.FirearmController hands)
        {
            if (UnityEngine.Input.GetMouseButton(0))
                return;
            if (hands == null || !hands.IsTriggerPressed)
                _clicked = false;
        }

        private static bool SoundPrefix(string soundName)
        {
            if (soundName == null || soundName.IndexOf("DryFire", System.StringComparison.OrdinalIgnoreCase) < 0)
                return true;
            Player me = LocalPlayer.Get();
            if (!(me?.HandsController is Player.FirearmController hands) || !RaygunMk2Ids.Is(hands.Item)
                || (!hands.IsTriggerPressed && !UnityEngine.Input.GetMouseButton(0)))
                return true;
            if (_clicked)
                return false;
            _clicked = true;
            return true;
        }
    }
}
