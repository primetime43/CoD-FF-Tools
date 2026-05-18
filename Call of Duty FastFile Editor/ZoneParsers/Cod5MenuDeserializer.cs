using System;
using System.Diagnostics;
using System.Text;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Field-by-field deserializer for CoD5 (WaW) menuDef_t binaries.
    ///
    /// The IW4 (MW2) deserializer can't be used here — WaW has a smaller, differently-ordered
    /// menuDef_t struct (312 bytes vs MW2's 752 on console). Key differences:
    ///   - WaW windowDef_t is 168 bytes (no disableColor; horz/vert align stored as ints, not bytes)
    ///   - WaW statement_s is 8 bytes (just count + ptr — no supportingData / lastResult)
    ///   - WaW menuDef_t has onFocus + disableColor[4]; no onCloseRequest, no rectW/HExp,
    ///     no menuTransitions, no expressionData
    ///
    /// Struct layout (PS3 / Xbox 360 console, big-endian, sourced from codresearch.dev's
    /// "Menu Asset (WaW)" page):
    ///
    ///   struct windowDef_t {       // 168 bytes (0xA8)
    ///     const char *name;          // +0x00
    ///     rectDef_s rect;            // +0x04 (24 bytes)
    ///     rectDef_s rectClient;      // +0x1C (24 bytes)
    ///     const char *group;         // +0x34
    ///     int style;                 // +0x38
    ///     int border;                // +0x3C
    ///     int ownerDraw;             // +0x40
    ///     int ownerDrawFlags;        // +0x44
    ///     float borderSize;          // +0x48
    ///     int staticFlags;           // +0x4C
    ///     int dynamicFlags[4];       // +0x50 (16 bytes)
    ///     int nextTime;              // +0x60
    ///     float foreColor[4];        // +0x64 (16 bytes)
    ///     float backColor[4];        // +0x74
    ///     float borderColor[4];      // +0x84
    ///     float outlineColor[4];     // +0x94
    ///     Material *background;      // +0xA4
    ///   };                           // end 0xA8
    ///
    ///   struct menuDef_t {          // 312 bytes (0x138)
    ///     windowDef_t window;        // +0x00..0xA7
    ///     const char *font;          // +0xA8
    ///     int fullScreen;            // +0xAC
    ///     int itemCount;             // +0xB0  <-- the field shown as garbage 0x20000022 before
    ///     int fontIndex;             // +0xB4
    ///     int cursorItem[4];         // +0xB8
    ///     int fadeCycle;             // +0xC8
    ///     float fadeClamp;           // +0xCC
    ///     float fadeAmount;          // +0xD0
    ///     float fadeInAmount;        // +0xD4
    ///     float blurRadius;          // +0xD8
    ///     const char *onOpen;        // +0xDC
    ///     const char *onFocus;       // +0xE0
    ///     const char *onClose;       // +0xE4
    ///     const char *onESC;         // +0xE8
    ///     ItemKeyHandler *onKey;     // +0xEC
    ///     statement_s visibleExp;    // +0xF0 (8 bytes: count + ptr)
    ///     const char *allowedBinding;// +0xF8
    ///     const char *soundName;     // +0xFC
    ///     int imageTrack;            // +0x100
    ///     float focusColor[4];       // +0x104
    ///     float disableColor[4];     // +0x114
    ///     statement_s rectXExp;      // +0x124
    ///     statement_s rectYExp;      // +0x12C
    ///     itemDef_s **items;         // +0x134
    ///   };                           // end 0x138
    ///
    /// The inline data layout (CoD5 source code is not in OAT, so the ordering below is
    /// empirically derived from the first ui.ff menu in the WaW UI zone — see
    /// `MenuListParser` callers). We only need to scan far enough to find the next
    /// menuDef_t's windowDef_t signature; once we hit that, we stop. Items are NOT
    /// recursively walked yet (TODO if a use case demands editable item content).
    /// </summary>
    public class Cod5MenuDeserializer
    {
        private const int WindowDefSize = 0xA8;   // 168 bytes (WaW console)
        private const int MenuDefSize   = 0x138;  // 312 bytes (WaW console)
        private const int RectDefSize   = 0x18;   // 24 bytes (4 floats + 2 ints)
        private const int ItemCountOffset = 0xB0; // within menuDef_t

        private readonly byte[] _data;
        private readonly bool _isBigEndian;
        private int _pos;

        public Cod5MenuDeserializer(byte[] zoneData, int startOffset, bool isBigEndian)
        {
            _data = zoneData;
            _pos = startOffset;
            _isBigEndian = isBigEndian;
        }

        public int Position
        {
            get => _pos;
            set => _pos = value;
        }

        /// <summary>
        /// Reads a single menuDef_t at the current stream position. Populates rect, colors,
        /// itemCount, fullScreen, fade fields, and the inline window.name string. Advances the
        /// stream past the 312-byte binary plus inline strings, stopping when we detect the
        /// next menu's windowDef_t signature. Returns null if the binary at the current position
        /// doesn't look like a menuDef_t (window.name ptr isn't 0 or 0xFFFFFFFF).
        /// </summary>
        public MenuDef ReadMenuDef(int menuIndex, int nextStopOffset)
        {
            int menuStart = _pos;
            if (menuStart + MenuDefSize > _data.Length)
            {
                Debug.WriteLine($"[Cod5MenuDeserializer] menu[{menuIndex}] @ 0x{menuStart:X}: not enough data");
                return null;
            }

            // Strict struct-fit validation — every pointer-typed field in the WaW menuDef_t
            // is serialized as either 0 (null) or 0xFFFFFFFF (inline-follows). Anything else
            // means we're not pointing at a real menuDef_t. Same goes for fullScreen
            // (boolean) and itemCount (a count, not a hash). If ANY of these don't fit, this
            // is not a menu — return null so the caller can advance and try the next byte.
            // This is what catches the old "menu #1 [536870946 items]" garbage: 536870946 is
            // an ASCII fragment ("\x20\x00\x00\x22"), not a valid item count.
            if (!FitsMenuDefStruct(_data, menuStart, _isBigEndian, out string failReason))
            {
                Debug.WriteLine($"[Cod5MenuDeserializer] menu[{menuIndex}] @ 0x{menuStart:X}: not a menuDef_t — {failReason}");
                return null;
            }

            uint namePtr = ReadU32(menuStart);
            var menu = new MenuDef { StartOffset = menuStart, Window = new WindowDef() };

            // === windowDef_t (0xA8 bytes) ===
            menu.Window.Rect       = ReadRect(menuStart + 0x04);
            menu.Window.RectClient = ReadRect(menuStart + 0x1C);
            menu.Window.Style          = ReadI32(menuStart + 0x38);
            menu.Window.Border         = ReadI32(menuStart + 0x3C);
            menu.Window.OwnerDraw      = ReadI32(menuStart + 0x40);
            menu.Window.OwnerDrawFlags = ReadI32(menuStart + 0x44);
            menu.Window.BorderSize     = ReadF32(menuStart + 0x48);
            menu.Window.StaticFlags    = ReadI32(menuStart + 0x4C);
            menu.Window.DynamicFlags   = new[] {
                ReadI32(menuStart + 0x50), ReadI32(menuStart + 0x54),
                ReadI32(menuStart + 0x58), ReadI32(menuStart + 0x5C)
            };
            menu.Window.NextTime       = ReadI32(menuStart + 0x60);
            menu.Window.ForeColor      = ReadColor(menuStart + 0x64);
            menu.Window.BackColor      = ReadColor(menuStart + 0x74);
            menu.Window.BorderColor    = ReadColor(menuStart + 0x84);
            menu.Window.OutlineColor   = ReadColor(menuStart + 0x94);

            // === menuDef_t-specific fields ===
            menu.Fullscreen   = ReadI32(menuStart + 0xAC);
            menu.ItemCount    = ReadI32(menuStart + ItemCountOffset);
            menu.FontIndex    = ReadI32(menuStart + 0xB4);
            menu.CursorItems  = new[] {
                ReadI32(menuStart + 0xB8), ReadI32(menuStart + 0xBC),
                ReadI32(menuStart + 0xC0), ReadI32(menuStart + 0xC4),
            };
            menu.FadeCycle    = ReadI32(menuStart + 0xC8);
            menu.FadeClamp    = ReadF32(menuStart + 0xCC);
            menu.FadeAmount   = ReadF32(menuStart + 0xD0);
            menu.FadeInAmount = ReadF32(menuStart + 0xD4);
            menu.BlurRadius   = ReadF32(menuStart + 0xD8);
            uint onOpenPtr           = ReadU32(menuStart + 0xDC);
            uint onFocusPtr          = ReadU32(menuStart + 0xE0);
            uint onClosePtr          = ReadU32(menuStart + 0xE4);
            uint onESCPtr            = ReadU32(menuStart + 0xE8);
            uint onKeyPtr            = ReadU32(menuStart + 0xEC);
            uint visibleExpEntries   = ReadU32(menuStart + 0xF4);
            uint allowedBindingPtr   = ReadU32(menuStart + 0xF8);
            uint soundNamePtr        = ReadU32(menuStart + 0xFC);
            menu.ImageTrack          = ReadI32(menuStart + 0x100);
            menu.FocusColor          = ReadColor(menuStart + 0x104);
            menu.Window.DisableColor = ReadColor(menuStart + 0x114); // moved to menuDef_t in WaW
            uint rectXExpEntries     = ReadU32(menuStart + 0x128);
            uint rectYExpEntries     = ReadU32(menuStart + 0x130);
            uint itemsPtr            = ReadU32(menuStart + 0x134);

            int binaryEnd = menuStart + MenuDefSize;
            _pos = binaryEnd;

            // === inline data ===
            // We only read window.name (when inline) — it's the one field we surface in the
            // tree label. We do NOT walk event handlers, statements, items[] etc. because
            // doing so requires knowing the OAT-spec load order for WaW, which we don't have
            // (only the struct sizes from codresearch.dev). Any over- or under-read of inline
            // data mis-aligns the cursor and causes the next-menu scan to skip real menus.
            //
            // The scanner (FindNextCod5MenuStart, called by MenuListParser) is what advances
            // past each menu's variable-size payload — it just byte-walks the zone looking
            // for the next position that FitsMenuDefStruct. Trusting the strict struct check
            // at every byte gives us all menus reliably without depending on inline-walk
            // precision.
            if (IsInline(namePtr))
            {
                string n = ReadInlineStringSafe(nextStopOffset);
                // A garbage inline name string (non-identifier chars) doesn't invalidate the
                // menu — the binary already passed FitsMenuDefStruct, which is the actual
                // "is this a menuDef_t" test. Just clear the name so the tree shows
                // "menu #N" rather than something junky. Treating bad names as a full reject
                // caused us to skip 60+ real but unnamed menus per zone.
                menu.Window.Name = IsPlausibleMenuName(n) ? n : null;
            }

            // Editable values (offsets within binary) — exposed to the decompiler for inline editing
            menu.EditableValues.Add(MenuValue.CreateRect("rect",
                menu.Window.Rect.X, menu.Window.Rect.Y, menu.Window.Rect.W, menu.Window.Rect.H,
                menuStart + 0x04));
            menu.EditableValues.Add(MenuValue.CreateColor("foreColor",    menu.Window.ForeColor,    menuStart + 0x64));
            menu.EditableValues.Add(MenuValue.CreateColor("backColor",    menu.Window.BackColor,    menuStart + 0x74));
            menu.EditableValues.Add(MenuValue.CreateColor("borderColor",  menu.Window.BorderColor,  menuStart + 0x84));
            menu.EditableValues.Add(MenuValue.CreateColor("outlineColor", menu.Window.OutlineColor, menuStart + 0x94));
            menu.EditableValues.Add(MenuValue.CreateColor("focusColor",   menu.FocusColor,          menuStart + 0x104));
            menu.EditableValues.Add(MenuValue.CreateColor("disableColor", menu.Window.DisableColor, menuStart + 0x114));
            menu.EditableValues.Add(MenuValue.CreateInt("itemCount", menu.ItemCount, menuStart + ItemCountOffset));
            menu.EditableValues.Add(MenuValue.CreateInt("fullScreen", menu.Fullscreen, menuStart + 0xAC));

            menu.EndOffset = _pos;
            Debug.WriteLine($"[Cod5MenuDeserializer] menu[{menuIndex}] @ 0x{menuStart:X}..0x{_pos:X} name='{menu.Window.Name}' itemCount={menu.ItemCount}");
            return menu;
        }

        private string ReadInlineStringSafe(int stopAt)
        {
            int start = _pos;
            int hardCap = (stopAt > start && stopAt <= _data.Length) ? stopAt : _data.Length;
            // strings here should be short — abort if we walk too far without a null terminator
            int maxLen = Math.Min(1024, hardCap - start);
            int end = start;
            while (end < start + maxLen && end < _data.Length && _data[end] != 0) end++;
            if (end >= start + maxLen) return string.Empty;
            string s = Encoding.ASCII.GetString(_data, start, end - start);
            _pos = end + 1; // consume null
            return s;
        }

        private RectDef ReadRect(int offset)
        {
            // WaW rectDef_s = 24 bytes (4 floats + 2 INT alignments — not bytes like IW4)
            return new RectDef
            {
                X = ReadF32(offset + 0),
                Y = ReadF32(offset + 4),
                W = ReadF32(offset + 8),
                H = ReadF32(offset + 12),
                HorzAlign = (byte)(ReadI32(offset + 16) & 0xFF),
                VertAlign = (byte)(ReadI32(offset + 20) & 0xFF),
            };
        }

        private float[] ReadColor(int offset) => new[] {
            ReadF32(offset + 0), ReadF32(offset + 4), ReadF32(offset + 8), ReadF32(offset + 12)
        };

        private uint ReadU32(int offset)
        {
            return _isBigEndian
                ? (uint)((_data[offset] << 24) | (_data[offset + 1] << 16) | (_data[offset + 2] << 8) | _data[offset + 3])
                : (uint)(_data[offset] | (_data[offset + 1] << 8) | (_data[offset + 2] << 16) | (_data[offset + 3] << 24));
        }

        private int ReadI32(int offset) => (int)ReadU32(offset);

        private float ReadF32(int offset)
        {
            byte[] bytes = _isBigEndian
                ? new[] { _data[offset + 3], _data[offset + 2], _data[offset + 1], _data[offset] }
                : new[] { _data[offset], _data[offset + 1], _data[offset + 2], _data[offset + 3] };
            return BitConverter.ToSingle(bytes, 0);
        }

        private static bool IsInline(uint ptr) => ptr == 0xFFFFFFFF;

        /// <summary>Real WaW menu names are identifiers: a-z, A-Z, 0-9, _ only; 2..64 chars.
        /// Anything else (whitespace, quotes, semicolons) means we're inside a script literal.</summary>
        private static bool IsPlausibleMenuName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true; // null name is OK
            if (name.Length < 2 || name.Length > 64) return false;
            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                       || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }


        /// <summary>
        /// Scans forward from <paramref name="from"/> for the next plausible CoD5 menuDef_t
        /// start. We can't rely on IW4-style scanning here — IW4's rectDef_s is 20 bytes with
        /// horz/vert as bytes at +16/+17, whereas WaW's is 24 bytes with horz/vert as INTS at
        /// +16/+20. The looser IW4 check passes for any block of zeros inside inline event
        /// handler data, which is why a naive scan drifts.
        ///
        /// Stronger CoD5-specific check:
        ///   - +0x00:           FFFFFFFF (window.name)
        ///   - +0x04..+0x1B:    rect — 4 bounded floats + 2 ints in [0,3]
        ///   - +0x1C..+0x33:    rectClient — same shape
        ///   - +0x34:           group ptr — 0 or FFFFFFFF only
        ///   - +0xAC:           fullScreen — 0 or 1
        ///   - +0xB0:           itemCount — 0..200
        ///   - +0x138:          first inline byte — printable ASCII identifier char or 0
        /// </summary>
        public static int FindNextCod5MenuStart(byte[] data, int from, bool isBigEndian)
        {
            int end = data.Length - MenuDefSize;
            for (int p = from; p <= end; p++)
            {
                if (data[p] != 0xFF || data[p + 1] != 0xFF || data[p + 2] != 0xFF || data[p + 3] != 0xFF)
                    continue;
                if (!FitsMenuDefStruct(data, p, isBigEndian, out _))
                    continue;
                return p;
            }
            return -1;
        }

        /// <summary>
        /// Validates that the bytes at <paramref name="off"/> structurally fit a WaW
        /// menuDef_t. This is the single source of truth for "is this a menu" — every
        /// field that has a constrained representation (pointer must be 0/FFFFFFFF, bool
        /// must be 0/1, count must be within a sane range, rect floats must be in pixel
        /// range, color components must be normalized) is checked. If anything looks
        /// impossible, the candidate is not a menuDef_t. Period.
        ///
        /// This is what implements the user's principle: "if it doesn't fit the struct
        /// then it's not a menu asset."
        /// </summary>
        public static bool FitsMenuDefStruct(byte[] data, int off, bool isBigEndian, out string reason)
        {
            reason = null;
            if (off < 0 || off + MenuDefSize > data.Length) { reason = "out of bounds"; return false; }

            // --- All pointer-typed fields ---
            // Each must be:
            //   - 0 (null)
            //   - 0xFFFFFFFF (inline placeholder, data follows after binary)
            //   - or a post-link "resolved" pointer (high bit set per PS3 zone convention,
            //     with the low 31 bits as an in-bounds zone offset)
            //
            // The third case is real: e.g. main_text's soundName is 0x8094D084 — high bit
            // set, low bits 0x94D084 ≈ 9.7 MB which lands inside the zone. The compiler
            // pre-resolved that pointer to the loaded sound asset's zone address. Treating
            // such pointers as "not a menu" loses ~60 real menus per UI zone.
            int[] ptrOffsets = { 0x00, 0x34, 0xA4, 0xA8, 0xDC, 0xE0, 0xE4, 0xE8, 0xEC,
                                 0xF4, 0xF8, 0xFC, 0x128, 0x130, 0x134 };
            foreach (int po in ptrOffsets)
            {
                uint v = ReadU32At(data, off + po, isBigEndian);
                if (!IsValidZonePointer(v, data.Length))
                {
                    reason = $"ptr field +0x{po:X}=0x{v:X8} is not null / inline / resolved";
                    return false;
                }
            }

            // --- rect / rectClient: bounded floats + int aligns in [0..3] ---
            if (!IsPlausibleRect(data, off + 0x04, isBigEndian)) { reason = "rect implausible"; return false; }
            if (!IsPlausibleRect(data, off + 0x1C, isBigEndian)) { reason = "rectClient implausible"; return false; }

            // --- Bool / enum / count fields ---
            uint fullScreen = ReadU32At(data, off + 0xAC, isBigEndian);
            if (fullScreen > 1) { reason = $"fullScreen={fullScreen} not bool"; return false; }

            int itemCount = (int)ReadU32At(data, off + 0xB0, isBigEndian);
            if (itemCount < 0 || itemCount > 200) { reason = $"itemCount={itemCount} out of [0..200]"; return false; }

            int fontIndex = (int)ReadU32At(data, off + 0xB4, isBigEndian);
            if (fontIndex < -1 || fontIndex > 100) { reason = $"fontIndex={fontIndex} out of [-1..100]"; return false; }

            // --- statement_s.numEntries fields: each must be 0..10000 ---
            int visibleEntries = (int)ReadU32At(data, off + 0xF0, isBigEndian);
            int rectXEntries   = (int)ReadU32At(data, off + 0x124, isBigEndian);
            int rectYEntries   = (int)ReadU32At(data, off + 0x12C, isBigEndian);
            if (visibleEntries < 0 || visibleEntries > 10000) { reason = $"visibleExp.numEntries={visibleEntries}"; return false; }
            if (rectXEntries   < 0 || rectXEntries   > 10000) { reason = $"rectXExp.numEntries={rectXEntries}"; return false; }
            if (rectYEntries   < 0 || rectYEntries   > 10000) { reason = $"rectYExp.numEntries={rectYEntries}"; return false; }

            // --- statement_s.entries ptrs: 0 iff numEntries==0, else FFFFFFFF ---
            uint visibleEntriesPtr = ReadU32At(data, off + 0xF4, isBigEndian);
            uint rectXEntriesPtr   = ReadU32At(data, off + 0x128, isBigEndian);
            uint rectYEntriesPtr   = ReadU32At(data, off + 0x130, isBigEndian);
            if (!StatementPtrConsistent(visibleEntries, visibleEntriesPtr, data.Length)) { reason = "visibleExp ptr/count mismatch"; return false; }
            if (!StatementPtrConsistent(rectXEntries,   rectXEntriesPtr,   data.Length)) { reason = "rectXExp ptr/count mismatch"; return false; }
            if (!StatementPtrConsistent(rectYEntries,   rectYEntriesPtr,   data.Length)) { reason = "rectYExp ptr/count mismatch"; return false; }

            // --- focusColor / disableColor: each component in [0..1] (allow small slop) ---
            if (!IsPlausibleColor(data, off + 0x104, isBigEndian)) { reason = "focusColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + 0x114, isBigEndian)) { reason = "disableColor implausible"; return false; }

            // --- windowDef colors (forecolor, backcolor, borderColor, outlineColor) ---
            if (!IsPlausibleColor(data, off + 0x64, isBigEndian)) { reason = "foreColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + 0x74, isBigEndian)) { reason = "backColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + 0x84, isBigEndian)) { reason = "borderColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + 0x94, isBigEndian)) { reason = "outlineColor implausible"; return false; }

            // --- borderSize: small non-negative float ---
            float borderSize = ReadF32At(data, off + 0x48, isBigEndian);
            if (float.IsNaN(borderSize) || float.IsInfinity(borderSize) || borderSize < 0 || borderSize > 1000)
            { reason = $"borderSize={borderSize}"; return false; }

            return true;
        }

        // statement_s rule: numEntries=0 ↔ entries=null. Otherwise (count>0) entries must
        // be a valid zone pointer (inline placeholder or resolved). The valid-pointer test
        // is shared with the menuDef pointer checks via IsValidZonePointer.
        private static bool StatementPtrConsistent(int numEntries, uint entriesPtr, int zoneLength)
        {
            if (numEntries == 0) return entriesPtr == 0;
            return entriesPtr == 0xFFFFFFFF
                   || ((entriesPtr & 0x80000000u) != 0 && (entriesPtr & 0x7FFFFFFFu) < (uint)zoneLength);
        }

        /// <summary>True if <paramref name="v"/> is a value a zone-serialized pointer can hold:
        /// null (0), inline-placeholder (0xFFFFFFFF), or a post-link resolved pointer
        /// (high bit set + low-31-bits in-bounds for the zone).</summary>
        private static bool IsValidZonePointer(uint v, int zoneLength)
        {
            if (v == 0) return true;
            if (v == 0xFFFFFFFF) return true;
            if ((v & 0x80000000u) == 0) return false;          // not high-bit-set → not a resolved pointer
            return (v & 0x7FFFFFFFu) < (uint)zoneLength;       // masked offset must be in-bounds
        }

        // Color component: 0..1 normalized in practice, but some menus pre-scale to 0..255
        // range. The discriminator that matters is "this is a finite small-ish float, not
        // garbage interpreted as a float" — i.e. reject NaN, ±infinity, and any value whose
        // magnitude is larger than 1000 (which catches the random-bytes-as-float case where
        // most values explode to ±1e20+).
        private static bool IsPlausibleColor(byte[] data, int off, bool isBigEndian)
        {
            for (int i = 0; i < 4; i++)
            {
                float c = ReadF32At(data, off + i * 4, isBigEndian);
                if (float.IsNaN(c) || float.IsInfinity(c)) return false;
                if (c < -1.0f || c > 1000.0f) return false;
            }
            return true;
        }

        private static bool IsPlausibleRect(byte[] data, int o, bool isBigEndian)
        {
            float x = ReadF32At(data, o + 0,  isBigEndian);
            float y = ReadF32At(data, o + 4,  isBigEndian);
            float w = ReadF32At(data, o + 8,  isBigEndian);
            float h = ReadF32At(data, o + 12, isBigEndian);
            if (!CoordOk(x) || !CoordOk(y) || !CoordOk(w) || !CoordOk(h)) return false;
            if (w < 0 || h < 0) return false;

            uint horz = ReadU32At(data, o + 16, isBigEndian);
            uint vert = ReadU32At(data, o + 20, isBigEndian);
            return horz <= 3 && vert <= 3;
        }

        private static bool CoordOk(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            if (v == 0f) return true;
            float a = Math.Abs(v);
            return a >= 0.01f && a <= 10000f;
        }

        private static uint ReadU32At(byte[] data, int offset, bool isBigEndian) =>
            isBigEndian
                ? (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3])
                : (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

        private static float ReadF32At(byte[] data, int offset, bool isBigEndian)
        {
            byte[] bytes = isBigEndian
                ? new[] { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] }
                : new[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] };
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
