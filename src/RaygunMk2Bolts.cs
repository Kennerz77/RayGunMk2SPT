using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// What a Mark II bolt looks and sounds like. The charges are explosive rounds on the server (a small burst of
    /// energy, no shrapnel, "raygunmk2_bolt" as the explosion's name, which no effect of the game's has): the game
    /// flies the bolt, bursts it on impact and deals the blast; this draws it. Each bolt gets a glowing ball at the
    /// front and a sharp green line behind it (mk2_fx.bundle, beside the plugin), and the impact a green burst with
    /// one of the original's three impact sounds. Red from a Pack-a-Punched gun.
    /// </summary>
    internal class RaygunMk2Bolts : MonoBehaviour
    {
        public const string ExplosionName = "raygunmk2_bolt";
        private const string BundleFile = "mk2_fx.bundle";
        private const int ImpactSounds = 3;

        private static AssetBundle _bundle;
        private static bool _bundleTried;
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        // Bolts in flight and the effect riding each
        private static readonly List<(Shot shot, GameObject fx)> Live = new List<(Shot, GameObject)>();

        // An impact nearer your eyes than this is drawn smaller and dimmer (metres)
        private const float CloseRange = 3f;

        // Whether the hit being handled came from a Pack-a-Punched gun (set by the hit handler, read by the effect)
        private static bool _upgradedHit;

        private static Harmony _harmony;
        private static readonly HashSet<MethodInfo> HookedWorlds = new HashSet<MethodInfo>();
        private static readonly HashSet<System.Type> CheckedWorlds = new HashSet<System.Type>();

        private static string Dir => Path.GetDirectoryName(typeof(RaygunMk2Bolts).Assembly.Location);

        public static void Enable(Harmony harmony)
        {
            _harmony = harmony;
            harmony.Patch(AccessTools.Method(typeof(BallisticsCalculator), nameof(BallisticsCalculator.CreateShot)),
                postfix: new HarmonyMethod(Method(nameof(CreateShotPostfix))));
            harmony.Patch(AccessTools.Method(typeof(Systems.Effects.Effects), nameof(Systems.Effects.Effects.EmitGrenade)),
                new HarmonyMethod(Method(nameof(EmitGrenadePrefix))));
            foreach (System.Type world in new[] { typeof(ClientGameWorld), typeof(GameWorld) })
            {
                MethodInfo hit = AccessTools.DeclaredMethod(world, "ShotDelegate");
                if (hit != null && HookedWorlds.Add(hit))
                    harmony.Patch(hit, new HarmonyMethod(Method(nameof(ShotDelegatePrefix))));
            }
        }

        private static MethodInfo Method(string name) => typeof(RaygunMk2Bolts).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

        // The game world in use may be a mod's own (Fika's client and host worlds override ShotDelegate without calling
        // EFT's): its own ShotDelegate is hooked the first time a Mark II fires (before any bolt lands)
        private static void HookWorldInUse()
        {
            object world = Comfort.Common.Singleton<GameWorld>.Instance;
            if (world == null || _harmony == null || !CheckedWorlds.Add(world.GetType()))
                return;
            for (System.Type t = world.GetType(); t != null && typeof(GameWorld).IsAssignableFrom(t); t = t.BaseType)
            {
                MethodInfo hit = AccessTools.DeclaredMethod(t, "ShotDelegate");
                if (hit == null || hit.IsAbstract || !HookedWorlds.Add(hit))
                    continue;
                try
                {
                    _harmony.Patch(hit, new HarmonyMethod(Method(nameof(ShotDelegatePrefix))));
                    RaygunMk2Plugin.Log.LogInfo($"Mark II: impacts hooked in {t.Name}");
                }
                catch (System.Exception e)
                {
                    RaygunMk2Plugin.Log.LogWarning($"Mark II: couldn't hook {t.Name}.ShotDelegate ({e.Message})");
                }
            }
        }

        internal static GameObject Prefab(string name)
        {
            if (Prefabs.TryGetValue(name, out GameObject cached) && cached != null)
                return cached;
            if (_bundle == null && !_bundleTried)
            {
                _bundleTried = true;
                string path = Path.Combine(Dir, BundleFile);
                _bundle = File.Exists(path) ? AssetBundle.LoadFromFile(path) : null;
                if (_bundle == null)
                    RaygunMk2Plugin.Log.LogWarning($"Mark II effects bundle missing or unreadable: {path}");
            }
            GameObject prefab = _bundle != null ? _bundle.LoadAsset<GameObject>(name) : null;
            Prefabs[name] = prefab;
            return prefab;
        }

        // ---------- The bolt in flight ----------

        private static void CreateShotPostfix(Shot __result, Ammo ammo, Item weapon, int fragmentIndex)
        {
            try
            {
                if (__result == null || fragmentIndex != 0 || !RaygunMk2Ids.IsCharge(ammo) || !RaygunMk2Ids.Is(weapon))
                    return;
                HookWorldInUse();
                RaygunMk2ShotSound.Play(__result.CurrentPosition, RaygunMk2PaP.IsInfused(weapon));
                GameObject prefab = Prefab(RaygunMk2PaP.IsInfused(weapon) ? "mk2_bolt_up" : "mk2_bolt");
                if (prefab == null)
                    return;
                GameObject fx = Instantiate(prefab, __result.CurrentPosition, Quaternion.identity);
                fx.SetActive(true);
                Live.Add((__result, fx));
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II bolt effect skipped: {e.Message}");
            }
        }

        private void LateUpdate()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var (shot, fx) = Live[i];
                if (fx == null)
                {
                    Live.RemoveAt(i);
                    continue;
                }
                if (shot == null || shot.IsShotFinished)
                {
                    Land(fx);
                    Live.RemoveAt(i);
                    continue;
                }
                fx.transform.position = shot.CurrentPosition;
            }
        }

        /// <summary>The bolt has hit: its ball goes out, the line, smoke and sparks it left fade where they hang.</summary>
        private static void Land(GameObject fx)
        {
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                else
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            foreach (Light light in fx.GetComponentsInChildren<Light>())
                light.enabled = false;
            foreach (TrailRenderer line in fx.GetComponentsInChildren<TrailRenderer>())
                line.emitting = false;
            Destroy(fx, 1.5f);
        }

        // ---------- The impact ----------

        private static void ShotDelegatePrefix(Shot shotResult)
        {
            try
            {
                _upgradedHit = shotResult?.Weapon != null && RaygunMk2Ids.Is(shotResult.Weapon) && RaygunMk2PaP.IsInfused(shotResult.Weapon);
            }
            catch (System.Exception)
            {
                _upgradedHit = false;
            }
        }

        /// <summary>Our explosion's name: the green burst and one of the original's impact sounds instead of the game's.</summary>
        private static bool EmitGrenadePrefix(string ename, Vector3 position, Vector3 normal)
        {
            if (ename != ExplosionName)
                return true;
            try
            {
                GameObject prefab = Prefab($"mk2_impact{(_upgradedHit ? "_up" : "")}_{Random.Range(0, ImpactSounds)}");
                if (prefab != null)
                {
                    GameObject fx = Instantiate(prefab, position + normal * 0.05f, Quaternion.LookRotation(normal == Vector3.zero ? Vector3.up : normal));
                    // Right in your face it's smaller and its flash dimmer, down to a third at point-blank
                    Camera cam = Camera.main;
                    float d = cam != null ? Vector3.Distance(cam.transform.position, position) : 99f;
                    if (d < CloseRange)
                    {
                        float k = Mathf.Lerp(0.35f, 1f, d / CloseRange);
                        fx.transform.localScale *= k;
                        foreach (Light l in fx.GetComponentsInChildren<Light>(true))
                        {
                            l.range *= k;
                            l.intensity *= k * k;
                        }
                    }
                    fx.SetActive(true);
                    fx.AddComponent<Flash>();
                    foreach (AudioSource audio in fx.GetComponentsInChildren<AudioSource>())
                        audio.volume *= RaygunMk2Settings.ImpactVolume.Value;
                    Destroy(fx, 3f);
                }
                RaygunMk2Pool.Leave(position, normal, _upgradedHit);
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II impact effect skipped: {e.Message}");
            }
            return false;
        }

        /// <summary>The impact's light: a flash that dies away.</summary>
        private class Flash : MonoBehaviour
        {
            private Light _light;
            private float _start, _intensity;

            private void Start()
            {
                _light = GetComponentInChildren<Light>();
                _start = Time.time;
                _intensity = _light != null ? _light.intensity : 0f;
            }

            private void Update()
            {
                if (_light == null)
                    return;
                float t = (Time.time - _start) / 0.4f;
                _light.intensity = _intensity * Mathf.Clamp01(1f - t);
                if (t >= 1f)
                    _light.enabled = false;
            }
        }
    }

    /// <summary>
    /// The original's pause between bursts (its burstFireDelay, 0.2 s): a new trigger pull counts only once that long
    /// has passed since the last shot of the burst. The shots inside a burst are the game's own (0.12 s apart).
    /// </summary>
    internal static class RaygunMk2BurstGap
    {
        private static float _lastShot = -10f;

        public static void Enable(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.InitiateShot)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(RaygunMk2BurstGap), nameof(ShotPostfix))));
            harmony.Patch(AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.SetTriggerPressed)),
                new HarmonyMethod(AccessTools.Method(typeof(RaygunMk2BurstGap), nameof(TriggerPrefix))));
        }

        private static bool Mine(Player.FirearmController fc)
        {
            Player me = LocalPlayer.Get();
            return me != null && ReferenceEquals(me.HandsController, fc) && RaygunMk2Ids.Is(fc.Item);
        }

        private static void ShotPostfix(Player.FirearmController __instance)
        {
            if (!Mine(__instance))
                return;
            _lastShot = Time.time;
            // The green lightning off the back of the gun
            RaygunMk2Arcs.Shot(__instance, RaygunMk2PaP.IsInfused(__instance.Item));
        }

        private static bool TriggerPrefix(Player.FirearmController __instance, bool pressed)
        {
            // Already held (mid-burst): never interrupted
            if (!pressed || __instance.IsTriggerPressed || !Mine(__instance))
                return true;
            // Mid-burst the trigger is already down; a new pull waits out the gap after the burst's last shot
            return Time.time - _lastShot >= RaygunMk2Settings.BurstGap.Value;
        }
    }
}
