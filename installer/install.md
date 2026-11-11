# Kawarimi portable install

Portable build of Kawarimi, the skin loader for NARUTO SHIPPUDEN: Ultimate Ninja STORM 4. It needs no installer and no admin rights. For the installer with a Start menu entry and an uninstaller, use `KawarimiSetup-<version>.exe` instead.

## Requirements

* NSUNS4 installed from Steam (the folder that contains `NSUNS4.exe`).
* .NET 8 runtime (x64). Install it from https://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe if it is missing. `Kawarimi.exe` itself is self-contained and runs without it.
* Close the game before you extract.

## Install

1. Extract the zip **directly into the game folder**, the one that contains `NSUNS4.exe`. The zip holds `winmm.dll`, `kawarimi\` and this file at its top level, so nothing else changes.
2. If the game folder already has a `winmm.dll` from another mod or tool, rename it to `winmm.dll.pre-kawarimi` before you extract. The installer does the same, and uninstalling restores it.
3. Create a `mods` folder in the game folder if it is not there. Put each mod folder in it, for example `mods\InoRed\`.
4. Start the game. The Kawarimi log window opens next to it.

Optional: run `kawarimi\Kawarimi.exe path add` once to put Kawarimi on your PATH. After you open a new terminal, `Kawarimi build <Name>` works from any folder (add `--zip` to also get `share\<Name>.zip` to hand out, or `--all` to build every project). `Kawarimi path remove` undoes it.

Check that it works: in character select, hover a character. The log shows its costume, the LT/RT number and how many mods it has. **F7** switches to the next mod, **F8** back to the original.

## Layout

```
<game>\
  NSUNS4.exe
  winmm.dll              loader, loaded by the game at start
  kawarimi\
    Kawarimi.exe         tool: create and build mods, open this window
    Kawarimi.Host.dll    runtime hooked into the game
    kawarimi.log         written while the game runs
    mods-report.txt      why a mod was not loaded
    docs\ samples\       reference
  mods\                  your mods
```

## Uninstall

1. Close the game.
2. Delete `winmm.dll` from the game folder.
3. If `winmm.dll.pre-kawarimi` exists, rename it back to `winmm.dll`.
4. Delete the `kawarimi` folder. Delete `mods` too if you do not want to keep your mods.

## Troubleshooting

* The game starts but no log window appears: check that `winmm.dll` is in the game folder next to `NSUNS4.exe`, and that `kawarimi\Kawarimi.Host.dll` exists.
* A mod does not load: read `kawarimi\mods-report.txt`. Everything else is in `kawarimi\kawarimi.log`.
* `Kawarimi.exe` does not start: install the .NET 8 runtime (see Requirements).

Offline only. Game archives are never modified; Kawarimi only serves mod files while the game runs.

Full guide: `docs\MODDING.md` (making mods) and `docs\API.md` (C# mods). License: GPL-3.0-or-later, see `LICENSE.txt`.
