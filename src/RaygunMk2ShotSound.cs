using System.Collections.Generic;
using System.Linq;
using EFT;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// The Mark II's shot, played from our own audio sources: one of the original's three single shots per bolt (the
    /// same on a Pack-a-Punched gun, as the Ray Gun), each whole, overlapping the shots before it. EFT's weapon sound player
    /// cut each shot short and ran it through its own gunshot mix, which came out quiet and muffled (user, 2026-11-15),
    /// so the gun's own shot bank is silent. Yours plays in your head, others' where they stand.
    /// </summary>
    internal static class RaygunMk2ShotSound
    {
        private const int Voices = 8;
        private static AudioClip[] _shots = new AudioClip[0];
        private static AudioClip _pap;
        private static readonly List<AudioSource> Pool = new List<AudioSource>();
        private static int _next;

        /// <summary>The clips ride in the gun's bundle, on its sound player (the view hands it over as a model appears).</summary>
        public static void Load(WeaponSoundPlayer sounds)
        {
            if (_shots.Length > 0 || sounds?.AdditionalSounds == null)
                return;
            AudioClip Clip(string ev) => sounds.AdditionalSounds.FirstOrDefault(e => e?.EventName == ev)?.SoundClips?.FirstOrDefault();
            _shots = new[] { Clip("SndMk2Shot0"), Clip("SndMk2Shot1"), Clip("SndMk2Shot2") }.Where(c => c != null).ToArray();
            _pap = Clip("SndMk2PapShot");
            if (_shots.Length == 0)
                RaygunMk2Plugin.Log.LogWarning("Mark II shot sounds missing from the gun's bundle");
        }

        /// <summary>A Mark II bolt has just left the gun at this point.</summary>
        public static void Play(Vector3 at, bool upgraded)
        {
            // The same shot plain or Pack-a-Punched, as the Ray Gun (user, 2026-11-16)
            AudioClip clip = _shots.Length > 0 ? _shots[Random.Range(0, _shots.Length)] : null;
            if (clip == null)
                return;
            AudioSource s = Voice();
            Camera cam = Camera.main;
            bool mine = cam != null && Vector3.Distance(cam.transform.position, at) < 2.5f;
            s.transform.position = at;
            s.spatialBlend = mine ? 0f : 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 4f;
            s.maxDistance = 150f;
            s.volume = RaygunMk2Settings.ShotVolume.Value;
            s.pitch = Random.Range(0.98f, 1.02f);
            s.clip = clip;
            s.Play();
        }

        private static AudioSource Voice()
        {
            Pool.RemoveAll(p => p == null);
            if (Pool.Count < Voices)
            {
                var go = new GameObject("RaygunMk2_ShotVoice");
                Object.DontDestroyOnLoad(go);
                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                Pool.Add(src);
                return src;
            }
            return Pool[_next++ % Pool.Count];
        }
    }
}
