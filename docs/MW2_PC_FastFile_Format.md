# MW2 PC FastFile Format — Research Notes

Verified against 11 retail MW2 PC samples (`code_post_gfx.ff`, `common.ff`, `so_bridge.ff`,
`code_post_gfx_mp.ff`, `common_mp.ff`, `mp_highrise.ff`, `mp_highrise_load.ff`,
`mp_rust.ff`, `mp_rust_load.ff`, `patch.ff`, `patch_mp.ff`). Format detail confirmed by
reading [OpenAssetTools'](https://github.com/Laupetin/OpenAssetTools) IW4 loader
(`ZoneLoaderFactoryIW4.cpp`, `ProcessorAuthedBlocks.cpp`, `ZoneConstantsIW4.h`, `IW4.h`).

**Current implementation status (May 2026):**
- ✅ Detect both unsigned (`IWffu100`) and signed (`IWff0100`) MW2 PC via LE version `0x114`
- ✅ Decompress unsigned MW2 PC (single zlib stream at file offset `0x15`)
- ✅ Decompress signed MW2 PC (Infinity Ward "authed chunks" — 8KB chunks in groups of 257, hash chunk skipped)
- ✅ Parse 56-byte zone header (8 blockSize slots, LE integers)
- ✅ Parse asset pool entries (`[type LE][ptr]` format)
- ✅ Parse rawfile assets (16-byte LE header with optional inner zlib compression)
- ✅ Parse localize entries
- 🟡 weapon — pattern-matching parser produces results but field alignment may be off
- ❌ techset / menufile / xanim / stringtable / material / image — listed in asset pool but skipped (parsers are BE-only)
- ✅ Recompress (unsigned) — `FastFileProcessor.CompressMW2PC` writes the unsigned layout: 12-byte standard header + 9-byte preamble (preserved from the original FF when available) + single zlib stream. Signed retail inputs are saved as unsigned — re-signing the `DB_AuthHeader` requires IW's RSA-2048 private key.
- 🔄 In-game verification pending

## TL;DR — MW2 PC mashes together MW2's compressed-rawfile model and a PC-style LE zone

| Aspect | MW2 PS3 | MW2 Xbox 360 | **MW2 PC** |
|---|---|---|---|
| Magic | `IWffu100` (unsigned) | `IWffu100` (unsigned) | `IWff0100` (signed) **or** `IWffu100` (unsigned) |
| Version bytes | `00 00 01 0D` (BE) | `00 00 01 0D` (BE) | `14 01 00 00` (**LE**, value `0x114`) |
| Outer compression | 64KB blocks (raw deflate) | Single zlib stream | Single zlib stream (unsigned) **or** authed chunks (signed) |
| Outer header before stream | 25 bytes (extended) | 25 bytes (extended) | 9 bytes (only `allowOnlineUpdate` + `fileCreationTime`) — region/entryCount/fileSize absent |
| Zone header size | 52 bytes (0x34, 7 blockSize slots) | 48 bytes (0x30, 6 blockSize slots — no `BlockSizeVertex`) | **56 bytes (0x38, 8 blockSize slots)** — same layout as Wii |
| Zone endianness | Big | Big | **Little** |
| Asset entry format | `[ptr][type BE]` | `[ptr][type BE]` | `[type LE][ptr]` |
| Asset type enum | `MW2AssetTypePS3` | `MW2AssetTypeXbox360` | `MW2AssetTypePC` (has both `vertexshader` and `vertexdecl`) |
| Rawfile header size fields | BE | BE | **LE** |

The PC zone layout is geometrically the same as **Wii WaW** (8 blockSize slots, asset
table at `0x38`), just with LE byte order.

## FF (compressed) layout — unsigned MW2 PC

```
00..07  IWffu100              (8 bytes, ASCII)
08..0B  14 01 00 00           (4 bytes, version 0x114 in little-endian)
0C      01                    allowOnlineUpdate (1 byte, usually 0x01)
0D..14  ?? ?? ?? ?? ?? ?? ?? ?? fileCreationTime (8 bytes, Windows FILETIME)
15..EOF [single zlib stream]  (starts with 78 DA / 78 9C / 78 5E / 78 01)
```

That's it — **no `region`, `entryCount`, or `fileSizes` fields**. The MW2 PS3/Xbox 360
25-byte extended header is truncated to just 9 bytes on PC. Decompression just feeds
bytes from offset `0x15` to end-of-file into a single `ZLibStream`.

Verified offsets in 3 unsigned MW2 PC samples (all start zlib at exactly `0x15`):

| File | Size | Decompressed |
|---|---|---|
| code_post_gfx.ff | 985 KB | 1,551,170 bytes |
| common.ff | 107.9 MB | 150,977,809 bytes |
| so_bridge.ff | 110.7 MB | 239,158,391 bytes |

## FF (compressed) layout — signed MW2 PC

Signed MW2 PC uses Infinity Ward's "authed chunks" format. The outer FF has the same
9-byte preamble at `0x15`, immediately followed by the `IWffs100` streaming magic and a
`DB_AuthHeader` containing the RSA-2048 signature and SHA-256 master block hashes:

```
00..07     IWff0100              (8 bytes, signed magic)
08..0B     14 01 00 00           (4 bytes, version 0x114 LE)
0C         01                    allowOnlineUpdate
0D..14     ?? × 8                fileCreationTime
15..2024   DB_AuthHeader         (8144 bytes — see breakdown below)
2025..2054 48 bytes padding      (AUTHED_CHUNK_SIZE 0x2000 − sizeof(DB_AuthHeader) 8144)
2055..end  Authed chunks         (groups of 257 × 0x2000 byte chunks)
```

### DB_AuthHeader breakdown (`IW4.h` from OpenAssetTools)

```c
struct DB_AuthHash      { char bytes[32];  };  // SHA-256
struct DB_AuthSignature { char bytes[256]; };  // RSA-2048

struct DB_AuthSubHeader {
    char fastfileName[32];
    unsigned int reserved;
    DB_AuthHash masterBlockHashes[244];  // 244 × 32 = 7808 bytes
};
// Total: 32 + 4 + 7808 = 7844 bytes

struct DB_AuthHeader {
    char magic[8];                          // "IWffs100"
    unsigned int reserved;
    DB_AuthHash subheaderHash;
    DB_AuthSignature signedSubheaderHash;
    DB_AuthSubHeader subheader;
};
// Total: 8 + 4 + 32 + 256 + 7844 = 8144 bytes
```

### Authed-chunk stream (`ProcessorAuthedBlocks.cpp` from OpenAssetTools)

```
AUTHED_CHUNK_SIZE             = 0x2000   (8KB per chunk)
AUTHED_CHUNK_COUNT_PER_GROUP  = 256      (data chunks per group)
```

From offset `0x2055` onwards, the file is a sequence of **groups**. Each group =
**257 chunks of 0x2000 bytes** (= 0x202000 = 2,105,344 bytes of FF, but only
256 × 0x2000 = 2 MiB of payload):

- **Chunk 0** of each group is *itself* the **hash table** — 256 × 32 = 8192 bytes
  of SHA-256 hashes, one per data chunk in the group. SHA-256(this chunk) must
  match the corresponding entry in `masterBlockHashes`. Contributes nothing to
  the zlib payload — *skip it*.
- **Chunks 1..256** are 8KB blobs of zlib-stream payload. SHA-256(chunk N) must
  match `chunkHashes[N - 1]` from chunk 0. Concatenate them and feed to a single
  `ZLibStream` to recover the zone.

After chunk 256, the next file chunk starts a fresh group with its own hash chunk.

The editor does **not** currently verify the SHA-256 hashes (no RSA signature check
either) — it just skips the hash chunks and decompresses the payload. The game would
of course validate everything; for read/edit purposes we trust the file is intact.

### Verified offsets in 6 signed MW2 PC samples

| File | Size | First zlib byte | Decompressed |
|---|---|---|---|
| mp_rust_load.ff | 24 KB | 0x4015 | 1,735 |
| mp_highrise_load.ff | 24 KB | 0x4015 | 1,743 |
| code_post_gfx_mp.ff | 168 KB | 0x4015 | 811,446 |
| common_mp.ff | 50.7 MB | 0x4015 | 98,065,199 |
| mp_rust.ff | 48.6 MB | 0x4015 | 93,650,037 |
| mp_highrise.ff | 71.2 MB | 0x4015 | 136,978,100 |

All signed samples have the **first data chunk start at exactly `0x4015`** — that's
`0x2055` (after auth header) + `0x2000` (the chunk-0 hash table). Subsequent groups
land at `0x4015 + N * 0x202000`.

## Zone (decompressed) layout — 56 bytes header

```
struct XFile {            // 40 bytes total (8 blockSize slots)
  int size;               // 0x00
  int externalSize;       // 0x04
  int blockSizeTemp;      // 0x08
  int blockSizePhysical;  // 0x0C
  int blockSizeRuntime;   // 0x10
  int blockSizeVirtual;   // 0x14
  int blockSizeLarge;     // 0x18
  int blockSizeCallback;  // 0x1C
  int blockSizeVertex;    // 0x20
  int blockSizeIndex;     // 0x24 — present on MW2 PC AND Wii (PS3/Xbox 360 don't have this)
};
struct XAssetList {       // 16 bytes, total header = 0x38 (56 bytes)
  int scriptStringCount;  // 0x28
  const char **scriptStringsPtr;  // 0x2C  (placeholder 0xFFFFFFFF in zone file)
  int assetCount;         // 0x30
  XAsset *assetsPtr;      // 0x34  (placeholder 0xFFFFFFFF in zone file)
};
```

All values **little-endian**.

Sample headers (verified from real samples):

| File | AssetCount @0x30 | ScriptStringCount @0x28 |
|---|---|---|
| code_post_gfx.zone | 1,900 | 4 |
| common.zone | 9,288 | 524 |
| patch_mp.zone | 225 | 11 |

This is exactly the same offset table as `Wii_*Offset` in `FastFileConstants` — the
library dispatcher routes both Wii WaW and MW2 PC through `UsesEightBlockSizeLayout`
because the asset-table location is identical.

## Asset entry format

Each XAsset is 8 bytes. MW2 PC uses **`[type LE][ptr]`** (same shape as PC WaW):

```
MW2 PC:  XX 00 00 00 FF FF FF FF
MW2 PS3: FF FF FF FF 00 00 00 XX   (pointer first, type BE)
```

Asset type IDs come from the `MW2AssetTypePC` enum (in `FastFileLib.GameDefinitions`).
The PC enum is shifted by +1 from PS3 for types ≥ `0x09` because PC has both
`vertexshader` (`0x07`) and `vertexdecl` (`0x08`), whereas Xbox 360 has neither and
PS3 has only `vertexshader`.

| Asset Type | MW2 PS3 | MW2 Xbox 360 | **MW2 PC** |
|---|---|---|---|
| rawfile | `0x23` | `0x22` | `0x24` |
| localize | `0x1A` | `0x19` | `0x1A` (Xbox 360 differs) |
| menufile | `0x19` | `0x18` | `0x19` |
| weapon | `0x1B` | `0x1A` | `0x1C` |
| stringtable | `0x24` | `0x23` | `0x25` |

(See `FastFileLib/GameDefinitions/MW2Definition.cs` for the full enum.)

## Rawfile format — LE size fields

MW2 rawfile structure on PC mirrors the console version but with **little-endian** size
fields:

```
[FF FF FF FF] [compressedLen LE 4B] [len LE 4B] [FF FF FF FF] [name\0] [data]
```

- `compressedLen > 0` → `data` is a complete zlib stream of `compressedLen` bytes
  decompressing to `len` bytes.
- `compressedLen == 0` → `data` is `len` bytes of uncompressed text.

Verified from `patch_mp.zone`:

| Filename | compressedLen (LE) | len (LE) |
|---|---|---|
| maps/mp/mp_afghan.gsc | 340 | 653 |
| maps/mp/_utility.gsc | 13,020 | 60,273 |
| maps/mp/gametypes/_damage.gsc | 15,542 | 71,791 |
| mp/basemaps.arena | 971 | 8,707 |
| vision/mp_vacant.vision | 274 | 646 |

Interpreting the same bytes BE gives nonsense GB-scale values (`compressedLen` shows
up as 1.4 GB etc.) — that's how we caught the original BE-only bug.

The rawfile parser uses the strict `IsValidRawFileName` whitelist
(`RawFileConstants.FileNamePatternStrings`: `.cfg`, `.gsc`, `.atr`, `.csc`, `.rmb`,
`.arena`, `.vision`). `.csv` filenames are stringtables, not rawfiles, and are
correctly rejected.

## How the editor handles MW2 PC

| Component | What it does |
|---|---|
| `FastFileInfo.DetectGameVersion` | Tries BE; if not recognized, tries LE; if MW2 matches, sets `IsPC=true` |
| `FastFile.IsXbox360` | **Excludes `IsPC && IsWii`** so signed MW2 PC isn't mistaken for Xbox 360 |
| `GameDefinitionFactory.GetDefinition` | Returns `_mw2PC` when `IsMW2File && IsPC` (must come before the `IsSigned` Xbox 360 check) |
| `FastFileProcessor.Decompress` | Dispatches to `TryDecompressMW2PC(isSigned)` for MW2+PC |
| `FastFileProcessor.TryDecompressMW2PC` | Unsigned: feeds bytes from `0x15` to a single `ZLibStream`. Signed: walks authed chunks, skipping hash chunks, concatenating payload, then decompresses. |
| `FastFileConstants.GetZoneHeaderSize` / `GetAssetCountOffset` / `GetScriptStringCountOffset` | Routes MW2+PC through `UsesEightBlockSizeLayout` → same offsets as Wii |
| `StructureBasedZoneParser` | 56-byte header, LE asset entries (Format A LE path) |
| `MW2GameDefinition.TryParseMW2Format` | Reads `compressedLen` / `len` as LE when `IsPC`; uses `IsValidRawFileName` whitelist |
| `FastFileLib.RawFileScanner` | LE size reads when `isPC=true`; handles MW2 PC's 16-byte LE header + zlib decompression. Editor's `RawFileParser` is a shim that wraps each `RawFileLocation` as a `RawFileNode`. |
| `FastFileProcessor.Recompress` / `CompressMW2PC` | Writes the unsigned PC layout (12-byte standard header + 9-byte preamble + single zlib at `0x15`). Signed retail input → unsigned output since IW's RSA-2048 private key isn't available. |
| `FastFileLib.FastFileSaveService` | Routes saves of MW2 PC through `FastFileProcessor.Recompress` with `platform="PC"`. Used by the editor's `FastFileSave` shim and by `ffcli compress`. |

## Lessons learned along the way

1. **MW2 PC signed files use IWff0100 magic — and so does Xbox 360.** Detection
   originally rejected MW2 PC as "invalid" because the BE-then-LE fallback in
   `FastFileInfo.DetectGameVersion` was gated to *unsigned* magic only. Now it tries
   LE for any unrecognized BE version regardless of signed flag.
2. **`FastFile.IsXbox360 = IsSigned` was wrong.** Signed magic ≠ Xbox 360 since MW2
   PC retail also uses signed magic. The check now also excludes `IsPC` and `IsWii`.
3. **The factory missed the MW2 PC branch.** `GameDefinitionFactory.GetDefinition`
   routed CoD4/CoD5 through `IsPC`, but for MW2 it fell through to
   `isXbox360 ? _mw2Xbox : _mw2Ps3`. Combined with bug #2, signed MW2 PC got
   `_mw2Xbox` and read the asset pool with the Xbox 360 enum (so `techset` showed up
   as `sound`, etc.).
4. **MW2 PC has a *shorter* extended header than PS3/Xbox 360 (9 bytes vs 25).**
   The PS3 extended header parser tried to read region/entryCount/fileSizes after
   the 9 real bytes and overshot the zlib stream by 16 bytes, then decoded random
   bytes as block-length prefixes — producing the cryptic "unsupported compression
   method" error.
5. **Signed MW2 PC files >2MB compressed need ProcessorAuthedBlocks.** Naive
   "decompress zlib from `0x4015`" works for files under one chunk-group, then
   blows up at exactly 2MB compressed when the next group's hash chunk masquerades
   as deflate data. OpenAssetTools' `ProcessorAuthedBlocks` was the reference for
   the correct chunked layout.
6. **`MW2GameDefinition` rawfile parsers were BE-only.** Both the 16-byte primary
   format and the 12-byte fallback hardcoded `ReadInt32BE` for `compressedLen` and
   `len`. They now branch on `IsPC`.
7. **The MW2 pattern-matching fallback was ALSO BE-only.** When structure-based
   parsing failed (which it always did because the editor couldn't navigate past
   MW2 PC's techsets/menufiles), the editor's pattern-matching fallback was the
   recovery path — and it had the same BE hardcoding. The fix was consolidating
   into `FastFileLib.RawFileScanner` which takes an explicit `isPC` flag; the
   editor's `RawFileParser` now delegates to it.
8. **Lenient extension check let `.csv` (stringtables) through as rawfiles.** The
   parsers now use `IsValidRawFileName`, which enforces the
   `RawFileConstants.FileNamePatternStrings` whitelist — single source of truth
   shared with the pattern matcher.

## Known unknowns

- **Signed re-signing** — not implemented. `FastFileProcessor.CompressMW2PC`
  writes the **unsigned** layout; signed retail input → unsigned output. Properly
  re-signing the `DB_AuthHeader` requires IW's RSA-2048 private key, which isn't
  public. The unsigned output still loads in any client that accepts unsigned FFs.
- **Non-rawfile asset parsing** — `techset`, `menufile`, `xanim`, `stringtable`,
  `material`, `image` all use BE parsers right now; would need LE-aware variants
  for MW2 PC. Same scope of work as PC WaW.
- **Hash verification** — we *skip* the per-chunk SHA-256 hashes rather than
  verify them. Good enough for reading; required for re-signing.
- **In-game test** — no one has launched an edited+saved MW2 PC FF in the game
  yet (and we can't save signed files anyway).
- **CoD4 PC / Wii** — presumed similar layouts, no samples to verify.

## Reference samples

11 MW2 PC samples were used: `code_post_gfx.ff`, `common.ff`, `so_bridge.ff` (unsigned
single-player); `code_post_gfx_mp.ff`, `common_mp.ff`, `mp_highrise.ff`, `mp_rust.ff`
(signed multiplayer); `mp_highrise_load.ff`, `mp_rust_load.ff` (signed load files);
`patch.ff`, `patch_mp.ff` (signed patches).
