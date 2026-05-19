# WaW Wii FastFile Format — Research Notes

Verified from real WaW Wii samples (`credits.ff`, `see1.ff`, `ber1.ff`, load files, etc).

**Current implementation status:**
- ✅ Detect WaW Wii via version `0x0000019B` (BE)
- ✅ Decompress Wii FF → zone (single zlib stream, same code path as PC)
- ✅ Parse 56-byte zone header (8 blockSize slots, includes `BlockSizeIndex`)
- ✅ Parse asset pool entries using PC-style asset type enum (no shader slots)
- ✅ Parse and edit rawfile assets
- ✅ Parse and edit localize entries
- ✅ Recompress zone → Wii FF (single zlib stream + BE Wii version `00 00 01 9B`).
  Verified round-trip on retail `ber1.ff`: original zone bytes byte-identical after
  compress → decompress. Differs from original only in zlib compression level
  (`78 9C` Optimal vs retail's `78 01` lowest); both are valid loadable variants.
- 🔄 In-game verification pending (no real WaW-Wii hardware/emulator test yet)
- ❌ weapon / menufile / xanim / stringtable / material / techset / image — listed in
  asset pool but not yet parsed on Wii (same scope as PC)

## TL;DR — Wii is a hybrid of PC + PS3 traits

| Aspect | PS3 WaW | **Wii WaW** | PC WaW |
|---|---|---|---|
| Compression | 64KB blocks, raw deflate, BE 2-byte lengths | **Single zlib stream** | Single zlib stream |
| Endianness | Big | **Big** (PowerPC) | Little |
| Version bytes | `00 00 01 83` (BE) | `00 00 01 9B` (BE) | `83 01 00 00` (LE) |
| Zone header size | 52 bytes (0x34) | **56 bytes (0x38)** — extra `BlockSizeIndex` slot | 52 bytes |
| End marker | `0x00 0x01` | None — natural end of zlib stream | None |
| MemAlloc1 | Fixed `0x10B0` | **Computed per zone** (preserved on save) | Computed per zone |
| Asset type enum | `CoD5AssetTypePS3` (has pixelshader + vertexshader) | **`CoD5AssetTypePC`** (no shader slots) | `CoD5AssetTypePC` |
| Asset entry format | `[type BE][ptr]` | `[type BE][ptr]` | `[type LE][ptr]` |

## FF (compressed) layout

```
00..07  IWffu100              (8 bytes, ASCII)
08..0B  00 00 01 9B           (4 bytes, version 0x19B in big-endian)
0C..EOF [single zlib stream]  (starts with 78 XX header byte)
```

Same shape as PC, just BE version bytes instead of LE. No 2-byte block length prefixes,
no `0x00 0x01` trailer.

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
  int blockSizeIndex;     // 0x24 — Wii only (PS3 doesn't have this slot)
};
struct XAssetList {       // 16 bytes, total header = 0x38 (56 bytes)
  int scriptStringCount;  // 0x28
  const char **scriptStringsPtr;  // 0x2C  (placeholder 0xFFFFFFFF in zone file)
  int assetCount;         // 0x30
  XAsset *assetsPtr;      // 0x34  (placeholder 0xFFFFFFFF in zone file)
};
```

All values **big-endian** (PowerPC).

Sample header from `credits.zone`:

| Field | Value |
|---|---|
| ZoneSize @0x00 | `0x001D5759` (1,922,905) |
| BlockSizeTemp @0x08 | `0x000004A0` |
| BlockSizeLarge @0x18 | `0x000F4401` |
| BlockSizeVertex @0x20 | `0x000D1840` |
| **BlockSizeIndex @0x24** | `0x00009890` |
| ScriptStringCount @0x28 | 205 |
| ScriptStringsPtr @0x2C | `0xFFFFFFFF` (placeholder) |
| AssetCount @0x30 | 1045 |
| AssetsPtr @0x34 | `0xFFFFFFFF` (placeholder) |

`ZoneSize = actualZoneBytes - 40` (vs PS3's `-36`) because the header is 4 bytes bigger.

## Asset entries — PC-style enum despite BE byte order

Each XAsset is 8 bytes, `[type BE][ptr]` format (same shape as PS3):

```
Wii:     00 00 00 XX FF FF FF FF
```

But the type IDs follow the **PC enum mapping** (`CoD5AssetTypePC`), not the PS3 enum.
This is verified from `credits.zone`'s type distribution:

| Type ID | PC enum | PS3 enum | Wii count |
|---|---|---|---|
| `0x17` | localize | menufile | 899 ✓ (credits text) |
| `0x09` | sound | techset | 58 |
| `0x04` | xanim | xanim | 31 |
| `0x20` | rawfile | character | 17 ✓ (mission scripts) |
| `0x07` | techset | pixelshader | 15 |
| `0x05` | xmodel | xmodel | 12 |

The PC enum interpretation gives a coherent breakdown for a credits screen (lots of
localized strings, some rawfiles, etc.). The PS3 enum gives nonsense (899 menufiles?
17 unused "character" entries?).

**Conclusion:** Wii inherited the asset type enum from PC because both platforms lack
the same shader asset slots — Wii's GPU is fixed-function-ish enough that the engine
doesn't allocate `pixelshader` / `vertexshader` asset types like PS3 does.

## How the editor handles Wii

| Component | What it does |
|---|---|
| `FastFileInfo.DetectGameVersion` | Detects `0x000001A2` → CoD4 Wii, `0x0000019B` → WaW Wii, sets `IsWii=true` |
| `FastFileProcessor.TryDecompressWiiZlib` | Single zlib stream from byte 12 onwards |
| `FastFileConstants.ZoneHeaderSize_Wii` | `0x38` (56 bytes) |
| `FastFileConstants.GetZoneHeaderSize / GetAssetCountOffset / GetScriptStringCountOffset` | Branch on `isWii` to use the +4-shifted offsets |
| `ZoneFile.GetHeaderFieldOffsets` | Uses `Wii_*Offset` constants when `ParentFastFile.IsWii` |
| `StructureBasedZoneParser` | Sets `_headerSize = 0x38` when `_isWii`; stores CoD5 type bytes in `AssetType_COD5_PC` field (since Wii uses PC enum) |
| `CoD5WiiGameDefinition` | Uses `CoD5AssetTypePC` enum mapping, BE base parsers, `IsWii=true` |
| `GameDefinitionFactory.GetDefinition` | Routes Wii CoD5 to `CoD5WiiGameDefinition` |
| `AssetSelectionDialog` / `AssetRecordProcessor` / `MainWindowForm` / `ZoneHexViewForm` | All read `AssetType_COD5_PC` when `IsWii \|\| IsPC` |

## Save path

Wii FFs use the same shape as PC: `IWffu100 + version + single zlib stream`. The
lib's `Compiler.Compile` routes both PC and Wii through one `CompileSingleStream`
method (renamed from the old PC-specific `CompilePc`); the only per-platform
difference is which version bytes `FastFileInfo.GetVersionBytes` emits — PC LE
`83 01 00 00`, Wii BE `00 00 01 9B`. `FastFileProcessor.Recompress` accepts
`platform="Wii"` and dispatches to that single-stream path.

Auto-detection: `FastFileInfo.IsZoneDataWii(byte[])` returns true when the zone
is BE *and* uses the 56-byte layout (markers at 0x2C and 0x34, no marker at
0x28). The CLI's `ffcli compress` checks PC first, then Wii, then defaults to
PS3 — so Wii zones get correctly identified without needing `--platform wii`.

Editor saves of Wii FFs flow through `FastFileSaveService.Save`, which derives
`platform="Wii"` from `openedFastFile.IsWii` (set by `FastFileInfo.FromFile` at
open time when version `0x19B` is detected).

## Known unknowns

- **CoD4 Wii format** — presumed same shape (single zlib stream, BE, PC enum, 56-byte
  header) but no samples confirmed yet.
- **`BlockSizeTemp` computation rule** — preserved from source on edit-and-save. A
  fresh-zone Wii build would need to compute it.
- **In-game loader strictness** — no in-game test of an edited+saved Wii FF yet.
- **`BlockSizeIndex` semantics** — what does the engine actually use this for on Wii?
  Likely something to do with vertex/index buffers but unconfirmed.
