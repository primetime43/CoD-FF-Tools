# Zone File Format Documentation

This document describes the zone file format for Call of Duty FastFiles (CoD4, WaW, MW2) based on reverse engineering and testing.

## Overview

A zone file is the decompressed content of a FastFile (.ff). The FastFile compresses the zone data in 64KB blocks using zlib compression (without the 2-byte zlib header).

## FastFile Structure

```
[8 bytes]  Magic: "IWffu100" (unsigned) or "IWff0100" (signed)
[4 bytes]  Version (big-endian):
           - CoD4: 0x00000001
           - WaW:  0x00000183
           - MW2:  0x0000010D
[N bytes]  Compressed blocks (each block: 2-byte length + compressed data)
[2 bytes]  End marker: 0x00 0x01
```

### Compressed Block Format

Each compressed block:
```
[2 bytes]  Block length (big-endian) - length of compressed data
[N bytes]  Compressed data (zlib without 2-byte header, WITH 4-byte Adler-32 checksum)
```

**Important:** The compressed data is zlib format with the 2-byte header stripped but the 4-byte Adler-32 checksum retained.

## Zone File Structure

```
[52 bytes]   Zone Header
[N bytes]    Asset Table (8 bytes per entry)
[N bytes]    Raw Files Section
[N bytes]    Localized Strings Section (optional)
[N bytes]    Footer Section
[N bytes]    Padding (to 64KB boundary)
```

## Zone Header (52 bytes)

The zone header consists of two structures: **XFile** (memory block allocation) and **XAssetList** (asset metadata).

### XFile Structure (Bytes 0x00-0x23)

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0x00 | 4 | ZoneSize | Total data size (big-endian), excludes 36-byte header |
| 0x04 | 4 | ExternalSize | External resource allocation (usually 0x00000000) |
| 0x08 | 4 | BlockSizeTemp | XFILE_BLOCK_TEMP allocation (big-endian) |
| 0x0C | 4 | BlockSizePhysical | XFILE_BLOCK_PHYSICAL allocation (usually 0x00000000) |
| 0x10 | 4 | BlockSizeRuntime | XFILE_BLOCK_RUNTIME allocation (usually 0x00000000) |
| 0x14 | 4 | BlockSizeVirtual | XFILE_BLOCK_VIRTUAL allocation (usually 0x00000000) |
| 0x18 | 4 | BlockSizeLarge | XFILE_BLOCK_LARGE allocation (big-endian) |
| 0x1C | 4 | BlockSizeCallback | XFILE_BLOCK_CALLBACK allocation (usually 0x00000000) |
| 0x20 | 4 | BlockSizeVertex | XFILE_BLOCK_VERTEX allocation (PS3/PC only) |

### XAssetList Structure (Bytes 0x24-0x33)

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0x24 | 4 | ScriptStringCount | Number of script strings/tags (usually 0x00000000 for patch zones) |
| 0x28 | 4 | ScriptStringsPtr | Memory pointer placeholder: 0xFFFFFFFF |
| 0x2C | 4 | AssetCount | Number of assets in the pool (big-endian) |
| 0x30 | 4 | AssetsPtr | Memory pointer placeholder: 0xFFFFFFFF |

### Memory Block Values (Critical)

These values are **critical** - the game engine uses them for memory allocation. Without correct values, the game will crash or hang.

| Game | BlockSizeTemp (0x08) | BlockSizeVertex (0x20) |
|------|----------------------|------------------------|
| CoD4 | 0x00000F70 | 0x00000000 |
| WaW | 0x000010B0 | 0x0005F8F0 |
| MW2 | 0x000003B4 | 0x00001000 |

### Size Calculations

```
ZoneSize = AssetTableSize + RawFilesSize + LocalizedSize + FooterSize + 16
BlockSizeLarge = 52 + AssetTableSize + RawFilesSize + LocalizedSize + FooterSize
```

Note: `BlockSizeLarge - ZoneSize = 36` (the header size minus 16)

## Asset Table

Each asset entry is 8 bytes:
```
[3 bytes]  Padding: 0x00 0x00 0x00
[1 byte]   Asset Type ID
[4 bytes]  Pointer marker: 0xFF 0xFF 0xFF 0xFF
```

### Asset Type IDs

| Game | RawFile Type | Localize Type |
|------|--------------|---------------|
| CoD4 | 0x21 (33) | 0x18 (24) |
| WaW | 0x22 (34) | 0x19 (25) |
| MW2 | 0x23 (35) | 0x1A (26) |

### Final Entry Requirement

**Important:** The asset table must include a **final rawfile entry** after all asset entries. This is required by the game engine.

```
Asset Table = [RawFile entries] + [Localize entries] + [1 Final RawFile entry]
AssetCount = RawFileCount + LocalizeCount + 1
```

Without this final entry, the game will fail to load the zone correctly.

## Raw File Entry Format

Rawfile entry layout depends on game + platform. `FastFileLib.RawFileScanner` is the canonical parser — call it instead of pattern-scanning by hand.

### CoD4 / WaW (all platforms) — 12-byte header
```
[4 bytes]  Marker: 0xFF 0xFF 0xFF 0xFF
[4 bytes]  Data size (big-endian)
[4 bytes]  Pointer marker: 0xFF 0xFF 0xFF 0xFF
[N bytes]  Filename (null-terminated ASCII string)
[N bytes]  File data
[1 byte]   Null terminator: 0x00
```
PC zones use the same BE size field here — only the outer zone header switches endianness on PC.

### MW2 PS3 / Xbox 360 — subsequent files (16-byte header)
```
[4 bytes]  Marker: 0xFF 0xFF 0xFF 0xFF
[4 bytes]  Compressed length (big-endian)
[4 bytes]  Uncompressed length (big-endian)
[4 bytes]  Pointer marker: 0xFF 0xFF 0xFF 0xFF
[N bytes]  Filename (null-terminated ASCII string)
[N bytes]  zlib-compressed file data (full zlib header)
```

### MW2 PS3 / Xbox 360 — first file (20-byte header)
Same as the 16-byte form but with an extra leading `0xFF 0xFF 0xFF 0xFF` marker.

### MW2 PC — 16-byte header, little-endian size fields
Same shape as the MW2 console 16-byte form, but `compressedLen` and `uncompressedLen` are stored little-endian. Reading them as BE produces nonsense (e.g. `0x18000000` for a 24-byte payload).

## Localized String Entry Format

Each localized string entry:
```
[8 bytes]  Marker: 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF
[N bytes]  Value string (null-terminated)
[N bytes]  Reference key (null-terminated)
```

## Footer Section

### CoD4/WaW Footer (12+ bytes)
```
[4 bytes]  Marker: 0xFF 0xFF 0xFF 0xFF
[4 bytes]  Padding: 0x00 0x00 0x00 0x00
[4 bytes]  Marker: 0xFF 0xFF 0xFF 0xFF
[N bytes]  Zone name (null-terminated with extra null)
```

### MW2 Footer (16+ bytes)
```
[4 bytes]  Marker: 0xFF 0xFF 0xFF 0xFF
[4 bytes]  Padding: 0x00 0x00 0x00 0x00
[4 bytes]  Padding: 0x00 0x00 0x00 0x00
[4 bytes]  Marker: 0xFF 0xFF 0xFF 0xFF
[N bytes]  Zone name (null-terminated with extra null)
```

## Padding

The zone file is padded with null bytes to align to a 64KB (0x10000) boundary.

## Common Issues and Solutions

### Issue: Game crashes on load
**Cause:** Missing or incorrect MemAlloc1/MemAlloc2 values
**Solution:** Ensure correct memory allocation values for the game version

### Issue: Game hangs/infinite loading
**Cause:** Incorrect AssetCount (missing +1 for final entry)
**Solution:** AssetCount must include all entries including the final rawfile entry

### Issue: Corrupted data after decompression
**Cause:** Missing Adler-32 checksum in compressed blocks
**Solution:** Use zlib compression and strip only the 2-byte header, keep the 4-byte checksum

### Issue: FF file not recognized
**Cause:** Missing end marker
**Solution:** Ensure FF file ends with 0x00 0x01 after last compressed block

## Patching vs Rebuilding

When modifying zone files:

### Patching (modifying existing zone)
- Only update ZoneSize header field
- Do NOT modify BlockSizeLarge (it's for memory allocation, set at creation)
- Shift data and update raw file size fields as needed

### Rebuilding (creating new zone)
- Calculate all header values fresh
- Include correct MemAlloc values
- Include final rawfile entry in asset table
- Count all assets including final entry for AssetCount
- Pad to 64KB boundary

**Recommendation:** When increasing raw file sizes, rebuild the zone from scratch rather than patching. This ensures all values are correctly calculated.

## References

- CoD Research: https://github.com/primetime43/CoD-Research
- FastFileLib source code
- Zone.md documentation
