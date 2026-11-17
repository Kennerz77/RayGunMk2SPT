using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RaygunMk2.Editor
{
    /// <summary>
    /// Dev check for the rear lightning's placement (RaygunMk2Arcs): the hands prefab posed in its fire animation, and
    /// where the original model's points land by each way of mapping them, against the bones and the gun's own mesh.
    /// Writes logs\arc_probe.txt. Run: Unity -batchmode -executeMethod RaygunMk2.Editor.Mk2ArcProbe.Run
    /// </summary>
    public static class Mk2ArcProbe
    {
        private const float Unit = 0.01f;
        private static readonly Quaternion IntoGun = Quaternion.Inverse(Quaternion.LookRotation(new Vector3(0f, -1f, 0f), new Vector3(-1f, 0f, 0f)));

        private static readonly (string name, Vector3 at)[] Points =
        {
            ("origin", Vector3.zero), ("tag_flash", new Vector3(40.55f, 0f, 7.61f)), ("tag_fin_animate", new Vector3(-4.63f, 0f, 6.53f)),
            ("tag_brass", new Vector3(-6.92f, 0f, 7.61f)), ("tag_bolt_animate", new Vector3(-9.55f, 3.75f, 2.64f)),
            ("cross left", new Vector3(-13.05f, 1.45f, 11.05f)), ("post left", new Vector3(-10f, 1.3f, 14.45f)),
        };

        public static void Run()
        {
            var log = new StringBuilder();
            try
            {
                GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Mk2Importer.WeaponPrefabPath));
                Transform[] all = go.GetComponentsInChildren<Transform>(true);
                log.AppendLine("j_gun transforms: " + string.Join(", ", all.Where(t => t.name == "j_gun").Select(t => PathOf(t))));
                Transform flash = all.First(t => t.name == "tag_flash");
                Transform jgun = null;
                for (Transform t = flash; t != null && jgun == null; t = t.parent)
                    if (t.name == "j_gun")
                        jgun = t;
                log.AppendLine("gun root: " + PathOf(jgun) + $" lossyScale {jgun.lossyScale}");
                Probe(log, "rest", go, jgun, all);
                string[] clips = Directory.GetFiles(Mk2Paths.Clips, "*.anim").Select(p => p.Replace('\\', '/')).ToArray();
                log.AppendLine("clips: " + string.Join(", ", clips.Select(Path.GetFileNameWithoutExtension)));
                foreach (string c in clips.Where(c => c.Contains("fire")).Take(2))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(c);
                    clip.SampleAnimation(go, 0.04f);
                    Probe(log, Path.GetFileNameWithoutExtension(c) + " @0.04", go, jgun, all);
                }
                Object.DestroyImmediate(go);
            }
            catch (System.Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            string outPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "logs", "arc_probe.txt"));
            File.WriteAllText(outPath, log.ToString());
            Debug.Log("[RaygunMk2] arc probe -> " + outPath);
            EditorApplication.Exit(0);
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            for (Transform u = t.parent; u != null; u = u.parent)
                p = u.name + "/" + p;
            return p;
        }

        private static void Probe(StringBuilder log, string label, GameObject go, Transform jgun, Transform[] all)
        {
            log.AppendLine($"--- {label}: j_gun pos {jgun.position:F4} rot {jgun.rotation.eulerAngles:F1} scale {jgun.lossyScale:F3}");
            // The rigid mesh on the gun root (tag_weapon's): its vertices, in the world
            MeshFilter root = jgun.GetComponentsInChildren<MeshFilter>(true).Where(m => m.transform.parent == jgun || m.transform == jgun)
                .OrderByDescending(m => m.sharedMesh != null ? m.sharedMesh.vertexCount : 0).FirstOrDefault();
            Vector3[] verts = root != null && root.sharedMesh != null && root.sharedMesh.isReadable
                ? root.sharedMesh.vertices.Select(v => root.transform.TransformPoint(v)).ToArray() : new Vector3[0];
            log.AppendLine($"root mesh: {(root != null ? PathOf(root.transform) : "none")} local {root?.transform.localPosition:F4} {root?.transform.localRotation.eulerAngles:F1}, {verts.Length} verts");
            foreach (var (name, at) in Points)
            {
                Vector3 mapped = jgun.TransformPoint(IntoGun * (new Vector3(-at.y, at.z, at.x) * Unit));
                Transform bone = all.FirstOrDefault(t => t.name == name);
                string boneText = bone != null ? $" bone {bone.position:F4} (off {(bone.position - mapped).magnitude * 100f:F1} cm)" : "";
                string near = "";
                if (verts.Length > 0)
                {
                    float best = verts.Min(v => (v - mapped).sqrMagnitude);
                    near = $" nearest root-mesh vertex {Mathf.Sqrt(best) * 100f:F2} cm";
                }
                log.AppendLine($"  {name,-16} mapped {mapped:F4}{boneText}{near}");
            }
        }
    }
}
