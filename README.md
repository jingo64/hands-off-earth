# Hands-off Earth

A Terra Invicta mod that cuts down late-game Earth bookkeeping. It finishes the mission phase for you when every councilor already has orders, makes the game's councilor automation less repetitive, and switches nations' priority presets for you based on rules you write.

## Features

**Mission phase**

- When every councilor is automated or on a repeat order, the mission phase finishes by itself and the game keeps running
- Councilors left without orders still stop the phase as normal
- The phase still stops if, since the last one, you lost a control point, a councilor was killed or detained, or an enemy mission targeted one of your councilors. A councilor message says why

**Smarter automation**

- Defend Interests and Protect are only favoured where something threatens the target: enemy control points, known enemy councilors, or a recently lost control point
- Repeating last phase's mission on a quiet target is discouraged
- Optionally, automated councilors can also use Gain Influence, Crackdown, Purge and Detain, but only in nations where you hold control points
- Only councilors you set to automate are affected. Anyone you manage by hand works exactly as before, and AI factions are untouched

**Priority rules**

- Write an ordered list of rules, each a preset plus conditions on a nation's stats: GDP, GDP per capita, population, research, mission control, military tech, sustainability, cohesion, unrest, democracy, education or inequality
- Tick **Use priority rules** on a nation's priorities tab and it gets the preset of the first rule that matches
- Opted-in nations are re-checked every month, so they move between presets as they develop
- A preview shows any nation's current stats and which rule it would get

## Installation

Subscribe on the Steam Workshop, or install by hand. Either way the mod needs [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) (UMM), version 0.24 or later.

1. **Install UMM for Terra Invicta.** Run UnityModManager.exe, pick Terra Invicta from the list, choose the **DoorstopProxy** method and click Install.
   - On Linux/Steam Deck (Proton), also set this Steam launch option for Terra Invicta:
     `WINEDLLOVERRIDES="winhttp=n,b" %command%`
2. **By hand:** build the mod (see below) or download a release, and put `HandsOffEarth.dll` and `ModInfo.json` in `Terra Invicta/Mods/Enabled/Hands-off Earth/`.
3. Start the game. Hands-off Earth should appear in the UMM window (Ctrl+F10).

## Usage

1. Turn on **Unpause after mission assignment** in the game's options, so the game carries on once the phase is finished.
2. Set your councilors to automate, or give them repeat orders.
3. For priority rules, open a nation's priorities tab and press **Priority rules**. Rules are checked top to bottom and the first one whose conditions all hold wins. A rule with no conditions always matches, so it works as a catch-all at the bottom. Then tick **Use priority rules** on the nations you want handled.
4. What stops the phase, and which extra missions automation may use, are set in the UMM window.

Worth knowing:

- The first mission phase after loading a save can't tell what you lost before the load, so it only checks for councilors without orders.
- Crackdown and Purge make the target faction hate you, which is why they're off by default.
- Priority rules are shared by all your campaigns and refer to presets by name, so save your custom presets under the same names in each campaign. A rule whose preset doesn't exist is skipped.
- Hand changes to an opted-in nation's priorities are replaced at the next monthly check. Untick it to manage it yourself.

## Compatibility

Tested on Terra Invicta 1.0.53b. Nothing is added to your save, so the mod can be added or removed mid-campaign.

- Priority rules are kept in `PriorityRules.json`, in a `HandsOffEarth` folder in the game's data folder (`AppData/LocalLow/Pavonis Interactive/TerraInvicta`).
- The nations using priority rules are kept in a small `<save name>.handsoff` file next to each save.

## Building from source

You need the .NET SDK (6.0 or later; the project targets .NET Framework 4.8 through reference packages) and an installed copy of the game with UMM.

```sh
dotnet build -c Release -p:TIGame="/path/to/Terra Invicta" -p:Deploy=true
```

`TIGame` points at your game folder (the default is the Steam on Linux location). `-p:Deploy=true` copies the built mod straight into `Mods/Enabled`.

## Feedback

Bug reports and suggestions are welcome in [Issues](https://github.com/jingo64/hands-off-earth/issues). If something goes wrong, attach UMM's log (`TerraInvicta_Data/Managed/UnityModManager/Log.txt`). The mod logs each automated councilor's pick and each preset switch there.

## License

MIT, see [LICENSE](LICENSE).
