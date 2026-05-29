# Ghosts FastFile Format — Research Notes

Verified against PS3 retail samples: small patch FFs (`patch_mp_prisonbreak.ff`,
`patch_homecoming.ff`, `patch_mp_dome_ns.ff`), DLC-updated zones
(`mp_character_room_dlc_updated.ff`), UI patches (`patch_ui_mp.ff`,
1256 pool entries), and base zones (`ghosts ps3 common.ff`, 29.8 MB).
All Ghosts (IW6) version `0x22E`. No PC, Xbox 360, or Wii-U samples
have been tested; layout details below apply to PS3 only.

**Current implementation status:**
- ✅ Detect Ghosts via `IWff0100` outer magic + version `0x22E` + `IWffS100` inner magic
- ✅ Decompress and inflate to zone in one pass (`FastFileProcessor.TryDecompressGhosts`)
  - Outer raw-deflate blocks (2-byte BE srcSize + payload, 64 KB out each)
  - Inner per-asset zlib streams expanded inline against decoded `compressedLen` from each asset header
- ✅ Walk the asset pool (`FastFileLib.GhostsZoneLayout`) — header-counts-driven, works for **patch FFs, DLC-updated zones, and base zones**. Reads `tagCount` @ `0x28` and `assetCount` @ `0x30` to navigate past the tag-string region; falls back to a brute scan when header counts are missing or layout is unexpected.
- ✅ Asset pool tab populates in the editor for every tested zone type, with type names from `GhostsAssetTypePS3`.
- ✅ Pair pool entries with asset bodies for **rawfile** (zlib-wrapped short header), **scriptfile** (zlib-wrapped long header), and **luafile** (flat 16-byte header). Editor surfaces names + offsets + body bytes for these in both the Asset Pool tab and the Raw Files tab. Flat-binary types (xmodel/image/sound/techset/weapon/material/…) are listed but not opened — they'd each need their own struct parser.
- ✅ Luafile bodies feed `FastFileLib.LuaBytecodeInspector`, which surfaces an extracted-strings summary in the editor's text viewer (Lua source isn't in the FF — IW6 ships compiled bytecode with custom format byte `0x0D`).
- ❌ Per-asset content parsers for other types (weapon struct, image, sound, …) — not implemented; `GhostsGameDefinition.Parse*` methods all return null.
- ❌ Recompression / re-signing — not implemented and not viable without IW's RSA-2048 private key.

## TL;DR — IW6 PS3 vs the previous IW signed-FF design

Comparison column uses MW2 PC (IW4) as the reference because that's the
closest signed-FF format with a `DB_AuthHeader` shape that's well-documented
in this repo (see `MW2_PC_FastFile_Format.md`). MW3 (IW5) uses the same
authed-chunks shape.

| Aspect | MW2 PC (IW4) signed | **Ghosts (IW6) PS3** |
|---|---|---|
| Outer magic | `IWff0100` | `IWff0100` |
| Version bytes | `14 01 00 00` (LE, `0x114`) | `00 00 02 2E` (BE, `0x22E`) |
| Inner streaming magic | `IWffs100` (lowercase) | **`IWffS100` (capital S)** |
| Outer compression | Authed chunks: 8 KB chunks in groups of 257 (1 hash + 256 data), inner zlib | **Raw-deflate blocks (2-byte BE srcSize, 64 KB out each)** |
| Master block hash size | SHA-256 (32 bytes) | **SHA-1 (20 bytes + 12 zero pad in a 32-byte slot)** |
| `DB_AuthHeader` total size | 8144 bytes | 8144 bytes (same overall) |
| Pre-payload metadata after auth header | 48 bytes padding | 48 bytes padding + **112 KB "LO" region** |
| Asset pool entry format | `[type LE][ptr]` | `[ptr][type BE]` |

## FF layout (Ghosts PS3)

The same downstream layout follows from the location of the `IWffS100` inner
magic. Call its file offset **A**. The relationship `A + 0x20000 = start of
compressed deflate-block stream` is what makes both FF variants share one
extractor.

### Patch FFs

```
0x00000..0x00023   Outer header                       36 bytes
0x00024..0x01FF3   IWffS100 + DB_AuthHeader        8 144 bytes  (A = 0x24)
0x01FF4..0x02023   Padding                            48 bytes
0x02024..0x20023   "LO" region                    114 688 bytes  (14 × 0x2000)
0x20024..EOF       Raw-deflate block stream       rest of file
```

### Base FFs

Same structure, but `IWffS100` sits further into the file because there's an
extra index table between the outer header and the auth header. For the
`common.ff` sample, `IWffS100` is at file offset `0x3294` and the index
table runs `0x0C..0x3293` (12 KB). The downstream `A + 0x20000` rule still
locates the deflate stream.

```
0x00000..0x0000B   Outer header (12 bytes: magic + version)
0x0000C..0x03293   Index table (~12 KB; file-offset pairs of unknown semantics)
0x03294..0x05263   IWffS100 + DB_AuthHeader        8 144 bytes  (A = 0x3294)
0x05264..0x05293   Padding                            48 bytes
0x05294..0x23293   "LO" region                    114 688 bytes
0x23294..EOF       Raw-deflate block stream       rest of file
```

The index table's record layout has not been reverse-engineered. Its
contents include u32 file-offset pairs separated by `0x00000001` markers
and zero-padding rows; it isn't required for decompression and is skipped.

### Outer header

| Offset | Size | Field | Notes |
|---|---|---|---|
| `0x00` | 8 | `IWff0100` outer magic | ASCII; both variants |
| `0x08` | 4 | Version `0x0000022E` | BE u32 = 558; both variants |
| `0x0C` | 4 | Flags `01 00 04 04` | Patch FFs only; identical across the 3 patch samples. Base FFs use `0x0C` onwards for the index table. |

Patch-FF-only fields (`0x10..0x23`):

| Offset | Size | Field |
|---|---|---|
| `0x10` | 12 | Zeros |
| `0x1C` | 4 | File size (BE u32; matches `.ff` size exactly in all 3 patch samples) |
| `0x20` | 4 | Max file size (BE u32; equals `0x1C` in all 3 patch samples) |

For patch FFs the outer header ends at `0x23` and `IWffS100` follows
immediately at `0x24`. Base FFs put the index table at `0x0C` onwards
instead; an analogous file-size field has not been located inside the
base-FF index table.

### `DB_AuthHeader` (8144 bytes from `IWffS100`)

Layout matches IW5 (MW3)'s `DB_AuthHeader` except that hashes are SHA-1
stored in 32-byte slots (20 bytes hash + 12 bytes zero pad) rather than full
32-byte SHA-256. Offsets are relative to `A` (the file offset of
`IWffS100`).

| Offset (from A) | Size | Field |
|---|---|---|
| `+0x00` | 8 | `IWffS100` inner magic |
| `+0x08` | 4 | Reserved (zeros) |
| `+0x0C` | 32 | `subheaderHash` — 20 B SHA-1 + 12 B zero pad |
| `+0x2C` | 256 | RSA-2048 signature (Activision public key) |
| `+0x12C` | 32 | `fastfileName` — ASCII, null-padded |
| `+0x14C` | 4 | Reserved (zeros) |
| `+0x150` | 7808 | `masterBlockHashes[244]` — 244 × (20 B SHA-1 + 12 B zero pad) |

Verified across all 4 samples: 244 master-block-hash slots, each with 20
bytes of nonzero hash data and 12 zero bytes of padding. The 12-byte pad is
not random data, it's literal zero bytes.

### Padding (`A+0x1FD0..A+0x2000`)

48 bytes. Not all zero — contains residual bytes whose source/meaning is
not understood. Decompression skips this region.

### "LO" region (`A+0x2000..A+0x20000`)

14 chunks of `0x2000` (8 KB) each = `0x1C000` bytes. Every byte in this
region has bit 7 = 0 (values strictly in `0x00..0x7F`); the constraint is
deliberate, not statistical (verified by bit-plane entropy: bit 7 has
P(1) = 0.0000, bits 0-6 each have P(1) ≈ 0.5). Purpose has not been
reverse-engineered.

Bit-plane analysis rules out it being any standard stream cipher
(AES-CTR/CBC, Salsa20, ChaCha20, RC4) or a repeating-XOR/Vigenère cipher,
since all of those produce uniform 8-bit output. The region's purpose
inside the engine hasn't been verified — only that the toolchain does
not need it to recover the zone payload.

### Raw-deflate block stream (`A+0x20000..EOF`)

```
[srcSize BE u16][raw-deflate payload, srcSize bytes]
[srcSize BE u16][raw-deflate payload, srcSize bytes]
...
[srcSize BE u16][raw-deflate payload]
```

Each block decompresses to exactly **`0x10000` bytes** (64 KB) — no end
marker, the stream simply ends when EOF is reached and the last block may
be a full block (no short-block convention has been observed).

Verified block counts:

| File | FF size | Compressed payload bytes | Blocks | Decompressed (raw zone) |
|---|---|---|---|---|
| `patch_mp_dome_ns.ff` | 145,493 B | 14,385 | 1 | 65,536 |
| `patch_homecoming.ff` | 194,709 B | 63,601 | 2 | 131,072 |
| `ghosts_patch_common_mp.ff` | 525,354 B | 394,246 | 9 | 589,824 |
| `ghosts ps3 common.ff` | 29,796,135 B | 29,652,115 | 717 | 46,989,312 |

"Compressed payload bytes" is `file_size − (A + 0x20000)` — the bytes
available to the deflate-block stream. The block walker consumes most of
these; the slack (e.g. 18 bytes for `ghosts_patch_common_mp.ff`) is
trailing data that follows the last complete block.

## Zone format (after the outer deflate, before inner-zlib inflation)

Same shape as CoD4/WaW/MW2: a fixed XFile header gives counts that drive
navigation through the tag pointer table and tag strings, then the asset
pool follows, then asset bodies. The layout is uniform across patch / DLC /
base zones — only the count values differ.

```
0x00..0x27   Fixed XFile fields (zone size, block sizes, …)
0x28..0x2B   tagCount   (BE u32)
0x2C..0x2F   placeholder
0x30..0x33   assetCount (BE u32)
0x34..0x37   placeholder
0x38..?      Either count3+placeholder (zones with tagCount > 0)
             or first pool entry (patch FFs where tagCount == 0)
...          Tag pointer placeholders (4 bytes × tagCount)
...          Tag strings (tagCount null-terminated ASCII)
...          Asset pool (8 bytes × assetCount)
...          Asset bodies (back-to-back)
```

### XFile header counts

| Sample | tagCount | assetCount | Pool offset |
|---|---:|---:|---:|
| `patch_mp_prisonbreak.zone` | 0 | 4 | `0x38` |
| `patch_mp_dome_ns.zone` | 0 | 4 | `0x38` |
| `patch_homecoming.zone` | 0 | 4 | `0x38` |
| `ghosts_patch_common_mp.zone` | 0 | 122 | `0x38` |
| `mp_character_room_dlc_updated.zone` | 212 | 1880 | `0xED5` |
| `patch_ui_mp.zone` | 249 | 1256 | `0x1997` |
| `ghosts ps3 common.zone` | varies | varies | varies |

**Pool location rule** (implemented in `FastFileLib.GhostsZoneLayout.LocatePool`):

1. **`tagCount == 0`** → pool starts at `0x38` immediately after the
   `assetCount` placeholder. This covers most patch FFs.
2. **`tagCount > 0`** → skip `tagCount × 4` placeholder bytes starting at
   `0x3C`, then skip `tagCount` null-terminated tag strings; the pool
   follows. A 32-byte probe window forward of the strings handles a
   small trailing field (purpose unknown — verified in
   `mp_character_room_dlc_updated.zone`: bytes `00 00 00 30` sit between
   the last tag's null and pool[0]).
3. **Fallback** brute-scan for the longest run of valid pool entries —
   used when header counts are missing or layout is unexpected.

### Asset pool

Each entry is 8 bytes. Pool entries use **`[pointer placeholder][type ID]`**
order (pointer first), matching MW2 PS3 — not the `[type][pointer]` order
used by MW2 PC.

```
PP PP PP PP  00 00 00 XX
└─ pointer ┘ └─ type ─┘    (type word: BE u32, high 3 bytes zero, low byte = type ID ≤ 0x35)
```

The pointer field has **four observed conventions** — any 4-byte value is
accepted by the pool walker; the strict type-word structure plus the
header's `assetCount` cap are what delimit the pool:

| Pattern | Meaning |
|---|---|
| `FF FF FF FF` | Standard inline placeholder (most common) |
| `00 00 00 00` | NULL — seen on the first scriptfile entry of `patch_mp_prisonbreak.zone` |
| `80 XX XX XX` | High-bit-set resolved pointer (CoD4/WaW convention) |
| `40 XX XX XX` | **0x40-flagged resolved pointer** — used by IW6, e.g. `40 1F DF 85` in `patch_ui_mp.zone` (material → image references). Earlier code that only accepted the `0x80`-flag form found 87 / 1256 entries in this zone. |

### Asset type IDs (IW6 PS3)

54 types in the IW6 PS3 enum. PC and other platform variants shift IDs
because some types are platform-only (e.g. `computeshader`,
`hullshader`, `domainshader`, `vertexdecl` are PC only) — those are not
covered here.

| ID | Type | ID | Type | ID | Type |
|---|---|---|---|---|---|
| 0x00 | physpreset | 0x12 | aipaths | 0x24 | aitype |
| 0x01 | phys_collmap | 0x13 | vehicle_track | 0x25 | mptype |
| 0x02 | xanim | 0x14 | map_ents | 0x26 | character |
| 0x03 | xmodelsurfs | 0x15 | fx_map | 0x27 | xmodelalias |
| 0x04 | xmodel | 0x16 | gfx_map | 0x28 | rawfile |
| 0x05 | material | 0x17 | lightdef | 0x29 | scriptfile |
| 0x06 | vertexshader | 0x18 | ui_map | 0x2A | stringtable |
| 0x07 | pixelshader | 0x19 | font | 0x2B | leaderboarddef |
| 0x08 | techset | 0x1A | menufile | 0x2C | structureddatadef |
| 0x09 | image | 0x1B | menu | 0x2D | tracer |
| 0x0A | sound | 0x1C | animclass | 0x2E | vehicle |
| 0x0B | sndcurve | 0x1D | localize | 0x2F | addon_map_ents |
| 0x0C | lpfcurve | 0x1E | attachment | 0x30 | netconststrings |
| 0x0D | reverbsendcurve | 0x1F | weapon | 0x31 | reverbpreset |
| 0x0E | loaded_sound | 0x20 | snddriverglobals | 0x32 | luafile |
| 0x0F | col_map | 0x21 | fx | 0x33 | scriptable |
| 0x10 | com_map | 0x22 | impactfx | 0x34 | equipsndtable |
| 0x11 | glass_map | 0x23 | surfacefx | 0x35 | dopplerpreset |

The table above is the IW6 PS3 column. Xbox 360 IDs are shifted −1 from
PS3 for everything ≥ `0x07` (no `vertexshader` slot). Wii-U adds
`fonticon` at `0x1A` and shifts the rest accordingly. PC IDs differ more
substantially because PC has `computeshader`, `hullshader`, `domainshader`,
and `vertexdecl` slots that consoles don't. Only PS3 IDs are wired up in
the code (`FastFileLib.GameDefinitions.GhostsAssetTypePS3`); other
platforms aren't tested.

### Per-asset data (after the pool)

Each asset entry is a small header followed by the asset's content. Two
distinct header shapes have been observed in PS3 zones, distinguished by
how many `0xFF` bytes immediately precede the asset name.

#### Notation used in the diagrams below

- `u8` / `u16` / `u32` / `u64` — unsigned integers of N bytes (1 / 2 / 4 / 8).
- `BE` — big-endian: most-significant byte first. PS3 is PowerPC, so every
  multi-byte integer in IW6 PS3 zones is big-endian.
  Example: bytes `00 00 0A E6` as a `u32 BE` decode to `0x00000AE6` = 2790.
- `LE` — little-endian (not used in IW6 PS3; appears in MW2 PC comparisons).
- `compLen` / `decLen` — compressed length / decompressed length, in bytes.
  In every asset header verified so far, `compLen` is the exact zlib stream
  byte count and `decLen` matches `len(zlib.decompress(stream))`.
- `<name>\0` — null-terminated ASCII string, the asset's name (e.g.
  `mp/constbaselines/bl_ps3_mp_boneyard_ns_war.bin`).

Three worked examples (all short-shape headers from PS3 retail). The header
is always 16 bytes — 4 leading `FF`s, the two `u32 BE` size fields, then 4
trailing `FF`s — followed by the null-terminated name and the asset body.

**Binary rawfile** — `mp/constbaselines/bl_ps3_mp_boneyard_ns_war.bin`:

```
Bytes:  FF FF FF FF 00 00 0A E6 00 00 94 00 FF FF FF FF
        └─ FF*4 ──┘ └─compLen─┘ └─ decLen─┘ └─ FF*4 ──┘
                     u32 BE      u32 BE
                     = 2 790      = 37 888
```

Body is a 2,790-byte zlib stream that decompresses to 37,888 bytes of binary
constbaseline data (PS3 netcode snapshot, not human-readable).

**Text rawfile** — `vision/mp_alien_town_thermal.vision`:

```
Bytes:  FF FF FF FF 00 00 03 39 00 00 0E E7 FF FF FF FF
        └─ FF*4 ──┘ └─compLen─┘ └─ decLen─┘ └─ FF*4 ──┘
                     u32 BE      u32 BE
                     =   825      =  3 815
```

Body is an 825-byte zlib stream that decompresses to 3,815 bytes of ASCII
vision config (`r_glow "0"`, `r_glowBloomCutoff "0.99"`, …).

**Text rawfile** — `maps/mp/mp_skeleton_fx.gsc`:

```
Bytes:  FF FF FF FF 00 00 03 6C 00 00 13 C6 FF FF FF FF
        └─ FF*4 ──┘ └─compLen─┘ └─ decLen─┘ └─ FF*4 ──┘
                     u32 BE      u32 BE
                     =   876      =  5 062
```

Body is an 876-byte zlib stream that decompresses to 5,062 bytes of GSC source
(`main() { level._effect[ "vfx_sunflare_midday_white" ] = loadfx(...); … }`).

All three were verified by reading bytes 0–15 of the file the editor's rawfile
extractor produced for each asset. After `InflateGhostsZoneAssets` runs the
zone holds the decompressed body in place of the zlib stream, but the per-asset
header keeps the original on-disk `compLen` / `decLen` as metadata.

**"Long" shape** (8 trailing FFs — 28 bytes header + name + content):
```
[≥4 FF bytes][compLen u32 BE][decLen u32 BE][??? u32 BE][8 FF bytes]<name>\0
```
Verified for 15 scriptfile (type `0x29`) assets in
`ghosts_patch_common_mp.zone`. Whether other types (MPTYPE / AITYPE /
other script-like types) also use this shape hasn't been verified — none
of the 4 test samples contain enough non-scriptfile zlib-wrapped assets
to confirm. The third u32 is not a size — values observed range from
below `decLen` (e.g. 3 for a 6-byte asset) to far above (e.g. 12459 for a
3031-byte asset). Its meaning is unconfirmed; possible interpretations
include an engine allocation hint or asset-type-specific count, but
neither has been tested.

**"Short" shape** (4 trailing FFs — 16 bytes header + name + content):
```
[≥4 FF bytes][compLen u32 BE][decLen u32 BE][4 FF bytes]<name>\0
```
Verified for rawfile (type `0x28`) assets across multiple zones —
`animscripts/animset` and `vision/mpnuke.vision` in `ghosts ps3 common.zone`,
`mp/constbaselines/bl_*.bin` in `patch_mp_dome_ns.zone`.

In both shapes the first u32 after the leading-FF block is the exact
compressed byte count of the zlib stream that follows. Verified across 20+
assets across the four samples: `compLen` matches the actual zlib stream
length exactly, `decLen` matches `len(zlib.decompress(stream))` exactly.

The asset content following the header is either:
- A standard zlib stream (`0x78 DA` / `0x78 9C` / `0x78 5E` / `0x78 01` magic),
  in which case `compLen` and `decLen` are populated. Used by every
  scriptfile and rawfile asset observed in patch zones (e.g. all 122
  scriptfile+rawfile entries in `ghosts_patch_common_mp.zone` are
  zlib-wrapped). Used sparingly by base zones — `common.zone` has 181
  inner zlib streams across its 47 MB body, almost all of them rawfile
  assets named like `animscripts/*`, `maps/*.gsc`, `vision/*.vision`.
- Flat binary data with no inner zlib. Used by asset types like xmodel /
  image / sound / world data — the outer deflate already compresses these
  once, so a second zlib pass would produce nearly-identical bytes.

The stream count detected by a scan-for-zlib-magic pass doesn't always
exactly match the pool entry count, because the detection can trip on
data inside other assets that happens to look like a zlib stream
preceded by FF-bracketed-name bytes. For pool-based asset enumeration,
walk the pool directly rather than scanning for zlib magic.

The toolchain's `InflateGhostsZoneAssets` expands every inner zlib stream
inline as part of decompression, so a zone produced by this codebase has
no remaining `78 XX` streams (verified: 0 residual streams in all four
samples after `TryDecompressGhosts` completes).

### Luafile bodies (flat 16-byte header — *not* zlib-wrapped)

`luafile` (type `0x32`) uses a different in-zone layout than rawfile /
scriptfile — flat header, body is plain Lua bytecode (compiled, not
compressed). Verified against `patch_ui_mp.zone` (85 luafile pool entries,
86 located bodies including one trailing entry not referenced by the pool).

```
[FF*4][size BE u32][unk u32][FF*4]<name>\0<Lua 5.1 bytecode>
```

- 16 bytes header.
- `size` is the exact byte count of the bytecode body that follows the name.
- `unk` is consistently `0x02000000` across every observed entry. Purpose
  unconfirmed — possibly a fixed type tag or chunk marker. The luafile
  scanner doesn't depend on its value.
- Name is path-style ASCII ending in `.lua`. The walker requires this
  suffix to distinguish luafiles from any other flat-header asset types.

Bodies always start with the Lua 5.1 signature `1B 4C 75 61 51` (`\x1B
LuaQ`). The signature check is what makes the scan safe against random
`[FF*4][u32][u32][FF*4]` byte sequences in dense asset data.

Stride from one luafile to the next is `nameEnd + 1 + size`. Handled by
`FastFileLib.GhostsZoneLayout.LocateAllLuaFiles`.

### Lua bytecode (IW6 custom format byte `0x0D`)

The Lua source is **not** in the FF — IW6's build pipeline compiles `.lua`
→ bytecode and ships only the bytecode. Recovering readable source needs
an external Lua 5.1 decompiler (e.g. `luadec`).

The 12-byte Lua header is standard except for the format byte:

| Offset | Value | Meaning |
|---|---|---|
| `0x00` | `1B` | escape |
| `0x01..0x03` | `Lua` | signature |
| `0x04` | `51` | Lua version 5.1 |
| `0x05` | **`0D`** | **format byte — non-zero = IW6 custom dialect** |
| `0x06` | `00` | endianness flag (declared BE — but length fields in the chunk body are actually LE) |
| `0x07` | `04` | sizeof(int) |
| `0x08` | `04` | sizeof(size_t) |
| `0x09` | `04` | sizeof(Instruction) |
| `0x0A` | `04` | sizeof(lua_Number) — **single-precision** (stock Lua 5.1 default is 8) |
| `0x0B` | `00` | integral flag (floating-point lua_Number) |

The chunk body past the 12-byte header doesn't follow stock Lua 5.1 chunk
layout — IW6 uses a customized chunk format whose string-constant length
prefix is 1 byte (not `sizeof(size_t)`) at least for the type-registry
strings (`TNIL`, `TBOOLEAN`, `TLIGHTUSERDATA`, …) at the chunk start. Full
chunk layout hasn't been reverse-engineered.

`FastFileLib.LuaBytecodeInspector` works around the custom format by
**not** parsing chunks: it reads the 12-byte header for metadata and then
ASCII-scans the body for every printable null-terminated run of length
3–256. That surfaces the useful signal (menu / widget / function /
identifier names) without depending on chunk structure — e.g. for
`ui/lui/mp_menus/clandetails.lua` it produces ~803 extracted strings
including `OnCreate`, `UpdateClanDetails`, `clan_details_main`,
`MenuBuilder`, etc., letting a reader understand what the menu does.

## How the editor handles Ghosts

All Ghosts pool-layout, header-scan, pairing, and Lua-summary logic lives in
`FastFileLib`. The editor classes are thin shims that adapt library DTOs to
the editor's model types — same pattern as `RawFileScanner` ↔ `RawFileParser`.

| Component | Behaviour |
|---|---|
| `FastFileInfo.DetectGameVersion` | Recognises version `0x22E` as Ghosts; reports platform as PS3 (signed magic with this version is not Xbox 360) |
| `FastFile.IsXbox360` | Excludes `IsGhostsFile` — Ghosts uses signed magic on PS3 retail, so `IsSigned` doesn't imply Xbox 360 |
| `GameDefinitionFactory.GetDefinition` | Returns `_ghostsPs3` for Ghosts; throws for other unsupported games as before |
| `FastFileProcessor.Decompress` | Short-circuits to `TryDecompressGhosts` before the shared CoD4/WaW/MW2 dispatch |
| `FastFileProcessor.TryDecompressGhosts` | Two passes: outer raw-deflate blocks → raw zone, then `InflateGhostsZoneAssets` expands inner zlib streams inline |
| `FastFileProcessor.TryReadGhostsAssetHeader` | Recognises both "long" (8 trailing FFs) and "short" (4 trailing FFs) header shapes; reads `compLen` from the first u32 after the leading-FF block |
| `FastFileLib.GhostsZoneLayout` | Library home for pool location (`LocatePool` + `WalkPool`), wrapped-asset header scan (`LocateAllHeaders`), luafile scan (`LocateAllLuaFiles`), and positional pool↔header pairing (`PairPoolWithHeaders`). Header-counts-driven, handles patch + DLC + base zones. |
| `FastFileLib.LuaBytecodeInspector` | Parses Lua 5.1 header + ASCII-scans body for null-terminated printable strings. Format-agnostic to handle IW6's custom format byte `0x0D`. |
| `ZoneFile.Load` | For Ghosts, skips `ReadHeaderFields` / `StructureBasedZoneParser` (those depend on PS3-MW2 header offsets) and runs `GhostsZoneParser` instead |
| `Editor: GhostsZoneParser` | Thin shim over `GhostsZoneLayout.ParsePool` — translates `GhostsPoolEntry` DTOs to `ZoneAssetRecord` and writes them onto the `ZoneFile`. |
| `Editor: GhostsAssetWalker` | Thin shim over `GhostsZoneLayout.LocateAllHeaders` + `LocateAllLuaFiles` + `PairPoolWithHeaders`. Mutates each `ZoneAssetRecord` with resolved offsets/names; emits `RawFileNode`s for rawfile + luafile entries (the luafile's `RawFileContent` is the `LuaBytecodeInspector` summary). |
| `GhostsGameDefinition` | Maps the 54 IW6 PS3 asset type IDs to names. All `Parse*` content methods return null — content is sourced via the walker instead. |
| `MainWindowForm.OpenFastFile` | Skips the asset-selection dialog for Ghosts (most types still have no content parsers); otherwise runs the normal flow so the asset-pool tab populates. |
| `UIManager.UpdateLoadedFileNameStatusStrip` | Includes `IsGhostsFile` in the `gameString` branch so the status bar shows `Ghosts: <name>` for IW6 files. |
| `ZoneHexViewForm` | Uses `GhostsGameDefinition.GetAssetTypeName` for the type column; per-asset content panels stay empty for non-wrapped/non-lua types |

## Known unknowns

- **XFile header field semantics.** Counts at `0x28` and `0x30` are mapped
  (tagCount + assetCount); other u32s in `0x00..0x27` are zone size + block
  size slots but exact roles unconfirmed. The pool walker doesn't need them.
- **Trailing field between tag strings and pool.** DLC zones have 4 bytes
  between the last tag string's null and pool[0] (`00 00 00 30` in
  `mp_character_room_dlc_updated.zone`). Purpose unconfirmed — `LocatePool`
  probes a 32-byte forward window to skip past it.
- **Asset pool trailing entry.** Header `assetCount` is consistently one
  greater than the count my walker locates (1879 vs 1880 in DLC zone, 1255
  vs 1256 in patch_ui_mp, 122 vs 122 in ghosts_patch_common_mp where they
  match). The last "entry" doesn't have a valid type byte and reads like a
  sentinel; impact is cosmetic.
- **Base FF index table.** The 12 KB table between the outer header and
  `IWffS100` in base FFs has a clear repeating structure (offset pairs +
  markers) but its record format and purpose are not reverse-engineered.
  It's not needed for decompression.
- **"LO" region.** Bit-7 is deliberately zero across all 112 KB but the
  encoding/meaning of the remaining 7-bit-per-byte payload is unknown.
- **Per-asset header's third u32 (long shape).** Not a size. Values seen
  range from less than `decLen` (3 vs 6) to several times `decLen` (12459
  vs 3031). Interpretation unconfirmed.
- **Luafile `unk` field.** Consistently `0x02000000`. Possibly a fixed
  chunk-type tag or flag word. Treated as opaque metadata.
- **IW6 Lua bytecode chunk format.** Custom format byte `0x0D`. Past the
  12-byte header, chunk layout doesn't follow stock Lua 5.1 — string
  constants use a 1-byte length prefix in the type-registry preamble at
  least. Full chunk layout not reverse-engineered;
  `LuaBytecodeInspector` works around this with an ASCII-run scan.
- **Asset-content layouts for non-wrapped types.** No internal struct
  parsing for xmodel / image / sound / weapon / techset / material /
  stringtable / etc. Each would need its own struct reverse-engineering.
- **Pointer flag bits.** Pool pointers use both `0x80......` (CoD4/WaW
  high-bit convention) and `0x40......` (IW6-specific). Meaning of the
  flag bits — whether they're heap region tags, alignment hints, or
  something else — isn't confirmed; the masked-off offset value usually
  lands inside the zone so they're probably both runtime address tags.
- **Non-PS3 platforms.** Xbox 360, Wii-U, and PC variants of IW6 use shifted
  asset type IDs (Xbox 360 has no `vertexshader`; PC adds `computeshader`,
  `hullshader`, `domainshader`, `vertexdecl`; Wii-U has `fonticon` at `0x1A`).
  No samples tested; only PS3 IDs are wired up.
- **Re-signing.** Saving an edited Ghosts FF would require IW's RSA-2048
  private key for the `DB_AuthHeader` signature. Not viable.

## Reference samples

| Sample | Size | Variant | Pool entries | Notable |
|---|---|---|---:|---|
| `patch_mp_prisonbreak.ff` | small | patch | 4 | 1 scriptfile + 3 rawfile. First scriptfile uses NULL pointer in pool entry |
| `patch_mp_dome_ns.ff` | 145 KB | patch | 4 | 2 rawfile + 2 scriptfile |
| `patch_homecoming.ff` | 195 KB | patch | 4 | all scriptfile |
| `ghosts_patch_common_mp.ff` | 525 KB | patch | 122 | 120 scriptfile + 2 rawfile |
| `patch_ui_mp.ff` | 31 MB | DLC patch | 1256 | tagCount=249, mixed types (510 image + 476 material + 85 luafile + 68 rawfile + 57 techset + …). First sample to exercise `0x40`-flagged pool pointers |
| `mp_character_room_dlc_updated.ff` | 26 MB | DLC updated | 1880 | tagCount=212, mostly xmodel (1846) + techset (29) + xanim (3) + rawfile (1). First sample with non-zero `tagCount` |
| `ghosts ps3 common.ff` | 29.8 MB | base | varies | full character / weapon / vfx pool |

All PS3 retail. All produce 0 residual zlib streams after
`FastFileProcessor.TryDecompress` completes.
