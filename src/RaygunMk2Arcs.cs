using System.Collections;
using System.Linq;
using EFT;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// The original's green lightning at the back of the gun on each of your shots (user, 2026-11-16, drawn on a
    /// screenshot): a jagged arc jumping between the two low side rods behind the sight, a couple of branches off it,
    /// and short sparks off the tips of the two tall posts, all re-jagged every few hundredths of a second for a moment
    /// (red on a Pack-a-Punched gun). Placed through the gun root's own transform each frame, in world space, so they
    /// sit on the gun aimed in or not and ride it through its kick. Your own gun only.
    /// </summary>
    internal static class RaygunMk2Arcs
    {
        private const float Life = 0.2f, Reshape = 0.03f;

        // Points on the original model (its own units: x forward, y left, z up), measured off its mesh (user, 2026-11-16, drawn on
        // a screenshot): the rear ends of the two low rods behind the sight, and the curled tips of the two tall posts
        private static readonly Vector3 CrossLeft = new Vector3(-13.05f, 1.45f, 11.05f), CrossRight = new Vector3(-13.05f, -1.45f, 11.05f);
        private static readonly Vector3 PostLeft = new Vector3(-10f, 1.3f, 14.45f), PostRight = new Vector3(-10f, -1.3f, 14.45f);

        private static Object _owner;

        // The original's model space (right-handed, Z up, X forward, Y left, its own units) to the gun root's local space
        // (j_gun, the original's tag_weapon): (-y, z, x), centimetres (checked in the editor against the bones and the
        // mesh, Mk2ArcProbe: 0.0254 put every point 2.5x too far out, off the gun), and j_gun's turn
        private const float Unit = 0.01f;
        private static readonly Quaternion IntoGun = Quaternion.Inverse(Quaternion.LookRotation(new Vector3(0f, -1f, 0f), new Vector3(-1f, 0f, 0f)));
        private static Transform _gun;

        /// <summary>
        /// A point on the model where it is in the world right now: through the gun root's own transform, so whatever
        /// the hands model does to its scale (squashed, not evenly: the bottom arc was off the gun, user 2026-11-16) the
        /// point stays on the model.
        /// </summary>
        private static Vector3 World(Vector3 m) => _frameOk
            ? _gun.TransformPoint(IntoGun * (new Vector3(-m.y, m.z, m.x) * Unit))
            : FromBones(m);

        // The muzzle and the rear fin and sight flap (model units), to check the mapping against, and the fallback frame
        private static readonly Vector3 FinAt = new Vector3(-4.63f, 0f, 6.53f), FlashAt = new Vector3(40.55f, 0f, 7.61f), FlapAt = new Vector3(-6.73f, 0f, 11.77f);
        private static Transform _fin, _flash, _flap;
        private static bool _frameOk;

        /// <summary>The older way: a frame built from three bones' positions (one scale for every axis).</summary>
        private static Vector3 FromBones(Vector3 m)
        {
            Vector3 fin = _fin.position, flash = _flash.position;
            float perUnit = Vector3.Distance(fin, flash) / Vector3.Distance(FinAt, FlashAt);
            Vector3 forward = (flash - fin).normalized;
            Vector3 up = Vector3.ProjectOnPlane(_flap.position - fin, forward).normalized;
            Vector3 left = Vector3.Cross(forward, up);
            Vector3 d = m - FinAt;
            return fin + (forward * d.x + left * d.y + up * d.z) * perUnit;
        }

        /// <summary>World metres per model unit along the gun, for how far the arcs stray.</summary>
        private static float MetresPerUnit => Vector3.Distance(World(Vector3.zero), World(Vector3.right));

        private static bool upgradedCore;

        public static void Shot(Player.FirearmController hands, bool upgraded)
        {
            upgradedCore = upgraded;
            try
            {
                if (!Bones(hands))
                    return;
                GameObject prefab = RaygunMk2Bolts.Prefab(upgraded ? "mk2_arc_up" : "mk2_arc");
                if (prefab == null)
                    return;
                int layer = (_gun != null ? _gun : _fin).gameObject.layer;
                // The low arc and its branches are seen against the gun's own body, which the game draws with its own
                // squashed depth: drawn over everything (mk2_arc_top), or they never show (user, 2026-11-16: "works on
                // the top but not the bottom"). An older bundle without it falls back to the plain arc.
                GameObject low = RaygunMk2Bolts.Prefab(upgraded ? "mk2_arc_top_up" : "mk2_arc_top") ?? prefab;
                // Across the back, low rod to low rod: the only one lit (seven lights a hand's width from the eye
                // flooded the view white: user, 2026-11-16)
                // Twice the top sparks' thickness and a little longer: it sits against the dark body, where the thin one
                // could only just be made out (user, 2026-11-16)
                Arc(low, layer, () => (World(CrossLeft), World(CrossRight)), 12, 0.05f, 5.5f, Life * 1.4f, lit: true, hot: true);
                // Branches off it, up or down from somewhere along it
                for (int i = 0; i < 3; i++)
                {
                    float along = Random.Range(0.3f, 0.7f);
                    Vector3 off = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.6f, 0.6f), Random.Range(-1.4f, 0.8f));
                    Arc(low, layer, () =>
                    {
                        Vector3 from = Vector3.Lerp(CrossLeft, CrossRight, along);
                        return (World(from), World(from + off));
                    }, 6, 0.04f, 3f, Life * Random.Range(0.8f, 1.2f), hot: true);
                }
                // Little sparks curling off the posts' tips, outwards (two on each)
                foreach (Vector3 post in new[] { PostLeft, PostRight, PostLeft, PostRight })
                {
                    Vector3 p = post;
                    float outward = Mathf.Sign(p.y);
                    Arc(prefab, layer, () => (World(p), World(p + new Vector3(Random.Range(-0.6f, 0.6f), outward * Random.Range(0.4f, 1.4f), Random.Range(-0.6f, 1f)))),
                        5, 0.03f, 1.4f, Life * Random.Range(0.5f, 0.8f));
                }
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II arcs skipped: {e.Message}");
            }
        }

        private static bool Bones(Player.FirearmController hands)
        {
            if (hands?._weaponPrefab == null)
                return false;
            if (_owner != hands._weaponPrefab || (_gun == null && _fin == null))
            {
                _owner = hands._weaponPrefab;
                Transform[] all = hands._weaponPrefab.GetComponentsInChildren<Transform>(true);
                // The gun's own root: the j_gun the muzzle hangs from (the arms rig has a j_gun of its own, found first,
                // which put the arcs and their light by the camera: user, 2026-11-16 "just really bright")
                Transform flash = all.FirstOrDefault(t => t.name == "tag_flash");
                _gun = null;
                for (Transform t = flash; t != null && _gun == null; t = t.parent)
                    if (t.name == "j_gun")
                        _gun = t;
                _flash = flash;
                _fin = all.FirstOrDefault(t => t.name == "tag_fin_animate");
                _flap = all.FirstOrDefault(t => t.name == "tag_flap_rear_animate");
                // The mapping checked on the muzzle: where it says tag_flash is against where it is
                _frameOk = false;
                if (_gun != null && _flash != null)
                {
                    _frameOk = true;
                    Vector3 said = World(FlashAt);
                    float off = Vector3.Distance(said, _flash.position);
                    float size = Vector3.Distance(World(Vector3.zero), said);
                    _frameOk = off < size * 0.03f;
                    RaygunMk2Plugin.Log.LogInfo($"Mark II arcs: gun root mapping off the muzzle by {off * 100f:0.0} cm (gun {size * 100f:0} cm): " +
                                                (_frameOk ? "using it" : "using the bone frame"));
                }
            }
            return _frameOk || (_fin != null && _flash != null && _flap != null);
        }

        /// <summary>One arc, its two ends worked out afresh each time it's re-jagged (stray in model units).</summary>
        private static void Arc(GameObject prefab, int layer, System.Func<(Vector3 a, Vector3 b)> ends, int points, float strayCm, float width, float life,
            bool lit = false, bool hot = false)
        {
            GameObject arc = Object.Instantiate(prefab);
            foreach (Light l in arc.GetComponentsInChildren<Light>(true))
            {
                if (!lit)
                    Object.Destroy(l.gameObject);
                else
                {
                    l.intensity *= 0.35f;
                    l.range = Mathf.Min(l.range, 0.35f);
                }
            }
            foreach (Transform t in arc.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
            LineRenderer line = arc.GetComponent<LineRenderer>();
            if (line != null)
            {
                line.useWorldSpace = true;
                line.positionCount = points;
                line.widthMultiplier = width;
                // A near-white hot core: green on the gun's own green-lit body hardly showed (the top ones are seen
                // against the room; user, 2026-11-16)
                if (hot)
                {
                    var g = new Gradient();
                    // Softer than it was: the near-white core at full strength glared into streaks (user, 2026-11-16)
                    Color core = upgradedCore ? new Color(0.75f, 0.5f, 0.5f) : new Color(0.5f, 0.75f, 0.5f);
                    g.SetKeys(new[] { new GradientColorKey(core, 0f), new GradientColorKey(core, 1f) },
                        new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.8f, 1f) });
                    line.colorGradient = g;
                }
            }
            arc.SetActive(true);
            RaygunMk2Plugin.Runner.StartCoroutine(Crackle(arc, line, ends, strayCm, life));
        }

        private static IEnumerator Crackle(GameObject arc, LineRenderer line, System.Func<(Vector3 a, Vector3 b)> ends, float strayCm, float life)
        {
            float end = Time.time + life;
            while (arc != null && Time.time < end && (_gun != null || _fin != null))
            {
                if (line != null)
                {
                    var (a, b) = ends();
                    Shape(line, a, b, strayCm * 10f * MetresPerUnit);
                    Light light = arc.GetComponentInChildren<Light>();
                    if (light != null)
                        light.transform.position = (a + b) * 0.5f;
                }
                yield return new WaitForSeconds(Reshape);
            }
            if (arc != null)
                Object.Destroy(arc);
        }

        /// <summary>A fresh jagged path between two points in the world (widest in the middle).</summary>
        private static void Shape(LineRenderer line, Vector3 a, Vector3 b, float stray)
        {
            int n = line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                if (i > 0 && i < n - 1)
                    p += Random.insideUnitSphere * stray * Mathf.Sin(t * Mathf.PI);
                line.SetPosition(i, p);
            }
        }
    }
}
