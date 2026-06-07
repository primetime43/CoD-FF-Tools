# IW4 (MW2) Zone Read Path & Pointer Model

Detailed notes on the IW4 pointer-following zone reader (`FastFileLib/Iw4/*`), the `ZonePointer`/`ZoneBlockLayout`/`ZonePointerResolution` model, and how they are wired into the editor for MW2 PS3. See also `docs/MW2_PS3_EBOOT_Zone_Load_Model.md` (authoritative EBOOT trace) and `docs/MW2_PS3_Pointer_Fixup_Comparison.md`.

## `ZonePointer` / `ZoneBlockLayout` / `ZonePointerResolution`

**IW4 (IW-lineage) zone-serialized pointer model**, ported from Jacob Schroeder's FastFile (https://github.com/jacob-schroeder/FastFile) and his PS3 `EBOOT.ELF` trace (`docs/MW2_PS3_EBOOT_Zone_Load_Model.md`), verified against the official `patch_mp_case_1`/`patch_mp_case_2` zones.

`ZonePointer` decodes a stored 32-bit field to `Null` (0) / `Inline` (`-1`, data follows in stream) / `Insert` (`-2`, inline data **plus** a reserved 4-byte alias cell in block 4) / `Offset`. **The encoding carries a `+1` null-avoidance bias** the loader strips first: `raw = stored − 1`, then block index = top nibble (`>>28`), byte offset = low 28 bits (`& 0x0FFFFFFF`); encode is `((block<<28)|offset)+1`. (This corrects the earlier "no `+1` bias" / "`<<29`" guesses.)

`ZoneBlockLayout` lays the XFILE stream blocks out (LARGE is index 4 and placed **last**, so `base[LARGE] = XFile.Size − blockSize[LARGE]`) and `TryResolve`s an Offset pointer to a physical zone position; `FromZoneHeader(...)` builds it from zone bytes.

`ZonePointerResolution` is the EBOOT-proofed **Direct vs Alias** per-field-path table: an Offset pointer resolves via `OffsetDirect` (offset → data) or `OffsetAlias` (offset → a 4-byte cell holding the data pointer) depending on the field's loader path, **not** its C# type — root asset header pointers are Alias, XStrings/rawfile buffers are Direct. It's **proof-gated**: any field path without traced EBOOT evidence stays `Unknown` and must not be relocated by a writer (encoding an Alias field as Direct, or moving an Unknown field, is the suspected PS3 black-screen cause). This is the **correct** IW4 fixup — distinct from `Cod5MenuDeserializer.IsValidZonePointer`'s loose `& 0x7FFFFFFF` WaW *validation* heuristic (which is not a real dereference and may use a different T5 encoding).

The IW4 **reader** (`Iw4/Pointer.cs`) never dereferences Offset pointers (real data is read by following inline `-1`/`-2` markers), so the `+1` correction doesn't change the 431/431 read; the Direct/Alias table is for the not-yet-built writer/validator.

## `Iw4/*` — Real IW4 (MW2 PS3) zone read path

Ported from Jacob Schroeder's FastFile (https://github.com/jacob-schroeder/FastFile): `Pointer`/`ZonePointer<T>`/`PointerKind` (`Pointer.cs`), big-endian span reads (`BinarySpanExtensions.cs`), `Memory`, the **`ZoneReadContext` deferred-resolution engine** (`ResolveQueued` — walks asset bodies in the engine's breadth-first inline-pointer order, so nested types read correctly), the asset/zone models (`Models.cs`: `XAssetType`/`XFile`/`XAssetList`/`XAsset`/`BaseAsset`/per-asset), the body readers + registry (`Readers.cs`), and the top-level `Iw4ZoneReader` (`ParseHeader`/`ParseXAssetList`/`ReadAsset`).

Walks an inflated zone the engine's way — following the IW pointer conventions, **not** pattern-scanning. The XFile header, script strings, and asset-pool type list are **always complete**; asset **body** reading is registry-dispatched (`XAssetReaderRegistry` in `Readers.cs`).

Ported body readers (per file under `Iw4/`): `rawfile`/`localize`/`techset`/`stringtable` (`Readers.cs`), `menufile` (full `MenuReader` — windows/items/statements/event-handlers, `MenuReader.cs`+`MenuModels.cs`), `material`+`image` (`MaterialReader.cs`+`MaterialModels.cs`), `structureddatadef` (`StructuredData.cs`), `weapon` (`Weapon.cs`), `xmodel` (`XModel.cs`), `fx` (`Fx.cs`), `tracer` (`Tracer.cs`).

The walk **stops cleanly at the first asset type without a ported reader** (an adaptation via `Iw4UnsupportedTypeException` — an unread body would otherwise leave the stream position wrong) and returns partial results. On a real `patch_mp.ff` (a fat patch zone that bundles weapon sub-assets — xmodel/fx/tracer/material — **inline**) this reads the **entire zone: 431/431 bodies** with correct names (weapons `model1887_mp`, menus `ui_mp/main.menu`, localize `PATCH_CRASH`, etc.); per-type `EnsureFixedSize` checks (WeaponDef 0x684, FxElemDef 0xFC, …) validate the layout. Remaining sub-asset types not present in this zone (sound/xanim/gfxmap/physpreset bodies/…) use stub `*Pointer` readers that read the pointer and stop cleanly only if unexpectedly inline. Add a reader to `XAssetReaderRegistry` to walk further. CLI: `ffcli assets <file>`.

## Editor wiring (MW2 PS3)

**Wired into the editor for MW2 PS3**: `MainWindowForm.LoadAssetRecordsData` calls `Services/Iw4AssetBridge.TryRead(zone)` (an editor-side bridge that runs `Iw4ZoneReader` and maps every top-level asset type the walk reads to its editor model:
- `RawFile`→`RawFileNode`
- `LocalizeEntry`→`LocalizedEntry`
- `StringTable`→`StringTable`
- `WeaponVariantDef`→`WeaponAsset` (name/display/clip + offset; deep WeaponDef enum fields left as `-1`/N/A)
- `MaterialTechniqueSet`→`TechSetAsset` (name/vert-format/active-count + offset)
- `MenuList`→`MenuList` (name/menuCount + per-menu window name/itemCount; each menuDef's `StartOffset` = its IW4 zone offset and `EndOffset` = the next serialized boundary (next menuDef or next asset body), which the editor's byte-level `MenuDecompiler` uses to extract editable strings)

So MW2 PS3 **rawfile, localize, stringtable, weapon, techset, AND menufile** all come from the pointer-following reader instead of pattern-scanning (i.e. **no pattern matching for a fully-walked MW2 PS3 zone** — the Asset Pool "Status" column reads "IW4 pointer-walk" for every parsed type), populating both their dedicated tabs and the Asset Pool list view (the walk returns assets in pool order, so each list lines up with the pool records of that type). The IW4 rawfile `Offset`/`DataOffset` match `RawFileScanner`'s header/data offsets, so the in-place save path is unchanged.

It's used **only when the full IW4 walk completes** (a partial walk would miss assets → falls back to the scanner), and only for MW2 PS3 (`IsMW2File && !IsPC && !IsXbox360`). The IW4 reader classifies `.csv` files as stringtables (not rawfiles), so scanner-only rawfiles (the `.csv` tables) are merged back in so they stay editable in the Raw Files tab. The IW4 top-level reader registry covers rawfile/localize/stringtable/menufile/structureddatadef/techset/weapon; **image/material/xmodel are sub-assets only** (the walk stops at a top-level image), so any zone whose full walk completes contains none of those as pool entries — images stay on the `AssetRecordProcessor` (pattern-scan) path, as do all other games and platforms. `MW2GameDefinition.IsTechSetType` is overridden (techset id PS3 `0x08`/Xbox360 `0x07`/PC `0x09`) so the pool view and per-type counts recognise techset records.

The **asset selector** (`AssetSelectionDialog`) exposes per-type load toggles — `rawfile`/`localize`/`menufile`/`stringtable`/`weapon`/`image`/`techset`/`tags` — each threaded through `LoadAssetRecordsData`'s `load*` params and honored when assigning the typed lists.

## Structure-based UI surfaces

**StringTables** open a rows×cols cell grid (`StringTableViewerForm`, from `RowCount`/`ColumnCount`/`Cells`).

**Weapons** are **editable** for MW2 PS3 via `Iw4WeaponEditorForm` (double-click) — NOT the WaW-tuned `WeaponEditorForm`/`AdvancedWeaponEditorForm` (those use the 0x9AC WaW layout and would corrupt the IW4 0x684 layout, so they're bypassed for `IsStructuredView` weapons). The IW4 editor reads/writes **only byte-offset-verified scalar fields**, all big-endian:
- In the variant (at `WeaponAsset.StartOffset`): adsZoomFov +0x14, clipSize +0x20, fireTime +0x28
- In the WeaponDef (at `WeaponAsset.WeaponDefOffset`): weapType +0x2C, weapClass +0x30, penetrateType +0x34, inventoryType +0x38, fireType +0x3C, iStartAmmo +0x208, iMaxAmmo +0x21C, damage +0x230, minDamage +0x598 (all verified against patch_mp.ff)

It patches the in-memory `zone.Data` directly. The bridge also fills `WeaponAsset.IsStructuredView` + `DetailFields` (the read-only `WeaponDetailForm` is still available for the fuller field dump).

The grid's **Type/Class/Fire Type/Penetrate/Impact/Inventory** columns are decoded from the WeaponDef enum cluster — the reader stores it as the 8-int `PlayerAnimTypeThroughStance` block whose OpenAssetTools IW4 order is `[0]`=playerAnimType, `[1]`=weapType, `[2]`=weapClass, `[3]`=penetrateType, `[4]`=inventoryType, `[5]`=fireType, `[6]`=offhandClass, `[7]`=stance (there is **no** impactType in the block — Impact uses the per-variant `impactType`), mapped through the **authoritative IW4 enum value lists** (per OAT — note IW4's values differ from WaW's, e.g. `WEAPCLASS_SPREAD=4`).

**Damage + Max Ammo** (and shotCount/startAmmo/playerDamage/meleeDamage in the detail view) are read big-endian straight from the zone at fixed `WeaponDef` byte offsets (`damage`=+0x230, `iMaxAmmo`=+0x21C, `iStartAmmo`=+0x208, `shotCount`=+0x220, `iMeleeDamage`=+0x238 — from the OAT IW4 `WeaponDef`; PC/PS3 share this layout, the size delta is the bool-flag tail). The detail view also shows the **damage falloff** (`minDamage`=+0x598, `minPlayerDamage`=+0x59C, `fMaxDamageRange`=+0x5A0, `fMinDamageRange`=+0x5A4 — these later offsets require *typed* struct sizing, since shorts/bools shift the region; computed offsets validate as model1887 minDamage 20, maxDamageRange 200, minDamageRange 600).

Note many WeaponDef scalars the reader exposes (accuracy/aiSpread/playerSpread/maxRange/turret arcs/turn-speeds) are **genuinely 0** for most player weapons — MW2 drives accuracy via the accuracy-graph and arcs are turret-only — so a 0 there is correct, not a misread (confirmed: the byte-faithful reader and direct typed offsets agree). All validated against `patch_mp.ff`: model1887 → bullet / spread-shotgun, 8-pellet shotCount, damage 35→20/pellet falloff, melee 135, reserve 56 (akimbo doubles to 112); airdrop_marker → grenade / grenade / item, 1 ammo.

**Menus** list each parsed item (type/text/dvar) as collapsed child nodes under each menu in the tree (from `MenuDef.Items`).

**TechSets** fill the technique-list column with the canonical IW4 technique-type slot name for each non-null technique slot (the shared technique bodies aren't dereferenced, so the name comes from the slot index).

**StructuredDataDef** assets get a dedicated code-built **Struct Data** tab (`PopulateStructuredDataDefs`, lazily added) listing each DefSet, with a double-click `StructuredDataDefViewerForm` showing the read-only layout dump — enums (entry name = value), structs (`+offset  name : type`), indexed/enumed arrays, and root type — rendered by `Iw4AssetBridge` from the fully-parsed `StructuredDataReader` output. `MW2GameDefinition.IsStructuredDataDefType` (id PS3 `0x26`/Xbox360 `0x25`/PC `0x27`) makes the pool view name them. The original `raw/mp/*.def` source format isn't shipped, so this is a view/dump, not an editor.
