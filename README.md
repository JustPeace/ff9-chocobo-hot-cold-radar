# Chocobo Hot & Cold Radar 1.0.1

English | [简体中文](README.zh-CN.md)

Shows the current treasure location in Chocobo's Forest, Chocobo's Lagoon and Chocobo's Air Garden. It draws a world marker, a compass in the top-right corner, distance indicators, an off-screen arrow and the game's Kweh proximity level.

## Configuration

Edit `ChocoboHotCold/ChocoboHotCold.ini`, then fully restart the game. The current code reads configuration only when the persistent overlay is first created.

| Setting | Default | Meaning |
| --- | --- | --- |
| Enabled | 1 | Enable the overlay |
| ShowWorldMarker | 1 | Mark the treasure in the scene |
| ShowCompass | 1 | Show the camera-relative compass |
| ShowDistance | 1 | Show distance |
| ShowKweh | 1 | Show the game's proximity level |
| ShowAllPoints | 0 | Show all nine candidate points for debugging |
| OnlyDuringMinigame | 0 | Use 1 to restrict display to the active minigame timer |
| UnitsPerMeter | 100 | Game coordinate units per displayed metre |
| Scale | 1.0 | Overall UI scale |
| MarkerHeight | 70 | Marker height in game coordinate units |

## Implementation and limitations

The original event scripts create nine hidden dig objects with UIDs 128–136 in fields 2950–2952. The overlay reads the target index from `mapVar[37]`, resolves `FindObjByUID(127 + index)`, and reads the Kweh level from `gEventGlobal[166]`.

The mod reads event data and draws an overlay; it does not intentionally write save data. Large mods that change these fields, variables or objects require separate compatibility testing. Distance is a configurable conversion, not a measured real-world scale.

Check `Memoria.log` for `[ChocoboHotCold] Main-thread pump installed`, `Version 1.0.1 ready`, `Dig field` and `Target` messages. Startup messages alone do not prove that treasure detection works.

## Requirements and validation status

- Windows Steam edition of FINAL FANTASY IX with [Memoria v2025.07.04, including the 2025-07-13 hotfix](https://github.com/Albeoris/Memoria/releases/tag/v2025.07.04).
- Memoria 2025-07-13 is the current compilation and compatibility baseline. Earlier versions and Canary have not been verified.
- Source compilation, package checks and both load-order checks against the installed engine resolver have passed. The author reports that the previously installed version works in-game. **The 1.0.1 patch has not received a separate in-game regression pass**. See [the release checklist](RELEASE-CHECKLIST.md), currently written in Chinese.
- Game files, Memoria binaries, translations, Moguri assets and save files are not included. Build references must come from your own game installation.

## Installation

1. Download `ChocoboHotCold-v1.0.1.zip` from Releases and close the game.
2. Extract it into the game directory containing `FF9_Launcher.exe`. The resulting path must be `ChocoboHotCold/StreamingAssets/Scripts/Memoria.Scripts.ChocoboHotCold.dll` without an extra nested mod folder.
3. Enable the mod in the Memoria launcher's Mods tab and restart the game.
4. If it is not listed, append `"ChocoboHotCold"` to the existing `FolderNames` and `Priorities` lists in the `[Mod]` section of `Memoria.ini`. Preserve the other entries and their order.

GitHub's automatic **Source code** archives do not include compiled DLLs. Use a release attachment or build the source yourself.

## Uninstall

Close the game, disable this mod in the launcher or remove its name from `FolderNames` / `Priorities`, then delete the `ChocoboHotCold` folder.

## Combining mods

FloatingHPBar and BattleInfoOverlay have cooperative initialisation logic. BattleInfoOverlay alone may appear only after the first battle action resolves.

Since radar 1.0.1, ChocoboHotCold no longer implements `IOverloadOnBattleInitScript`. It retains its existing attribute-based startup and camera/log callbacks. Automated checks against the installed Memoria 2025-07-13 resolver confirm that FloatingHPBar keeps its battle-init hook in either ordering. Radar 1.0 could claim that hook when placed above FloatingHPBar.

BattleInfoOverlay has Chinese interface labels, and NeroNames uses fixed Chinese names. Other text depends on the game and installed translation. Compatibility with large gameplay overhauls is not guaranteed.

## Building and packaging

Install a stable .NET SDK, version 8 or newer. Run these commands from the repository root:

```powershell
./build.ps1 -GamePath 'D:/Games/FINAL FANTASY IX'
./package.ps1 -GamePath 'D:/Games/FINAL FANTASY IX'
```

The build reads references from `x64/FF9_Data/Managed` in the supplied game directory and writes the DLL into this repository's `ChocoboHotCold/StreamingAssets/Scripts` directory. It does not install the mod or modify the game directory.

An additional Bash build entry point is provided; it has not been run on macOS or Linux:

```bash
bash build.sh '/path/to/FINAL FANTASY IX'
```

Source: `Source/ChocoboHotCold.cs`. Packages and SHA-256 checksums: `dist/`. Generated DLLs and `dist/` are ignored by Git; upload the ZIP separately as a Release attachment. Packaging refuses to overwrite an existing ZIP; rename or remove that ZIP before rebuilding a package.

## Startup compatibility check

On Windows with .NET Framework 4.x and FloatingHPBar installed, run:

```powershell
./tools/verify-hooks.ps1 -GamePath 'D:/Games/FINAL FANTASY IX'
```

This check invokes the real installed engine resolver for both mod orders and each mod alone. It inspects the startup attribute without executing Unity callbacks, and does not launch the game or modify saves. It does not test scene rendering.

## Reporting problems

Include the mod version, Memoria version, operating system, resolution, game language, active mod order, reproduction steps and relevant `Memoria.log` excerpts. Remove personal paths before sharing logs. Screenshots are helpful for layout problems.

## Author and license

Author: **JustPeace**. Original code and content in this mod are released under the [MIT License](LICENSE). Retain the copyright and license notice when redistributing.

FINAL FANTASY IX, Memoria and third-party mods remain the property of their respective rights holders. This license does not grant rights to their code or assets.