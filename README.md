# CoD FastFile Tools

Tools for extracting, editing, and building Call of Duty FastFile (.ff) archives.

## Supported Games

| Game | PS3 | Xbox 360 | PC | Wii |
|------|-----|----------|-----|-----|
| CoD4: Modern Warfare | Full | Full | Partial | Extract |
| WaW: World at War | Full | Full | **Partial+menus** | **Partial** |
| MW2: Modern Warfare 2 | Full | **Full (unverified)** ¹ | **Partial** | - |

**Full** = decompress, edit all asset types, recompress
**Full (unverified)** = code path complete, hardware load test pending
**Partial** = decompress, edit rawfile + localize, recompress (other asset types listed but skipped)
**Partial+menus** = Partial, plus read-only menulist parsing (per-menu rect / colors / itemCount / window name)
**Extract** = decompress to zone only (no asset editing)

¹ MW2 Xbox 360 shares the same `MW2GameDefinition` as PS3 (rawfile / localize / weapon / partial menu support), with the library handling the platform differences (48-byte zone header, MW2 Xbox 360 asset enum, single-zlib-stream save, IW4 authed-chunks decode at `0x25` for signed retail files). Round-trip has not yet been verified on real Xbox 360 hardware.

> Xbox 360 needs a patched XEX to load modified FastFiles. Signed files are saved as unsigned (RSA cannot be regenerated).
> MW2 PC retail files use IW4's signed "authed chunks" format on read; saves emit the unsigned variant.

## Asset Support

| Support Level | Asset Types | What You Can Do |
|---------------|-------------|-----------------|
| **Full** | `rawfile`, `localize`, `weapon` | Parse, view, edit, and save changes |
| **Partial** | `menufile` (MenuList) | Parse menus and surface per-menu rect, 5 colors, itemCount, and window name as editable values. Full menu reconstruction (adding items, retargeting handlers) is not implemented. |
| **View Only** | `stringtable`, `xanim`, `material`, `techset`, `image`, `col_map_sp`, `col_map_mp` | Parse and view content, but cannot edit |
| **Detected** | All others (xmodel, sound, fx, etc.) | Shows in asset pool list, no parsing/editing |

> **Note**: Full asset list per game available in [docs/SupportedFormats.md](docs/SupportedFormats.md)

### What Each Level Means

- **Full**: These assets can be fully modified. RawFiles include GSC scripts, vision files, configs, etc. Localize entries are the in-game text strings.
- **Partial**: The editor parses and displays the asset, exposing select fields for editing, but the asset cannot be reconstructed from scratch on save.
- **View Only**: The editor can parse and display these, but saving changes isn't supported yet.
- **Detected**: The editor recognizes these asset types and shows them in the asset pool, but cannot parse their internal structure.

> When a zone is rebuilt (e.g. after a raw file size change), only `rawfile` and `localize` assets are preserved — all other types are lost. The editor warns before rebuild if unsupported types are present.

## Tools

| Tool | Description |
|------|-------------|
| FastFile Editor | Edit raw files, localized strings, weapons, and menus inside existing FastFiles. Async loader with drag/drop, File Report viewer, and a logging tab. |
| FastFile Compiler | Build new FastFiles from scratch using raw files and localized strings. Platform dropdown for PS3 / Xbox 360 (signed or unsigned) / PC / Wii. |
| FastFile Converter | Convert FastFiles between platforms (PS3 ↔ Xbox 360 in-place; rebuild path for anything → PC, anything → Wii, and MW2 PS3 ↔ MW2 PC) |
| FastFile Extractor | Simple extract/repack utility for zone files |
| FastFile CLI (`ffcli`) | Command-line tool for FastFile/zone operations |

## Screenshots

<details>
  <summary>FastFile Editor v2.0.0</summary>
  <p>Main Window with a loaded file</p>
  <img src="https://github.com/user-attachments/assets/9c476da4-8081-4479-96fe-46ae208b5edf" alt="Main Window with a loaded file">
  <p>String Tables</p>
  <img src="https://github.com/user-attachments/assets/6c14f173-cec4-40d2-892a-c626ccace509" alt="String Tables">
  <p>Localized String Assets</p>
  <img src="https://github.com/user-attachments/assets/4c29c5d4-7fae-4364-a0c4-12a93e0ab05d" alt="Localized String Assets">
  <p>Asset Pool Records</p>
  <img src="https://github.com/user-attachments/assets/866f50ff-dd3e-46c7-834e-984eb28eb81e" alt="Asset Pool Records">
  <p>Zone Header Addresses</p>
  <img src="https://github.com/user-attachments/assets/34e82bdc-37d3-4982-859c-9da1f85ef97" alt="Zone Header Addresses">
  <p>Tags</p>
  <img src="https://github.com/user-attachments/assets/bfef9118-3ef1-4c58-a7a1-109775bbea73" alt="Tags">
</details>

<details>
  <summary>FastFile Editor v1.0.0</summary>
  <p>Main Window with a loaded file</p>
  <img src="https://github.com/primetime43/CoD-FF-Tools/assets/12754111/9ae17ce3-1fb3-4d5d-86a7-f3e3d7ba23d0" alt="Main Window with a loaded file">
  <p>Edit Toolstrip Window</p>
  <img src="https://github.com/primetime43/CoD-FF-Tools/assets/12754111/b22d4af8-f4cf-411e-97ff-b6981d170ec5" alt="Edit Toolstrip Window">
  <p>Tools Toolstrip Window</p>
  <img src="https://github.com/primetime43/CoD-FF-Tools/assets/12754111/b3c3e4c6-a73d-42ea-8bb6-504d542524f6" alt="Tools Toolstrip Window">
  <p>File Structure Info Window</p>
  <img src="https://github.com/primetime43/CoD-FF-Tools/assets/12754111/59a0aaad-3be6-43c5-b6a7-ca67a793d8a0" alt="File Structure Info Window">
</details>

## Requirements

[.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

## Download

Grab the latest build from [Releases](https://github.com/primetime43/CoD-FF-Tools/releases).

## Project Layout

| Project | What it does |
|---------|--------------|
| FastFileLib | Core library - compression, decompression, zone build/patch/save, format detection |
| FastFileLib.WinForms | Shared WinForms helpers used by every GUI |
| Call of Duty FastFile Editor | Main editor GUI |
| FastFileCompilerGUI | FastFile creation GUI |
| FastFileConverterGUI | Platform conversion GUI |
| FastFileExtractor | Extract/repack GUI (FF ⇄ zone) |
| FastFileCLI | Command-line tool (`ffcli`) for FastFile/zone operations |
| FastFileCLI.Tests | xUnit test suite for the CLI |

## Contributing

Found a bug or want to add something? Open an issue or PR.

## Credits

**primetime43** - Author

Thanks to:
- BuC-ShoTz
- aerosoul94
- EliteMossy
- Fixed Username (testing)
