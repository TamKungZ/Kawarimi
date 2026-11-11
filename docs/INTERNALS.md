# Internals

Facts about the game (NSUNS4.exe build 5ef2f684) that the code relies on. Addresses are in `src/native/game.h`;
`scripts/re/exe_tools.py` helps with static analysis.

## Archives

* CPK payloads are CRILAYLA-compressed, then encrypted with the CyberConnect2 xorshift128 stream
  (seeds `154f048d 02b3d1cd 43a3d2f9 617e9602`, new state per file, little-endian mask). Encrypted files start `8f cf 31 40 76 41 95 d1`.
* Later archives win: `data/launch` < `data/patch/12` < `dlc`.

## xfbin (NUCC, big endian)

* Header: `"NUCC"`, version, u32 table size @16, counts @28 (types, paths, names, maps, map indices, extra), string tables,
  maps (type, path, name), extra pairs, map indices. Chunks start after the header; where exactly differs between small and large
  files (`XfbinFile.ChunkStartCandidates`).
* Chunk: u32 size, u32 map index, u16 version (may differ from the file version), u16 flags, data.
* A chunk's map index is relative to its page: `maps[indices[pageStart + index]]`; every `nuccChunkPage` advances `pageStart`
  by its first u32. References inside chunk data use the same scheme.
* Model chunk: NUD after a prefix, then u16 count + material references. Material chunk: references to texture chunks.

## NTP3 / NUD

* Texture chunk: 12-byte prefix, `NTP3`, textures as header + data. Format 8 = RGB565 big endian; mip sizes at header +0x30.
* NUD (`NDP3`, Smash 4 layout): polygons with u16 triangle strips (0xFFFF restart). Skinned vertices keep position/normal/weights in
  the second vertex clump and colour/UV in the first.

## Game state

* Character table: `[mgr+0xf8]` -> count +0x18, codes +0x20, records +0x28 (0x2d8 bytes, slot s at 8*s, costume index = slot - 1).
* Costume selector: vector of 40-byte entries `{charId, costumeIdx, ...}` at `[mgr+0x20]`; the LT/RT number is the position among a
  character's entries. Appending entries does not change the visible list.
* Files are read through CRI (`criFsBinder_Find` -> `criFsLoader_Load`); completion is polled through the game's file-slot wrappers.
  Kawarimi serves mod files there. Reporting completion through `criFsLoader_GetStatus` itself breaks fights (CRI reuses loader handles).

## Open

* Where the selector list is built (needed to list mods as costumes).
* A way to make the game reload the previewed model, so F7 could show the change immediately.
* Cross-character new-code skins hang the fight loader.
