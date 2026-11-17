"""Prepares the original's Mark II sounds (GrayWolf export) for the Unity import, into sounds_source\\.

    C:\\Git\\.venv\\Scripts\\python.exe -I tools\\prepare_sounds.py

The original's player fire sound (plr\\shot\\wpn_raygun_mk2_fire.wav) is a whole three-shot burst, and EFT plays the
gun's shot sound once per shot, so it stacked into four-plus shots. The shots here are single:
  * mk2_shot_00..02: the original's per-bolt shots (shot\\shot_0N.wav) with its low-end layer (lfe) mixed under them,
    played one per shot (EFT picks one at random)
  * wpn_raygun_mk2_pap_single: the last of the three shots in the Pack-a-Punched burst (from 0.24 s), which carries
    the full tail; Pack-a-Punch plays it per shot on the upgraded gun
plus the reload foley and the three impact sounds, copied as they are.
"""
import os
import shutil
import sys
import wave

import numpy as np

ROOT = os.environ.get('KZ_GRAYWOLF_BO3', r'E:\GrayWolf\exported_files\bo3')
SRC = os.path.join(ROOT, 'sounds', 'all', 'wpn', 'energy', 'raygun_mk2')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'sounds_source')

COPY = {
    r'reload\raise.wav': 'raise.wav',
    r'reload\switch.wav': 'switch.wav',
    r'reload\pullout.wav': 'pullout.wav',
    r'reload\putin.wav': 'putin.wav',
    r'imp\imp_00.wav': 'mk2_impact_00.wav',
    r'imp\imp_01.wav': 'mk2_impact_01.wav',
    r'imp\imp_02.wav': 'mk2_impact_02.wav',
}


def read(path):
    with wave.open(path) as w:
        sr, ch, n = w.getframerate(), w.getnchannels(), w.getnframes()
        a = np.frombuffer(w.readframes(n), dtype=np.int16).reshape(-1, ch).astype(np.float32) / 32768
    return a, sr


def write(path, a, sr):
    a = np.clip(a, -1, 1)
    with wave.open(path, 'wb') as w:
        w.setnchannels(a.shape[1])
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes((a * 32767).astype(np.int16).tobytes())


def louder(a, target_rms_db, mono=True):
    """Mono (EFT plays these as 3D sounds), raised to the target loudness, with a soft limiter keeping the peaks
    under full scale (the original's mixes sit 5-10 dB below the Ray Gun's: too quiet in game, user 2026-11-15)."""
    if mono and a.shape[1] > 1:
        a = a.mean(axis=1, keepdims=True)
    rms = np.sqrt((a ** 2).mean()) + 1e-9
    gain = 10 ** (target_rms_db / 20) / rms
    b = a * gain
    knee = 0.7
    over = np.abs(b) > knee
    b[over] = np.sign(b[over]) * (knee + (1 - knee) * np.tanh((np.abs(b[over]) - knee) / (1 - knee)))
    return b * 0.98


def to_channels(a, ch):
    if a.shape[1] == ch:
        return a
    return np.repeat(a.mean(axis=1, keepdims=True), ch, axis=1)


def main():
    os.makedirs(OUT, exist_ok=True)
    for old in ('wpn_raygun_mk2_fire.wav', 'wpn_raygun_mk2_pap.wav', 'wpn_raygun_mk2_lfe.wav'):
        p = os.path.join(OUT, old)
        if os.path.exists(p):
            os.remove(p)
    for src, dst in COPY.items():
        a, sr = read(os.path.join(SRC, src))
        # Reload foley a little under the shots; the impacts as loud as a shot
        write(os.path.join(OUT, dst), louder(a, -11 if dst.startswith('mk2_impact') else -14), sr)

    lfe, lsr = read(os.path.join(SRC, 'lfe', 'wpn_raygun_mk2_lfe.wav'))
    for i in range(3):
        shot, sr = read(os.path.join(SRC, 'shot', f'shot_0{i}.wav'))
        if lsr != sr:
            sys.exit(f'lfe rate {lsr} != shot rate {sr}')
        bass = to_channels(lfe, shot.shape[1]) * 0.6
        n = max(len(shot), len(bass))
        mix = np.zeros((n, shot.shape[1]), np.float32)
        mix[:len(shot)] += shot
        mix[:len(bass)] += bass
        write(os.path.join(OUT, f'mk2_shot_0{i}.wav'), louder(mix, -10), sr)

    pap, sr = read(os.path.join(SRC, 'pap', 'plr', 'wpn_raygun_mk2_pap.wav'))
    single = pap[int(0.24 * sr) - int(0.004 * sr):].copy()
    fade = int(0.004 * sr)
    single[:fade] *= np.linspace(0, 1, fade)[:, None]
    write(os.path.join(OUT, 'wpn_raygun_mk2_pap_single.wav'), louder(single, -10), sr)
    print('sounds_source:', ', '.join(sorted(os.listdir(OUT))))


if __name__ == '__main__':
    main()
