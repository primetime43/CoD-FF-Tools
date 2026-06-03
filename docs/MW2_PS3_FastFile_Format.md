# MW2 PS3 FastFile & Zone Format Documentation

This document details the MW2 PS3 FastFile (.ff) and Zone file formats discovered during the development of CoD-FF-Tools. This information was gathered through reverse engineering and binary analysis of working MW2 PS3 mod files.

---

## Table of Contents

1. [Overview](#overview)
2. [Two-Level Compression](#two-level-compression)
3. [FastFile Header Structure](#fastfile-header-structure)
4. [Zone File Structure](#zone-file-structure)
5. [Asset Table Format](#asset-table-format)
6. [Raw File Format](#raw-file-format)
7. [Footer Format](#footer-format)
8. [Key Differences from CoD4/WaW](#key-differences-from-cod4waw)
9. [Common Pitfalls](#common-pitfalls)

---

## Overview

MW2 PS3 FastFiles use a more complex format compared to CoD4 and WaW. The key characteristics are:

- **Platform**: PlayStation 3 (Big-Endian byte order)
- **Magic**: `IWffu100` (unsigned format)
- **Version**: `0x0000010D` (269 decimal)
- **Two-level compression**: FF-level blocks + zone-level raw file compression

---

## Two-Level Compression

MW2 PS3 uses two distinct compression layers:

### FF-Level Compression (Outer Layer)

The FastFile itself is compressed in 64KB blocks using **raw deflate** (zlib without header).

| Property | Value |
|----------|-------|
| Algorithm | Deflate (no zlib header) |
| Block size | 65536 bytes (0x10000) |
| Length prefix | 2 bytes, big-endian |
| End marker | `0x00 0x01` |

**Important**: The compressed blocks do NOT have the zlib header bytes (`0x78 0x9C`). The 2-byte zlib header must be stripped when compressing.

```
FF Block Structure:
[2-byte length BE][raw deflate data][2-byte length BE][raw deflate data]...[0x00 0x01]
```

### Zone-Level Compression (Inner Layer)

Individual raw files within the zone CAN be compressed using **standard zlib** (with header).

| Property | Value |
|----------|-------|
| Algorithm | Zlib (with 0x78 header) |
| Header bytes | `0x78` followed by `0x01`, `0x5E`, `0x9C`, or `0xDA` |
| Per-file | Each raw file compressed independently |

**Note**: Not all raw files are compressed. The compression is indicated by the `compressedLen` field in the raw file header.

---

## FastFile Header Structure

MW2 PS3 uses an extended header format with additional fields beyond CoD4/WaW.

### Standard Header (12 bytes)

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0x00 | 8 | magic | `IWffu100` for unsigned |
| 0x08 | 4 | version | `0x0000010D` (big-endian) |

### Extended Header (follows standard header)

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0x0C | 1 | allowOnlineUpdate | Usually `0x01` for patch files |
| 0x0D | 8 | fileCreationTime | Windows FILETIME format |
| 0x15 | 4 | region | Region code (usually `0x00000001`) |
| 0x19 | 4 | entryCount | Number of entries (usually 0) |
| 0x1D | var | entries | `entryCount * 0x14` bytes (if any) |
| var | 4 | fileSize | Actual FF file size on disk (big-endian) |
| var+4 | 4 | maxFileSize | Same as fileSize (big-endian) |

**Critical**: The `fileSize` and `maxFileSize` fields must contain the actual FF file size AFTER compression. These values must be updated after the compression process completes.

### Example Header Hex Dump

```
00: 49 57 66 66 75 31 30 30  IWffu100 (magic)
08: 00 00 01 0D              version = 0x10D
0C: 01                       allowOnlineUpdate = 1
0D: XX XX XX XX XX XX XX XX  fileCreationTime
15: 00 00 00 01              region = 1
19: 00 00 00 00              entryCount = 0
1D: 00 02 DA 21              fileSize (example: 186913)
21: 00 02 DA 21              maxFileSize (same)
25: [compressed blocks start]
```

---

## Zone File Structure

The decompressed zone file has a specific structure that differs from CoD4/WaW.

### Zone Header (52 bytes for MW2 PS3)

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0x00 | 4 | totalSize1 | Points to footer header start |
| 0x04 | 4 | externalSize | External allocation (usually 0) |
| 0x08 | 4 | blockSizeTemp | `0x000003B4` for MW2 |
| 0x0C | 4 | blockSizePhysical | Usually 0 |
| 0x10 | 4 | blockSizeRuntime | Usually 0 |
| 0x14 | 4 | blockSizeVirtual | Usually 0 |
| 0x18 | 4 | totalSize2 | Points to end of raw file data |
| 0x1C | 4 | blockSizeCallback | Usually 0 |
| 0x20 | 4 | blockSizeVertex | `0x00001000` for MW2 |
| 0x24 | 4 | scriptStringCount | Usually 0 |
| 0x28 | 4 | scriptStringsPtr | `0xFFFFFFFF` placeholder |
| 0x2C | 4 | assetCount | Number of assets |
| 0x30 | 4 | assetsPtr | `0xFFFFFFFF` placeholder |

**Important**: MW2 PS3 uses the **same 52-byte (`0x34`) header as CoD4/WaW** — the full
XFile + XAssetList layout, with `assetsPtr` at `0x30` and the asset pool starting at
`0x34`. (`FastFileConstants.GetZoneHeaderSize` returns `ZoneHeaderSize_PS3 = 0x34` for
MW2 PS3; only MW2 **Xbox 360** is 48 bytes, because it drops `blockSizeVertex`.)

> **Correction:** Earlier revisions of this doc described the MW2 PS3 header as 48 bytes
> with the pool starting at `0x30`. That counted the `assetsPtr` placeholder at `0x30` as
> the first asset entry — an off-by-one-field reading. The pool locator scans forward and
> tolerates either alignment, so a 48-byte-built mod still loads, but the canonical
> engine layout (and what the library models) is 52 bytes. The genuinely MW2-specific,
> mod-verified finding below — the `[ptr][type]` asset-entry **order** — is unaffected.

### Size Field Calculations

```
totalSize1 = headerSize + assetTableSize + rawFilesSize + localizedSize
totalSize2 = headerSize + assetTableSize + rawFilesSize + localizedSize + footerSize
```

Where:
- `headerSize` = 52 bytes for MW2 PS3 (same as CoD4/WaW; 48 for MW2 Xbox 360, 56 for MW2 PC)
- `assetTableSize` = `assetCount * 8` bytes
- `rawFilesSize` = sum of all raw file entries (headers + names + data)
- `localizedSize` = sum of all localized entries
- `footerSize` = 16 + zoneName length + 2 (null terminators)

---

## Asset Table Format

The asset table follows the zone header and any script strings.

**Note**: For mod files without script strings, the asset table starts immediately after the 52-byte header at offset `0x34`. Offset `0x30` holds the `assetsPtr` placeholder (`0xFFFFFFFF`), which is the last header field — not the first asset entry.

### Entry Format (8 bytes per entry)

**MW2 PS3 mod files** use `[ptr][type]` format:

| Offset | Size | Field | Value |
|--------|------|-------|-------|
| 0x00 | 4 | pointer | `0xFFFFFFFF` (placeholder) |
| 0x04 | 4 | type | `0x000000XX` (asset type ID, big-endian) |

```
Example at offset 0x34:  FF FF FF FF 00 00 00 23  (rawfile type 0x23)
```

**Verified**: This `[ptr][type]` format has been confirmed working by comparing a functional mod's zone file against a broken one. The working mod used `FF FF FF FF 00 00 00 23` format.

### Asset Type IDs (MW2 PS3)

| Type | ID | Hex |
|------|-----|-----|
| rawfile | 35 | 0x23 |
| localize | 26 | 0x1A |

### Asset Table Structure

```
[Entry 0: rawfile]  FF FF FF FF 00 00 00 23
[Entry 1: rawfile]  FF FF FF FF 00 00 00 23
...
[Entry N: rawfile]  FF FF FF FF 00 00 00 23  <- Final entry (footer)
```

The asset count includes all raw files + localized entries + 1 final entry for the footer.

---

## Raw File Format

Raw files in MW2 PS3 zones use a compressed format with variable header sizes.

### First Raw File Header (20 bytes)

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0x00 | 4 | marker1 | `0xFFFFFFFF` |
| 0x04 | 4 | marker2 | `0xFFFFFFFF` |
| 0x08 | 4 | compressedLen | Compressed data size (big-endian) |
| 0x0C | 4 | uncompressedLen | Original data size (big-endian) |
| 0x10 | 4 | pointer | `0xFFFFFFFF` |
| 0x14 | var | filename | Null-terminated ASCII string |
| var | var | data | Zlib-compressed data (if compressedLen > 0) |

### Subsequent Raw File Headers (16 bytes)

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0x00 | 4 | marker | `0xFFFFFFFF` |
| 0x04 | 4 | compressedLen | Compressed data size (big-endian) |
| 0x08 | 4 | uncompressedLen | Original data size (big-endian) |
| 0x0C | 4 | pointer | `0xFFFFFFFF` |
| 0x10 | var | filename | Null-terminated ASCII string |
| var | var | data | Zlib-compressed data (if compressedLen > 0) |

### Raw File Packing

**Critical**: Raw files are packed tightly with **NO separators** between them. Each file's `FFFFFFFF` header starts immediately after the previous file's data ends.

```
[File 0 header (20 bytes)][filename\0][compressed data]
[File 1 header (16 bytes)][filename\0][compressed data]  <- No gap!
[File 2 header (16 bytes)][filename\0][compressed data]
...
```

**Do NOT add null terminators between raw files.** This was a key bug that caused game crashes.

### Compression Detection

A raw file is compressed if:
1. `compressedLen > 0`
2. `compressedLen != uncompressedLen`
3. Data starts with zlib header (`0x78` followed by `0x01`, `0x5E`, `0x9C`, or `0xDA`)

---

## Footer Format

The footer is the final "raw file" entry in the zone, containing the zone name.

### MW2 PS3 Footer (16 bytes + name)

| Offset | Size | Field | Value |
|--------|------|-------|-------|
| 0x00 | 4 | marker | `0xFFFFFFFF` |
| 0x04 | 4 | compressedLen | `0x00000000` |
| 0x08 | 4 | uncompressedLen | `0x00000000` |
| 0x0C | 4 | pointer | `0xFFFFFFFF` |
| 0x10 | var | zoneName | Null-terminated string + extra null |

```
FF FF FF FF 00 00 00 00 00 00 00 00 FF FF FF FF [zonename] 00 00
```

The `totalSize1` field in the zone header points to offset `0x00` of this footer.

---

## Key Differences from CoD4/WaW

| Feature | CoD4/WaW | MW2 PS3 |
|---------|----------|---------|
| Zone header size | 52 bytes | 52 bytes (Xbox 360: 48, PC: 56) |
| Asset table entry order | `[ptr][type]` | `[ptr][type]` |
| Raw file asset type ID | 0x21 (CoD4), 0x22 (WaW) | 0x23 |
| Localize asset type ID | 0x18 (CoD4), 0x19 (WaW) | 0x1A |
| First raw file header | 12 bytes | 20 bytes |
| Other raw file headers | 12 bytes | 16 bytes |
| Footer size | 12 bytes | 16 bytes |
| Raw file compression | No | Yes (zlib) |
| MemAlloc1 value | 0x0F70 (CoD4), 0x10B0 (WaW) | 0x03B4 |
| MemAlloc2 value | 0x00 (CoD4), 0x05F8F0 (WaW) | 0x1000 |

---

## Common Pitfalls

These are bugs that were discovered during development that caused game crashes:

### 1. Adding Null Separators Between Raw Files
**Wrong**: Adding `0x00` after each raw file's data
**Correct**: Raw files are packed tightly with no separators

### 2. Using Wrong Zone Header Size
**Wrong**: Treating MW2 PS3 as a 48-byte header (pool starting at `0x30`)
**Correct**: 52-byte header — `assetsPtr` placeholder at `0x30`, asset pool at `0x34` (48 bytes is the MW2 Xbox 360 variant)

### 3. Including Zlib Header in FF Blocks
**Wrong**: Keeping `0x78 0x9C` in FF-level compressed blocks
**Correct**: Strip the 2-byte zlib header for raw deflate

### 4. Wrong fileSizes Field
**Wrong**: Setting to zone file size or leaving as original value
**Correct**: Must be the actual FF file size after compression completes

### 5. Wrong First Raw File Header Size
**Wrong**: Using 16-byte header for first file
**Correct**: First file needs 20-byte header (extra FFFFFFFF marker)

### 6. Wrong Asset Table Entry Order
**Wrong**: Using `[type][ptr]` format (`00 00 00 23 FF FF FF FF`)
**Correct**: Use `[ptr][type]` format (`FF FF FF FF 00 00 00 23`)

This was verified by comparing a working mod zone against a broken one. The working mod had `FF FF FF FF 00 00 00 23` at the start of the asset pool.

---

## References

- CoD Research Wiki: https://codresearch.dev/
- Original MW2 PS3 mod files analyzed for format verification
- RPCS3 emulator used for testing

---

*Document created during CoD-FF-Tools development for GitHub Issue #54*
*Last updated: January 2025*
