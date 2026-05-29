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
- ✅ **End-to-end modding verified on real Wii hardware** — for both CoD4 Reflex
  Edition and WaW Wii: edit a rawfile in the editor → recompress → load on the
  device → the modded behaviour shows up in actual gameplay. Confirms the full
  pipeline, not just header parsing.
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

Editor saves of Wii FFs flow through the editor's `FastFileSave` shim → `FastFileLib.FastFileSaveService.Save`, which derives
`platform="Wii"` from `openedFastFile.IsWii` (set by `FastFileInfo.FromFile` at
open time when version `0x19B` is detected).

## CoD4 Wii (Reflex Edition) — confirmed

CoD4 Wii shares most of the WaW Wii shape (single zlib stream, BE, 56-byte header) but
uses a **different asset type enum**. Verified from retail `CoD-MWR-extracted` files:

| Aspect | WaW Wii | **CoD4 Wii (Reflex)** |
|---|---|---|
| FF version (BE) | `00 00 01 9B` (0x19B) | `00 00 01 A2` (0x1A2) |
| Studio | Treyarch | Infinity Ward |
| Zone header size | 56 bytes (8 blockSize slots) | same |
| Compression | Single zlib stream | same |
| Endianness | Big (PowerPC) | same |
| Rawfile entry format | 12-byte CoD4/WaW BE | same |
| **Asset type enum** | `CoD5AssetTypePC` (no shader slots) | **`CoD4AssetTypeXbox360`** (drops vertexshader only) |
| rawfile asset id | 0x20 | **0x20** (same byte, different reason) |
| techset asset id | 0x07 | **0x06** |
| image asset id | 0x08 | **0x07** |
| Extensions | none observed | `packindex = 0x22` (for `.pak` texture archives) |

Asymmetry proof: `ac130_load.zone` (a Reflex load-screen FF) has asset distribution
`1×0x07 + 3×0x06 + 1×0x20`. With CoD4 Xbox 360 enum that's `1 image + 3 techsets +
1 rawfile` — exactly what a load screen contains. With CoD4 PC enum it would be
`1 sound + 3 images + 1 stringtable` for a loading screen, which is implausible.

IW (CoD4) kept the pixelshader slot at 0x05 even though Wii has fixed-function-ish
graphics (TEV stages, no programmable shaders); Treyarch (WaW) cleaned up by removing
both shader slots. Different studios, different enum decisions for the same hardware.

### CoD4 Wii zone variants

CoD4 Wii FFs come in three flavors per map:
- `<mapname>_temp.ff` — rawfile-only zones (mission scripts as `.gsc`/`.csc`)
- `<mapname>_load.ff` — load-screen FFs (image + techsets + rawfile)
- `<mapname>_loose.ff` — texture pack indexes (`packindex` entries pointing into `.pak` files)

The `_temp` FFs often have `ScriptStringCount = 0`, which means `ScriptStringsPtr` at
0x2C is `0x00000000` (null), **not** `0xFFFFFFFF`. Detection logic accepts both
(`FastFileInfo.IsZoneWiiInternal` checks `HasMarker(0x2C) || ScriptStringCount@0x28 == 0`).

### CLI roundtrip (CoD4 Wii)

```
ffcli compress map_temp.zone map_temp.ff --game cod4 --platform wii
```

The `--game cod4 --platform wii` flags are required: the zone bytes alone can't
distinguish CoD4 Wii from WaW Wii (rawfile=0x20 in both enums, same header layout).
Without flags, auto-detect defaults to WaW Wii (more common modding target). The
editor's save path knows the game/platform from the loaded FF header, so it doesn't
need overrides.

## Known unknowns

- **`BlockSizeTemp` computation rule** — preserved from source on edit-and-save. A
  fresh-zone Wii build would need to compute it.
- **In-game loader strictness** — no in-game test of an edited+saved Wii FF yet.
- **`BlockSizeIndex` semantics** — what does the engine actually use this for on Wii?
  Likely something to do with vertex/index buffers but unconfirmed.
- **CoD4 Wii packindex (0x22) format** — observed in `_loose.ff` files. Header starts
  with `"0KAP"` (PAK0 reversed) followed by an entry count and a list of
  `[hash][offset]` pairs. Full struct not yet reverse-engineered.
