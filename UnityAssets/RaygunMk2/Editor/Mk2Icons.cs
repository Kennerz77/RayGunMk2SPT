using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RaygunMk2.Editor
{
    /// <summary>
    /// Inventory pictures, rendered here instead of by the game's icon studio (which washes these models out white):
    /// the Mark II, the Pack-a-Punched one in PaP's own camo (default and Origins looks) and the battery clip. Written
    /// as transparent PNGs to icons/ in the project root; the client plugin ships them and swaps them into the cells.
    /// </summary>
    public static class Mk2Icons
    {
        public static string IconsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../icons"));

        // PaP's camo albedo (read only, never changed) and the tiling PaP paints it with
        private const string CamoAlbedo = @"C:\Git\3_Working\PaPHideoutModule\UnityProject\Assets\PaP\Camo\pap_camo_albedo.png";
        private const float CamoTiling = 2.5f; // as the client asks PaP for (RaygunMk2PaP.CamoTiling)
        private const float CamoEmission = 0.06f;
        private const string OriginsCrystalPath = @"C:\Git\3_Working\PaPHideoutModule\UnityProject\Assets\PaP\Camo\origins_crystal_c.png";
        private static Texture2D _crystal;

        private const int Width = 1024;

        public static void RenderAll()
        {
            Directory.CreateDirectory(IconsDir);
            var rig = new List<Object>();
            try
            {
                rig.AddRange(Lights());
                RenderGun("raygunmk2", camo: null, upgraded: false);
                RenderGun("raygunmk2_nomag", camo: null, upgraded: false, noMag: true);
                Texture2D camo = LoadTexture(CamoAlbedo);
                if (camo != null)
                {
                    rig.Add(camo);
                    RenderGun("raygunmk2_pap", camo, upgraded: true);
                    RenderGun("raygunmk2_pap_nomag", camo, upgraded: true, noMag: true);
                    _crystal = LoadTexture(OriginsCrystalPath);
                    if (_crystal != null)
                    {
                        rig.Add(_crystal);
                        RenderGun("raygunmk2_pap_origins", camo, upgraded: true, origins: true);
                        RenderGun("raygunmk2_pap_origins_nomag", camo, upgraded: true, origins: true, noMag: true);
                    }
                }
                else
                {
                    Debug.LogWarning($"[RaygunMk2] PaP camo not found at {CamoAlbedo}; no Pack-a-Punched icon");
                }
                RenderClip("clip");
            }
            finally
            {
                foreach (Object o in rig)
                    Object.DestroyImmediate(o);
            }
        }

        private static IEnumerable<Object> Lights()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.45f);
            foreach (var (rotation, intensity) in new[] { (Quaternion.Euler(30f, 60f, 0f), 1.0f), (Quaternion.Euler(-20f, -120f, 0f), 0.45f) })
            {
                var go = new GameObject("icon_light");
                Light light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                go.transform.rotation = rotation;
                yield return go;
            }
        }

        private static Texture2D LoadTexture(string path)
        {
            if (!File.Exists(path))
                return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        /// <summary>
        /// The gun from its left side, muzzle to the left as EFT pictures its guns. Laid out in the original game space
        /// through j_gun: the barrel (+X) along +Z, up (+Z) along +Y, its left (+Y) along -X.
        /// </summary>
        private static void RenderGun(string name, Texture2D camo, bool upgraded, bool origins = false, bool noMag = false)
        {
            GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Mk2Importer.WeaponPrefabPath));
            try
            {
                Transform jgun = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "j_gun");
                jgun.SetParent(null, true);
                jgun.position = Vector3.zero;
                jgun.rotation = Mk2Space.Rot(Mk2Classic.LoadModel().Bones[0].Rest);
                Object.DestroyImmediate(go);
                go = jgun.gameObject;
                // A gun with no magazine (its own picture, user 2026-11-16): the clip's meshes left out
                if (noMag)
                {
                    Transform clip = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "tag_clip");
                    if (clip != null)
                        foreach (Renderer r in clip.GetComponentsInChildren<Renderer>(true))
                            Object.DestroyImmediate(r.gameObject);
                }
                Dress(go, camo, upgraded, origins);
                Shoot(go, name, forward: Vector3.right, up: Vector3.up);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>The battery clip from its side: looking along its thinnest axis, its longest across the picture.</summary>
        private static void RenderClip(string name)
        {
            GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Mk2Importer.MagPrefabPath));
            try
            {
                Dress(go, null, false, false);
                MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                Vector3 size = mf.sharedMesh.bounds.size;
                Vector3 thin = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
                Vector3 longest = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
                Vector3 forward = mf.transform.TransformDirection(thin);
                Vector3 up = Vector3.Cross(forward, mf.transform.TransformDirection(longest));
                Shoot(go, name, forward, up);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Copies of the model's materials: the power cells green or red, the rest in camo where PaP would paint it.</summary>
        private static void Dress(GameObject go, Texture2D camo, bool upgraded, bool origins)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = r.sharedMaterials.Select(m =>
                {
                    if (m == null)
                        return null;
                    var copy = new Material(m);
                    if (m.name.StartsWith(Mk2Importer.PowerMaterial))
                    {
                        copy.SetColor("_EmissionColor", Mk2Importer.PowerGlow(upgraded));
                        return copy;
                    }
                    // As PaPCamo.ShouldPaint: nothing from the alpha-test queue up, nothing without a colour map
                    Texture2D skin = origins ? _crystal : camo;
                    if (camo != null && skin != null && m.renderQueue < 2450 && m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null)
                    {
                        copy.SetTexture("_MainTex", skin);
                        copy.SetTextureScale("_MainTex", Vector2.one * CamoTiling);
                        copy.color = Color.white;
                        copy.SetTexture("_EmissionMap", skin);
                        copy.SetTextureScale("_EmissionMap", Vector2.one * CamoTiling);
                        copy.SetColor("_EmissionColor", new Color(CamoEmission, CamoEmission, CamoEmission));
                        copy.EnableKeyword("_EMISSION");
                    }
                    return copy;
                }).ToArray();
            }
        }

        /// <summary>An orthographic shot framing the model tightly, transparent around it.</summary>
        private static void Shoot(GameObject go, string name, Vector3 forward, Vector3 up)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            Quaternion view = Quaternion.LookRotation(forward, up);
            Quaternion inv = Quaternion.Inverse(view);
            // Bounds in the camera's frame: corners of every renderer's world bounds
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (Renderer r in renderers)
            {
                Bounds wb = r.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    Vector3 p = inv * corner;
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            Vector3 centre = view * ((min + max) / 2f);
            float w = max.x - min.x, h = max.y - min.y;
            int width = Width, height = Mathf.Max(64, Mathf.RoundToInt(Width * h / w));

            var camGo = new GameObject("icon_cam");
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = h / 2f * 1.04f;
                cam.aspect = (float)width / height;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.transform.rotation = view;
                cam.transform.position = centre - forward.normalized * ((max.z - min.z) + 1f);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = (max.z - min.z) * 2f + 4f;
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(IconsDir, $"{name}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log($"[RaygunMk2] Icon {name}.png: {width}x{height}");
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(camGo);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
