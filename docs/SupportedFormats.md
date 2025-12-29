# Supported Formats

This document lists what the FastFile Tools can currently parse, edit, and rebuild.

## Supported Games & Platforms

| Game | PS3 | Xbox 360 | PC | Wii |
|------|-----|----------|-----|-----|
| CoD4: Modern Warfare | ✅ Full | ✅ Full | ⚠️ Extract | ⚠️ Extract |
| WaW: World at War | ✅ Full | ✅ Full | ⚠️ Extract | ⚠️ Extract |
| MW2: Modern Warfare 2 | ✅ Full | 🔬 Untested | ⚠️ Extract | ➖ |

### Version IDs

| Game | Console | PC | Wii |
|------|---------|-----|-----|
| CoD4 | `0x00000001` | `0x00000005` | `0x000001A2` |
| WaW | `0x00000183` | `0x00000183` | `0x0000019B` |
| MW2 | `0x0000010D` | `0x00000114` | ➖ |

### Legend
- ✅ **Full** - Decompress, parse assets, edit, and recompress
- 🔬 **Untested** - Implementation complete but not verified on hardware
- ⚠️ **Extract** - Decompress to zone file only (no asset editing/recompress)
- ➖ **Not Available** - Game not released on this platform

### Xbox 360 Notes
- Xbox 360 requires a **patched XEX** to load modified FastFiles
- Original signed FastFiles are converted to unsigned format when saving
- The editor preserves hash tables from original files but cannot regenerate RSA signatures

---

## Platform Compression Formats

| Game | PS3 | Xbox 360 | PC |
|------|-----|----------|-----|
| CoD4 | Block (raw deflate) | Block (raw deflate) | Block (raw deflate) |
| WaW | Block (raw deflate) | Block (raw deflate) | Block (raw deflate) |
| MW2 | Block (zlib) | Single stream (zlib) | Single stream (zlib) |

### Block vs Single Stream
- **Block compression**: Data split into 64KB chunks, each compressed separately with 2-byte length prefix
- **Single stream**: Entire zone compressed as one continuous zlib stream

### Header Formats
| Format | Magic | Description |
|--------|-------|-------------|
| Unsigned | `IWffu100` | Standard format for PS3 and unsigned files |
| Signed | `IWff0100` | Xbox 360 signed format (RSA signature) |
| Streaming | `IWffs100` | Xbox 360 signed streaming format (CoD4/WaW) |

---

## Asset Support Summary

| Support Level | Asset Types | Capabilities |
|---------------|-------------|--------------|
| ✅ **Full** | `rawfile`, `localize`, `weapon`, `menufile` | Parse, view, edit, save |
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

### Call of Duty: Modern Warfare 2

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
- PC/Wii versions: extract only, no recompression support
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
