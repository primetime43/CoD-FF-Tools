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
        public static MenuList? ParseMenuList(byte[] zoneData, int offset, bool isBigEndian = true)
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

            // Parse only menu[0] for now. menuDef_t is a ~1500-byte binary struct followed by
            // recursive inline data (items, transitions, expressions, event handlers) whose
            // total size depends on N items × per-item inline data. Without a full deserializer
            // matching the engine's load order, we can only locate menu[0] reliably — its data
            // starts right after the pointer array. For menu[1..N] we'd have to know the
            // previous menu's exact end, which we don't.
            //
            // The old code fabricated MenuDef entries for every index by stepping forward in
            // hardcoded 0x180-byte chunks, which produced garbage names ("csv", "Ad",
            // "classIndex") harvested from random byte positions inside the binary data.
            // Better to emit just the one menu we trust than mislead the user with 276 fakes.
            if (menuCount > 0 && afterPointersOffset < zoneData.Length)
            {
                var firstMenu = ParseMenuDef(zoneData, afterPointersOffset, isBigEndian, 0);
                if (firstMenu != null)
                {
                    menuList.Menus.Add(firstMenu);
                }
            }

            // DataEndOffset is set to just past the pointer array. We don't know the true end
            // of the menu data region, so downstream callers (e.g. the AssetRecordProcessor
            // pattern-matching loop for the next menufile) will scan forward from here through
            // the binary menu blob until they hit the next MenuList header. The pattern is
            // strict enough (12-byte FFFFFFFF + small menuCount + FFFFFFFF + .txt-suffixed
            // name) that menu-internal FFFFFFFF markers don't false-positive.
            menuList.DataEndOffset = afterPointersOffset;

            Debug.WriteLine($"[MenuListParser] Completed MenuList '{name}': {menuList.Menus.Count} of {menuCount} menus parsed (binary deserializer for menuDef_t not implemented; only menu[0] returned), pointer-array end=0x{afterPointersOffset:X}");
            return menuList;
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
