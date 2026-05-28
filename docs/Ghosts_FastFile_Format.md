# Ghosts FastFile Format — Research Notes

Verified against 4 retail PS3 samples: `ghosts_patch_common_mp.ff` (525 KB
patch), `patch_homecoming.ff` (195 KB patch), `patch_mp_dome_ns.ff` (145 KB
patch), `ghosts ps3 common.ff` (29.8 MB base). All Ghosts (IW6) version
`0x22E`. No PC, Xbox 360, or Wii-U samples have been tested; layout details
below apply to PS3 only.

**Current implementation status:**
- ✅ Detect Ghosts via `IWff0100` outer magic + version `0x22E` + `IWffS100` inner magic
- ✅ Decompress and inflate to zone in one pass (`FastFileProcessor.TryDecompressGhosts`)
  - Outer raw-deflate blocks (2-byte BE srcSize + payload, 64 KB out each)
  - Inner per-asset zlib streams expanded inline against decoded `compressedLen` from each asset header
- 🟡 Walk the asset pool (`GhostsZoneParser`) — works for **patch FFs only**. Patches use 8-byte `[FF*4][type BE u32]` pool entries starting at `0x40`; base FFs use a different layout (a long run of pointer placeholders with type information stored elsewhere) and aren't walked correctly yet.
- ✅ Asset pool tab populates in the editor for patch FFs (type names from `GhostsAssetTypePS3`); base FFs decompress + inflate fine but the asset-pool tab stays empty.
- ❌ Per-asset content parsers (rawfile body, scriptfile body, weapon struct, etc.) — not implemented; all `Parse*` methods on `GhostsGameDefinition` return null
- ❌ Recompression / re-signing — not implemented and not viable without IW's RSA-2048 private key

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

```
0x000   XFile header
0x040   Asset pool             (8 bytes per entry, terminated by pattern break)
....    Per-asset data         (back-to-back asset entries)
```

### XFile header

The first 64 bytes (`0x00..0x3F`) contain a sequence of u32 BE fields:
zone size at `0x00`, several block-size slots at `0x08..0x2F`, then count
+ pointer-placeholder pairs around `0x28..0x37`. The exact field layout
has not been fully mapped; in particular the layout differs between
patch and base zones.

**Patch zones** (e.g. `patch_homecoming.zone`, `ghosts_patch_common_mp.zone`):
the asset pool starts at `0x40` immediately after the XFile header, with
8-byte `[ptr][type]` entries terminated by the pattern breaking. The pool
walker (`GhostsZoneParser`) finds it by scanning the first `0x200` bytes
for the longest contiguous run of valid
`[FFFFFFFF][type BE u32 ≤ 0x35]` 8-byte records.

**Base zones** (e.g. `common.zone`): different layout that hasn't been
reverse-engineered. Bytes from `0x40` onwards are a long run of pointer
placeholders (continuous `FF FF FF FF`) rather than 8-byte `[ptr][type]`
records, suggesting the type information is stored separately (perhaps a
parallel type array later in the zone). The current pool walker doesn't
recognise this layout and the asset-pool tab stays empty for base zones.

### Asset pool (patch zones only)

In patch zones each entry is 8 bytes. Pool entries use
**`[pointer placeholder][type ID]`** order (pointer first), matching
MW2 PS3 — not the `[type][pointer]` order used by MW2 PC.

```
FF FF FF FF  00 00 00 XX
└─ pointer ┘ └─ type ─┘    (BE u32, high 3 bytes zero, low byte = type ID)
```

The pool starts at `0x40` immediately after the XFile header. Verified
for the 3 patch samples — `patch_homecoming.zone` (4 entries, all
scriptfile), `patch_mp_dome_ns.zone` (4 entries — 2 rawfile + 2
scriptfile), `ghosts_patch_common_mp.zone` (122 entries — 120
scriptfile + 2 rawfile).

Base zones use a different layout (see XFile header section above) and
this 8-byte `[ptr][type]` walking doesn't apply.

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

## How the editor handles Ghosts

| Component | Behaviour |
|---|---|
| `FastFileInfo.DetectGameVersion` | Recognises version `0x22E` as Ghosts; reports platform as PS3 (signed magic with this version is not Xbox 360) |
| `FastFile.IsXbox360` | Excludes `IsGhostsFile` — Ghosts uses signed magic on PS3 retail, so `IsSigned` doesn't imply Xbox 360 |
| `GameDefinitionFactory.GetDefinition` | Returns `_ghostsPs3` for Ghosts; throws for other unsupported games as before |
| `FastFileProcessor.Decompress` | Short-circuits to `TryDecompressGhosts` before the shared CoD4/WaW/MW2 dispatch |
| `FastFileProcessor.TryDecompressGhosts` | Two passes: outer raw-deflate blocks → raw zone, then `InflateGhostsZoneAssets` expands inner zlib streams inline |
| `FastFileProcessor.TryReadGhostsAssetHeader` | Recognises both "long" (8 trailing FFs) and "short" (4 trailing FFs) header shapes; reads `compLen` from the first u32 after the leading-FF block |
| `ZoneFile.Load` | For Ghosts, skips `ReadHeaderFields` / `StructureBasedZoneParser` (those depend on PS3-MW2 header offsets) and runs `GhostsZoneParser` instead |
| `GhostsZoneParser` | Searches first `0x200` bytes for the longest run of valid `[FFFFFFFF][type ≤ 0x36 BE u32]` entries, then walks until the pattern breaks |
| `GhostsGameDefinition` | Maps the 54 IW6 PS3 asset type IDs to names. All `Parse*` content methods return null — pool listing only |
| `MainWindowForm.OpenFastFile` | Skips the asset-selection dialog for Ghosts (no content parsers, nothing to select); otherwise runs the normal flow so the asset-pool tab populates |
| `ZoneHexViewForm` | Uses `GhostsGameDefinition.GetAssetTypeName` for the type column; per-asset content panels stay empty |

## Known unknowns

- **XFile header structure.** Block-size fields, `AssetCount` location, and any
  additional metadata fields between the XFile header and the asset pool are
  not mapped. The pool walker works around this by pattern-matching pool entries.
- **Base FF index table.** The 12 KB table between the outer header and
  `IWffS100` in base FFs has a clear repeating structure (offset pairs +
  markers) but its record format and purpose are not reverse-engineered.
- **"LO" region.** Bit-7 is deliberately zero across all 112 KB but the
  encoding/meaning of the remaining 7-bit-per-byte payload is unknown.
- **Per-asset header's third u32 (long shape).** Not a size. Values seen
  range from less than `decLen` (3 vs 6) to several times `decLen` (12459
  vs 3031). Interpretation unconfirmed.
- **Asset-content layouts.** No internal struct parsing for any asset type
  on Ghosts. `GhostsGameDefinition.Parse*` all return null.
- **Non-PS3 platforms.** Xbox 360, Wii-U, and PC variants of IW6 use shifted
  asset type IDs (Xbox 360 has no `vertexshader`; PC adds `computeshader`,
  `hullshader`, `domainshader`, `vertexdecl`; Wii-U has `fonticon` at `0x1A`).
  No samples tested; only PS3 IDs are wired up.
- **Re-signing.** Saving an edited Ghosts FF would require IW's RSA-2048
  private key for the `DB_AuthHeader` signature. Not viable.

## Reference samples

| Sample | Size | Variant | Block count | Inflated zone |
|---|---|---|---|---|
| `patch_mp_dome_ns.ff` | 145,493 B | patch | 1 | 153,118 B |
| `patch_homecoming.ff` | 194,709 B | patch | 2 | 179,579 B |
| `ghosts_patch_common_mp.ff` | 525,354 B | patch | 9 | 799,277 B |
| `ghosts ps3 common.ff` | 29,796,135 B | base | 717 | 47,689,203 B |

All four are PS3 retail. All four produce 0 residual zlib streams after
`FastFileProcessor.TryDecompress` completes.
