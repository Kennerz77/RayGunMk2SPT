# Ray Gun Mark II

The **Ray Gun Mark II** from classic zombies, in SPT. The HD model with its own first-person animations and sounds, firing three-round bursts of green energy bolts from a battery clip, with a chalk wall buy in your hideout to get it. Pack-a-Punch it into **Porter's Mark II Ray Gun**.

Version 1.0.0, for **SPT 4.1.6**. Made by **Kenner**.

**Dr K's SPZombies:** with Kenner's zombies mode installed, the Mark II is one of the wonder weapons the host can pick for the mystery box. On its own it works in normal play exactly as described here.

## Requirements

| Mod | Version | Why |
|---|---|---|
| SPT | 4.1.x (made on 4.1.6) | |
| [Hideout Uncensored](https://sp-mod.com/mod/3167/hideout-uncensored) by **trappuss** ([GitHub](https://github.com/trappuss/SPT-Mods-Release)) | 1.3.0 or newer, **both** its client and server parts | Weapons anywhere in the hideout: the wall buy hands you the Mark II straight away, outside the shooting range |

Optional:

| Mod | What it adds |
|---|---|
| [Pack-a-Punch Hideout Module](https://github.com/Kennerz77/papHideoutModule) **2.2.1** (by Kenner) | Pack-a-Punch the Mark II into **Porter's Mark II Ray Gun**: red bolts and twice the shots a clip. |
| [Dr K's SPZombies](https://github.com/Kennerz77/KennerZombiesSPT) **0.2.0 or newer** (by Kenner) | The Mark II as a wonder weapon in the mystery box. |

## Installing

1. Close the game and the SPT server.
2. Extract the zip into your SPT folder (the one with the `SPT_Runtime` folder). You should end up with:
   - `BepInEx\plugins\RaygunMk2\` (`RaygunMk2.dll`, `mk2_fx.bundle`, `icons\`, `sounds\`)
   - `SPT_Runtime\user\mods\RaygunMk2\` (`RaygunMk2Server.dll`, `bundles.json`, `bundles\`)
3. Start the server, then the game.

## Getting it

Once your hideout's **side room is open**, there's a **chalk outline of the Mark II** on a wall in your hideout, with a green **ammo case** beside it.

- **The wall:** buy the Ray Gun Mark II for **950,000 ₽** (from your stash). It slides out of the wall, loaded. If a primary slot is free it's equipped and drawn; otherwise it goes to your stash. The wall sells it again on your first hideout visit after a raid.
- **The ammo case:** buy a full **battery clip** for **15,000 ₽**. It goes into a free spot on you first, then into your stash.
- Not enough money or no room: nothing is charged.

The wall and the ammo case are the only way to get the Mark II and its clips: they're never at traders, on the flea market, in loot or on bots.

## The gun

- A 3x2 gun for either primary slot, 2.4 kg.
- Fires **three-round bursts** of green energy bolts, with a short pause between bursts. A battery clip holds **21 shots**.
- Each bolt bursts where it lands and leaves a small glowing acid pool for a moment. Lightning crackles across the back of the gun with each shot.
- The power cells light up as you draw it.

## Porter's Mark II Ray Gun (with Pack-a-Punch Hideout Module)

- It goes into the machine as a plain Mark II and comes out as Porter's Mark II, with red bolts, red cells and **42 shots** a clip.
- Stripping its power makes it a Mark II again.

## Selling

Traders pay the wall price for the Mark II (plus the Pack-a-Punch fee when infused). A clip sells by the shots left in it.

## Settings (F12)

- **Normal gameplay:** whether the Mark II and its wall buy are in your normal hideout and raids at all, or kept for the zombies mode only.
- **Volume:** **Shot**, **Reload and handling**, **Bolt impact**, **Purchase**, **Wall buy** and **Deny**.

Prices are in `SPT_Runtime\user\mods\RaygunMk2\config.json` (create it to change them):

```json
{ "WallBuyPrice": 950000, "CartridgePrice": 15000 }
```

## Compatibility

- Never changes another mod's files, settings or load order.
- Works with Fika co-op as it is; Fika itself isn't changed.
- Other gun and item mods work alongside it, the original Ray Gun mod included.

## Uninstalling

Delete `BepInEx\plugins\RaygunMk2\` and `SPT_Runtime\user\mods\RaygunMk2\`. The mod keeps its items out of your profile file and adds them back when the server loads, so without the mod they're simply gone.

## Credits

- Made by Kenner.
- **Hideout Uncensored** by **trappuss**: needed for the hideout wall buy. Not included.
- Third-party game assets belong to their respective owners. This is a free, non-commercial fan mod.

## Changelog

- **1.0.0:** first release.
