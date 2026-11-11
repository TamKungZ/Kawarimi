# Modding

A costume is a code (`2ino` = Ino, second outfit) and its files are `data/spc/<code>*.xfbin`. Find codes with
`Kawarimi characters <name>` or in the log window (`COSTUME ...`) while hovering a costume.

## Project -> mod

```
Kawarimi new 2ino InoRed     projects\InoRed\  original\ edit\ InoRed.csproj Mod.cs
Kawarimi build InoRed        mods\InoRed\      InoRed.dll (skin.json without a .NET SDK) + files\
```

Share only `mods\<Name>\`.

## Building

Every way below writes the same `mods\<Name>\` (the C# dll plus `files\` with the changed xfbin). A running game reloads it within a few seconds.

| how | command | notes |
|---|---|---|
| Kawarimi | `Kawarimi build InoRed` | works without a .NET SDK (then `skin.json` instead of the dll) |
| all projects | `Kawarimi build --all` | one failing project does not stop the others |
| zip to share | `Kawarimi build InoRed --zip` | also writes `share\InoRed.zip`; others extract it into their `mods\` |
| .NET | `dotnet build` in `projects\InoRed\` | needs the .NET SDK 8 or later |
| IDE | Build in Visual Studio, Rider or VS Code | same as `dotnet build` |

`dotnet build` and IDEs build the mod through `kawarimi\Kawarimi.Mod.targets`, which the project's `.csproj` imports.
Projects made by older versions need this line before `</Project>`:

```xml
<Import Project="..\..\kawarimi\Kawarimi.Mod.targets" Condition="Exists('..\..\kawarimi\Kawarimi.Mod.targets')" />
```

MSBuild properties: `-p:KawarimiSkipMod=true` builds only the dll, `-p:KawarimiModsDir=<folder>` writes the mod somewhere else.

To run `Kawarimi` from any folder, run `kawarimi\Kawarimi.exe path add` once (the installer does this) and open a new terminal.
`Kawarimi path remove` undoes it.

## Editing textures

`edit\<file>\textures\tex_<chunk>_<index>_<w>x<h>.png`

* Any change. Any size: the image is scaled to the game texture's size on build, and the smaller mip levels are made from it.
* Keep the `tex_<chunk>_<index>_` start of the file name; the size part may change.
* Only RGB565 textures are exported. Other formats and new textures are not supported yet.

## Editing the model

`edit\<file>\model\<file>.obj` opens with its textures in Blender, Maya, 3ds Max and others. It is in meters, Y up, so it
stands upright and life-sized with the default OBJ import and export settings. Export it back to the same path with UVs and
normals; faces with more than three corners are split into triangles on build.

Each object is one part of the model with one material, named `c<chunk>_<set>_<index>_<name>` (for example
`c025_0_0_2ino00t0_body`). Kawarimi finds parts by these names; a suffix such as `.001` is ignored.

| you do | result in game |
|---|---|
| move, scale, sculpt | the part changes; nothing else in the file is touched |
| delete faces, add faces or vertices, change UVs, split into pieces | the part is rebuilt from the OBJ |
| delete an object | the part disappears |
| add an object with a new name | it joins the part nearest to it and uses that part's texture |
| rename an object | counts as deleting the old part and adding a new one |

A rebuilt part takes positions, UVs and normals from the OBJ. Bone weights, vertex colours and tangents come from the
nearest original vertex of the same part, so new geometry bends with the bones closest to it. New UVs map onto the
part's existing texture.

* At most 65534 vertices per part.
* Parts ending in `_lod1` are shown from a distance. Edit them too, or the change disappears when the camera moves away.
* Teeth and eyes sit at the origin (they are bone-relative).
* Parts that are only moved keep the game's normals unless you use `Kawarimi model import ... --normals`.

## Legacy mods

A folder with plain `<costume>*.xfbin` files and no `skin.json` or dll (directly, in `files\`, or in `data\spc\`) is loaded as a legacy mod: each costume code found (the first 4 characters of the file names) replaces that costume, like a mod built with `Kawarimi build`. The log and `mods-report.txt` say `legacy mod`. Put such folders in `mods\` or in `mods\legacy\`.

## Other commands

`Kawarimi help` lists all of them: `extract`/`encode` (archive files), `info`/`unpack`/`pack`/`retarget` (xfbin),
`textures`/`model export|import`, `skin compose|compare|verify` (new-code skins), `capture` (research).

## Troubleshooting

`kawarimi\mods-report.txt` says why a mod was not loaded. `kawarimi\kawarimi.log` has everything else.
