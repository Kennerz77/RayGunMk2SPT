using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// The local player, cheaply: the game world's main player in a raid (and the hideout), else the hideout player
    /// found by a scene search at most every few seconds.
    /// </summary>
    internal static class LocalPlayer
    {
        private static Player _cached;
        private static float _nextSearch;

        public static Player Get()
        {
            if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance.MainPlayer is Player main && main != null)
                return main;
            if (_cached != null)
                return _cached;
            if (Time.unscaledTime < _nextSearch)
                return null;
            _nextSearch = Time.unscaledTime + 3f;
            return _cached = Object.FindObjectsOfType<Player>().FirstOrDefault(p => p.IsYourPlayer);
        }
    }

    /// <summary>
    /// Every way the game builds an item model (icons, the inspect window, loot, the hands): a Mark II model gets its
    /// <see cref="RaygunMk2View"/>; a charge's model (the copper slug it's cloned from: in the chamber, at the shell
    /// port as the gun "ejects" it) is hidden. Also our own icon hash, so the pictures in icons/ are cached on their own.
    /// </summary>
    internal static class RaygunMk2Models
    {
        public static void Enable(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(IconsHash), nameof(IconsHash.GetItemHash)),
                postfix: new HarmonyMethod(Method(nameof(IconHashPostfix))));
            harmony.Patch(AccessTools.FirstMethod(typeof(ObjectsFactory), m =>
                    m.Name == nameof(ObjectsFactory.CreateItemAsync) && m.ReturnType == typeof(Task<GameObject>)
                    && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(Item)),
                postfix: new HarmonyMethod(Method(nameof(CreateAsyncPostfix))));
            foreach (MethodInfo create in AccessTools.GetDeclaredMethods(typeof(ObjectsFactory)).Where(m =>
                         (m.Name == nameof(ObjectsFactory.CreateItem) || m.Name == nameof(ObjectsFactory.CreateCleanLootPrefab)
                          || m.Name == nameof(ObjectsFactory.CreateLootPrefab) || m.Name == nameof(ObjectsFactory.CreateItemUsablePrefab))
                         && m.ReturnType == typeof(GameObject)
                         && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(Item)))
                harmony.Patch(create, postfix: new HarmonyMethod(Method(nameof(CreatePostfix))));
        }

        private static MethodInfo Method(string name) => typeof(RaygunMk2Models).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

        private static void IconHashPostfix(Item item, ref int __result)
        {
            if (RaygunMk2Ids.Is(item))
                __result = RaygunMk2Icons.Salted((__result * 31 + (RaygunMk2PaP.IsInfused(item) ? (RaygunMk2PaP.IsOrigins(item) ? 2 : 1) : 0)) * 2
                                           + (item is Weapon gun && gun.GetCurrentMagazine() == null ? 1 : 0));
            else if (RaygunMk2Ids.IsClip(item))
                __result = RaygunMk2Icons.Salted(__result);
        }

        private static void CreatePostfix(GameObject __result, Item item)
        {
            if (__result != null)
                Dress(__result, item);
        }

        private static void CreateAsyncPostfix(ref Task<GameObject> __result, Item item)
        {
            if (RaygunMk2Ids.Is(item) || RaygunMk2Ids.IsCharge(item))
                __result = Dressed(__result, item);
        }

        private static async Task<GameObject> Dressed(Task<GameObject> task, Item item)
        {
            GameObject go = await task;
            if (go != null)
                Dress(go, item);
            return go;
        }

        private static void Dress(GameObject go, Item item)
        {
            try
            {
                if (RaygunMk2Ids.Is(item))
                {
                    RaygunMk2Tracker.Attach(go);
                    // A pooled model already had its view: redo its look for this gun
                    go.GetComponentInChildren<RaygunMk2View>(true)?.Refresh();
                    RaygunMk2ClipShown.Apply(go, item);
                }
                HiddenCharge.Set(go, RaygunMk2Ids.IsCharge(item));
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II model setup skipped: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Marks a charge's model as hidden. Models are pooled by prefab, and the charge shares the vanilla copper slug's,
    /// so a model built for a real slug afterwards gets its renderers back.
    /// </summary>
    internal class HiddenCharge : MonoBehaviour
    {
        public static void Set(GameObject go, bool hide)
        {
            HiddenCharge marker = go.GetComponent<HiddenCharge>();
            if (hide == (marker != null))
                return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                r.forceRenderingOff = hide;
            if (hide)
                go.AddComponent<HiddenCharge>();
            else
                Destroy(marker);
        }
    }

    /// <summary>
    /// Twice a second: the Mark II in the hands gets its view if the factory hooks missed it (a pooled model, say).
    /// </summary>
    internal class RaygunMk2Tracker : MonoBehaviour
    {
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.1f; // quick enough that a reload never shows 22
            Player player = LocalPlayer.Get();
            RaygunMk2DryClick.Tick(player?.HandsController as Player.FirearmController);
            // A raid seen since a wall buy: the wall offers the gun again on the next hideout visit
            if (player != null && !(player is HideoutPlayer))
                RaygunMk2Stations.RaidSeen();
            if (!(player?.HandsController is Player.FirearmController hands) || !RaygunMk2Ids.Is(hands.Item))
                return;
            RaygunMk2Rounds.Check(hands);
            if (hands._weaponPrefab != null && hands._weaponPrefab.GetComponentInChildren<RaygunMk2View>(true) == null)
            {
                RaygunMk2Plugin.Log.LogInfo("Mark II hands model had no view; attaching it from the firearm controller");
                Attach(hands._weaponPrefab.gameObject);
            }
        }

        /// <summary>A Mark II model just built: the view goes on its animator (the weapon root).</summary>
        public static void Attach(GameObject model)
        {
            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null)
                return;
            // A pooled model the menu froze (RaygunMk2MenuPose) animates again wherever it's used next
            if (RaygunMk2MenuPose.Frozen.Remove(animator))
                animator.enabled = true;
            if (animator.GetComponent<RaygunMk2View>() == null)
                animator.gameObject.AddComponent<RaygunMk2View>();
        }
    }

    /// <summary>
    /// A Mark II model's look: its sound player at the F12 levels (and the original's Pack-a-Punched shot handed to
    /// PaP), PaP's camo by the gun's state, and the power cells green on a plain gun, red on a Pack-a-Punched one
    /// (the red material rides in the model on the switched-off renderer mk2_upgraded_materials).
    /// </summary>
    internal class RaygunMk2View : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<WeaponPrefab, Weapon> WeaponData = AccessTools.FieldRefAccess<WeaponPrefab, Weapon>("_weaponData");

        private const string PowerMaterial = "mk2_body_power";

        /// <summary>Live views, so a Pack-a-Punch change can refresh them at once.</summary>
        public static readonly HashSet<RaygunMk2View> Live = new HashSet<RaygunMk2View>();

        // Every renderer slot that draws the power cells
        private readonly List<(Renderer renderer, int index)> _power = new List<(Renderer, int)>();
        private Material _plain, _upgraded;
        private WeaponSoundPlayer _sounds;
        private SoundBank _plainBody, _plainBodySilenced, _papBody, _papBodySilenced;

        /// <summary>This gun's own copy of a fire bank with every clip the Pack-a-Punched one.</summary>
        private static SoundBank PapCopy(SoundBank bank, AudioClip pap)
        {
            if (bank == null)
                return null;
            SoundBank copy = Instantiate(bank);
            copy.name = bank.name + "_pap";
            foreach (EnvironmentVariety environment in copy.Environments ?? new EnvironmentVariety[0])
                foreach (DistanceVarity distance in environment?.Clips ?? new DistanceVarity[0])
                    if (distance != null)
                        distance.Clips = new[] { pap };
            copy.ClipLength = pap.length;
            return copy;
        }
        private bool? _shownInfused;
        private float _nextCheck;

        public WeaponPrefab Prefab { get; private set; }

        private void Awake()
        {
            Live.Add(this);
            // The bolt's resting pose, taken as the model is made (the idle's pose): the first raise's power switch pulls
            // it back and lets it go
            _bolt = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "tag_bolt_animate");
            if (_bolt != null)
                _boltRest = _bolt.localRotation;
        }

        // The draw: the cells dark until the power switch is thrown (user, 2026-11-16), as in the original's first raise
        private Transform _bolt;
        private Quaternion _boltRest;
        private bool _wasInHands, _drawing, _drawWentOff;
        private float _drawStart, _poweredAt = -10f;

        private bool InMyHands()
        {
            Player me = LocalPlayer.Get();
            return me?.HandsController is Player.FirearmController fc && fc._weaponPrefab != null && fc._weaponPrefab == Prefab;
        }

        /// <summary>
        /// From the moment the gun comes into your hands its cells are dark, until the switch's pull (the bolt back) is
        /// let go; a draw that never moves it (the plain pullout) lights after a moment.
        /// </summary>
        private void TrackDraw()
        {
            bool inHands = InMyHands();
            if (inHands && !_wasInHands)
            {
                _drawing = true;
                _drawWentOff = false;
                _drawStart = Time.time;
            }
            _wasInHands = inHands;
            if (!inHands)
            {
                _drawing = false;
                return;
            }
            if (!_drawing)
                return;
            float pulled = _bolt != null ? Quaternion.Angle(_bolt.localRotation, _boltRest) : 0f;
            if (pulled > 20f)
                _drawWentOff = true;
            if ((_drawWentOff && pulled < 6f) || (!_drawWentOff && Time.time - _drawStart > 0.9f) || Time.time - _drawStart > 4f)
            {
                _drawing = false;
                _poweredAt = Time.time;
            }
        }

        private void OnDestroy() => Live.Remove(this);

        private void Start()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                if (r.name == "mk2_upgraded_materials")
                {
                    _upgraded = mats.FirstOrDefault(m => m != null && m.name.StartsWith(PowerMaterial));
                    continue;
                }
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].name.StartsWith(PowerMaterial) && !mats[i].name.Contains("_up"))
                    {
                        _plain = _plain ?? mats[i];
                        _power.Add((r, i));
                    }
                }
            }
            Prefab = GetComponentInParent<WeaponPrefab>();
            _clipParts = RaygunMk2ClipShown.Parts(gameObject);
            RaygunMk2ClipShown.Show(_clipParts, RaygunMk2ClipShown.Wanted(ShownWeapon(), Prefab));
            WeaponSoundPlayer sounds = Prefab?.GetComponent<WeaponSoundPlayer>();
            AudioClip papShot = sounds?.AdditionalSounds?.FirstOrDefault(e => e?.EventName == "SndMk2PapShot")?.SoundClips?.FirstOrDefault();
            // The Pack-a-Punched fire replaces the gun's own (Pack-a-Punch would only layer a sound over the plain
            // fire, which then still sounded plain: user, 2026-11-15). Built before the volume sliders scale the banks
            _sounds = sounds;
            if (sounds != null && papShot != null)
            {
                _plainBody = sounds.Body;
                _plainBodySilenced = sounds.BodySilenced;
                // The shot player (RaygunMk2ShotSound) plays the shots, the PaP one too: the bank stays silent either way
                RaygunMk2ShotSound.Load(sounds);
            }
            RaygunMk2Sounds.Register(sounds);
            RaygunMk2PaP.KeepOwnShot();
            Refresh();
        }

        /// <summary>The gun this model shows: the container's weapon data, else the local player's held Mark II.</summary>
        public Weapon ShownWeapon()
        {
            Weapon w = Prefab != null ? WeaponData(Prefab) : null;
            if (w != null && RaygunMk2Ids.Is(w))
                return w;
            Player me = LocalPlayer.Get();
            return me?.HandsController is Player.FirearmController fc && fc._weaponPrefab != null && fc._weaponPrefab == Prefab && RaygunMk2Ids.Is(fc.Item)
                ? fc.Item as Weapon : null;
        }

        // The cells' plasma, as the Ray Gun's tube: UV per second along and round, a slow wobble, and the pulse
        private static readonly Vector2 Scroll = new Vector2(0.11f, 0.045f);
        private const float WobbleAmount = 0.06f, WobbleHz = 0.23f, PulseDepth = 0.18f, PulseHz = 1.1f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");
        private readonly MaterialPropertyBlock _plasma = new MaterialPropertyBlock();
        private const float SlideTime = 0.6f;

        /// <summary>Same values as the build's Mk2Importer.PowerGlow x 1.4.</summary>
        private static Color Glow(bool upgraded) => (upgraded ? new Color(1f, 0.12f, 0.08f) : new Color(0.25f, 1f, 0.2f)) * 2.2f * 1.4f;

        /// <summary>Every frame: the cells' glow slides along them and pulses (user, 2026-11-16: the Ray Gun's tube, staying green).</summary>
        private void AnimatePlasma()
        {
            TrackDraw();
            float t = Time.time;
            float pulse = 1f + PulseDepth * Mathf.Sin(t * PulseHz * 2f * Mathf.PI);
            // Powered on, the plasma slides into the cells as it fills them (user, 2026-11-16): the glow eases up over
            // 0.6 s while the cloud rushes in along them and settles to its drift
            float on = _drawing ? 0f : Mathf.Clamp01((Time.time - _poweredAt) / SlideTime);
            float ease = 1f - (1f - on) * (1f - on) * (1f - on);
            _plasma.SetColor(EmissionColor, Glow(_shownInfused == true) * pulse * ease);
            _plasma.SetVector(MainTexST, new Vector4(1f, 1f, Scroll.x * t + (1f - ease) * 1.5f, Scroll.y * t + WobbleAmount * Mathf.Sin(t * WobbleHz * 2f * Mathf.PI)));
            foreach (var (renderer, index) in _power)
                if (renderer != null)
                    renderer.SetPropertyBlock(_plasma, index);
        }

        // The clip's mesh objects: hidden with no magazine fitted (RaygunMk2ClipShown)
        private List<GameObject> _clipParts = new List<GameObject>();
        private float _nextClip;

        private void Update()
        {
            AnimatePlasma();
            if (Time.unscaledTime >= _nextClip)
            {
                _nextClip = Time.unscaledTime + 0.05f;
                RaygunMk2ClipShown.Show(_clipParts, RaygunMk2ClipShown.Wanted(ShownWeapon(), Prefab));
            }
            if (Time.unscaledTime < _nextCheck)
                return;
            _nextCheck = Time.unscaledTime + 0.5f;
            bool infused = RaygunMk2PaP.IsInfused(ShownWeapon());
            if (_shownInfused != infused)
                Refresh();
        }

        /// <summary>The power cells and the camo, by the gun's current state.</summary>
        public void Refresh()
        {
            Weapon weapon = ShownWeapon();
            bool infused = RaygunMk2PaP.IsInfused(weapon);
            _shownInfused = infused;
            if (_sounds != null && _papBody != null)
            {
                _sounds.Body = infused ? _papBody : _plainBody;
                _sounds.BodySilenced = infused ? _papBodySilenced ?? _plainBodySilenced : _plainBodySilenced;
            }
            Material want = infused && _upgraded != null ? _upgraded : _plain;
            if (want != null)
            {
                foreach (var (renderer, index) in _power)
                {
                    if (renderer == null)
                        continue;
                    Material[] mats = renderer.sharedMaterials;
                    if (index < mats.Length && mats[index] != want)
                    {
                        mats[index] = want;
                        renderer.sharedMaterials = mats;
                    }
                }
            }
            if (Prefab != null && weapon != null)
                RaygunMk2PaP.ApplyCamo(Prefab.gameObject, weapon);
        }
    }
}
