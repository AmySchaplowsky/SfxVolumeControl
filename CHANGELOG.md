# Changelog

## 0.2.0 - 2026-09-30
- Added volume settings for sounds attached to objects in the world (torch flames, portal hum and similar). They appear under `World:` sections and are named after the object. Version 0.1.0 could not see these sounds.
- Looping sounds are now handled on live objects instead of as effect prefabs, so each sound has one setting and is not scaled twice.
- Added `AffectMusic` (off by default). Music is skipped unless it is on.
- `World sound:` log lines list every world sound found, with its clip name and audio group.

## 0.1.0 - 2026-09-30
- First version. Finds every sound-effect prefab and adds a volume setting for each, plus a master volume and a scale method setting.

## Known issues
- Whether music is skipped depends on the audio group name containing "music". That is an assumption and is not verified.
- Sounds from objects that load after the 0.25 second scan can play at full volume briefly.
- Volume can only be lowered.
