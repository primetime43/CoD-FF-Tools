# MW2 PS3 (IW4) EBOOT Zone Load Model

Authoritative model of how the **PS3 IW4 EBOOT.ELF** loads a decompressed fastfile zone:
pointer encoding, the direct-vs-alias offset distinction, insert pointers, stream blocks, and
the per-field resolution rules.

**Source of truth:** Jacob Schroeder's PS3 `EBOOT.ELF` trace —
<https://github.com/jacob-schroeder/FastFile> (`EBOOT_ZONE_LOAD_MODEL.md` + the
"EBOOT.ELF Fastfile / XFile Loader Summary"). Verified against the official
`patch_mp_case_1` / `patch_mp_case_2` zones. This **supersedes** the earlier guesses recorded in
`docs/MW2_PS3_Pointer_Fixup_Comparison.md` (the "no +1 bias" and "`<< 29` / 29-bit offset" notes).

**Implemented (this repo):** `FastFileLib/ZonePointer.cs`, `FastFileLib/ZoneBlockLayout.cs`,
`FastFileLib/ZonePointerResolution.cs`; reader path in `FastFileLib/Iw4/Pointer.cs`.

---

## 1. Pointer encoding — there IS a `+1` bias

A 32-bit pointer field stores its encoded value **plus one**:

```
encoded = ((blockIndex << 28) | offset) + 1     // writer
raw     = encoded - 1                            // loader strips the bias first
blockIndex = raw >> 28          (top nibble, 0..15)
offset     = raw & 0x0FFFFFFF   (low 28 bits)
```

Worked example: stored `0x40000101` → `- 1` → `0x40000100` → block 4 (LARGE), offset `0x100`
→ runtime target `g_streamBlocks[LARGE] + 0x100` (a block-memory address, **not** a file offset).

### Marker values

| Stored value | Meaning |
|---|---|
| `0x00000000` | **Null** — no data, no resolution. |
| `0xFFFFFFFF` (`-1`) | **Inline** — the pointed-to data is materialized immediately from the current stream position. |
| `0xFFFFFFFE` (`-2`) | **Insert** — inline data **plus** a reserved 4-byte alias cell in block 4. The loader reserves the cell, reads the inline data, then writes the loaded pointer into the cell so alias pointers can target it. |
| anything else | **Offset** — encoded `(block, offset)` per the formula above (strip the `+1` first). |

---

## 2. Direct vs Alias offset pointers

An Offset pointer is **not** resolved one way. The EBOOT has two fixup helpers, and which one a
field uses is a property of that field's loader path — **not** of its C# value type:

- **OffsetDirect** (`0x0011DC00`): `target = g_streamBlocks[block] + offset`.
  The decoded offset points straight at the data.
- **OffsetAlias** (`0x0011DBD0`): `cell = g_streamBlocks[block] + offset; target = *(uint32*)cell`.
  The decoded offset points at a **4-byte pointer cell** that holds the data pointer — one level of
  indirection.

**Root asset header pointers are alias.** Every root XAsset wrapper (RawFile, StringTable,
LocalizeEntry, Material, Techset, MenuFile/Menu, Weapon, StructuredDataDef, Tracer, Fx, XModel)
reads its 4-byte header pointer and, for the offset case, calls **OffsetAlias** — it must point at a
4-byte cell, not the asset body bytes. **XStrings are direct** (OffsetDirect at `0x00102924`).

A writer must encode Direct pointers to the data span and Alias pointers to the 4-byte alias cell.
Getting this wrong is the suspected **PS3 black-screen** cause for edited fastfiles that move data:
the decoded offset still lands in a valid block range (so range-only audits pass), but the EBOOT
dereferences the wrong thing during asset fixup.

### Proof gate

A field path is classified Direct/Alias **only** when its exact EBOOT loader call was traced.
Everything else stays **Unknown** and must not be relocated by a writer. Value type, decoded block,
and "reasonable-looking offset" can help find the next branch to trace, but are not authority.
`FastFileLib.ZonePointerResolution` is the proofed table; it is complete (zero Unknown) for
`patch_mp_case_1.zone` and `patch_mp_case_2.zone`.

---

## 3. Insert pointers (`-2`) exactly

EBOOT `InsertPointer` helper (`0x0011DB88`):

```
push stream block 4 (LARGE)
align to mask 3 (4-byte)
record current position           -> the reserved cell offset
advance 4 bytes                    -> reserves the cell
pop stream block
```

For a `-2` field the loader reserves this cell, reads/materializes the inline data, registers the
asset, then patches the cell with the loaded pointer. A writer reproducing `-2` must create and patch
the same block-4 alias cell.

---

## 4. Stream blocks

The loader tracks `serialized file position` and the `active stream block offset` separately —
they advance together but are not the same cursor. PS3 patch zones use:

| Block | Index | Role (patch zones) |
|---|---|---|
| TEMP | 0 | bootstrap / temp (`0x3B4` in both patch cases) |
| PHYSICAL | 1 | `0` |
| RUNTIME | 2 | `0` |
| VIRTUAL | 3 | `0` |
| LARGE | 4 | **primary serialized asset-data block** (`0x14DC1E` / `0x153AB2`) |
| CALLBACK | 5 | `0` |
| VERTEX | 6 | constant `0x1000` reserved |

`ZoneBlockLayout` lays the blocks out from `XFile.Size - sum(blockSizes)`, places LARGE **last**, so
`blockBase[LARGE] = XFile.Size - blockSize[LARGE]`. (This base math is unaffected by the `+1` pointer
bias, which only touches encode/decode.)

### Key EBOOT helper functions

| Address | Helper |
|---|---|
| `0x0011DA58` | Set active stream block |
| `0x0011DA90` / `0x0011DB00` | Push / Pop stream block |
| `0x0011DB40` | Get current stream position |
| `0x0011DB50` | Align position: `aligned = (pos + mask) & ~mask` |
| `0x0011DB70` | Advance position |
| `0x0011DB88` | **InsertPointer** (reserve 4-byte cell in block 4) |
| `0x0011DBD0` | **OffsetAlias** |
| `0x0011DC00` | **OffsetDirect** |
| `0x0011DD00` | ReadStream / advance |
| `0x001167C0` | Root XAssetList loader |
| `0x00116738` | XAsset array loader (8-byte entries: `[type][header ptr]`) |
| `0x001028E0` | XString loader (offset path → OffsetDirect @ `0x00102924`) |

---

## 5. Per-asset notes (writer-relevant)

- **RawFile** (`0x00103F70` / body `0x00103EC0`): `buffer` at `+0xC` is written **inline in block 4**,
  not as an offset pointer. Size read = `CompressedLen` when nonzero, else `Len + 1`. `name` is XString.
- **Material** (`0x0010D980`): root ref is alias; `TechniqueSet` alias; texture/constant/state tables
  and `Info.Name` direct; `MaterialTextureDef.Image` alias.
- **Weapon** (`0x00115560` → WeaponDef `0x00114678`): root ref alias; body mixes Direct array spines
  (`HideTags`, `XAnims`, `szXAnimsR/L`, accuracy-graph knots, `BounceSound`, `ParallelBounce`,
  `LocationDamageMultipliers`, …) with Alias element wrappers (`GunXModel.Element`,
  `WorldGunXModel.Element` go through the XModel wrapper). String/model array **elements** follow the
  element's own rule.
- **StringList** (`0x00103A90` → `0x001039D8` → XString): `Strings` and `Strings[n]` are Direct.
- **Menu**: root Menu/MenuList refs alias; `Window.Background` alias; statements, event handlers,
  expression data, items, names — Direct (the unconditional-script handler resolves through XString,
  traced via `0x0010C160`).

The full proofed map lives in `FastFileLib/ZonePointerResolution.cs`.

---

## 6. PS3-readiness rule

Do not consider a writer PS3-ready for arbitrary edits until both official patch zones report:

- zero **Unknown** offset pointer-resolution paths,
- **Direct** offsets landing on materialized data spans,
- **Alias** offsets landing on valid 4-byte alias cells,
- **Insert** pointers creating and patching their block-4 alias cells exactly as the EBOOT does.

This repo currently ships the **read** path (the proofed table + corrected decode); the
direct/alias-aware **writer** is the remaining work.

---

## References

- Jacob Schroeder, `EBOOT_ZONE_LOAD_MODEL.md` and "EBOOT.ELF Fastfile / XFile Loader Summary"
  — <https://github.com/jacob-schroeder/FastFile>
- `docs/MW2_PS3_Pointer_Fixup_Comparison.md` — history of the model and what changed
- `FastFileLib/ZonePointer.cs`, `ZoneBlockLayout.cs`, `ZonePointerResolution.cs`
