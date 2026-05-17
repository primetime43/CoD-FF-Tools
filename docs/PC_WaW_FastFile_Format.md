# PC WaW FastFile Format — Research Notes

Verified from 5 real PC WaW samples (`default.ff`, `mp_makin_day_load.ff`, `credits.ff`,
`patch.ff`, `patch_mp.ff`). Use this document to plan write-side support (issue #21).

## TL;DR — PC WaW is structurally different from PS3/Xbox 360 WaW

| Aspect | PS3 / Xbox 360 WaW | **PC WaW** |
|---|---|---|
| Compression | 64KB blocks, raw deflate, **BE** 2-byte lengths | **Single zlib stream** (no blocks, no length prefixes) |
| Endianness | Big | **Little** (header bytes, zone fields, asset entries) |
| Version bytes | `00 00 01 83` (BE) | `83 01 00 00` (LE) |
| Zone header size | 52 bytes (0x34) | 52 bytes (0x34) — **same** as PS3 (initial 56-byte guess was wrong, see Correction below) |
| End marker | `0x00 0x01` after last block | None — just the natural end of the zlib stream |
| MemAlloc1 (`BlockSizeTemp`) | Fixed `0x10B0` | **Computed per zone** (varies: 28, 264, 484, 2,098,020, 2,656 observed) |
| Asset entry format | `[type][ptr]` (BE) | `[type][ptr]` (LE) — same shape as PS3, just little-endian type field |

## FF (compressed) layout

```
00..07  IWffu100              (8 bytes, ASCII)
08..0B  83 01 00 00           (4 bytes, version 0x183 in little-endian)
0C..EOF [single zlib stream]  (starts with 78 01 / 78 9C / 78 DA / 78 5E)
```

All five samples observed start with `78 01` — `CMF=0x78` (deflate, 32K window) and
`FLG=0x01` (low compression). No 2-byte block length prefixes, no `00 01` trailer.

**This means our current `Compiler.CompressZoneBlocks` (which writes 64KB blocks with
BE length prefixes) produces a file the game cannot read.** A PC build path needs to
emit `IWffu100 + LE version + single zlib stream`.

## Zone (decompressed) layout — **52 bytes header, same as PS3**

```
struct XFile {            // 36 bytes total (7 blockSize slots)
  int size;               // 0x00
  int externalSize;       // 0x04
  int blockSizeTemp;      // 0x08
  int blockSizePhysical;  // 0x0C
  int blockSizeRuntime;   // 0x10
  int blockSizeVirtual;   // 0x14
  int blockSizeLarge;     // 0x18
  int blockSizeCallback;  // 0x1C
  int blockSizeVertex;    // 0x20
};
struct XAssetList {       // 16 bytes, total header = 0x34 (52 bytes)
  int scriptStringCount;  // 0x24
  const char **scriptStringsPtr;  // 0x28  (placeholder 0xFFFFFFFF in zone file)
  int assetCount;         // 0x2C
  XAsset *assetsPtr;      // 0x30  (placeholder 0xFFFFFFFF in zone file)
};
```

### Correction: PC WaW does NOT have an INDEX block

The initial draft of this document assumed PC had 8 blockSize slots (adding an
INDEX block based on the `#ifdef PC` in Zone.md). That's wrong for WaW PC.
Verified counts across 5 samples:

| File | ScriptStrings @0x24 | Assets @0x2C | First asset entry @0x34 |
|---|---|---|---|
| default.ff | 0 | 19 | `20 00 00 00 FF FF FF FF` (rawfile) |
| mp_makin_day_load.ff | 0 | 9 | `07 00 00 00 FF FF FF FF` (techset) |
| patch.ff | 152 | 214 | `00 00 00 00 FF FF FF FF` (xmodelpieces) |
| patch_mp.ff | 0 | 27 | `07 00 00 00 FF FF FF FF` (techset) |
| credits.ff | 226 | 1144 | `00 00 00 00 FF FF FF FF` (xmodelpieces) |

The `INDEX` block from the C struct may apply to a different game (MW2 PC perhaps),
not WaW. We have no MW2 PC samples to verify.

All values are little-endian.

### Observed `ZoneSize` value vs actual zone byte length

| File | `ZoneSize` field @0x00 | Actual zone bytes | Δ |
|---|---|---|---|
| default.ff | 0x000B7F38 (753,464) | 753,500 | 36 |
| mp_makin_day_load.ff | 0x0000ABC3 (43,971) | 44,007 | 36 |
| credits.ff | 0x00948D48 (9,735,496) | 9,735,532 | 36 |
| patch.ff | 0x0021749D (2,192,541) | 2,192,577 | 36 |
| patch_mp.ff | 0x00089268 (561,768) | 561,804 | 36 |

The `ZoneSize` field is exactly `(zoneByteLength - 36)` across all samples. Same `−36`
relationship as documented for PS3/Xbox 360 (header size 52 − 16).

### `BlockSizeLarge` value vs actual

| File | `BlockSizeLarge` @0x18 | Actual zone bytes | Δ |
|---|---|---|---|
| default.ff | 753,220 | 753,500 | 280 |
| credits.ff | 5,618,745 | 9,735,532 | varies |
| patch_mp.ff | 554,377 | 561,804 | varies |

`BlockSizeLarge` ≈ size of the bulk data section. Per-game/per-content basis — not a
fixed magic value.

### MemAlloc constants — **PC WaW does NOT use fixed MemAlloc values**

Console WaW uses fixed `0x10B0` for `BlockSizeTemp` regardless of content. PC WaW
computes it per zone:

| File | BlockSizeTemp (@0x08) |
|---|---|
| default.ff | 0x1C (28) |
| mp_makin_day_load.ff | 0x108 (264) |
| credits.ff | 0x200364 (2,098,020) |
| patch.ff | 0xA60 (2,656) |
| patch_mp.ff | 0x1E4 (484) |

This means the existing `CoD5Definition.PCMemAlloc1 = { 0xB0, 0x10, 0x00, 0x00 }`
constant is **wrong** — there is no single value to use. A compiler that writes the
correct value will need to compute it from the actual zone contents (likely
"sum of bytes the engine needs to allocate at runtime for the TEMP pool").

For initial save-back support (where we're not changing the asset layout
significantly), we can preserve `BlockSizeTemp` from the source zone unchanged.

## Asset entry format

Each XAsset is 8 bytes. PC WaW uses the **same shape as PS3** — type first, then
pointer placeholder — just with the type field stored little-endian:
```
PC WaW:  XX 00 00 00 FF FF FF FF   ([type LE][ptr])
PS3 WaW: 00 00 00 XX FF FF FF FF   ([type BE][ptr])
```

The existing `StructureBasedZoneParser` already handles both layouts via its
"Format A / Format B" branch. No changes needed for reading.

Asset type IDs use the `CoD5AssetTypePC` enum (in `FastFileLib.GameDefinitions`),
which is correctly shifted to account for PC having neither `pixelshader` nor
`vertexshader` on this codebase's enum mapping.

Example observed first asset entry:
- `default.ff` @0x38: `FF FF FF FF 20 00 00 00` → type `0x20` = `rawfile` (CoD5 PC)
- `patch_mp.ff` @0x38: `FF FF FF FF 15 00 00 00` → type `0x15` = `menufile` (CoD5 PC)

## What's currently broken in our compile path

| File | Issue |
|---|---|
| `FastFileLib/Compiler.cs` `CompressZoneBlocks()` | Hard-codes 64KB block + BE 2-byte length prefix. PC WaW needs single zlib stream. |
| `FastFileLib/Compiler.cs` `Compile()` | Appends `00 01` end marker. PC WaW has no end marker. |
| `FastFileLib/FastFileInfo.cs` `GetVersionBytes()` | Has correct LE handling for PC: `GameVersion.CoD4 when normalizedPlatform == "PC" => new byte[] { 0x00, 0x00, 0x00, 0x05 }` — wait this is BE. Needs verification: do we emit `05 00 00 00` (LE) for CoD4 PC, or `00 00 00 05` (BE)? |
| `FastFileLib/ZoneBuilder.cs` `BuildHeaderSection()` | Uses `WriteBigEndian` for all fields. PC needs LE writes. |
| `FastFileLib/ZoneBuilder.cs` | Uses 52-byte header. PC needs 56 bytes (extra `BlockSizeIndex` field). |
| `FastFileLib/ZoneBuilder.cs` | Uses BE asset entry `[type][ptr]`. PC needs LE `[ptr][type]`. |
| `FastFileLib/FastFileConstants.cs` `GetMemAlloc1()` | Returns fixed BE bytes. PC needs computed-per-zone. For initial save-back, we should preserve from source. |
| Editor save path | Need to verify `_openedFastFile.IsPC` propagates through to the compile call. |

## Implementation plan (in dependency order)

1. **`FastFileInfo.GetVersionBytes()`** — audit and fix to return LE bytes for any
   `platform == "PC"` case. Add tests for each combination.
2. **`Compiler` — add PC code path:**
   - New `CompilePc()` or branch in `Compile()` based on `_platform == "PC"`.
   - Writes header (12 bytes) + single zlib stream (using `ZLibStream(stream, CompressionLevel.Optimal)`).
   - No end marker.
3. **`ZoneBuilder` — add PC code path:**
   - 56-byte header with LE writes for every field.
   - `[ptr][type]` LE asset entries.
   - `BlockSizeTemp` etc. need to come from either (a) source zone (round-trip) or
     (b) a new computation we'd have to reverse-engineer.
4. **Editor save path** — confirm `IsPC` flows through. Likely needs a `platform`
   parameter on `ZoneSaveService` calls.
5. **Round-trip test** — open `patch_mp.ff`, save unchanged, byte-diff. The output
   should round-trip identically (or close enough that the game loads it).
6. **In-game test** — change a small rawfile in `patch_mp.ff` and load it in real
   WaW PC. This is the only definitive verification.

## Known unknowns

- **What does CoD4 PC FF look like?** Same single-zlib-stream layout, or different?
  We have no CoD4 PC samples. The version byte order for CoD4 PC is also unclear
  from this data.
- **What computes `BlockSizeTemp`?** Reverse-engineering this isn't required if we
  preserve from source on edit-and-save, but would be required to build fresh PC
  zones from scratch.
- **Does the in-game loader validate `ZoneSize` strictly?** Need to test whether
  setting `ZoneSize = actualLen - 36` is required or just informational.

## Reference samples

15 PC WaW samples were used for this analysis. They covered patch files (`patch.ff`,
`patch_mp.ff`, `nazi_zombie_*_patch.ff`), localized maps, load files, and standalone
files (`default.ff`, `credits.ff`, `outro.ff`, `intro_pac.ff`). All confirm the
findings above.
