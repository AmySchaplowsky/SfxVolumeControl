# SFX Volume Control

Gives every sound effect in Valheim its own volume setting, plus a master multiplier. Client-side only.

## Requirements

- Valheim with [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- A configuration manager is strongly recommended. The config has hundreds of entries.

## Install

Install through your mod manager, or copy `SfxVolumeControl.dll` into `BepInEx/plugins/SfxVolumeControl/`.

## Config

`BepInEx/config/local.sfxvolumecontrol.cfg`

- **Master / Volume**: multiplier applied to every sound (0 = silent, 1 = default).
- **Master / ScaleMethod**: how volume is changed (`Both`, `AudioSourceOnly`, `ZsfxRangeOnly`). Try another value if sounds get quieter than expected.
- **Master / AffectMusic**: also apply volumes to music (off by default).
- **Sounds: <name>** sections: one volume entry per one-shot sound effect, from 0 (silent) to 1 (default). Sounds are grouped by the first word of their name, for example `sfx_wood_blocked` is under `Sounds: wood`.
- **World: <name>** sections: one volume entry per sound that belongs to an object in the world, such as torch flames or portal hum. They are named after the object, for example `piece_walltorch`, and appear when the object is first loaded near you.

Sound effect entries are added the first time the game has loaded a world. World sound entries are added as objects with sounds come into range, so stand near a torch or portal once, then open your configuration manager and search for it by name. Changes apply while the game is running.

## What it covers

- One-shot sound effects the game plays by creating a sound object (hits, blocks, building, footsteps, creature sounds, and so on), one setting each.
- Sounds attached to objects in the world, such as torch flames and portal hum, one setting per object name.
- Music is excluded unless `AffectMusic` is on.

## Limits

- Volume can be lowered, not raised. The maximum is the game's default level.
- Sounds already playing keep their old volume. The new volume applies to the next time the sound plays.

## Building from source

1. Install the .NET SDK and BepInEx.
2. Double-click `build.bat`. It finds Valheim and BepInEx (game folder, r2modman or Thunderstore profile), builds, and copies the DLL into `BepInEx/plugins/SfxVolumeControl/`.

## Troubleshooting

Search `BepInEx/LogOutput.log` for `SFX Volume Control` and `World sound:`. The `World sound:` lines list the name, clip and audio group of each world sound found.
