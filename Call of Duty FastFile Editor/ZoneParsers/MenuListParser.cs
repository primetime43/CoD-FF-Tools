using Call_of_Duty_FastFile_Editor.Models;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Parser for MenuList (menufile) assets in CoD5 WaW PS3 zone files.
    ///
    /// MenuList structure in zone:
    /// struct MenuList {
    ///     const char* name;      // 4 bytes - 0xFFFFFFFF when inline
    ///     int menuCount;         // 4 bytes (big-endian on PS3)
    ///     menuDef_t** menus;     // 4 bytes - 0xFFFFFFFF when inline
    /// };
    ///
    /// Zone layout when inline:
    /// [FF FF FF FF] [menuCount BE] [FF FF FF FF] [name\0] [menu pointers...] [menu data...]
    ///
    /// Each menuDef_t starts with a windowDef_t which contains:
    /// struct windowDef_t {
    ///     const char* name;       // 0x00 - 4 bytes (0xFFFFFFFF when inline)
    ///     rectDef_t rect;         // 0x04 - 20 bytes (x, y, w, h, horzAlign, vertAlign, padding)
    ///     rectDef_t rectClient;   // 0x18 - 20 bytes
    ///     const char* group;      // 0x2C - 4 bytes pointer
    ///     int style;              // 0x30
    ///     int border;             // 0x34
    ///     int ownerDraw;          // 0x38
    ///     int ownerDrawFlags;     // 0x3C
    ///     float borderSize;       // 0x40
    ///     int staticFlags;        // 0x44
    ///     int dynamicFlags[1];    // 0x48 (varies)
    ///     ... more fields
    /// };
    /// </summary>
    /// <summary>Which menuDef_t binary layout to use when walking menus inside a MenuList.</summary>
    public enum MenuBinaryLayout
    {
        /// <summary>IW4 (MW2) layout — 752 bytes console / 400 PC, full OAT-spec walker.</summary>
        Iw4,
        /// <summary>CoD5 (WaW) console layout — 312 bytes (PS3 / Xbox 360), big-endian.</summary>
        Cod5,
        /// <summary>CoD5 (WaW) PC layout — 288 bytes (dynamicFlags[1] + cursorItem[1] vs console's [4]/[4]), little-endian.</summary>
        Cod5PC,
    }

    public static class MenuListParser
    {
        // WindowDef size varies between PC and console
        // PS3 Console: Approximately 0xB0 bytes (larger due to multi-resolution rects)
        // PC: 0xA4 bytes
        private const int WINDOW_DEF_SIZE_CONSOLE = 0xB0;
        private const int RECT_DEF_SIZE = 0x14; // 20 bytes: x, y, w, h (4 floats) + horzAlign, vertAlign (2 bytes) + 2 padding

        // menuDef_t has windowDef_t at start, then additional fields
        // The full structure is very complex, but we can estimate end by looking for next marker
        private const int MIN_MENU_DEF_SIZE = 0x180; // Minimum size estimate

        /// <summary>
        /// Parses a MenuList asset starting at the given offset.
        /// </summary>
        public static MenuList? ParseMenuList(byte[] zoneData, int offset, bool isBigEndian = true,
                                              MenuBinaryLayout layout = MenuBinaryLayout.Iw4)
        {
            Debug.WriteLine($"[MenuListParser] Parsing MenuList at offset 0x{offset:X}");

            if (offset + 12 > zoneData.Length)
            {
                Debug.WriteLine($"[MenuListParser] Not enough data at offset 0x{offset:X}");
                return null;
            }

            // Check for name pointer marker
            uint namePtr = ReadUInt32(zoneData, offset, isBigEndian);
            if (namePtr != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[MenuListParser] Expected 0xFFFFFFFF at offset 0x{offset:X}, got 0x{namePtr:X}");
                return null;
            }

            // Read menu count
            int menuCount = ReadInt32(zoneData, offset + 4, isBigEndian);
            if (menuCount < 0 || menuCount > 500)
            {
                Debug.WriteLine($"[MenuListParser] Invalid menu count {menuCount} at offset 0x{offset + 4:X}");
                return null;
            }

            // Check for menus pointer marker
            uint menusPtr = ReadUInt32(zoneData, offset + 8, isBigEndian);
            if (menusPtr != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[MenuListParser] Expected 0xFFFFFFFF at offset 0x{offset + 8:X}, got 0x{menusPtr:X}");
                return null;
            }

            // Read name string (starts after the 12-byte header)
            int nameOffset = offset + 12;
            string name = ReadNullTerminatedString(zoneData, nameOffset);

            if (string.IsNullOrEmpty(name))
            {
                Debug.WriteLine($"[MenuListParser] Empty name at offset 0x{nameOffset:X}");
                return null;
            }

            // Validate name looks like a menu file path
            if (!IsValidMenuFileName(name))
            {
                Debug.WriteLine($"[MenuListParser] Invalid menu file name: '{name}'");
                return null;
            }

            int nameByteCount = Encoding.ASCII.GetByteCount(name) + 1;
            int afterNameOffset = nameOffset + nameByteCount;

            var menuList = new MenuList
            {
                Name = name,
                MenuCount = menuCount,
                StartOfFileHeader = offset,
                EndOfFileHeader = afterNameOffset,
                DataStartOffset = afterNameOffset
            };

            Debug.WriteLine($"[MenuListParser] Found MenuList: '{name}' with {menuCount} menus");

            // Read the inline menuDef_t pointer array — each entry is 0xFFFFFFFF when the
            // menu is serialized inline (the usual case).
            int afterPointersOffset = afterNameOffset;
            if (menuCount > 0)
            {
                afterPointersOffset = afterNameOffset + (menuCount * 4);
                if (afterPointersOffset > zoneData.Length)
                {
                    Debug.WriteLine($"[MenuListParser] Pointer array extends past zone end");
                    return null;
                }
            }

            // Walk each menu with the right strategy per layout.
            //
            // IW4 (MW2): use the full OAT-ported field-by-field walker; if it gives up
            // partway, fall back to signature scanning + heuristic field reads.
            //
            // CoD5 (WaW): skip the IW4 walker entirely (wrong struct layout produces
            // garbage itemCount). Instead, signature-scan for menu starts and read each
            // candidate's fields via the CoD5-aware reader. This finds the same set of
            // menus the old fallback path found, but with correct itemCount / colors /
            // rect / inline name instead of garbage.
            int currentOffset = afterPointersOffset;
            int lastSuccessfulEnd = afterPointersOffset;

            if (layout == MenuBinaryLayout.Cod5 || layout == MenuBinaryLayout.Cod5PC)
            {
                // Strict CoD5 scan: scan forward byte-by-byte from the pointer array end
                // and accept every position whose menuDef_t binary FitsMenuDefStruct (every
                // pointer is 0/FFFFFFFF/resolved, every count/bool/color is in range, etc.).
                // This finds all menuCount inline menus without needing to walk each menu's
                // variable-size inline payload — the struct check itself is what tells
                // itemDef_s candidates apart from menuDef_t candidates.
                //
                // PC vs console differs only in struct sizes (288 vs 312 bytes) and byte
                // order (LE vs BE) — both handled by the same deserializer via flags.
                bool isPCLayout = layout == MenuBinaryLayout.Cod5PC;
                var cod5Reader = new Cod5MenuDeserializer(zoneData, afterPointersOffset, isBigEndian, isPCLayout);
                int searchFrom = afterPointersOffset;

                for (int i = 0; i < menuCount; i++)
                {
                    int menuStart = Cod5MenuDeserializer.FindNextCod5MenuStart(zoneData, searchFrom, isBigEndian, isPCLayout);
                    if (menuStart < 0)
                    {
                        Debug.WriteLine($"[MenuListParser] CoD5{(isPCLayout?"-PC":"")}: no more menuDef_t-shaped data after 0x{searchFrom:X} (found {menuList.Menus.Count} of {menuCount})");
                        break;
                    }

                    cod5Reader.Position = menuStart;
                    var menu = cod5Reader.ReadMenuDef(i, -1);
                    if (menu == null)
                    {
                        searchFrom = menuStart + 1;
                        i--;
                        continue;
                    }
                    menuList.Menus.Add(menu);
                    lastSuccessfulEnd = menu.EndOffset;
                    // Advance by ONE byte, not by sizeof(menuDef_t) — we don't actually know
                    // exactly where the next menu starts (would need a full items[] walker),
                    // and the menus aren't always sizeof(menuDef_t) apart. The strict
                    // FitsMenuDefStruct check guarantees the next match is also a menu.
                    searchFrom = menuStart + 1;
                }
            }
            else
            {
                var deser = new Iw4MenuDeserializer(zoneData, afterPointersOffset, isConsole: isBigEndian, isBigEndian: isBigEndian);
                int fallbackStartIndex = -1;

                for (int i = 0; i < menuCount; i++)
                {
                    int menuStartBefore = deser.Position;
                    var menu = deser.ReadMenuDef(i);
                    if (menu == null)
                    {
                        Debug.WriteLine($"[MenuListParser] Deserializer failed at menu[{i}] (offset 0x{menuStartBefore:X}); falling back to signature scan for the remaining {menuCount - i} menus");
                        fallbackStartIndex = i;
                        currentOffset = menuStartBefore;
                        break;
                    }
                    menuList.Menus.Add(menu);
                    lastSuccessfulEnd = menu.EndOffset;
                    currentOffset = menu.EndOffset;
                }

                if (fallbackStartIndex >= 0)
                {
                    const int minMenuSize = 0x2B0;
                    const int maxScanPerMenu = 4 * 1024 * 1024;
                    for (int i = fallbackStartIndex; i < menuCount; i++)
                    {
                        int menuStart = FindMenuStartSignature(zoneData, currentOffset, maxScanPerMenu, isBigEndian);
                        if (menuStart < 0)
                        {
                            Debug.WriteLine($"[MenuListParser] Signature fallback also failed at menu[{i}] (from 0x{currentOffset:X})");
                            break;
                        }
                        var menu = ParseMenuDef(zoneData, menuStart, isBigEndian, i);
                        if (menu == null) break;
                        menuList.Menus.Add(menu);
                        lastSuccessfulEnd = Math.Max(menu.EndOffset, menuStart + minMenuSize);
                        currentOffset = menuStart + minMenuSize;
                    }
                }
            }

            menuList.DataEndOffset = lastSuccessfulEnd;
            Debug.WriteLine($"[MenuListParser] Completed MenuList '{name}': {menuList.Menus.Count} of {menuCount} menus parsed (layout={layout}), end=0x{menuList.DataEndOffset:X}");
            return menuList;
        }

        /// <summary>
        /// Scans forward from <paramref name="startOffset"/> for the next plausible menuDef_t
        /// start. The signature requires 0xFFFFFFFF + two back-to-back rectDef_s structs (rect
        /// + rectClient), with all floats finite and roughly UI-sized, plus horz/vert align
        /// bytes in [0,3]. Stops after <paramref name="maxScanDistance"/> bytes. Returns -1
        /// if no signature found.
        /// </summary>
        private static int FindMenuStartSignature(byte[] data, int startOffset, int maxScanDistance, bool isBigEndian)
        {
            int end = Math.Min(startOffset + maxScanDistance, data.Length - 0x2C);
            for (int p = startOffset; p < end; p++)
            {
                // Cheap reject: must start with 0xFFFFFFFF (name pointer placeholder).
                if (data[p] != 0xFF || data[p + 1] != 0xFF || data[p + 2] != 0xFF || data[p + 3] != 0xFF)
                    continue;

                // rect (offset +0x04): 4 floats + horz/vert align byte + 2 padding bytes
                if (!IsPlausibleRectAt(data, p + 4, isBigEndian))
                    continue;

                // rectClient (offset +0x18): same shape
                if (!IsPlausibleRectAt(data, p + 24, isBigEndian))
                    continue;

                return p;
            }
            return -1;
        }

        /// <summary>
        /// Validates a rectDef_s at <paramref name="rectOffset"/>: 4 floats (x, y, w, h) in
        /// plausible UI coordinate range, plus horz/vert align bytes in [0,3]. Allows w/h to
        /// be 0 since some menus use 0-size rects.
        /// </summary>
        private static bool IsPlausibleRectAt(byte[] data, int rectOffset, bool isBigEndian)
        {
            float x = ReadFloat(data, rectOffset, isBigEndian);
            float y = ReadFloat(data, rectOffset + 4, isBigEndian);
            float w = ReadFloat(data, rectOffset + 8, isBigEndian);
            float h = ReadFloat(data, rectOffset + 12, isBigEndian);

            // Reject NaN / Infinity / denormals (each value must be 0 OR a normal float)
            if (!IsPlausibleCoord(x)) return false;
            if (!IsPlausibleCoord(y)) return false;
            if (!IsPlausibleCoord(w)) return false;
            if (!IsPlausibleCoord(h)) return false;

            // Width/height should be non-negative (zero is OK; some hidden menus use 0-size rects)
            if (w < 0 || h < 0) return false;

            // horz/vert align bytes must be small enum values
            byte horzAlign = data[rectOffset + 16];
            byte vertAlign = data[rectOffset + 17];
            if (horzAlign > 3 || vertAlign > 3) return false;

            return true;
        }

        /// <summary>
        /// A "plausible" UI coordinate is either exactly zero or a normal float in pixel range
        /// (|v| >= 0.01 and |v| <= 10000). This rejects sub-pixel denormals like 4.98E-10 that
        /// frequently show up when reading random bytes as floats — a strong false-positive
        /// signal when scanning for menuDef_t headers inside binary menu payloads.
        /// </summary>
        private static bool IsPlausibleCoord(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            if (v == 0f) return true;
            float abs = Math.Abs(v);
            return abs >= 0.01f && abs <= 10000f;
        }

        /// <summary>
        /// Parses a single menuDef_t structure.
        /// </summary>
        private static MenuDef? ParseMenuDef(byte[] zoneData, int offset, bool isBigEndian, int menuIndex)
        {
            Debug.WriteLine($"[MenuListParser] Parsing menuDef_t #{menuIndex} at offset 0x{offset:X}");

            if (offset + 4 > zoneData.Length)
                return null;

            // menuDef_t starts with windowDef_t
            // windowDef_t starts with name pointer
            uint windowNamePtr = ReadUInt32(zoneData, offset, isBigEndian);

            var menu = new MenuDef
            {
                Window = new WindowDef(),
                StartOffset = offset
            };

            int currentOffset = offset;

            // Parse window name
            if (windowNamePtr == 0xFFFFFFFF)
            {
                // Name is inline - skip the windowDef_t structure to find where name string is
                // The name string typically follows immediately after the window structure
                // But we need to parse the structure first to know where inline strings go

                // For now, search for the name string after the minimum window structure size
                int nameSearchStart = offset + 4; // After the name pointer
                string windowName = FindWindowName(zoneData, nameSearchStart, isBigEndian);

                if (!string.IsNullOrEmpty(windowName))
                {
                    menu.Window.Name = windowName;
                    Debug.WriteLine($"[MenuListParser] Menu #{menuIndex} name: '{windowName}'");
                }
            }
            else if (windowNamePtr == 0)
            {
                // Null name - menu has no name
                menu.Window.Name = $"(menu_{menuIndex})";
            }
            else
            {
                // External reference - shouldn't happen for inline menus
                Debug.WriteLine($"[MenuListParser] Menu #{menuIndex} has external name reference: 0x{windowNamePtr:X}");
                menu.Window.Name = $"(external_{windowNamePtr:X})";
            }

            // Parse rect (offset 0x04 in windowDef_t) and track it
            int rectOffset = offset + 4;
            if (rectOffset + RECT_DEF_SIZE <= zoneData.Length)
            {
                menu.Window.Rect = ParseRectDef(zoneData, rectOffset, isBigEndian);

                // Add rect as editable value
                menu.EditableValues.Add(MenuValue.CreateRect("rect",
                    menu.Window.Rect.X, menu.Window.Rect.Y,
                    menu.Window.Rect.W, menu.Window.Rect.H,
                    rectOffset));
            }

            // Parse colors - they're at specific offsets in windowDef_t
            // PS3 windowDef_t layout (approximate):
            // 0x4C-0x5B: foreColor (16 bytes)
            // 0x5C-0x6B: backColor (16 bytes)
            // 0x6C-0x7B: borderColor (16 bytes)
            // 0x7C-0x8B: outlineColor (16 bytes)
            // 0x8C-0x9B: disableColor (16 bytes)
            ParseAndTrackColors(zoneData, offset, isBigEndian, menu);

            // Try to find item count - it's at a fixed offset in menuDef_t
            // For PS3, itemCount is typically around offset 0xB8-0xC0 in the menuDef_t
            // This varies by game version, so we'll try to detect it
            int itemCount = TryFindItemCount(zoneData, offset, isBigEndian);
            if (itemCount >= 0 && itemCount < 200)
            {
                menu.ItemCount = itemCount;
                Debug.WriteLine($"[MenuListParser] Menu #{menuIndex} itemCount: {itemCount}");
            }

            // Estimate end offset - look for next 0xFFFFFFFF marker that could start another structure
            menu.EndOffset = EstimateMenuDefEnd(zoneData, offset, isBigEndian);

            return menu;
        }

        /// <summary>
        /// Parses color values from windowDef_t and tracks them for editing.
        /// </summary>
        private static void ParseAndTrackColors(byte[] zoneData, int menuOffset, bool isBigEndian, MenuDef menu)
        {
            // Color offsets in windowDef_t vary by platform
            // Try multiple possible offsets and validate the values
            int[][] colorOffsetSets = new int[][]
            {
                // PS3 typical offsets
                new int[] { 0x4C, 0x5C, 0x6C, 0x7C, 0x8C },
                // Alternative offsets
                new int[] { 0x50, 0x60, 0x70, 0x80, 0x90 },
                new int[] { 0x54, 0x64, 0x74, 0x84, 0x94 },
            };

            string[] colorNames = { "foreColor", "backColor", "borderColor", "outlineColor", "disableColor" };

            foreach (var offsets in colorOffsetSets)
            {
                bool allValid = true;
                float[][] colors = new float[5][];

                for (int i = 0; i < 5; i++)
                {
                    int colorOffset = menuOffset + offsets[i];
                    if (colorOffset + 16 > zoneData.Length)
                    {
                        allValid = false;
                        break;
                    }

                    colors[i] = new float[4];
                    for (int j = 0; j < 4; j++)
                    {
                        colors[i][j] = ReadFloat(zoneData, colorOffset + j * 4, isBigEndian);
                    }

                    // Validate color values are in reasonable range (0-1 for normalized, or 0-255)
                    if (!IsValidColor(colors[i]))
                    {
                        allValid = false;
                        break;
                    }
                }

                if (allValid)
                {
                    // Found valid colors at these offsets
                    for (int i = 0; i < 5; i++)
                    {
                        int colorOffset = menuOffset + offsets[i];
                        menu.EditableValues.Add(MenuValue.CreateColor(colorNames[i], colors[i], colorOffset));

                        // Also store in Window for display
                        switch (i)
                        {
                            case 0: menu.Window.ForeColor = colors[i]; break;
                            case 1: menu.Window.BackColor = colors[i]; break;
                            case 2: menu.Window.BorderColor = colors[i]; break;
                            case 3: menu.Window.OutlineColor = colors[i]; break;
                            case 4: menu.Window.DisableColor = colors[i]; break;
                        }
                    }

                    Debug.WriteLine($"[MenuListParser] Found colors at offset set starting 0x{offsets[0]:X}");
                    return;
                }
            }

            Debug.WriteLine($"[MenuListParser] Could not find valid color values");
        }

        /// <summary>
        /// Validates that a color array contains reasonable values.
        /// </summary>
        private static bool IsValidColor(float[] color)
        {
            if (color == null || color.Length < 4)
                return false;

            foreach (var c in color)
            {
                // Colors should be in range 0-1 (normalized) or 0-255
                // Also allow small negative values for effects
                if (float.IsNaN(c) || float.IsInfinity(c))
                    return false;
                if (c < -1 || c > 255)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Finds the window name by searching for a valid menu name string.
        /// </summary>
        private static string FindWindowName(byte[] zoneData, int searchStart, bool isBigEndian)
        {
            // The name string location varies, but it's usually after the header structures
            // Look for 0xFFFFFFFF markers and try to find valid strings after them

            for (int searchOffset = searchStart; searchOffset < Math.Min(searchStart + 512, zoneData.Length - 4); searchOffset++)
            {
                // Look for a sequence that looks like a name
                if (zoneData[searchOffset] >= 'a' && zoneData[searchOffset] <= 'z' ||
                    zoneData[searchOffset] >= 'A' && zoneData[searchOffset] <= 'Z')
                {
                    string candidate = ReadNullTerminatedString(zoneData, searchOffset);
                    if (IsValidMenuName(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to find the item count in the menuDef_t structure.
        /// </summary>
        private static int TryFindItemCount(byte[] zoneData, int menuOffset, bool isBigEndian)
        {
            // itemCount is typically at a known offset, but varies by platform
            // On PS3, it's often around 0xB8 or 0xBC
            int[] possibleOffsets = { 0xB8, 0xBC, 0xAC, 0xB0, 0xB4 };

            foreach (int off in possibleOffsets)
            {
                if (menuOffset + off + 4 <= zoneData.Length)
                {
                    int value = ReadInt32(zoneData, menuOffset + off, isBigEndian);
                    // Item count should be a reasonable number
                    if (value >= 0 && value <= 100)
                    {
                        // Additional check: the next int might be fontIndex or similar (0-10 range)
                        int nextValue = ReadInt32(zoneData, menuOffset + off + 4, isBigEndian);
                        if (nextValue >= 0 && nextValue <= 20)
                        {
                            return value;
                        }
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Parses a rectDef_t structure.
        /// </summary>
        private static RectDef ParseRectDef(byte[] zoneData, int offset, bool isBigEndian)
        {
            return new RectDef
            {
                X = ReadFloat(zoneData, offset, isBigEndian),
                Y = ReadFloat(zoneData, offset + 4, isBigEndian),
                W = ReadFloat(zoneData, offset + 8, isBigEndian),
                H = ReadFloat(zoneData, offset + 12, isBigEndian),
                HorzAlign = zoneData[offset + 16],
                VertAlign = zoneData[offset + 17]
            };
        }

        /// <summary>
        /// Estimates where a menuDef_t structure ends.
        /// </summary>
        private static int EstimateMenuDefEnd(byte[] zoneData, int startOffset, bool isBigEndian)
        {
            // Look for the next 0xFFFFFFFF marker that could be the start of another menuDef_t or the end
            int searchEnd = Math.Min(startOffset + 0x2000, zoneData.Length - 4); // Max 8KB per menu

            for (int pos = startOffset + MIN_MENU_DEF_SIZE; pos < searchEnd; pos++)
            {
                uint value = ReadUInt32(zoneData, pos, isBigEndian);
                if (value == 0xFFFFFFFF)
                {
                    // Check if this looks like the start of another structure
                    // A new menuDef_t would have: [FF FF FF FF] followed by rect data (floats)
                    if (pos + 8 < zoneData.Length)
                    {
                        float possibleX = ReadFloat(zoneData, pos + 4, isBigEndian);
                        // Menu positions are usually in the range -1000 to 2000
                        if (possibleX >= -1000 && possibleX <= 2000)
                        {
                            return pos;
                        }
                    }
                }
            }

            // If we can't find a marker, use minimum size
            return Math.Min(startOffset + MIN_MENU_DEF_SIZE, zoneData.Length);
        }

        /// <summary>
        /// Finds the next menuDef_t by looking for the characteristic pattern.
        /// </summary>
        private static int FindNextMenuDef(byte[] zoneData, int searchStart, bool isBigEndian)
        {
            int searchEnd = Math.Min(searchStart + 0x2000, zoneData.Length - 4);

            for (int pos = searchStart; pos < searchEnd; pos++)
            {
                uint value = ReadUInt32(zoneData, pos, isBigEndian);
                if (value == 0xFFFFFFFF)
                {
                    // Validate this looks like a menuDef_t start
                    if (pos + 8 < zoneData.Length)
                    {
                        float possibleX = ReadFloat(zoneData, pos + 4, isBigEndian);
                        if (possibleX >= -1000 && possibleX <= 2000)
                        {
                            return pos;
                        }
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Validates that a string looks like a valid menu file name.
        /// Examples: "ui_mp/main.menu", "ui/scriptmenus/class.menu"
        /// </summary>
        private static bool IsValidMenuFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 256)
                return false;

            // Should contain path separators or end with .menu
            if (!name.Contains('/') && !name.Contains('\\') && !name.EndsWith(".menu", StringComparison.OrdinalIgnoreCase))
                return false;

            // Check for valid path characters
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '/' && c != '\\' && c != '.' && c != '-')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that a string looks like a valid menu name.
        /// Examples: "main_menu", "class_select", "popup_findmatch"
        /// </summary>
        private static bool IsValidMenuName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2 || name.Length > 128)
                return false;

            // Must start with a letter
            if (!char.IsLetter(name[0]))
                return false;

            // Check for valid identifier characters (letters, digits, underscores)
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }

        #region Read Helpers

        private static string ReadNullTerminatedString(byte[] data, int offset)
        {
            var sb = new StringBuilder();
            while (offset < data.Length && data[offset] != 0)
            {
                char c = (char)data[offset];
                if (c < 0x20 || c > 0x7E)
                    break; // Non-printable character
                sb.Append(c);
                offset++;
            }
            return sb.ToString();
        }

        private static uint ReadUInt32(byte[] data, int offset, bool isBigEndian)
        {
            if (offset + 4 > data.Length) return 0;
            if (isBigEndian)
            {
                return (uint)((data[offset] << 24) | (data[offset + 1] << 16) |
                              (data[offset + 2] << 8) | data[offset + 3]);
            }
            return BitConverter.ToUInt32(data, offset);
        }

        private static int ReadInt32(byte[] data, int offset, bool isBigEndian)
        {
            return (int)ReadUInt32(data, offset, isBigEndian);
        }

        private static float ReadFloat(byte[] data, int offset, bool isBigEndian)
        {
            if (offset + 4 > data.Length) return 0;
            if (isBigEndian)
            {
                byte[] bytes = { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] };
                return BitConverter.ToSingle(bytes, 0);
            }
            return BitConverter.ToSingle(data, offset);
        }

        #endregion
    }
}
