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
        // ===== Per-platform struct sizes =====
        // PC differs from console (PS3/Xbox 360) in two places:
        //   - windowDef_t.dynamicFlags[N] is [1] on PC, [4] on console (saves 12 bytes)
        //   - menuDef_t.cursorItem[N]      is [1] on PC, [4] on console (saves 12 bytes)
        // rectDef_s stays 24 bytes (4 floats + 2 ints) on both. So:
        //   PC windowDef = 168 - 12 = 156 (0x9C),  console = 168 (0xA8)
        //   PC menuDef   = 312 - 24 = 288 (0x120), console = 312 (0x138)
        // Verified empirically against retail ui.ff zones from both PS3 and PC builds.
        private const int ConsoleWindowDefSize = 0xA8;
        private const int ConsoleMenuDefSize   = 0x138;
        private const int PcWindowDefSize      = 0x9C;
        private const int PcMenuDefSize        = 0x120;

        private readonly byte[] _data;
        private readonly bool _isBigEndian;
        private readonly bool _isPC;
        private int _pos;

        public int WindowDefSize => _isPC ? PcWindowDefSize : ConsoleWindowDefSize;
        public int MenuDefSize   => _isPC ? PcMenuDefSize   : ConsoleMenuDefSize;

        public Cod5MenuDeserializer(byte[] zoneData, int startOffset, bool isBigEndian, bool isPC = false)
        {
            _data = zoneData;
            _pos = startOffset;
            _isBigEndian = isBigEndian;
            _isPC = isPC;
        }

        /// <summary>
        /// Per-platform field offsets within a WaW menuDef_t binary. Offsets up through
        /// staticFlags (0x4C) are identical; after that PC saves 12 bytes for dynamicFlags
        /// and another 12 for cursorItem, so all later fields are -24 from console.
        /// </summary>
        private readonly struct Offsets
        {
            // windowDef_t (only fields whose offset differs from console)
            public int NextTime { get; }
            public int ForeColor { get; }
            public int BackColor { get; }
            public int BorderColor { get; }
            public int OutlineColor { get; }
            // menuDef_t-specific
            public int Font { get; }
            public int FullScreen { get; }
            public int ItemCount { get; }
            public int FontIndex { get; }
            public int CursorItem { get; }
            public int FadeCycle { get; }
            public int FadeClamp { get; }
            public int FadeAmount { get; }
            public int FadeInAmount { get; }
            public int BlurRadius { get; }
            public int OnOpen { get; }
            public int OnFocus { get; }
            public int OnClose { get; }
            public int OnESC { get; }
            public int OnKey { get; }
            public int VisibleExpCount { get; }
            public int VisibleExpEntries { get; }
            public int AllowedBinding { get; }
            public int SoundName { get; }
            public int ImageTrack { get; }
            public int FocusColor { get; }
            public int DisableColor { get; }
            public int RectXExpCount { get; }
            public int RectXExpEntries { get; }
            public int RectYExpCount { get; }
            public int RectYExpEntries { get; }
            public int Items { get; }
            // Validation-only views
            public int Background { get; }      // windowDef.background ptr
            public int[] PointerFields { get; } // all 15 pointer slots for FitsMenuDefStruct

            public Offsets(bool isPC)
            {
                int s = isPC ? -12 : 0;   // shift after dynamicFlags ends (PC vs console)
                int s2 = isPC ? -24 : 0;  // shift after cursorItem ends
                NextTime     = 0x60 + s;
                ForeColor    = 0x64 + s;
                BackColor    = 0x74 + s;
                BorderColor  = 0x84 + s;
                OutlineColor = 0x94 + s;
                Background   = 0xA4 + s;
                Font         = 0xA8 + s;
                FullScreen   = 0xAC + s;
                ItemCount    = 0xB0 + s;
                FontIndex    = 0xB4 + s;
                CursorItem   = 0xB8 + s;
                FadeCycle         = 0xC8 + s2;
                FadeClamp         = 0xCC + s2;
                FadeAmount        = 0xD0 + s2;
                FadeInAmount      = 0xD4 + s2;
                BlurRadius        = 0xD8 + s2;
                OnOpen            = 0xDC + s2;
                OnFocus           = 0xE0 + s2;
                OnClose           = 0xE4 + s2;
                OnESC             = 0xE8 + s2;
                OnKey             = 0xEC + s2;
                VisibleExpCount   = 0xF0 + s2;
                VisibleExpEntries = 0xF4 + s2;
                AllowedBinding    = 0xF8 + s2;
                SoundName         = 0xFC + s2;
                ImageTrack        = 0x100 + s2;
                FocusColor        = 0x104 + s2;
                DisableColor      = 0x114 + s2;
                RectXExpCount     = 0x124 + s2;
                RectXExpEntries   = 0x128 + s2;
                RectYExpCount     = 0x12C + s2;
                RectYExpEntries   = 0x130 + s2;
                Items             = 0x134 + s2;
                PointerFields = new[] {
                    0x00, 0x34, Background, Font,
                    OnOpen, OnFocus, OnClose, OnESC, OnKey,
                    VisibleExpEntries, AllowedBinding, SoundName,
                    RectXExpEntries, RectYExpEntries, Items,
                };
            }
        }

        private static Offsets GetOffsets(bool isPC) => new Offsets(isPC);
        private static int MenuDefSizeFor(bool isPC) => isPC ? PcMenuDefSize : ConsoleMenuDefSize;

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
            if (!FitsMenuDefStruct(_data, menuStart, _isBigEndian, _isPC, out string failReason))
            {
                Debug.WriteLine($"[Cod5MenuDeserializer] menu[{menuIndex}] @ 0x{menuStart:X}: not a menuDef_t — {failReason}");
                return null;
            }

            uint namePtr = ReadU32(menuStart);
            var menu = new MenuDef { StartOffset = menuStart, Window = new WindowDef() };
            var o = GetOffsets(_isPC);

            // === windowDef_t ===
            // Fields that are at the same offset on both platforms (everything up through staticFlags).
            menu.Window.Rect       = ReadRect(menuStart + 0x04);
            menu.Window.RectClient = ReadRect(menuStart + 0x1C);
            menu.Window.Style          = ReadI32(menuStart + 0x38);
            menu.Window.Border         = ReadI32(menuStart + 0x3C);
            menu.Window.OwnerDraw      = ReadI32(menuStart + 0x40);
            menu.Window.OwnerDrawFlags = ReadI32(menuStart + 0x44);
            menu.Window.BorderSize     = ReadF32(menuStart + 0x48);
            menu.Window.StaticFlags    = ReadI32(menuStart + 0x4C);
            // dynamicFlags is [1] on PC, [4] on console — pad the array on PC for consistency.
            menu.Window.DynamicFlags   = _isPC
                ? new[] { ReadI32(menuStart + 0x50), 0, 0, 0 }
                : new[] { ReadI32(menuStart + 0x50), ReadI32(menuStart + 0x54),
                          ReadI32(menuStart + 0x58), ReadI32(menuStart + 0x5C) };
            menu.Window.NextTime       = ReadI32(menuStart + o.NextTime);
            menu.Window.ForeColor      = ReadColor(menuStart + o.ForeColor);
            menu.Window.BackColor      = ReadColor(menuStart + o.BackColor);
            menu.Window.BorderColor    = ReadColor(menuStart + o.BorderColor);
            menu.Window.OutlineColor   = ReadColor(menuStart + o.OutlineColor);

            // === menuDef_t-specific fields ===
            menu.Fullscreen   = ReadI32(menuStart + o.FullScreen);
            menu.ItemCount    = ReadI32(menuStart + o.ItemCount);
            menu.FontIndex    = ReadI32(menuStart + o.FontIndex);
            menu.CursorItems  = _isPC
                ? new[] { ReadI32(menuStart + o.CursorItem), 0, 0, 0 }
                : new[] { ReadI32(menuStart + o.CursorItem),     ReadI32(menuStart + o.CursorItem + 4),
                          ReadI32(menuStart + o.CursorItem + 8), ReadI32(menuStart + o.CursorItem + 12) };
            menu.FadeCycle    = ReadI32(menuStart + o.FadeCycle);
            menu.FadeClamp    = ReadF32(menuStart + o.FadeClamp);
            menu.FadeAmount   = ReadF32(menuStart + o.FadeAmount);
            menu.FadeInAmount = ReadF32(menuStart + o.FadeInAmount);
            menu.BlurRadius   = ReadF32(menuStart + o.BlurRadius);
            uint onOpenPtr           = ReadU32(menuStart + o.OnOpen);
            uint onFocusPtr          = ReadU32(menuStart + o.OnFocus);
            uint onClosePtr          = ReadU32(menuStart + o.OnClose);
            uint onESCPtr            = ReadU32(menuStart + o.OnESC);
            uint onKeyPtr            = ReadU32(menuStart + o.OnKey);
            uint allowedBindingPtr   = ReadU32(menuStart + o.AllowedBinding);
            uint soundNamePtr        = ReadU32(menuStart + o.SoundName);
            menu.ImageTrack          = ReadI32(menuStart + o.ImageTrack);
            menu.FocusColor          = ReadColor(menuStart + o.FocusColor);
            menu.Window.DisableColor = ReadColor(menuStart + o.DisableColor);
            uint itemsPtr            = ReadU32(menuStart + o.Items);

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
            menu.EditableValues.Add(MenuValue.CreateColor("foreColor",    menu.Window.ForeColor,    menuStart + o.ForeColor));
            menu.EditableValues.Add(MenuValue.CreateColor("backColor",    menu.Window.BackColor,    menuStart + o.BackColor));
            menu.EditableValues.Add(MenuValue.CreateColor("borderColor",  menu.Window.BorderColor,  menuStart + o.BorderColor));
            menu.EditableValues.Add(MenuValue.CreateColor("outlineColor", menu.Window.OutlineColor, menuStart + o.OutlineColor));
            menu.EditableValues.Add(MenuValue.CreateColor("focusColor",   menu.FocusColor,          menuStart + o.FocusColor));
            menu.EditableValues.Add(MenuValue.CreateColor("disableColor", menu.Window.DisableColor, menuStart + o.DisableColor));
            menu.EditableValues.Add(MenuValue.CreateInt("itemCount", menu.ItemCount, menuStart + o.ItemCount));
            menu.EditableValues.Add(MenuValue.CreateInt("fullScreen", menu.Fullscreen, menuStart + o.FullScreen));

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
        /// start. Uses the strict struct-fit check that knows the platform-specific layout
        /// (PC menuDef is 288 bytes / windowDef 156, console is 312 / 168). The IW4 looser
        /// scanner can't be used — its rectDef_s is 20 bytes with byte aligns, whereas
        /// WaW's is 24 bytes with int aligns.
        /// </summary>
        public static int FindNextCod5MenuStart(byte[] data, int from, bool isBigEndian, bool isPC = false)
        {
            int end = data.Length - MenuDefSizeFor(isPC);
            for (int p = from; p <= end; p++)
            {
                if (data[p] != 0xFF || data[p + 1] != 0xFF || data[p + 2] != 0xFF || data[p + 3] != 0xFF)
                    continue;
                if (!FitsMenuDefStruct(data, p, isBigEndian, isPC, out _))
                    continue;
                return p;
            }
            return -1;
        }

        /// <summary>
        /// Validates that the bytes at <paramref name="off"/> structurally fit a WaW
        /// menuDef_t. This is the single source of truth for "is this a menu" — every
        /// field that has a constrained representation (pointer must be 0/FFFFFFFF/resolved,
        /// bool must be 0/1, count must be within a sane range, rect floats must be in pixel
        /// range, color components must be in normalized-or-pre-scaled range) is checked.
        /// If anything looks impossible, the candidate is not a menuDef_t.
        ///
        /// Implements the principle: "if it doesn't fit the struct then it's not a menu asset."
        /// </summary>
        public static bool FitsMenuDefStruct(byte[] data, int off, bool isBigEndian, bool isPC, out string reason)
        {
            reason = null;
            int size = MenuDefSizeFor(isPC);
            if (off < 0 || off + size > data.Length) { reason = "out of bounds"; return false; }

            var o = GetOffsets(isPC);

            // --- All pointer-typed fields ---
            // Each must be:
            //   - 0 (null)
            //   - 0xFFFFFFFF (inline placeholder, data follows after binary)
            //   - or a post-link "resolved" pointer (high bit set, low 31 bits are an
            //     in-bounds zone offset). E.g. main_text's soundName is 0x8094D084 — high
            //     bit set, low bits 0x94D084 (9.7 MB) lands inside the zone.
            foreach (int po in o.PointerFields)
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
            uint fullScreen = ReadU32At(data, off + o.FullScreen, isBigEndian);
            if (fullScreen > 1) { reason = $"fullScreen={fullScreen} not bool"; return false; }

            int itemCount = (int)ReadU32At(data, off + o.ItemCount, isBigEndian);
            if (itemCount < 0 || itemCount > 200) { reason = $"itemCount={itemCount} out of [0..200]"; return false; }

            int fontIndex = (int)ReadU32At(data, off + o.FontIndex, isBigEndian);
            if (fontIndex < -1 || fontIndex > 100) { reason = $"fontIndex={fontIndex} out of [-1..100]"; return false; }

            // --- statement_s.numEntries fields: each must be 0..10000 ---
            int visibleEntries = (int)ReadU32At(data, off + o.VisibleExpCount, isBigEndian);
            int rectXEntries   = (int)ReadU32At(data, off + o.RectXExpCount,   isBigEndian);
            int rectYEntries   = (int)ReadU32At(data, off + o.RectYExpCount,   isBigEndian);
            if (visibleEntries < 0 || visibleEntries > 10000) { reason = $"visibleExp.numEntries={visibleEntries}"; return false; }
            if (rectXEntries   < 0 || rectXEntries   > 10000) { reason = $"rectXExp.numEntries={rectXEntries}"; return false; }
            if (rectYEntries   < 0 || rectYEntries   > 10000) { reason = $"rectYExp.numEntries={rectYEntries}"; return false; }

            // --- statement_s.entries ptrs: 0 iff numEntries==0, else FFFFFFFF/resolved ---
            uint visibleEntriesPtr = ReadU32At(data, off + o.VisibleExpEntries, isBigEndian);
            uint rectXEntriesPtr   = ReadU32At(data, off + o.RectXExpEntries,   isBigEndian);
            uint rectYEntriesPtr   = ReadU32At(data, off + o.RectYExpEntries,   isBigEndian);
            if (!StatementPtrConsistent(visibleEntries, visibleEntriesPtr, data.Length)) { reason = "visibleExp ptr/count mismatch"; return false; }
            if (!StatementPtrConsistent(rectXEntries,   rectXEntriesPtr,   data.Length)) { reason = "rectXExp ptr/count mismatch"; return false; }
            if (!StatementPtrConsistent(rectYEntries,   rectYEntriesPtr,   data.Length)) { reason = "rectYExp ptr/count mismatch"; return false; }

            // --- colors (each component in [-1, 1000] — wide enough to allow pre-scaled colors) ---
            if (!IsPlausibleColor(data, off + o.FocusColor,    isBigEndian)) { reason = "focusColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + o.DisableColor,  isBigEndian)) { reason = "disableColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + o.ForeColor,     isBigEndian)) { reason = "foreColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + o.BackColor,     isBigEndian)) { reason = "backColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + o.BorderColor,   isBigEndian)) { reason = "borderColor implausible"; return false; }
            if (!IsPlausibleColor(data, off + o.OutlineColor,  isBigEndian)) { reason = "outlineColor implausible"; return false; }

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
        /// (high bit set + low-31-bits in-bounds for the zone).
        ///
        /// NOTE: this is a WaW (T5) menu-scanning *validation heuristic*, NOT the real IW4
        /// pointer fixup. The IW4/MW2 decode is block index = top nibble (v &gt;&gt; 28),
        /// offset = low 28 bits (v &amp; 0x0FFFFFFF) resolved against a per-block base — see
        /// FastFileLib.ZonePointer / ZoneBlockLayout (ported from Jacob Schroeder's FastFile,
        /// https://github.com/jacob-schroeder/FastFile). The 0x7FFFFFFF mask below is deliberately
        /// loose (it only needs to accept/reject candidates while byte-scanning for menuDefs);
        /// do NOT reuse it to actually dereference IW4 pointers, and whether WaW genuinely uses
        /// a different encoding than IW4 is still unverified against a WaW sample.</summary>
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
