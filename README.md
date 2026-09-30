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
- **Sounds: <name>** sections: one volume entry per sound effect, from 0 (silent) to 1 (default). Sounds are grouped by the first word of their name, for example `sfx_wood_blocked` is under `Sounds: wood`.

Individual entries are added the first time the game has loaded a world, so load a world once, then open your configuration manager and search for a sound by name. Changes apply while the game is running.

## What it covers

Sound effects that the game plays by creating a sound object (hits, blocks, building, footsteps, creature sounds, and so on). It does not cover music, or ambient sounds that are part of the world itself.

## Limits

- Volume can be lowered, not raised. The maximum is the game's default level.
- Sounds already playing keep their old volume. The new volume applies to the next time the sound plays.

## Building from source

1. Install the .NET SDK and BepInEx.
2. Double-click `build.bat`. It finds Valheim and BepInEx (game folder, r2modman or Thunderstore profile), builds, and copies the DLL into `BepInEx/plugins/SfxVolumeControl/`.

## Troubleshooting

Search `BepInEx/LogOutput.log` for `SFX Volume Control`. It reports how many sounds it found.

