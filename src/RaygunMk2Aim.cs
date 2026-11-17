using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// The Mark II's bolts go where the player looks, all three of a burst around the same line (a small random spread
    /// each, F12 "Spread"). EFT fires along the
    /// muzzle's own axis, and the original's shot kick tips the gun up a little more with each shot, so a burst climbed
    /// in a line (user, 2026-11-15). The first shot of a burst takes the middle of the screen; the rest of the burst
    /// keeps that direction. Just before each shot the muzzle is turned onto the aim point (the Ray Gun's way).
    /// </summary>
    internal static class RaygunMk2Aim
    {
        private const float MaxRange = 1500f;
        // Hits closer than this are the player's own gun, arms or body
        private const float MinRange = 1.2f;
        // Shots closer together than this are one burst (0.12 s apart in a burst)
        private const float BurstWindow = 0.2f;

        private static FieldInfo _player;
        private static float _lastShot = -10f;
        private static Vector3 _burstDir = Vector3.forward;

        public static void Enable(Harmony harmony)
        {
            _player = AccessTools.Field(typeof(Player.FirearmController), "_player");
            harmony.Patch(AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.Shot)),
                new HarmonyMethod(typeof(RaygunMk2Aim).GetMethod(nameof(ShotPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }

        private static void ShotPrefix(Player.FirearmController __instance, Weapon weapon)
        {
            try
            {
                if (!RaygunMk2Ids.Is(weapon) || !(_player?.GetValue(__instance) is Player player) || !player.IsYourPlayer)
                    return;
                BifacialTransform fireport = __instance.CurrentFireport;
                Camera cam = Camera.main;
                if (fireport?.Original == null || cam == null)
                    return;
                // The burst's direction, in the camera's own axes, so turning mid-burst still turns the shots with you
                if (Time.time - _lastShot > BurstWindow)
                    _burstDir = Vector3.forward;
                _lastShot = Time.time;
                Vector3 from = cam.transform.position;
                // A little random spread around the burst's line, each shot its own (user, 2026-11-15: random but tight)
                Vector2 jitter = Random.insideUnitCircle * Mathf.Tan(RaygunMk2Settings.Spread.Value * Mathf.Deg2Rad);
                Vector3 forward = cam.transform.TransformDirection((_burstDir + new Vector3(jitter.x, jitter.y, 0f)).normalized);
                AimAt(__instance, player, fireport, AimPoint(from, forward));
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II aim skipped: {e.Message}");
            }
        }

        /// <summary>
        /// Turns the fireport's muzzle so the shot EFT builds from it goes through the target, through EFT's own
        /// AdjustShotVectors (not a plain rotation, so refined a few times).
        /// </summary>
        private static void AimAt(Player.FirearmController controller, Player player, BifacialTransform fireport, Vector3 target)
        {
            Transform muzzle = fireport.Original;
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = fireport.position;
                Vector3 direction = muzzle.TransformDirection(player.LocalShotDirection);
                if (direction.sqrMagnitude < 1e-6f)
                    return;
                controller.AdjustShotVectors(ref position, ref direction);
                Vector3 want = target - position;
                if (want.sqrMagnitude < 1e-6f)
                    return;
                Quaternion turn = Quaternion.FromToRotation(direction, want);
                if (Quaternion.Angle(turn, Quaternion.identity) < 0.001f)
                    return;
                muzzle.rotation = turn * muzzle.rotation;
            }
        }

        /// <summary>The first solid thing along the view beyond arm's reach, or a point far out.</summary>
        private static Vector3 AimPoint(Vector3 from, Vector3 forward)
        {
            float best = MaxRange;
            foreach (RaycastHit hit in Physics.RaycastAll(from, forward, MaxRange, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= MinRange && hit.distance < best)
                    best = hit.distance;
            }
            return from + forward * best;
        }
    }

    /// <summary>
    /// A Mark II holds exactly a clip's worth: 21, 42 Pack-a-Punched. EFT keeps a round in the chamber on top of the
    /// clip, so a reload with a round still chambered came out at 22 (the "one always left" the user saw). While the
    /// gun is held and not mid-reload, a chambered round plus a full clip is trimmed back by one round from the clip.
    /// </summary>
    internal static class RaygunMk2Rounds
    {
        public const int Clip = 21, UpgradedClip = 42;

        public static void Check(Player.FirearmController fc)
        {
            try
            {
                if (!(fc?.Item is Weapon gun) || !RaygunMk2Ids.Is(gun) || Reloading(fc))
                    return;
                Magazine mag = gun.GetCurrentMagazine();
                if (mag == null)
                    return;
                int chambered = gun.Chambers?.Count(c => c.ContainedItem != null) ?? 0;
                int most = RaygunMk2PaP.IsInfused(gun) ? UpgradedClip : Clip;
                int extra = mag.Count + chambered - most;
                if (extra <= 0)
                    return;
                Ammo stack = mag.Cartridges.Items.OfType<Ammo>().LastOrDefault();
                if (stack == null)
                    return;
                if (stack.StackObjectsCount > extra)
                    stack.StackObjectsCount -= extra;
                else
                    mag.Cartridges.Remove(stack, false);
                RaygunMk2Plugin.Log.LogInfo($"Mark II: {mag.Count + chambered + extra} rounds after a reload, trimmed to {most}");
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II round count check skipped: {e.Message}");
            }
        }

        private static bool Reloading(Player.FirearmController fc)
        {
            string op = fc.CurrentOperation?.GetType().Name ?? "";
            return op.IndexOf("Reload", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
