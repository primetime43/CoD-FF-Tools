# Ghosts FastFile Extraction — Algorithm

Format reference: [`Ghosts_FastFile_Format.md`](Ghosts_FastFile_Format.md).

Extracts a Call of Duty: Ghosts (IW6) PS3 retail `.ff` to a single
fully-inflated `.zone` file in two passes. Verified against 4 retail PS3
samples (1 base zone + 3 patches). Read-only: the pipeline does not
rebuild or re-sign.

## Prerequisites

- A standard zlib / deflate library (every mainstream language's standard
  library has one).
- A hex editor (for spot-checking).
- A Ghosts PS3 retail `.ff` file. Format check:
  - bytes `0x00..0x07` = `IWff0100`
  - bytes `0x08..0x0B` = `00 00 02 2E` (big-endian; version 558)
  - the ASCII string `IWffS100` appears somewhere in the first 64 KB

## The anchor

Two FF variants exist in the Ghosts family. Both share the same
downstream layout from the `IWffS100` inner magic onwards.

| Variant | `IWffS100` location | Example |
|---|---|---|
| Patch FF | file offset `0x24` (immediately after the 36-byte outer header) | `patch_common_mp.ff`, `patch_homecoming.ff` |
| Base FF | further in, after a 12 KB index table between the outer header and `IWffS100` | `common.ff` (location: `0x3294`) |

Let **A** = the file offset of `IWffS100`. The compressed deflate-block
stream always starts at **A + 0x20000**. The intervening 128 KB is the
`DB_AuthHeader` (8 KB) + 48 bytes padding + a 112 KB metadata region; none
of it is needed for decompression.

## Algorithm

Two passes. Total ~30 lines of code in any language with a deflate library.

### Pass 1 — outer raw deflate

```
search file for "IWffS100"           → call its offset A
seek to file offset A + 0x20000
while not EOF:
    src_size = read 2 bytes, big-endian
    block    = read src_size bytes
    chunk    = raw-deflate-decompress(block)        # produces exactly 65 536 bytes
    append chunk to raw_zone buffer
```

"Raw deflate" means deflate without the 2-byte zlib header — in .NET
`DeflateStream`, in Go `compress/flate`, in browser JS
`DecompressionStream("deflate-raw")`. The block stream has no end marker;
read until EOF.

### Pass 2 — inner per-asset zlib

Each asset's content sits behind a small header in the raw zone, and some
asset types wrap their content in a standard zlib stream (`0x78 DA` etc.).
Expand every such stream inline.

```
i ← 0x40    # skip XFile header territory
while i < len(raw_zone) - 2:
    if raw_zone[i] is zlib magic (78 01 | 78 5E | 78 9C | 78 DA):
        comp_len ← parse_asset_header(raw_zone, i)  # see below
        if comp_len is valid:
            decompressed ← zlib_decompress(raw_zone[i : i + comp_len])
            record (i, comp_len, decompressed)
            i ← i + comp_len
            continue
    i ← i + 1

output ← copy of raw_zone, with each recorded (i, comp_len) range
         replaced by its decompressed bytes
write output to .zone file
```

`parse_asset_header(raw_zone, i)` walks back from the zlib-magic offset to
recognise one of two header shapes and returns the declared
`compressedLen`:

```
p ← i - 1
require raw_zone[p] == 0x00      # name terminator
walk back over printable-ASCII name (1..127 chars)
name_start ← p + 1

# Try "long" shape first (verified for scriptfile, type 0x29):
if raw_zone[name_start - 8 .. name_start - 1] are all 0xFF:
    size_fields_start ← name_start - 8 - 12      # 8 trailing FFs + 3 u32 fields
    if raw_zone[size_fields_start - 4 .. size_fields_start - 1] are all 0xFF:
        comp_len ← BE_u32(raw_zone[size_fields_start..])
        return comp_len if 0 < comp_len < 0x4000000 else INVALID

# Else "short" shape (verified for rawfile, type 0x28):
if raw_zone[name_start - 4 .. name_start - 1] are all 0xFF:
    size_fields_start ← name_start - 4 - 8       # 4 trailing FFs + 2 u32 fields
    if raw_zone[size_fields_start - 4 .. size_fields_start - 1] are all 0xFF:
        comp_len ← BE_u32(raw_zone[size_fields_start..])
        return comp_len if 0 < comp_len < 0x4000000 else INVALID

return INVALID
```

Try long before short — long is more specific (it requires 8 trailing FFs,
which short-shaped headers won't satisfy when bit-7-clear bytes happen to
sit in the middle of the size fields).

The first u32 after the leading-FF block is **always** the byte length of
the zlib stream that follows; the second u32 is the decompressed length.
The long shape has a third u32 whose meaning isn't reverse-engineered (it
isn't a size).

## Verification

After running both passes, open the `.zone` in a hex editor:

- `0x00..0x03` should be the zone size as a big-endian uint32, slightly
  smaller than the `.zone` file size (the difference is padding).
- The asset pool (8-byte `FF FF FF FF 00 00 00 XX` entries) starts at
  `0x40` for patch zones; for base zones the XFile header runs longer and
  the pool starts at a higher offset.
- A text search should find recognisable strings depending on the FF:

| Zone | Sample strings to expect |
|---|---|
| `patch_common_mp.zone` | `allies`, `axis`, `death`, `returned`, `timeout` |
| `patch_mp_dome_ns.zone` | `maps/mp/mp_dome_ns`, `compass_map_mp_dome_ns`, `give_alien_weapon` |
| `common.zone` (base) | `j_mainroot`, `tag_sync`, `j_hip_le`, `animscripts/animset`, `maps/mp/mp_*` |

## Reference output sizes

| Input FF | Variant | Inflated .zone |
|---|---|---|
| `patch_mp_dome_ns.ff` (145 KB) | patch | 153,118 bytes |
| `patch_homecoming.ff` (195 KB) | patch | 179,579 bytes |
| `ghosts_patch_common_mp.ff` (525 KB) | patch | 799,277 bytes |
| `ghosts ps3 common.ff` (29.8 MB) | base | 47,689,203 bytes |

All four produce zero residual `78 DA`-style streams after both passes —
every inner zlib stream is fully expanded.

## Caveats

- Verified against 4 PS3 retail samples (3 patch + 1 base). Xbox 360,
  Wii-U, and PC variants of Ghosts haven't been tested; they likely use
  shifted asset type IDs and may use different inner-magic casing.
- Read-only. Saving a modified FF would require IW's RSA-2048 private key
  to re-sign the `DB_AuthHeader`. Not viable.
- Asset content comes out as raw bytes. Per-asset-type internal struct
  parsing (weapon definitions, GSC scriptfile decoding, image headers,
  etc.) is a separate problem and isn't covered by this pipeline.
- Same header-shape logic might apply to IW7 (Infinite Warfare) and H1
  (MWR) — both are IW-family successors to IW6. Worth confirming the
  inner magic is also `IWffS100` (capital S) before reusing this.
