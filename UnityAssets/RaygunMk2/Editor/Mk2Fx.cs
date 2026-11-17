using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RaygunMk2.Editor
{
    /// <summary>
    /// The Mark II's bolt and impact effects, built as prefabs for mk2_fx.bundle (loaded by the client from its
    /// plugin folder). The bolt: a hot glowing ball at the front with a sharp, bright green line trailing it, as in the
    /// original. The impact: a quick green burst with sparks and a flash, with one of the original's three impact sounds
    /// (mk2_impact_0..2). Green for the plain gun, red for the Pack-a-Punched one ("_up").
    /// Particles use Unity's legacy particle shaders (proven in EFT by the Ray Gun).
    /// </summary>
    public static class Mk2Fx
    {
        public const string BundleName = "mk2_fx.bundle";
        public const int ImpactSounds = 3;

        public static IEnumerable<string> Prefabs
        {
            get
            {
                foreach (string tag in new[] { "", "_up" })
                {
                    yield return "mk2_bolt" + tag;
                    yield return "mk2_arc" + tag;
                    yield return "mk2_arc_top" + tag;
                    yield return "mk2_pool" + tag;
                    for (int i = 0; i < ImpactSounds; i++)
                        yield return $"mk2_impact{tag}_{i}";
                }
            }
        }

        public static string PrefabPath(string name) => $"{Mk2Paths.Root}/Fx/{name}.prefab";

        private static readonly Color Green = new Color(0.3f, 1f, 0.25f);
        private static readonly Color Red = new Color(1f, 0.18f, 0.12f);

        public static void BuildAll(Dictionary<string, AudioClip> sounds)
        {
            Directory.CreateDirectory($"{Mk2Paths.Root}/Fx");
            Texture2D glow = SaveTexture("mk2_fx_glow", 64, GlowPixel);
            Texture2D puff = SaveTexture("mk2_fx_puff", 128, PuffPixel);
            Texture2D streak = SaveTexture("mk2_fx_streak", 64, StreakPixel);
            Texture2D pool = SaveTexture("mk2_fx_pool", 128, PoolPixel);
            Texture2D scorch = SaveTexture("mk2_fx_scorch", 128, ScorchPixel);
            Mesh quad = SaveQuad();
            foreach (bool up in new[] { false, true })
            {
                Color c = up ? Red : Green;
                string tag = up ? "_up" : "";
                Material glowMat = SaveMaterial($"mk2_fx_glow{tag}", "Legacy Shaders/Particles/Additive", glow, c);
                Material smokeMat = SaveMaterial($"mk2_fx_smoke{tag}", "Legacy Shaders/Particles/Alpha Blended", puff, Color.white);
                Material lineMat = SaveMaterial($"mk2_fx_line{tag}", "Legacy Shaders/Particles/Additive", streak, c);
                BuildBolt("mk2_bolt" + tag, c, glowMat, smokeMat, lineMat);
                BuildArc("mk2_arc" + tag, c, lineMat);
                // The same arc drawn over the gun's body (the low arc and its branches: the gun's own depth hid them)
                Material topMat = SaveMaterial($"mk2_fx_line_top{tag}", "RaygunMk2/ArcOnTop", streak, c);
                BuildArc("mk2_arc_top" + tag, c, topMat);
                Material poolMat = SaveMaterial($"mk2_fx_pool{tag}", "Legacy Shaders/Particles/Additive", pool, c);
                Material scorchMat = SaveMaterial($"mk2_fx_scorch{tag}", "Legacy Shaders/Particles/Alpha Blended", scorch, Color.white);
                BuildPool("mk2_pool" + tag, c, quad, poolMat, scorchMat, smokeMat);
                for (int i = 0; i < ImpactSounds; i++)
                {
                    sounds.TryGetValue($"mk2_impact_0{i}", out AudioClip boom);
                    if (boom == null)
                        Debug.LogWarning($"[RaygunMk2] No impact sound mk2_impact_0{i}");
                    BuildImpact($"mk2_impact{tag}_{i}", c, glowMat, smokeMat, boom);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[RaygunMk2] FX prefabs built");
        }

        // ---------- The bolt ----------

        private static void BuildBolt(string name, Color c, Material glowMat, Material smokeMat, Material lineMat)
        {
            var root = new GameObject(name);
            try
            {
                // The ball at the front: a few bright sprites held on the bolt, a hot white middle in a green glow
                ParticleSystem core = Particles(root, "core", glowMat, local: true);
                var main = core.main;
                main.loop = true;
                main.startLifetime = 0.05f;
                main.startSpeed = 0f;
                // Half the old size and brightness: it bloomed into a white blob over the sight as it left (user, 2026-11-16)
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.07f);
                // Every colour at or under white (user, 2026-11-16: the shots and lightning glared into strong streaks)
                main.startColor = c * 0.8f;
                main.maxParticles = 20;
                var emission = core.emission;
                emission.rateOverTime = 140f;

                ParticleSystem hot = Particles(root, "hot", glowMat, local: true);
                main = hot.main;
                main.loop = true;
                main.startLifetime = 0.05f;
                main.startSpeed = 0f;
                main.startSize = 0.025f;
                main.startColor = Color.Lerp(c, Color.white, 0.35f) * 0.7f;
                main.maxParticles = 10;
                emission = hot.emission;
                emission.rateOverTime = 120f;

                // The trail, as the Ray Gun's (without its rings): a thin, see-through green smoke left hanging in the
                // world all the way back to the gun, swelling and fading, with green sparks inside it
                ParticleSystem trail = Particles(root, "trail", smokeMat, local: false);
                main = trail.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.025f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(c.r, c.g, c.b, 0.18f);
                main.maxParticles = 400;
                emission = trail.emission;
                emission.rateOverTime = 0f;
                emission.rateOverDistance = 30f;
                Grow(trail, 1.5f);
                Fade(trail);

                ParticleSystem sparks = Particles(root, "sparks", glowMat, local: false);
                main = sparks.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                main.startColor = c * 0.7f;
                main.maxParticles = 300;
                emission = sparks.emission;
                emission.rateOverTime = 0f;
                emission.rateOverDistance = 25f;
                Fade(sparks);

                // The line: a bright streak chasing the ball
                var line = root.AddComponent<TrailRenderer>();
                line.sharedMaterial = lineMat;
                // A solid, thin, bright line (user, 2026-11-15: too thick and fading)
                line.time = 0.2f;
                line.minVertexDistance = 0.08f;
                line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.012f), new Keyframe(0.9f, 0.012f), new Keyframe(1f, 0.004f));
                line.colorGradient = Solid(c * 0.9f);
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.alignment = LineAlignment.View;
                line.emitting = true;

                Light light = new GameObject("light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Point;
                light.color = c;
                light.range = 1.5f;
                light.intensity = 0.5f;
                light.shadows = LightShadows.None;

                Save(root, name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- The arcs off the back of the gun ----------

        /// <summary>
        /// One crackling arc: a thin, bright jagged line the client bends into a new shape every few hundredths of a
        /// second (RaygunMk2Arcs), sparking off the rear fins as the gun fires, as in the original.
        /// </summary>
        private static void BuildArc(string name, Color c, Material lineMat)
        {
            var root = new GameObject(name);
            try
            {
                var line = root.AddComponent<LineRenderer>();
                line.sharedMaterial = lineMat;
                line.useWorldSpace = false;
                line.positionCount = 8;
                line.widthCurve = AnimationCurve.Constant(0f, 1f, 0.0022f);
                line.colorGradient = Solid(c * 1.1f); // was 3: glare streaks (user, 2026-11-16)
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.alignment = LineAlignment.View;
                Light light = new GameObject("light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Point;
                light.color = c;
                light.range = 0.6f;
                light.intensity = 0.8f;
                light.shadows = LightShadows.None;
                Save(root, name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- The impact ----------

        private static void BuildImpact(string name, Color c, Material glowMat, Material smokeMat, AudioClip boom)
        {
            var root = new GameObject(name);
            try
            {
                // The flash: a bright ball that pops and fades where the bolt hit
                ParticleSystem ball = Particles(root, "ball", glowMat, local: false);
                var main = ball.main;
                main.loop = false;
                main.duration = 0.3f;
                main.startLifetime = 0.3f;
                main.startSpeed = 0f;
                main.startSize = 1.1f;
                main.startColor = c * 1.1f;
                main.maxParticles = 3;
                Burst(ball, 2);
                SizeCurve(ball, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(1f, 1.2f)));
                Fade(ball);

                // Sparks thrown out of the hit
                ParticleSystem sparks = Particles(root, "sparks", glowMat, local: false);
                main = sparks.main;
                main.loop = false;
                main.duration = 0.2f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                main.startColor = c * 1.1f;
                main.gravityModifier = 0.5f;
                main.maxParticles = 30;
                Burst(sparks, 18);
                var shape = sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.05f;
                Fade(sparks);
                Drag(sparks, 3f);

                // A thin green wisp that hangs a moment
                ParticleSystem wisp = Particles(root, "wisp", smokeMat, local: false);
                main = wisp.main;
                main.loop = false;
                main.duration = 0.2f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(c.r, c.g, c.b, 0.4f);
                main.gravityModifier = -0.04f;
                main.maxParticles = 12;
                Burst(wisp, 6);
                shape = wisp.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;
                Grow(wisp, 2f);
                Fade(wisp);

                Light light = new GameObject("light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Point;
                light.color = c;
                light.range = 3f;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;

                if (boom != null)
                {
                    var audio = root.AddComponent<AudioSource>();
                    audio.clip = boom;
                    audio.loop = false;
                    audio.playOnAwake = true;
                    audio.spatialBlend = 1f;
                    audio.volume = 1f;
                    audio.rolloffMode = AudioRolloffMode.Linear;
                    audio.minDistance = 2f;
                    audio.maxDistance = 60f;
                }
                Save(root, name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- Building blocks ----------

        private static Gradient Solid(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private static Gradient Fading(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static ParticleSystem Particles(GameObject root, string name, Material mat, bool local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Grow(ParticleSystem ps, float grow) =>
            SizeCurve(ps, new AnimationCurve(new Keyframe(0f, 1f / grow), new Keyframe(1f, 1f)), grow);

        private static void SizeCurve(ParticleSystem ps, AnimationCurve curve, float scale = 1f)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(scale, curve);
        }

        private static void Fade(ParticleSystem ps)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(g);
        }

        private static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0f;
            limit.dampen = 0f;
            limit.drag = drag;
        }

        private static void Save(GameObject root, string name)
        {
            string path = PrefabPath(name);
            AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        // ---------- The acid pool a bolt leaves where it hits ----------

        /// <summary>
        /// The pool a bolt leaves on a surface (user, 2026-11-16): a glowing, bubbling splash of the bolt's colour lying
        /// flat on it, with a thin smoke rising and a faint light, over a dark scorch. The client (RaygunMk2Pool) fades
        /// the glow, smoke and light over a few seconds and the scorch a while later. Flat quads facing out along +Z.
        /// </summary>
        private static void BuildPool(string name, Color c, Mesh quad, Material poolMat, Material scorchMat, Material smokeMat)
        {
            var root = new GameObject(name);
            try
            {
                GameObject scorch = Flat(root, "scorch", quad, scorchMat, 0.22f, 0.001f);
                GameObject glow = Flat(root, "glow", quad, poolMat, 0.17f, 0.003f);
                GameObject core = Flat(root, "core", quad, poolMat, 0.08f, 0.004f);

                ParticleSystem smoke = Particles(root, "smoke", smokeMat, local: false);
                var main = smoke.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var tint = Color.Lerp(c, Color.white, 0.55f);
                main.startColor = new Color(tint.r, tint.g, tint.b, 0.22f);
                main.maxParticles = 60;
                var emission = smoke.emission;
                emission.rateOverTime = 9f;
                var shape = smoke.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.05f;
                // Out of the surface (+Z), then drifting up
                smoke.transform.localRotation = Quaternion.identity;
                var force = smoke.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.y = new ParticleSystem.MinMaxCurve(0.25f);
                Grow(smoke, 2.2f);
                Fade(smoke);

                Light light = new GameObject("light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.localPosition = new Vector3(0f, 0f, 0.06f);
                light.type = LightType.Point;
                light.color = c;
                light.range = 0.7f;
                light.intensity = 1.0f;
                light.shadows = LightShadows.None;
                Save(root, name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject Flat(GameObject root, string name, Mesh quad, Material mat, float size, float lift)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, lift);
            go.transform.localScale = new Vector3(size, size, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A unit quad facing +Z (its own asset, so the bundle carries it).</summary>
        private static Mesh SaveQuad()
        {
            string path = $"{Mk2Paths.Root}/Fx/mk2_fx_quad.asset";
            AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = "mk2_fx_quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            // Both faces, so it shows whichever way the surface's normal came out
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // A splash: a blobby, uneven round shape with brighter bubbles in it
        private static Color PoolPixel(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float edge = 0.78f + 0.18f * (Mathf.PerlinNoise(x * 2.3f + 7f, y * 2.3f + 3f) - 0.5f) * 2f;
            float body = Mathf.Clamp01((edge - d) / 0.25f);
            float bubbles = Mathf.Pow(Mathf.PerlinNoise(x * 9f + 11f, y * 9f + 5f), 3f) * 1.6f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(body * (0.55f + bubbles)));
        }

        // A scorch: dark and soft, darkest in the middle, ragged at the edge
        private static Color ScorchPixel(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float edge = 0.85f + 0.15f * (Mathf.PerlinNoise(x * 3.1f + 1f, y * 3.1f + 8f) - 0.5f) * 2f;
            float a = Mathf.Clamp01((edge - d) / 0.45f) * 0.85f;
            return new Color(0.05f, 0.05f, 0.04f, a);
        }

        private delegate Color Pixel(float x, float y);

        // A soft round glow: bright middle, smooth falloff
        private static Color GlowPixel(float x, float y)
        {
            float d = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
            return new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2.2f));
        }

        // Across a line: a sharp bright middle, quick soft edges (V runs across a trail)
        private static Color StreakPixel(float x, float y)
        {
            float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 3f);
            return new Color(1f, 1f, 1f, a);
        }

        // A smoke puff: a soft disc broken up by noise
        private static Color PuffPixel(float x, float y)
        {
            float d = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
            float n = Mathf.PerlinNoise(x * 3.1f + 5f, y * 3.1f + 9f) * 0.6f + Mathf.PerlinNoise(x * 7.3f + 2f, y * 7.3f + 4f) * 0.4f;
            float a = Mathf.Clamp01((1f - d) * 1.6f - 0.25f) * Mathf.Lerp(0.45f, 1f, n);
            return new Color(1f, 1f, 1f, a);
        }

        private static Texture2D SaveTexture(string name, int size, Pixel pixel)
        {
            string path = $"{Mk2Paths.Root}/Fx/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, pixel((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material SaveMaterial(string name, string shader, Texture2D tex, Color tint)
        {
            string path = $"{Mk2Paths.Root}/Fx/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            // Our own shader by its file if Shader.Find hasn't picked it up yet (fresh import in a batch run)
            Shader found = Shader.Find(shader);
            if (found == null && shader == "RaygunMk2/ArcOnTop")
                found = AssetDatabase.LoadAssetAtPath<Shader>($"{Mk2Paths.Root}/Shaders/Mk2ArcOnTop.shader");
            if (found == null)
                throw new System.Exception($"[RaygunMk2] Shader not found: {shader}");
            var mat = new Material(found) { name = name, mainTexture = tex };
            if (mat.HasProperty("_TintColor"))
                mat.SetColor("_TintColor", new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, 0.5f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
