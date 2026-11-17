using System.Collections;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// The F12 settings: volumes and the shot push-back (kept for players), and the dev section with the debug give
    /// (hidden in release builds).
    /// </summary>
    internal static class RaygunMk2Settings
    {
        public const string VolumeSection = "Volume";
        public const string ShootingSection = "1. Shooting";
        public const string DebugSection = "9. Debug (dev)";
        public const string StationsSection = "8. Hideout stations (dev)";

        public static ConfigEntry<float> PurchaseVolume, WallBuyVolume, DenyVolume;
        public static ConfigEntry<bool> FlipChalk;
        public static ConfigEntry<float> WallX, WallY, WallZ, WallYaw, BoxX, BoxY, BoxZ, BoxYaw;
        private static ConfigEntry<bool> _wallHere, _boxHere, _logStations;
        public static ConfigEntry<float> ShotVolume, FoleyVolume, ImpactVolume, PushBack, BurstGap, Spread;
        private static ConfigEntry<bool> _giveGun, _giveClip, _giveZombies;

        public static void Bind(ConfigFile config)
        {
            ShotVolume = config.Bind(VolumeSection, "Shot", 0.15f, Volume("The Mark II's shot. (The Pack-a-Punched shot is Pack-a-Punch's \"Pack-a-Punch shot\" slider.)"));
            FoleyVolume = config.Bind(VolumeSection, "Reload and handling", 1f, Volume("Drawing, reloading, the switch and the dry click."));
            ImpactVolume = config.Bind(VolumeSection, "Bolt impact", 0.12f, Volume("The bolts bursting where they hit."));
            PushBack = config.Bind(ShootingSection, "Push back per shot (m)", 0.02f, new ConfigDescription(
                "How far each shot pushes you back. 0 = off.", new AcceptableValueRange<float>(0f, 0.2f)));
            Spread = config.Bind(ShootingSection, "Spread (degrees)", 0.6f, new ConfigDescription(
                "How far each bolt may stray from where you aim, at random. 0 = dead straight.", new AcceptableValueRange<float>(0f, 3f)));
            BurstGap = config.Bind(ShootingSection, "Pause between bursts (s)", 0.2f, new ConfigDescription(
                "How long after a burst the next trigger pull fires (the original: 0.2).", new AcceptableValueRange<float>(0f, 0.6f)));
            PurchaseVolume = config.Bind(VolumeSection, "Purchase", 0.8f, Volume("The \"cha-ching\" when you buy from the wall or the ammo box."));
            WallBuyVolume = config.Bind(VolumeSection, "Wall buy", 0.8f, Volume("The sound of the gun coming out of the chalk wall."));
            DenyVolume = config.Bind(VolumeSection, "Deny", 0.8f, Volume("The buzz when a purchase is refused (not enough money, no room)."));

            // Where the hideout's wall buy and ammo box stand: placed by the user in game (2026-11-16), baked in
            WallX = config.Bind(StationsSection, "Wall buy X", 7.863848f, new ConfigDescription(
                "The chalk wall buy across the hideout (metres).", new AcceptableValueRange<float>(-25f, 25f)));
            WallY = config.Bind(StationsSection, "Wall buy height", 1.430282f, new ConfigDescription(
                "The chalk wall buy up and down (metres).", new AcceptableValueRange<float>(-3f, 6f)));
            WallZ = config.Bind(StationsSection, "Wall buy Z", 6.47f, new ConfigDescription(
                "The chalk wall buy along the hideout (metres).", new AcceptableValueRange<float>(-25f, 25f)));
            WallYaw = config.Bind(StationsSection, "Wall buy facing", 179.1f, new ConfigDescription(
                "Which way the chalk faces, out of the wall (degrees).", new AcceptableValueRange<float>(0f, 360f)));
            BoxX = config.Bind(StationsSection, "Ammo box X", 7.927934f, new ConfigDescription(
                "The ammo box across the hideout (metres).", new AcceptableValueRange<float>(-25f, 25f)));
            BoxY = config.Bind(StationsSection, "Ammo box height", -0.008028165f, new ConfigDescription(
                "The ammo box up and down (metres).", new AcceptableValueRange<float>(-3f, 6f)));
            BoxZ = config.Bind(StationsSection, "Ammo box Z", 6.39f, new ConfigDescription(
                "The ammo box along the hideout (metres).", new AcceptableValueRange<float>(-25f, 25f)));
            BoxYaw = config.Bind(StationsSection, "Ammo box facing", 182.4f, new ConfigDescription(
                "Which way the ammo box faces (degrees).", new AcceptableValueRange<float>(0f, 360f)));
            FlipChalk = config.Bind(StationsSection, "Flip chalk (muzzle the other way)", false,
                "Mirrors the chalk outline and turns the gun hung in it to match, for the other wall.");
            _wallHere = config.Bind(StationsSection, "Put the wall buy where I am looking", false,
                "Puts the chalk outline on the wall you're looking at (within 6 m), facing out of it. In the hideout.");
            _boxHere = config.Bind(StationsSection, "Put the ammo box where I am looking", false,
                "Puts the ammo box on the floor you're looking at (within 6 m), facing you. In the hideout.");
            _logStations = config.Bind(StationsSection, "Log the station positions", false,
                "Writes both stations' places to the BepInEx log (to bake in as defaults).");
            _wallHere.SettingChanged += (_, __) => { if (_wallHere.Value) { _wallHere.Value = false; PlaceWall(); } };
            _boxHere.SettingChanged += (_, __) => { if (_boxHere.Value) { _boxHere.Value = false; PlaceBox(); } };
            _logStations.SettingChanged += (_, __) => { if (_logStations.Value) { _logStations.Value = false; LogStations(); } };

            _giveGun = config.Bind(DebugSection, "Give a Mark II (hideout)", false,
                "Puts a loaded Ray Gun Mark II and three spare clips in your stash. Use it in the hideout.");
            _giveClip = config.Bind(DebugSection, "Give a battery clip (hideout)", false,
                "Puts one full Mark II battery clip in your stash. Use it in the hideout.");
            _giveGun.SettingChanged += (_, __) => Press(_giveGun, "weapon");
            _giveClip.SettingChanged += (_, __) => Press(_giveClip, "clip");
            _giveZombies = config.Bind(DebugSection, "Give in a zombies match", false,
                "Hands you the Ray Gun Mark II in a Kennerz Zombies match the way the mystery box does (a full mag, drawn). Only in a match.");
            _giveZombies.SettingChanged += (_, __) =>
            {
                if (!_giveZombies.Value)
                    return;
                _giveZombies.Value = false;
                RaygunMk2ZombiesGive.Give();
            };
            RaygunMk2Fit.Bind(config);
        }

        /// <summary>A tick box used as a button: it sends the give, then unticks itself.</summary>
        private static void Press(ConfigEntry<bool> entry, string what)
        {
            if (!entry.Value)
                return;
            entry.Value = false;
            RaygunMk2DebugGive.Send(what);
        }

        public static (Vector3, Quaternion) WallAt() => (new Vector3(WallX.Value, WallY.Value, WallZ.Value), Quaternion.Euler(0f, WallYaw.Value, 0f));

        public static (Vector3, Quaternion) BoxAt() => (new Vector3(BoxX.Value, BoxY.Value, BoxZ.Value), Quaternion.Euler(0f, BoxYaw.Value, 0f));

        /// <summary>What the camera looks at within 6 m (the stations' own colliders ignored).</summary>
        private static bool Look(out RaycastHit hit)
        {
            hit = default;
            Camera cam = Camera.main;
            if (cam == null)
                return false;
            foreach (RaycastHit h in Physics.RaycastAll(cam.transform.position, cam.transform.forward, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform root = h.collider.transform.root;
                if (root.name == RaygunMk2Stations.WallName || root.name == RaygunMk2Stations.BoxName || h.collider.GetComponentInParent<EFT.Player>() != null)
                    continue;
                if (hit.collider == null || h.distance < hit.distance)
                    hit = h;
            }
            return hit.collider != null;
        }

        private static void PlaceWall()
        {
            if (!Look(out RaycastHit hit))
                return;
            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.01f)
                return;
            Vector3 at = hit.point + normal.normalized * 0.01f;
            WallX.Value = at.x;
            WallY.Value = at.y;
            WallZ.Value = at.z;
            WallYaw.Value = Quaternion.LookRotation(normal.normalized).eulerAngles.y;
            LogStations();
        }

        private static void PlaceBox()
        {
            Camera cam = Camera.main;
            if (cam == null || !Look(out RaycastHit hit))
                return;
            BoxX.Value = hit.point.x;
            BoxY.Value = hit.point.y;
            BoxZ.Value = hit.point.z;
            Vector3 back = -cam.transform.forward;
            back.y = 0f;
            BoxYaw.Value = back.sqrMagnitude > 0.01f ? Quaternion.LookRotation(back.normalized).eulerAngles.y : 0f;
            LogStations();
        }

        private static void LogStations() => RaygunMk2Plugin.Log.LogInfo(
            $"[stations] wall {WallX.Value:F4}, {WallY.Value:F4}, {WallZ.Value:F4} yaw {WallYaw.Value:F2}; " +
            $"box {BoxX.Value:F4}, {BoxY.Value:F4}, {BoxZ.Value:F4} yaw {BoxYaw.Value:F2}");

        private static ConfigDescription Volume(string description) => new ConfigDescription(description, new AcceptableValueRange<float>(0f, 1f));
    }

    /// <summary>
    /// Each shot nudges the shooter back a little (F12 "Push back per shot"), spread over a tenth of a second so it
    /// reads as a shove, not a jump. Only the local player's own Mark II; the character controller's own collision
    /// keeps it out of walls.
    /// </summary>
    internal static class RaygunMk2PushBack
    {
        private const float Spread = 0.1f;

        public static void Enable(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.InitiateShot)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(RaygunMk2PushBack), nameof(ShotPostfix))));
        }

        private static void ShotPostfix(Player.FirearmController __instance)
        {
            try
            {
                float distance = RaygunMk2Settings.PushBack.Value;
                if (distance <= 0f || !RaygunMk2Ids.Is(__instance.Item))
                    return;
                Player me = LocalPlayer.Get();
                if (me == null || !ReferenceEquals(me.HandsController, __instance))
                    return;
                Vector3 back = -me.Transform.forward;
                back.y = 0f;
                if (back.sqrMagnitude < 1e-4f)
                    return;
                RaygunMk2Plugin.Runner.StartCoroutine(Shove(me, back.normalized * distance));
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II push back skipped: {e.Message}");
            }
        }

        private static IEnumerator Shove(Player player, Vector3 total)
        {
            float done = 0f;
            while (done < Spread && player != null && player.MovementContext?.CharacterController != null)
            {
                float dt = Mathf.Min(Time.deltaTime, Spread - done);
                done += dt;
                player.MovementContext.CharacterController.Move(total * (dt / Spread), dt);
                yield return null;
            }
        }
    }
}
