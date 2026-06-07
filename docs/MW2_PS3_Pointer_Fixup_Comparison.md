# MW2 PS3 Zone Pointer Fixup — Model + Implementation

How the **IW4 zone-stream pointer-fixup model** works, how it compares to what this codebase
used to assume, and how it's now implemented.

> **Superseded in part — June 2026.** Jacob's PS3 `EBOOT.ELF` trace
> (`EBOOT_ZONE_LOAD_MODEL.md`) is now the authority. Two things below were guessed wrong and have
> been corrected here: (1) there **is** a `+1` encode bias, and (2) `-2` is a distinct **insert**
> pointer, not just another inline marker. It also adds the **direct-vs-alias** offset distinction.
> The authoritative, current model lives in **`docs/MW2_PS3_EBOOT_Zone_Load_Model.md`**; this file is
> kept for the history of how we got there.

**Reference implementation:** Jacob Schroeder's FastFile —
<https://github.com/jacob-schroeder/FastFile>. The model below is ported from it
(`FastFile.Models/Data/Pointer.cs`, `FastFile.Logic/Zone/ZonePointerRebaser.cs`).

**Verified** against his source **and** a real MW2 PS3 zone
(`patch_mp - Elite Mossy v1 1.14`): `XFile.Size = 1,439,165`, `blockSize[LARGE] = 1,367,070`,
`blockSize[TEMP] = 948`, `blockSize[VERTEX] = 4,096`, others 0.

**Implemented (this repo):** `FastFileLib/ZonePointer.cs` + `FastFileLib/ZoneBlockLayout.cs`,
with tests in `FastFileCLI.Tests/ZonePointerTests.cs` (the real numbers above are the oracle).

---

## The model (as implemented)

```
decode a stored 32-bit pointer field:
  stored == 0          -> Null
  stored == -1         -> Inline   (referenced data is written inline after the parent struct)
  stored == -2         -> Insert   (inline data + a reserved 4-byte alias cell in block 4)
  else                 -> Offset:  raw        = stored - 1          (strip the +1 bias)
                                   blockIndex = (raw >> 28) & 0xF
                                   offset     =  raw & 0x0FFFFFFF

resolve an Offset pointer to a physical zone position:
  physical = offset + blockBase[blockIndex]

block bases (GetBlockBases): lay every block out sequentially starting at
  (XFile.Size - sum(blockSizes)), skipping LARGE, then place LARGE LAST. Therefore:
  blockBase[LARGE] = XFile.Size - blockSize[LARGE]

encode (rebaser, inverse): stored = ((blockIndex << 28) | offset) + 1
```

Worked example from the real zone:
`blockBase[LARGE] = 1,439,165 − 1,367,070 = 72,095 (0x119DF)`; a LARGE pointer with
`offset = 0x100` resolves to `72,095 + 0x100`.

**The `+1` bias is real** (EBOOT trace, June 2026): the stored value is the encoded `(block|offset)`
**plus one**, and the loader strips it back off before splitting block/offset. The earlier
"no `+1` bias" claim below (table row #3) was disproven by the EBOOT and is corrected.

---

## Summary table — was-assumed vs verified

Status legend: ✅ confirmed · ❌ our old assumption was wrong · 🔧 now implemented

| # | Topic | What we assumed earlier | Verified (code + real zone) | Status |
|---|-------|-------------------------|-----------------------------|--------|
| 1 | LARGE = block index 4 | index 4 (implied by header order) | `XFILE_BLOCK.LARGE = 4` | ✅ 🔧 |
| 2 | LARGE holds the bulk inline data | `BlockSizeLarge ≈ whole payload` | true, but it's **smaller** than the zone (see #8) | ✅ |
| 3 | `+1` null-avoidance bias | "we're missing the `−1` decode" | **the `+1` bias is real** (EBOOT): `stored = ((block<<28)\|offset)+1`; loader does `raw = stored−1` before decoding. The June-2026 EBOOT trace overturned the earlier "no `+1`" reading | ✅ 🔧 *(corrected)* |
| 4 | offset resolution | "we treat masked value as a direct base-0 zone offset" | `physical = offset + blockBase[block]`, `blockBase[LARGE] = Size − blockSize[LARGE]` | ✅ 🔧 |
| 5 | `-1` / `-2` markers | partial (only `-1` reserved in pointer test) | `-1` = Inline, **`-2` = Insert** (inline data + reserved block-4 alias cell) — distinct kinds | ✅ 🔧 *(corrected)* |
| 6 | block index encoding | "maybe high bits; unconfirmed" | **block index = top nibble (`>>28`)**, offset = low 28 bits (`& 0x0FFFFFFF`), after stripping the `+1` | ✅ 🔧 |
| 7 | WaW `& 0x7FFFFFFF` mask | "our resolved-pointer test" | **wrong encoding for IW4** — folds the block nibble into the offset. Kept only as a loose WaW *validation* heuristic | ❌ |
| 8 | size-sign | our doc: `BlockSizeLarge = ZoneSize + 36` (LARGE bigger) | real zone: LARGE (1.37 MB) **<** Size (1.44 MB); base `= +72,095` | ❌ our doc |

---

## Two structural facts worth keeping in mind

- **Even the reference reader does not "chase" Offset pointers to read data.** In his
  `ZoneReadContext`, `PointerKind.Offset → SetResult(default)`. All real data is read by
  following **Inline** (`-1`/`-2`) pointers in stream order via per-asset struct walkers. Offset
  pointers are cross-references into already-read inline data; their block+offset is only decoded
  for the **rebaser** (rewriting them when block sizes change on save).
- **It's a full IW4 reader/writer/rebaser** with real per-asset schemas (Weapon, Material, XModel,
  Menu, StringTable, …) — categorically beyond our `FF FF FF FF` pattern-scan. Our approach finds
  rawfile/localize by scanning markers; his decodes the actual struct graph.

---

## Status of the codebase

**Done — pointer model**
- ✅ `FastFileLib.ZonePointer` — decode stored → `Null`/`Inline`/`Insert`/`Offset` (+ block/offset),
  encode, **with the EBOOT `+1` bias** and the distinct `-2` insert kind.
- ✅ `FastFileLib.ZoneBlockLayout` — `GetBlockBases` port + `TryResolve(offset pointer) → physical`,
  plus `FromZoneHeader(...)` to build it straight from zone bytes.
- ✅ `FastFileLib.ZonePointerResolution` — the EBOOT-proofed **Direct/Alias** per-field-path rule
  table (proof-complete for `patch_mp_case_1`/`patch_mp_case_2`; everything else stays `Unknown`).
- ✅ Tests pin the model to the real MW2 PS3 numbers + the resolution table (`ZonePointerTests`,
  `ZonePointerResolutionTests`).
- ✅ `Cod5MenuDeserializer.IsValidZonePointer` comment now flags it as a WaW validation heuristic,
  NOT the IW4 fixup, and points at the new model.

**Done — real read path (ported from his `ZoneReader`/`ZoneReadContext`/readers)**
- ✅ Full port of his pipeline under `FastFileLib/Iw4/`: `Pointer`/`ZonePointer<T>`/`PointerKind`,
  `BinarySpanExtensions` (BE), `Memory`, the **`ZoneReadContext` deferred-resolution engine**
  (`ResolveQueued` — the breadth-first inline-pointer queue, so nested types read in the engine's
  order), the models (`XAssetType`/`XFile`/`XAssetList`/`XAsset`/`BaseAsset`/per-asset), the
  body readers + registry, and the top-level `Iw4ZoneReader`.
- ✅ Body readers ported: `rawfile`, `localize`, `techset`, `stringtable`, **`menufile`** (full
  `MenuReader`), **`material`+`image`**, **`structureddatadef`**, **`weapon`** (→ `WeaponDef` →
  inline **`xmodel`** + **`fx`** + **`tracer`** + material). Add to `XAssetReaderRegistry` to walk
  further.
- ✅ Adaptation: the walk stops cleanly at the first asset type without a ported reader
  (`Iw4UnsupportedTypeException`) and returns partial results, instead of desyncing.
- ✅ CLI: `ffcli assets <file.ff|.zone>`. Verified on a real `patch_mp.ff` — **the entire zone now
  parses: 431 / 431 bodies**, every type with correct names:
  Localize (`PATCH_CRASH`), RawFile (`maps/mp/mp_afghan.gsc`), Techset, MenuFile (`ui_mp/main.menu`),
  Weapon (`model1887_mp`, `model1887_akimbo_mp`), StringTable (`mp/unlocktable.csv`),
  StructuredDataDef (`mp/playerconstantdata.def`). The patch weapons bundle xmodel/fx/tracer/material
  **inline**, all of which read through the deferred engine; the per-type `EnsureFixedSize` checks
  (WeaponDef 0x684, FxElemDef 0xFC, XModelLodInfo 40, …) all pass.
- ✅ Synthetic-zone tests (`Iw4ZoneReaderTests`) cover header/script-string/pool/flat-body walk and
  the clean stop at an un-ported type.

**Remaining**
- [ ] Other asset types not present in `patch_mp.ff` (Sound, XAnim, GfxMap, ColMap, PhysPreset/
  PhysCollmap bodies, …) for other zones — same pattern: add a reader + model, register it. The
  engine + the common sub-readers (material/image/xmodel/fx/tracer) are all in place.
- [x] **Wired `Iw4ZoneReader` into the editor for MW2 PS3** (rawfile + localize) via
  `Services/Iw4AssetBridge` — see `docs/IW4_Zone_Read_Path.md`.

**Offset-pointer reading — the confirmed model (from Jacob Schroeder's EBOOT.ELF trace, June 2026)**

This is the resolved answer to the old open item #8. **Authoritative copy:**
`docs/MW2_PS3_EBOOT_Zone_Load_Model.md`.

- **Encoding:** `stored = ((block << 28) | offset) + 1` — block in the top nibble (`>>28 & 0xF`),
  offset in the low 28 bits (`& 0x0FFFFFFF`), with a `+1` null-avoidance bias the loader strips
  first. (Both EBOOT helpers `OffsetDirect` @`0x0011DC00` and `OffsetAlias` @`0x0011DBD0` decode this
  way. The earlier `<< 29` / 29-bit-offset guess was wrong.)
- **Pointer kinds:** `0` = null (no data) · `-1` = inline (data follows now in the stream) ·
  `-2` = **insert** (inline data **plus** a reserved 4-byte alias cell in block 4) · anything else
  = encoded `(block, offset)` into a block stream.
- **Direct vs alias offsets:** an offset pointer resolves via `OffsetDirect`
  (`target = block_base + offset`) **or** `OffsetAlias` (`target = *(block_base + offset)`, one extra
  indirection) depending on the field's loader path — **not** its value type. Root asset header
  pointers are alias; XStrings/rawfile buffers are direct. The proofed per-field map is
  `FastFileLib/ZonePointerResolution.cs`; a writer that encodes an alias field as direct (or moves an
  Unknown field) is the suspected PS3 black-screen cause.
- **An asset is split across blocks.** For a rawfile: the struct → VIRTUAL, the name → another spot
  in VIRTUAL, the compressed payload → LARGE. The struct's pointers `(block, offset)` are how the
  engine stitches the parts back together at load. **Shared data is the normal case** — one buffer
  written once to LARGE, referenced by `(LARGE, offset)` from every asset that uses it (the
  "back-reference" pointers).
- **You cannot map a pointer to a file position with a formula.** A pointer is a *runtime
  block-memory* address. To resolve it you must **demultiplex the zone into the 7 block streams**
  (replay the load, routing each write to its block) and then index `block[offset]`. Neither the
  reference reader nor ours does this demux — both read the zone as one sequential stream (which is
  why following inline `-1` works, but Offset pointers don't resolve).

**Empirical confirmation (retail patch_mp.ff):** the deduplicated localize keys ARE in the zone and
ARE reachable — e.g. "Storm"'s key pointer's offset `0x9025` + base `0x355` lands exactly on
`PATCH_STORM` (and `0x9031`→`PATCH_DESC_MAP_STORM`, `0x8FED`→`PATCH_COMPACT`, …). But `0x355` is
**mid-pointer inside the asset pool** — not a structural boundary — confirming it's a runtime
mapping, not `base+offset` over the file. A trial resolve using a computed base (`ZoneBlockLayout`
`Size − blockSize[LARGE]` = 72,107, or the asset-pool start = `0x128`) produced wrong strings
(`"K"`, `mp_boneyard`). So **resolving Offset (shared) strings requires implementing the block-stream
demux** — that's the remaining work; until then those entries' keys are left blank (matching the
reference reader) rather than shipping wrong values.

**Mapped zone layout (patch_mp.ff, 1.44 MB), for the demux work:**
```
0x00          XFile header (52 bytes): ZoneSize, ExternalSize, blockSize[7], XAssetList
0x34 – 0x128  script strings (10): pointer array + inline C strings
0x128 – 0xEA0 asset pool: 431 × 8-byte [type][ptr=FFFFFFFF] entries
0xEA0 – ~0x12000  text region: inline struct/name data + DEDUP KEY CLUSTER (@MPUI_*, PATCH_*) ~0x8EB0+
~0x6C41B+     binary rawfile compressed payloads
```
block sizes: TEMP `0x3B4`(948), LARGE `0x153AB2`(1,391,282), VERTEX `0x1000`(4096); PHYSICAL/RUNTIME/
VIRTUAL/CALLBACK = 0. The dedup-key base `0x355` sits ~69 entries into the asset pool — so the blocks
are **interleaved in serialization order**, not concatenated; a `(block,offset)` pointer can only be
resolved by replaying the load and demultiplexing into the 7 in-memory streams (need the per-field
block-routing: which `Load_*` call pushes which stream). codresearch.dev/Localize_Asset is 403 to
automated fetch — paste its serialization details to unblock this.
- [ ] Swap the editor's pattern-scan rawfile/localize path over to `Iw4ZoneReader` once body
  coverage is broad enough to be a net win (today it stops early, so keep the scanner).
- [ ] Confirm whether WaW (T5) genuinely uses a different pointer encoding than IW4 against a WaW
  sample before unifying the two. (Our WaW `0x8094D084` has the top *bit* set, which doesn't fit a
  top-*nibble* block index of 0..7 cleanly — so treat T5 as possibly-different until checked.)
- [ ] Re-derive the `docs/ZoneFileFormat.md` `+16` / `+36` size constants — they were fit from
  trivial rebuilt patch zones and don't hold for real multi-asset zones (item #8).

---

## References

**Reference implementation (Jacob Schroeder, credited in our ported files):**
- `EBOOT_ZONE_LOAD_MODEL.md` + "EBOOT.ELF Fastfile / XFile Loader Summary" — **the PS3 EBOOT authority**
- `FastFile.Models/Data/Pointer.cs`, `PointerKind.cs` — pointer decode
- `FastFile.Models/Zone/XFILE_BLOCK.cs` — block enum
- `FastFile.Logic/Zone/ZonePointerRebaser.cs` — `GetBlockBases`, rebase math
- `FastFile.Logic/Zone/ZoneReadContext.cs` — inline-vs-offset read behavior

**Ours:**
- `docs/MW2_PS3_EBOOT_Zone_Load_Model.md` — the authoritative current model (supersedes parts of this file)
- `FastFileLib/ZonePointer.cs`, `FastFileLib/ZoneBlockLayout.cs`, `FastFileLib/ZonePointerResolution.cs` — the pointer model port
- `FastFileLib/Iw4/Iw4ZoneReader.cs`, `FastFileLib/Iw4/Iw4AssetType.cs` — the read-path port (`ffcli assets`)
- `FastFileCLI.Tests/ZonePointerTests.cs`, `FastFileCLI.Tests/Iw4ZoneReaderTests.cs` — tests
- `Call of Duty FastFile Editor/ZoneParsers/Cod5MenuDeserializer.cs` — WaW validation heuristic (not the IW4 fixup)
- `FastFileLib/RawFileScanner.cs` — the current MW2 PS3 path (pattern-scan, follows no pointers)
- `docs/ZoneFileFormat.md` — block order + (now-suspect) size formulas
