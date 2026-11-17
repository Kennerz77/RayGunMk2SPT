using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// Where a bolt hits a wall or the ground it leaves a glowing pool of acid green (red Pack-a-Punched) in the hole,
    /// bubbling, smoking and lighting the spot for a few seconds before it fades, then a dark scorch that fades a while
    /// later (user, 2026-11-16). Not on people. At most a few dozen at once: the oldest go first.
    /// </summary>
    internal class RaygunMk2Pool : MonoBehaviour
    {
        private const float GlowTime = 4f, ScorchTime = 20f, ScorchFade = 3f;
        private const int Most = 40;
        private static readonly int TintColor = Shader.PropertyToID("_TintColor");
        private static readonly LinkedList<RaygunMk2Pool> Live = new LinkedList<RaygunMk2Pool>();

        private Renderer _glow, _core, _scorch;
        private ParticleSystem _smoke;
        private Light _light;
        private Color _glowTint, _scorchTint;
        private float _born, _lightLevel, _seed;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private LinkedListNode<RaygunMk2Pool> _node;

        public static void Leave(Vector3 at, Vector3 normal, bool upgraded)
        {
            try
            {
                if (normal == Vector3.zero)
                    return;
                // Not on a body: a player or a bot within reach of the hit
                foreach (Collider c in Physics.OverlapSphere(at, 0.12f, ~0, QueryTriggerInteraction.Ignore))
                    if (c.GetComponentInParent<Player>() != null)
                        return;
                GameObject prefab = RaygunMk2Bolts.Prefab(upgraded ? "mk2_pool_up" : "mk2_pool");
                if (prefab == null)
                    return;
                // Flat on the surface, a little turned at random so no two look alike
                Quaternion lay = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                GameObject go = Instantiate(prefab, at + normal * 0.004f, lay);
                go.transform.localScale *= Random.Range(0.85f, 1.2f);
                go.SetActive(true);
                go.AddComponent<RaygunMk2Pool>();
                while (Live.Count > Most)
                {
                    RaygunMk2Pool oldest = Live.First.Value;
                    Live.RemoveFirst();
                    if (oldest != null)
                        Destroy(oldest.gameObject);
                }
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Mark II pool skipped: {e.Message}");
            }
        }

        private void Start()
        {
            _node = Live.AddLast(this);
            _born = Time.time;
            _seed = Random.Range(0f, 100f);
            _glow = transform.Find("glow")?.GetComponent<Renderer>();
            _core = transform.Find("core")?.GetComponent<Renderer>();
            _scorch = transform.Find("scorch")?.GetComponent<Renderer>();
            _smoke = GetComponentInChildren<ParticleSystem>();
            _light = GetComponentInChildren<Light>();
            _lightLevel = _light != null ? _light.intensity : 0f;
            _glowTint = _glow != null && _glow.sharedMaterial.HasProperty(TintColor) ? _glow.sharedMaterial.GetColor(TintColor) : Color.white;
            _scorchTint = _scorch != null && _scorch.sharedMaterial.HasProperty(TintColor) ? _scorch.sharedMaterial.GetColor(TintColor) : Color.white;
        }

        private void OnDestroy()
        {
            if (_node != null && _node.List != null)
                Live.Remove(_node);
        }

        private void Update()
        {
            float age = Time.time - _born;
            // The glow: bubbling (an uneven flicker), easing out over its few seconds
            float left = Mathf.Clamp01(1f - age / GlowTime);
            float bubble = 0.8f + 0.35f * Mathf.PerlinNoise(Time.time * 4f + _seed, _seed);
            float level = left * left * bubble;
            Tint(_glow, _glowTint, level);
            Tint(_core, _glowTint, level * 1.3f);
            if (_light != null)
                _light.intensity = _lightLevel * level;
            if (_smoke != null && age > GlowTime * 0.8f && _smoke.isEmitting)
                _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            // The scorch: there, then fading
            float scorch = age < ScorchTime ? 1f : Mathf.Clamp01(1f - (age - ScorchTime) / ScorchFade);
            Tint(_scorch, _scorchTint, scorch);
            if (age > ScorchTime + ScorchFade)
                Destroy(gameObject);
        }

        private void Tint(Renderer r, Color tint, float level)
        {
            if (r == null)
                return;
            r.GetPropertyBlock(_block);
            _block.SetColor(TintColor, new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Clamp01(level)));
            r.SetPropertyBlock(_block);
            r.enabled = level > 0.005f;
        }
    }
}
