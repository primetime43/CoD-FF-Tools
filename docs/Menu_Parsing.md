# Menu Parsing & Menus Tab

Detailed notes on the editor's **Menus** tab, the WaW (CoD5) menu binary layout, Treyarch zone-serialized pointer conventions, and the WaW PS3 `0x0000`-prefixed raw block extension.

## Menus tab display rules

The editor's **Menus** tab is a **visual layout view**: a tree of menulists → menus on the left, and on the right a `MenuPreviewControl` (`UI/MenuPreviewControl.cs`) that draws the 640×480 virtual screen with each item as a positioned box (from its window rect, colored by `ItemType`), over a read-only properties grid below. Item boxes are positioned by **resolving the windowDef `horzAlign`/`vertAlign`** (4=fullscreen, 2=center-relative to 320/240, 3=right/bottom-relative to the far edge, 0/1=left/top-raw) — an approximation of CoD's menu coordinate system (the full system has widescreen scale modes like NOSCALE/TO640). The frame is the **16:9 game window** (≈853×480, how MW2 renders) with the **640×480 4:3 authoring/safe area** drawn as a dotted inner reference; the view **fits to the union of the window and the normal-sized items** (so nothing bleeds into the panel), and overscan/backdrop items (≈ full-window in a dimension or ≥½ its area) render as faint dotted outlines so the interactive buttons/text stand out. Labels use GDI+ `DrawString` (stays inside the box). Selecting a menu shows its window properties (name, rect, the 5 colors as swatches via `EditableValues`, itemCount); the **rect/color rows are editable** — committing a cell parses the value and writes it back to the zone at the value's offset via `MenuDecompiler.ApplyMenuValueChanges` (BE-aware), patching `zone.Data` directly (name/itemCount/offset and item rows stay read-only). Clicking an item box in the preview swaps the grid to that item's properties (type/name/text/dvar/rect, read-only — item fields have no write path yet). This **replaced** the old right-hand text-decompiler dump (the `MenuDecompiler` pseudo-code with offsets/empty event-handler stubs) — that was hard to read and fragile to edit. The visual view is read-only; per-item rects and the menu rect come from the IW4 walk (`Iw4AssetBridge` maps `MenuDef.Window.Rect` + each `ItemDef.Window.Rect`), so it's fully populated for MW2 PS3 and shows the menu bounds + properties (no item boxes) for games whose menus are parsed without item rects.

Tree rows:
- `name.menu` with 1 menu → flat row `name.menu [N items]`
- `name.menu` with 1 menu but parse failed → flat row `name.menu [1 menu (parse failed)]`
- `name.menu` / `name.txt` with N > 1 menus → tree `name (N menus)` with one child per parsed menu. Child label is the extracted window name (e.g. `mw2_main_background`) if available, else `menu #N`. Item count appended when known.
- If the asset pool declared more menus than the scanner located, a trailing `+N menu(s) not located` child appears.

`MenuListParser.FindMenuStartSignature` walks each menulist by scanning for the windowDef_t header pattern (FFFFFFFF + valid rect + valid rectClient) — that's how we find menu[1..N] without implementing the full menuDef_t deserializer.

The **Partial** menufile support uses two backends keyed by game version: `Iw4MenuDeserializer` for MW2 (IW4 layout, OAT-style walker that recurses into items[]) and `Cod5MenuDeserializer` for WaW (struct-fit byte scanner; works for both PS3/Xbox 360 console and PC). The CoD5 path doesn't walk inline data (no authoritative OAT spec for WaW) — instead it advances byte-by-byte and accepts every position whose 312-byte (console) or 288-byte (PC) window passes `FitsMenuDefStruct` (15 pointer fields must each be 0/FFFFFFFF/resolved-with-high-bit; rect floats, colors, counts must be in plausible ranges). This finds all `menuCount` declared inline menus on PS3 ui.ff (95/95) and most on PC (118/145 — the remainder have field values outside the strict ranges). Per-menu rect, 5 colors, itemCount, and inline window.name are surfaced as `EditableValues`. Full menu reconstruction (adding items, retargeting handlers, etc.) is not implemented.

## WaW menu binary layout (CoD5)

Authoritative source: `Menu Asset (WaW)` and `MenuFile Asset` pages on codresearch.dev. Key sizes (PS3/Xbox 360 console first, PC in parentheses when different):

| Struct | Console | PC | Diff |
|---|---|---|---|
| `windowDef_t` | 168 bytes (0xA8) | 156 bytes (0x9C) | `dynamicFlags[4]` vs `[1]` |
| `menuDef_t`   | 312 bytes (0x138) | 288 bytes (0x120) | windowDef diff + `cursorItem[4]` vs `[1]` |
| `itemDef_s`   | 472 bytes (0x1D8) | (unverified — IW4 PC has different `textRect`/`cursorPos` slot counts) | — |
| `rectDef_s`   | 24 bytes (4 floats + 2 ints) | same | identical |
| `statement_s` | 8 bytes (count + entries ptr) | same | identical |
| `ItemKeyHandler` | 12 bytes | same | identical |
| `listBoxDef_s` | 384 bytes | unverified | — |
| `multiDef_s`   | 392 bytes (32+32 string ptrs, 32 floats, count, strDef) | same | identical |
| `editFieldDef_s` | 32 bytes | same | identical |
| `expressionEntry` | 12 bytes (type + 8-byte union) | same | identical |

PC menuDef field offsets diverge from console after `staticFlags` (0x4C):
- Console nextTime at 0x60, PC at 0x54 (−12)
- Console fadeCycle at 0xC8, PC at 0xB0 (−24)
- Console items at 0x134, PC at 0x11C (−24)

`Cod5MenuDeserializer.Offsets` (nested struct) computes all field offsets from the `isPC` flag using two shift constants (−12 after windowDef, −24 after cursorItem).

## Treyarch zone-serialized pointer conventions

Pointer fields in a serialized zone can hold:
- `0` — null
- `0xFFFFFFFF` — "inline placeholder", real data follows immediately after the parent struct
- High-bit-set value (e.g. `0x82579FFE`) — **pre-link resolved pointer**: the zone compiler has already filled in the runtime address. Mask off the high bit (`v & 0x7FFFFFFF`) to get a zone-internal offset. Both PS3 and PC WaW zones use this convention; ignoring it loses ~60 real menus per UI zone (e.g. `main_text` has `soundName = 0x8094D084` because the linker pre-resolved the sound asset pointer).

`Cod5MenuDeserializer.IsValidZonePointer` enforces exactly these three cases. Any other value (small ints, float-shaped values, etc.) means "not a real pointer" → the candidate isn't a menuDef_t.

This is a loose WaW *validation* heuristic and is distinct from the IW4 (`ZonePointer`) Direct/Alias model — see `docs/IW4_Zone_Read_Path.md`.

## WaW PS3 ui.ff: `0x0000`-prefixed raw blocks

UI zones for WaW PS3 use an extension to the standard 64 KB block format: a length prefix of `0x0000` means "the next 64 KB are stored uncompressed; copy them verbatim and continue." Treyarch added this so already-compressed payloads (Bink video frames, packed audio, DDS textures with internal compression) don't waste CPU on near-zero-ratio deflate. The standard end marker is still `0x0001`. `FastFileProcessor.DecompressStandardBlocks` handles both:

```
length = 0x0001  → end of stream
length = 0x0000 + ≥64 KB left  → next 64 KB are stored raw, consume and continue
length = 0x0000 + <64 KB left  → tolerant EOF (some files end with 0x0000)
length = N       → next N bytes are raw deflate, decompress and continue
```

Without this, retail `ui.ff` decompresses to ~1.8 MB instead of its full ~50 MB and the Bink intro video is truncated.
