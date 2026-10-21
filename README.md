# NORMAN (NuclearOption-RangeManualAdjustmentNode)
This BepInEx plugin automatically finds every available air-to-air missile in the player's game directory, including the ones added through mods.

Each of the missiles gets an entry in the config file of this mod that simply saves a percentage of its range, which the player can customize in the F1 menu however they see fit. The standard value for every missile is 100, which is equal to its corresponding vanilla range. Changing that value will extend or shorten the maximum range of that missile by whatever percentage is put there. This calculation uses the vanilla range for every individual missile, so a 15% increase on a Scimitar will have a much greater effect than the same 15% change on a Scythe, simply due to the longer standard range of the Scimitar.

The plugin should recognize and work with pretty much every available weapon pack and mod out there, as long as the missiles use the same seeker logic as the vanilla ones. It has been tested and confirmed to work with:
- all A2A missiles added by Aryx's mods
- kingwixly's MultiMissile Pack
- the missiles that come with Iornman's MiG-29 and MiG-29SMT
- the backwards-mounted MMR-S3 added with the KR-33 by Phoenix1509
- and the AAM-21 Glaive SARH missile by BruhBoi50

In the F1 menu tab, the missiles found by the plugin are grouped by their seeker types and sorted into neat and tidy drop-down menus to avoid having a mile-long list in the future, when more weapon mods are expected to be released.

The change in max range is realized by modifying the burn time and fuel amount the missile has. The range indicator in the HUD acknowledges the changes from this mod and gives accurate readouts. Modified values of missile mods that are no longer installed are hidden, but still available and come right back into effect, should the mod be reinstalled at a later point in time.

As of right now, the max range of air-to-air missiles is the only parameter this mod touches. Expanded functionality may be added sooner, later, or whenever the stars align.
While this mod's code is written entirely by AI, the concept and UI structure are my own vision, made into reality through the power of friendsh... vibecoding!

This project is an unofficial community modification and is not affiliated with, sponsored by, or endorsed by Shockfront Studios Pty Ltd. Original Nuclear Option assets, vehicle designs, audio, and code are Copyright (c) 2026 Shockfront Studios Pty Ltd. All rights reserved. Nuclear Option and Shockfront Studios are trademarks or registered trademarks of Shockfront Studios Pty Ltd. Original mod content and all other trademarks belong to their respective owners.
