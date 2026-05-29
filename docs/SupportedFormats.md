# Supported Formats

This document lists what the FastFile Tools can currently parse, edit, and rebuild.

## Supported Games & Platforms

| Game | PS3 | Xbox 360 | PC | Wii |
|------|-----|----------|-----|-----|
| CoD4: Modern Warfare | ✅ Full | ✅ Full | 🟡 Partial | ⚠️ Extract |
| WaW: World at War | ✅ Full | ✅ Full | 🟡 Partial | 🟡 Partial |
| MW2: Modern Warfare 2 | ✅ Full | 🔬 Full (unverified) | 🟡 Partial | ➖ |
| Ghosts | 🟡 Partial+lua | ❓ Untested | ❓ Untested | ➖ |

### Version IDs

| Game | Console | PC | Wii |
|------|---------|-----|-----|
| CoD4 | `0x00000001` | `0x00000005` | `0x000001A2` |
| WaW | `0x00000183` | `0x00000183` | `0x0000019B` |
| MW2 | `0x0000010D` | `0x00000114` | ➖ |
| Ghosts | `0x0000022E` | — | ➖ |

### Legend
- ✅ **Full** - Decompress, parse assets, edit, and recompress
- 🟡 **Partial** - Decompress, parse + edit rawfile/localize, recompress (round-trip verified, in-game test pending). Other asset types currently skipped.
- 📖 **Read-only** - Decompress, parse rawfile/localize, but **no recompress yet**. Opens both unsigned and signed retail files.
- 🔬 **Full (unverified)** - Implementation feature-parity with the verified platform; no hardware load test yet
- ⚠️ **Extract** - Decompress to zone file only (no asset editing/recompress)
- ❓ **Untested** - No samples available; format may or may not match the verified platform
- ➖ **Not Available** - Game not released on this platform

### PC Notes
- PC WaW save uses a single zlib stream (not the 64KB block format used by console).
- Round-trip is byte-stable for WaW PC: decompress → recompress → decompress produces identical zone bytes (verified against 4 retail samples).
- Editable asset types on CoD4/WaW PC: **rawfile** (.cfg / .gsc / .csc / etc.) and **localize**.
- Other asset types (weapon, menufile, xanim, stringtable, material, techset, image) are detected and listed but not yet parsed on PC.

### MW2 PC Notes
- **Distinct from CoD4/WaW PC.** MW2 PC has:
  - A 9-byte preamble (`allowOnlineUpdate` + `fileCreationTime`) between the standard header and the zlib stream — *shorter* than the 25-byte MW2 PS3/Xbox 360 extended header.
  - A 56-byte zone header (8 blockSize slots, same as Wii WaW) instead of WaW PC's 52-byte layout.
  - Little-endian rawfile size fields (`compressedLen`, `len`).
  - Its own asset type enum (`MW2AssetTypePC`) — shifted +1 from PS3 for IDs ≥ `0x09` because PC has both `vertexshader` (`0x07`) AND `vertexdecl` (`0x08`).
- **Two FF variants**:
  - **Unsigned** (`IWffu100`): single zlib stream at file offset `0x15`. SP/campaign files.
  - **Signed** (`IWff0100`): "authed chunks" format using `IWffs100` at offset `0x15`, then 8144-byte `DB_AuthHeader` with RSA-2048 signature and 244 SHA-256 master block hashes, then 8KB chunks in groups of 257 (1 hash chunk + 256 data chunks). Multiplayer + patch files.
- **Recompression** (`FastFileProcessor.CompressMW2PC`) writes the unsigned layout: standard header + 9-byte preamble (preserved from the original FF when available) + single zlib stream. Signed inputs are saved as unsigned — re-signing the `DB_AuthHeader` requires Infinity Ward's RSA-2048 private key. Unsigned FFs are a valid loadable variant (used for SP/campaign files in retail).
- See `docs/MW2_PC_FastFile_Format.md` for the full breakdown.

### Ghosts (IW6) Notes
- **Read-only.** PS3 retail `.ff` files decompress + inflate to a fully-expanded `.zone`, the asset pool walks across all sample zone types (patch / DLC / base — header-counts-driven via `FastFileLib.GhostsZoneLayout`), and content parsers exist for **rawfile** (zlib-wrapped short header), **scriptfile** (zlib-wrapped long header), and **luafile** (flat 16-byte header, Lua 5.1 bytecode body). Flat-binary types (xmodel/image/sound/techset/weapon/material/…) are listed in the asset pool with type names but their bodies aren't parsed — each would need its own struct reverse-engineering.
- Both FF variants share the same downstream layout from `IWffS100` onwards:
  - **Patch FF** (e.g. `patch_common_mp.ff`): `IWffS100` immediately after the 36-byte outer header at file offset `0x24`.
  - **Base FF** (e.g. `common.ff`): a 12 KB index table sits between the outer header and `IWffS100` (which lands at `0x3294` for the sample). The index table's record format hasn't been reverse-engineered; it isn't needed for decompression.
- Decompression anchors on `IWffS100`: 8 KB `DB_AuthHeader` + 48 bytes padding + 112 KB "LO" metadata region + raw-deflate block stream at `IWffS100 + 0x20000`. Each block has a 2-byte BE size header and decompresses to exactly 64 KB.
- Inner per-asset zlib streams (used by rawfile + scriptfile bodies) are expanded inline as a second pass — final `.zone` has all asset content readable directly in a hex view (0 residual `78 XX` streams across all tested samples).
- Zone layout (same shape as CoD4/WaW/MW2): fixed XFile header with **`tagCount` @ `0x28`** and **`assetCount` @ `0x30`** (both BE u32) drive navigation. Pool location: tagCount=0 → pool at `0x38`; tagCount>0 → skip tag placeholders + tag strings then probe forward 32 bytes for the first valid pool entry. Each pool entry is 8 bytes `[ptr][type BE u32 ≤ 0x35]`; pointer accepts four conventions including the IW6-specific `0x40`-flagged form (not just the WaW `0x80`-flagged form).
- Two zlib-wrapped header shapes: "long" (24 bytes, 8 trailing FFs, 3 size u32s — scriptfile/MPTYPE/AITYPE) and "short" (16 bytes, 4 trailing FFs, 2 size u32s — rawfile). The first u32 after the leading FFs is always the exact zlib stream byte count.
- **Luafile** uses a different layout: `[FF*4][size BE][unk u32][FF*4]<name>\0<Lua bytecode>`, no zlib wrapping. Bodies are IW6-customized Lua 5.1 bytecode (format byte `0x0D`, signature `\x1B LuaQ`). Original Lua source isn't in the FF — `FastFileLib.LuaBytecodeInspector` produces an extracted-strings summary so the editor's text viewer can show menu/widget/function/identifier names without needing a full decompiler.
- Auth header is the same shape as MW3's `DB_AuthHeader` but with SHA-1 hashes (20 bytes + 12 zero pad in 32-byte slots) instead of SHA-256, and uses `IWffS100` (capital S) inner magic vs MW3's lowercase `IWffs100`.
- No encryption anywhere — outer is raw deflate, inner is standard zlib.
- No save support: re-signing the `DB_AuthHeader` requires Infinity Ward's RSA-2048 private key.
- Only PS3 verified. Xbox 360 / Wii-U / PC variants use shifted asset type IDs and aren't wired up.
- See `docs/Ghosts_FastFile_Format.md` for the format reference and `docs/Ghosts_Extraction_Howto.md` for the extraction algorithm.

### Wii Notes
- WaW Wii uses a **single zlib stream** like PC (not block format), but the zone is **big-endian** like PS3.
- Zone header is **56 bytes** (8 blockSize slots — has an extra `BlockSizeIndex` slot that PS3 doesn't).
- Asset entries use the **PC-style enum** (no `pixelshader` or `vertexshader` types), so the same enum mapping `CoD5AssetTypePC` is used for both PC and Wii — just with BE byte order on Wii.
- Editable asset types on Wii: **rawfile** and **localize** (same scope as PC).
- Save support has the same engine as PC's single-zlib-stream path; round-trip not yet verified against an actual Wii in-game test.

### Xbox 360 Notes
- Xbox 360 requires a **patched XEX** to load modified FastFiles
- Original signed FastFiles are converted to unsigned format when saving
- The editor preserves hash tables from original files but cannot regenerate RSA signatures

### MW2 Xbox 360 Notes
- Shares the same `MW2GameDefinition` as MW2 PS3 — rawfile / localize / weapon / partial menu support all wired up.
- **Decompress**: handles both formats — unsigned (single zlib stream after 25-byte `DB_Header`) and signed (IW4 authed-chunks at `0x25`, same format as MW2 PC signed but with the full 25-byte `DB_Header` instead of the 9-byte PC preamble).
- **Save**: writes the unsigned single-zlib variant. Signed input → unsigned output (no IW RSA-2048 private key).
- **Zone layout**: 48-byte zone header (drops `BlockSizeVertex`), `MW2AssetTypeXbox360` enum (no `vertexshader`, IDs ≥ `0x07` shift −1 from PS3).
- **Status**: code path complete; round-trip not yet verified on real Xbox 360 hardware. The implementation parallels MW2 PS3 — the same parsing/editing pipeline runs above the platform-specific FF I/O layer in `FastFileLib`.

---

## Platform Compression Formats

| Game | PS3 | Xbox 360 | PC | Wii |
|------|-----|----------|-----|-----|
| CoD4 | Block (raw deflate) | Block (raw deflate) | Single stream (zlib) ¹ | Single stream (zlib) ¹ |
| WaW | Block (raw deflate) | Block (raw deflate) | Single stream (zlib) | Single stream (zlib) |
| MW2 | Block (raw deflate) | Single stream (zlib) | Single stream (unsigned) / **Authed chunks (signed)** ² | ➖ |

¹ Verified directly for WaW PC and WaW Wii against retail samples; CoD4 PC/Wii presumed same shape but no samples available to confirm.

² MW2 PC ships in two flavors: unsigned SP files use a plain single zlib stream at file offset `0x15`; signed multiplayer/patch files use Infinity Ward's "authed chunks" format with 8KB chunks in groups of 257 (1 hash chunk skipped + 256 data chunks fed to one zlib stream).

### Block vs Single Stream vs Authed Chunks
- **Block compression**: Data split into 64KB chunks, each compressed separately with 2-byte length prefix.
- **Single stream**: Entire zone compressed as one continuous zlib stream.
- **Authed chunks** (signed MW2 PC only): Same logical zlib stream as "single stream", but split into 8KB chunks for incremental SHA-256 authentication. Every 257th chunk holds the hash table for the next 256 data chunks.

### Header Formats
| Format | Magic | Description |
|--------|-------|-------------|
| Unsigned | `IWffu100` | Standard format for PS3, CoD4/WaW PC, unsigned Xbox 360, MW2 PC SP files |
| Signed | `IWff0100` | Xbox 360 signed format (RSA signature) **and** MW2 PC retail (LE version `0x114`) |
| Streaming | `IWffs100` | Inner magic for signed-streaming layouts. At offset `0x0C` for Xbox 360 CoD4/WaW. At offset `0x15` for MW2 PC (after the 9-byte preamble). |

---

## Asset Support Summary

| Support Level | Asset Types | Capabilities |
|---------------|-------------|--------------|
| ✅ **Full** | `rawfile`, `localize`, `weapon` | Parse, view, edit, save |
| 🟡 **Partial** | `menufile` (MenuList) | MenuList wrapper + menu[0] only; tree shown in **Menus** tab. `menuDef_t` binary deserializer not implemented yet — multi-menu files (like `ui_mp/menus.txt` with 276 menus) show as `name (N menus)` with a single `menu (binary parsing pending)` placeholder child. Single-menu files (like `ui_mp/main.menu`) flatten to one row. |
| 👁️ **View Only** | `stringtable`, `xanim`, `material`, `techset`, `image`, `col_map_sp`, `col_map_mp` | Parse and display, no editing |
| 📋 **Detected** | All others | Shows in asset pool, no parsing |

---

## Asset Type IDs (Full/View Support)

All assets in the zone pool are automatically detected and displayed. The tables below show only assets with parsing/editing support.

### Call of Duty 4: Modern Warfare

| Asset Type | ID | Support |
|------------|-----|---------|
| xanim | `0x02` | 👁️ View |
| material | `0x04` | 👁️ View |
| techset | `0x07` | 👁️ View |
| image | `0x08` | 👁️ View |
| col_map_sp | `0x0C` | 👁️ View |
| col_map_mp | `0x0D` | 👁️ View |
| menufile | `0x16` | ✅ Full |
| localize | `0x18` | ✅ Full |
| weapon | `0x19` | ✅ Full |
| rawfile | `0x21` | ✅ Full |
| stringtable | `0x22` | 👁️ View |

### Call of Duty: World at War

| Asset Type | ID | Support |
|------------|-----|---------|
| xanim | `0x04` | 👁️ View |
| material | `0x06` | 👁️ View |
| techset | `0x09` | 👁️ View |
| image | `0x0A` | 👁️ View |
| col_map_sp | `0x0D` | 👁️ View |
| col_map_mp | `0x0E` | 👁️ View |
| menufile | `0x17` | ✅ Full |
| localize | `0x19` | ✅ Full |
| weapon | `0x1A` | ✅ Full |
| rawfile | `0x22` | ✅ Full |
| stringtable | `0x23` | 👁️ View |

### Call of Duty: Modern Warfare 2 (PS3)

| Asset Type | ID | Support |
|------------|-----|---------|
| xanim | `0x02` | 👁️ View |
| material | `0x05` | 👁️ View |
| techset | `0x09` | 👁️ View |
| image | `0x0A` | 👁️ View |
| col_map_sp | `0x0E` | 👁️ View |
| col_map_mp | `0x0F` | 👁️ View |
| menufile | `0x19` | ✅ Full |
| localize | `0x1A` | ✅ Full |
| weapon | `0x1B` | ✅ Full |
| rawfile | `0x23` | ✅ Full |
| stringtable | `0x24` | 👁️ View |

### Call of Duty: Modern Warfare 2 (Xbox 360)

IDs shift **−1** from PS3 for types ≥ `0x07` because Xbox 360 lacks `vertexshader`.

| Asset Type | ID | Support |
|------------|-----|---------|
| menufile | `0x17` | ✅ Full |
| menu | `0x18` | 📋 Detected |
| localize | `0x19` | ✅ Full |
| weapon | `0x1A` | ✅ Full |
| rawfile | `0x22` | ✅ Full |
| stringtable | `0x23` | 👁️ View |

### Call of Duty: Modern Warfare 2 (PC)

IDs shift **+1** from PS3 for types ≥ `0x09` because PC has both `vertexshader` (`0x07`) and `vertexdecl` (`0x08`).

| Asset Type | ID | Support |
|------------|-----|---------|
| menufile | `0x19` | 📖 Read |
| localize | `0x1A` | 🟡 Partial |
| weapon | `0x1C` | 📖 Read (pattern-matched; alignment may be off) |
| rawfile | `0x24` | 🟡 Partial |
| stringtable | `0x25` | 👁️ Detected (not parsed) |

Other MW2 PC asset types (`techset`, `xanim`, `material`, `image`) are listed in the asset pool but the parsers are BE-only and produce no output. Recompress writes the unsigned MW2 PC format (`FastFileProcessor.CompressMW2PC`); signed inputs round-trip to unsigned outputs.

---

## Feature Capabilities

### RawFile Operations
| Feature | Status | Description |
|---------|--------|-------------|
| View | ✅ | Display raw file content in text editor |
| Edit | ✅ | Modify text content directly |
| Extract | ✅ | Save raw file to disk |
| Inject | ✅ | Replace raw file content from external file |
| Resize | ✅ | Increase file size allocation (triggers zone rebuild) |

### Menus (menufile) Operations
The editor's **Menus** tab shows each `menufile` (a.k.a. `MenuList` per the wiki — see https://codresearch.dev/index.php/MenuFile_Asset) found in the zone.

| Feature | Status | Description |
|---------|--------|-------------|
| Detect menufile assets | ✅ | All menufile records from the asset pool are surfaced (full zone scan, not the old 1MB window) |
| MenuList wrapper | ✅ | Reads name + menuCount + pointer array correctly |
| All menus located | ✅ | `Iw4MenuDeserializer` walks each `menuDef_t` field-by-field (port of OpenAssetTools' `ContentLoaderIW4` + `menuDef_t.txt` reorder spec). It reads the binary struct + recursively walks every inline pointer (window strings, event handlers, key handlers, expression statements, items array, support data, etc.). When the deserializer hits an edge case it doesn't handle yet, it falls back to a signature scanner for the remaining menus so the user still sees something. All declared menus get parsed — `ui_mp.ff::menus.txt`: 276/276, MW2 TU6 patch: 4/4, 18/18, 5/5, etc. |
| Real menu names | ✅ | Recovered from inline `window.name` string for menus the deserializer walked; signature-scan fallback uses heuristic ASCII detection. Examples: `menu_online_barracks`, `hud_fullscreen`, `settings_map`, `menu_xboxlive_privatelobby`, `player_popup_party`, `playercard_spectator_hd`. |
| Per-menu rect / colors / itemCount | 🟡 | Read from fixed offsets in `windowDef_t`. Reliable for any menu the deserializer fully parsed. Menus where the deserializer's `itemDef_s` walker stops early and the signature fallback kicks in may still pick up an inner item rect/itemCount. Still navigable & editable in either case. |
| Inline strings | ✅ | `MenuDecompiler` extracts event-handler script strings, item text, etc. with original offsets so edits round-trip back to the zone bytes. |
| Edit & save (zone-level) | ✅ | The text editor surfaces `// 0x{offset}` annotations next to each editable value/string. Modify the number/string in place; `ApplyMenuFileChangesToZone` writes the updated bytes back at their original offsets. Length-bound for strings (truncate or pad with nulls). Triggers a zone-level save like any other edit. |
| Full `menuDef_t` field reconstruction | ❌ | The C# code does NOT walk the engine's struct field-by-field — only the editable subset above is extracted. Adding/removing items, retargeting event handlers, etc. requires a proper deserializer port from OpenAssetTools. |

Display rules:
- `name.menu` containing 1 menu → flat row: `ui_mp/main.menu [N items]`
- `name.menu` containing 1 menu but parse failed → `ui_mp/xxx.menu [1 menu (parse failed)]`
- `name.txt` or `name.menu` containing N > 1 menus → tree: `ui_mp/main.menu (4 menus)` with one child per parsed menu. Child labels use the extracted window name when the parser found one (e.g. `mw2_main_background`, `ac130_overlay_grain`); otherwise fall back to `menu #N`. Item count is appended when known: `menu #0 [28 items]`.
- If the asset pool declared more menus than the scanner located, a trailing `+N menu(s) not located` child surfaces the gap.

### Localize Operations
| Feature | Status | Description |
|---------|--------|-------------|
| View | ✅ | Display all localized strings with keys |
| Edit | ✅ | Modify individual string values (double-click) |
| In-place Patch | ✅ | Save changes without rebuild (if text size ≤ original) |
| Zone Rebuild | ✅ | Automatically rebuild zone when text size increases |
| Export | ✅ | Export all entries to tab-separated TXT file |
| Import | ✅ | Import entries from TXT file (triggers zone rebuild) |

### Zone Operations
| Feature | Status | Description |
|---------|--------|-------------|
| Decompress | ✅ | Extract .zone from .ff file |
| Recompress | ✅ | Rebuild .ff from modified .zone |
| Fresh Zone Build | ✅ | Create new zone with supported assets only |
| View Hex | ✅ | View raw zone data in hex viewer |
| Asset Pool View | ✅ | Display all assets in zone |

---

## Limitations

### Zone Rebuild Behavior
When a zone is rebuilt (due to size changes or import):

| Asset Type | Preserved? |
|------------|------------|
| rawfile | ✅ Yes |
| localize | ✅ Yes |
| All other types | ❌ **Lost** |

**Warning**: Zones containing unsupported asset types will show a warning before rebuild.

### Known Limitations
- Xbox 360 requires patched XEX to load modified FastFiles
- Cannot edit binary assets (models, textures, sounds, etc.)
- PC WaW/CoD4: rawfile and localize editing supported; other asset types are listed but not parsed/editable yet
- Wii WaW: rawfile and localize editing supported; same scope as PC
- **MW2 PC**: rawfile and localize editing supported. Saves always produce the unsigned variant — signed retail files (multiplayer, patch) round-trip to unsigned outputs since RSA re-signing requires Infinity Ward's private key. See `docs/MW2_PC_FastFile_Format.md`.
- Some edge cases in localize parsing for unusual character encodings

---

## File Format Reference

### FastFile Structure - PS3/Unsigned (.ff)
```
┌─────────────────────────────────┐
│ Header (12 bytes)               │
│  - Magic: "IWffu100" (8 bytes)  │
│  - Version: 4 bytes (big-endian)│
├─────────────────────────────────┤
│ [MW2 only] Extended Header      │
│  - 25 bytes metadata            │
├─────────────────────────────────┤
│ Compressed Block 1              │
│  - Size: 2 bytes (big-endian)   │
│  - Data: up to 64KB compressed  │
├─────────────────────────────────┤
│ Compressed Block N...           │
├─────────────────────────────────┤
│ End Marker: 0x00 0x01           │
└─────────────────────────────────┘
```

### FastFile Structure - Xbox 360 Signed (CoD4/WaW)
```
┌─────────────────────────────────┐
│ Header (12 bytes)               │
│  - Magic: "IWff0100" (8 bytes)  │
│  - Version: 4 bytes (big-endian)│
├─────────────────────────────────┤
│ Streaming Header (8 bytes)      │
│  - Magic: "IWffs100"            │
├─────────────────────────────────┤
│ Hash Table (0x3FF8 bytes)       │
│  - SHA-1 hashes for validation  │
├─────────────────────────────────┤
│ Single Zlib Stream              │
│  - Entire zone as one stream    │
└─────────────────────────────────┘
```

### FastFile Structure - MW2 Xbox 360
```
┌─────────────────────────────────┐
│ Header (12 bytes)               │
│  - Magic: "IWffu100" (8 bytes)  │
│  - Version: 4 bytes (big-endian)│
├─────────────────────────────────┤
│ Extended Header (25 bytes)      │
│  - allowOnlineUpdate (1 byte)   │
│  - fileCreationTime (8 bytes)   │
│  - region (4 bytes)             │
│  - entryCount (4 bytes)         │
│  - fileSizes (8 bytes)          │
├─────────────────────────────────┤
│ Single Zlib Stream              │
│  - Entire zone as one stream    │
└─────────────────────────────────┘
```

### FastFile Structure - MW2 PC (Unsigned)
```
┌─────────────────────────────────┐
│ Header (12 bytes)               │
│  - Magic: "IWffu100" (8 bytes)  │
│  - Version: 4 bytes (LE) = 0x114│
├─────────────────────────────────┤
│ Preamble (9 bytes)              │
│  - allowOnlineUpdate (1 byte)   │
│  - fileCreationTime (8 bytes)   │
│  - (NO region/entryCount/sizes) │
├─────────────────────────────────┤
│ Single Zlib Stream @ 0x15       │
│  - Entire zone as one stream    │
└─────────────────────────────────┘
```

### FastFile Structure - MW2 PC (Signed) — Authed Chunks
```
┌─────────────────────────────────┐
│ Header (12 bytes)               │
│  - Magic: "IWff0100" (8 bytes)  │
│  - Version: 4 bytes (LE) = 0x114│
├─────────────────────────────────┤
│ Preamble (9 bytes)              │
│  - allowOnlineUpdate + time     │
├─────────────────────────────────┤
│ DB_AuthHeader (8144 bytes)      │  @ 0x15
│  - "IWffs100" magic (8 bytes)   │
│  - reserved (4 bytes)           │
│  - subheaderHash SHA-256 (32 B) │
│  - signedSubheaderHash RSA-2048 │
│  - fastfileName (32 bytes)      │
│  - reserved (4 bytes)           │
│  - masterBlockHashes[244]       │   (244 × SHA-256 = 7808 B)
├─────────────────────────────────┤
│ Padding (48 bytes)              │  pad to AUTHED_CHUNK_SIZE 0x2000
├─────────────────────────────────┤
│ Authed Chunk Group 0  @ 0x2055  │
│  ┌─Chunk 0 (8KB): hash table──┐ │  256 × SHA-256 for chunks 1-256
│  │  (SKIP for decompression)  │ │  this chunk's hash matches
│  └────────────────────────────┘ │  masterBlockHashes[0]
│  ┌─Chunk 1 (8KB): zlib data ──┐ │  @ 0x4015
│  ├─Chunk 2 (8KB): zlib data ──┤ │
│  │     ... (256 chunks) ...   │ │
│  └─Chunk 256 (8KB): zlib data ┘ │
├─────────────────────────────────┤
│ Authed Chunk Group 1, 2, ...    │  same shape, masterBlockHashes[N]
└─────────────────────────────────┘
```

### Zone Structure (.zone)
```
┌─────────────────────────────────┐
│ Zone Header (52 bytes)          │
│  - Memory allocation values     │
│  - Asset counts and pointers    │
├─────────────────────────────────┤
│ Asset Pool                      │
│  - Asset entries (8 bytes each) │
│  - [type: 4 bytes][ptr: 4 bytes]│
├─────────────────────────────────┤
│ Asset Data                      │
│  - RawFile data                 │
│  - Localize entries             │
│  - Other asset data             │
├─────────────────────────────────┤
│ Footer                          │
│  - Zone name (null-terminated)  │
└─────────────────────────────────┘
```

---

## References

- [ZoneTool](https://github.com/ZoneTool/zonetool) - Reference for compression formats
- [COD Research Wiki](https://codresearch.dev/)
