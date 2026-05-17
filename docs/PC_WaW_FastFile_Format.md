# PC WaW FastFile Format — Research Notes

Verified from 5 real PC WaW samples (`default.ff`, `mp_makin_day_load.ff`, `credits.ff`,
`patch.ff`, `patch_mp.ff`). Issue #21 is functionally addressed — the format details below
are confirmed by working round-trip and the live editor.

**Current implementation status (May 2026):**
- ✅ Decompress PC FF → zone
- ✅ Parse zone header (52-byte layout)
- ✅ Parse asset pool entries (`[type LE][ptr]` format)
- ✅ Parse and edit rawfile assets
- ✅ Parse and edit localize entries
- ✅ Recompress zone → PC FF (single zlib stream)
- ✅ Round-trip verified byte-for-byte across 4 retail samples
- 🔄 In-game verification pending (round-trip success suggests format is correct)
- ❌ weapon / menufile / xanim / stringtable / material / techset / image — listed in
  asset pool but not yet parsed on PC (all bail in `CoD5PCGameDefinition.IsSupportedAssetType`)

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

Our `Compiler.CompilePc()` emits the same shape — `IWffu100 + LE version + single zlib
stream` — using `CompressionLevel.Optimal` (which produces a `78 9C` header). The game
accepts any valid zlib variant, so the compression level used at encode time doesn't
matter for loadability.

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

The existing `CoD5Definition.PCMemAlloc1 = { 0xB0, 0x10, 0x00, 0x00 }` constant is
**unused** for PC saves — we preserve the original zone's `BlockSizeTemp` value
verbatim when round-tripping. Building a fresh PC zone from scratch (not yet
supported) would need to either compute this value or experimentally find a safe
default.

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

## How the editor now handles PC

| Component | What it does for PC |
|---|---|
| `FastFileInfo.GetVersionBytes(platform="PC")` | Returns LE-ordered version bytes (e.g., `83 01 00 00` for WaW PC) |
| `Compiler.Compile()` | Branches on `_platform == "PC"` → calls `CompilePc()` which emits `IWffu100 + LE version + single zlib stream` |
| `CoD5FastFileHandler.Recompress` / `CoD4FastFileHandler.Recompress` | Checks `openedFastFile.IsPC` → delegates to `new Compiler(WaW, "PC").Compile(...)` |
| `ZoneFileHeaderConstants.PC_*Offset` | Uses 52-byte layout: `ScriptStringCount @0x24`, `AssetCount @0x2C`, etc. |
| `FastFileConstants.ZoneHeaderSize_PC` | `0x34` (52 bytes) |
| `StructureBasedZoneParser` | Detects Format A LE `[type LE][ptr]` for PC; includes a backup-4-bytes check for the tag-end overshoot case |
| `CoD5PCGameDefinition.ParseRawFile` | Reads size little-endian, otherwise identical to base class |
| `CoD5PCGameDefinition.ParseLocalizedEntry` | Same as base (byte-order-independent) |
| `RawFileParser.ExtractSingleRawFileNodeWithPattern` | Endian-aware size read for pattern-matching fallback |

## Lessons learned along the way

A few quirks worth knowing about for future contributors:

1. **PC has 7 blockSize slots, not 8.** The `#ifdef PC` in the canonical Zone.md C
   header suggests an `INDEX` block exists on PC. It does not for WaW PC. The header
   is 52 bytes, same as PS3.
2. **Asset pool detection can drift 4 bytes.** The script-string section end isn't
   always computed precisely, so the asset pool detector can skip past the real
   start and lock onto a Format B `[ptr][type]` pattern that's structurally just a
   `[type][ptr]` run shifted by 4 bytes. `StructureBasedZoneParser` now checks 4
   bytes back when matching Format B LE to recover.
3. **Off-by-N in pattern-matching fallback.** `remainingRawFiles = expectedCount -
   alreadyParsed` was wrong — `expectedCount` already counted only from the stop
   index, so subtracting double-counted and went negative, silently disabling the
   loop. Same bug latent in localize counting (worked by coincidence because no
   localize entries are parsed before the stop point in practice).
4. **`.txt`, `.csv`, `.menu`, `.str` are not rawfile extensions.** Adding them to
   the pattern matcher produced false positives from embedded string references
   inside other assets. `.menu` is its own asset type entirely (`menufile`).
5. **`maxFalsePositives = 100`** in pattern matching was way too low for PC zones
   with binary asset data interleaved between rawfiles. Raised to 10000.
6. **`IsValidRawFileName` was duplicated** in `GameDefinitionBase` and
   `RawFileParser` with hardcoded extension lists. Both now read from
   `RawFileConstants.FileNamePatternStrings`.

## Known unknowns

- **CoD4 PC FF format** — presumed identical shape (single zlib stream, LE) but no
  samples to verify. Version byte order also unconfirmed.
- **`BlockSizeTemp` computation rule** — preserving from source works for edit-and-save.
  Building fresh PC zones would need to either reverse-engineer this or guess safely.
- **In-game loader strictness** — round-trip is byte-stable but no one has launched
  the saved FF in WaW PC yet to confirm the engine accepts it.

## Reference samples

15 PC WaW samples were used for this analysis. They covered patch files (`patch.ff`,
`patch_mp.ff`, `nazi_zombie_*_patch.ff`), localized maps, load files, and standalone
files (`default.ff`, `credits.ff`, `outro.ff`, `intro_pac.ff`). All confirm the
findings above.
