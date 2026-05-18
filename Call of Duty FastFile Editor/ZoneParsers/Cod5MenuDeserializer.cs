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
            // Order (empirically validated against ui.ff menu #0 from WaW PS3):
            //   1. window.name (if FFFFFFFF) — null-terminated string
            //   2. window.group (if FFFFFFFF)
            //   3. font (if FFFFFFFF)
            //   4..N. event handler scripts (onOpen/onFocus/onClose/onESC) — also null-terminated
            //         strings in the simple "uiScript ..." case (we don't walk MenuEventHandlerSet
            //         binaries here since WaW's spec for that isn't in the docs we have).
            //   N+1. allowedBinding, soundName (if FFFFFFFF) — strings
            //
            // Anything past simple-string consumption that we can't recognize, we abandon —
            // the binary cursor falls back to a signature scan to find the next menu.
            //
            // We don't recursively walk items[] because that requires an authoritative WaW
            // itemDef_s layout (and event handler binary format) we don't have spec for yet.
            // Instead we cap our forward scan at `nextStopOffset` if the caller provides one
            // (the next plausible windowDef signature) so we don't bleed into adjacent menus.

            if (IsInline(namePtr))
            {
                menu.Window.Name = ReadInlineStringSafe(nextStopOffset);
                // If the inline name isn't a real identifier (no special chars allowed),
                // this candidate is almost certainly inside a script literal — bail.
                if (!IsPlausibleMenuName(menu.Window.Name))
                {
                    Debug.WriteLine($"[Cod5MenuDeserializer] menu[{menuIndex}] @ 0x{menuStart:X}: rejected garbage name '{menu.Window.Name}'");
                    return null;
                }
            }
            else
            {
                // window.name is null. To give the user something better than "menu #N" in
                // the tree, look for a representative @-prefixed localization key in the
                // inline payload — these are item labels like "@MENU_MAIN_MENU" that
                // typically identify the menu's purpose in screenshots. Bounded scan up to
                // nextStopOffset (or 4 KB if no boundary known).
                menu.Window.Name = FindRepresentativeLabel(menuStart + MenuDefSize, nextStopOffset);
            }
            // Track plausible additional inline strings up to nextStopOffset. We don't bind these
            // to specific menuDef fields beyond name — they're presented to the decompiler via
            // EditableValues below if non-trivial.
            ConsumeInlineStringIfFlag(menu, "group",          ReadU32(menuStart + 0x34), nextStopOffset);
            ConsumeInlineStringIfFlag(menu, "font",           ReadU32(menuStart + 0xA8), nextStopOffset);
            ConsumeInlineStringIfFlag(menu, "onOpen",         onOpenPtr,         nextStopOffset);
            ConsumeInlineStringIfFlag(menu, "onFocus",        onFocusPtr,        nextStopOffset);
            ConsumeInlineStringIfFlag(menu, "onClose",        onClosePtr,        nextStopOffset);
            ConsumeInlineStringIfFlag(menu, "onESC",          onESCPtr,          nextStopOffset);
            ConsumeInlineStringIfFlag(menu, "allowedBinding", allowedBindingPtr, nextStopOffset);
            ConsumeInlineStringIfFlag(menu, "soundName",      soundNamePtr,      nextStopOffset);

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

        private void ConsumeInlineStringIfFlag(MenuDef menu, string fieldName, uint ptr, int stopAt)
        {
            if (!IsInline(ptr)) return;
            string s = ReadInlineStringSafe(stopAt);
            if (string.IsNullOrEmpty(s)) return;
            // We don't have dedicated fields on MenuDef for most of these — surface them via
            // EditableValues so the decompiler can show + edit them.
            menu.EditableValues.Add(MenuValue.CreateString(fieldName, s, _pos - s.Length - 1, s.Length));
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
        /// When a menu's window.name is null, scan the inline payload for an @-prefixed
        /// localization key (e.g. @MENU_MAIN_MENU). These are item labels that typically
        /// identify what a menu actually shows on screen — much more useful in a tree view
        /// than "menu #N". Returns null if no such label is found within the search bound.
        /// </summary>
        private string FindRepresentativeLabel(int searchStart, int stopAt)
        {
            int hardCap = stopAt > searchStart ? Math.Min(stopAt, _data.Length) : Math.Min(searchStart + 4096, _data.Length);
            for (int p = searchStart; p < hardCap - 4; p++)
            {
                if (_data[p] != (byte)'@') continue;
                // Read identifier following @ — must be SCREAMING_SNAKE_CASE-ish
                int end = p + 1;
                while (end < hardCap && end < _data.Length)
                {
                    byte b = _data[end];
                    bool ok = (b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9') || b == '_';
                    if (!ok) break;
                    end++;
                }
                int len = end - (p + 1);
                if (len >= 4 && len <= 64 && end < _data.Length && _data[end] == 0)
                {
                    return Encoding.ASCII.GetString(_data, p, end - p); // include the @
                }
            }
            return null;
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

            // --- All pointer-typed fields. Each must be 0 (null) or 0xFFFFFFFF (inline). ---
            // (name, group, background, font, onOpen, onFocus, onClose, onESC, onKey,
            //  visibleExp.entries, allowedBinding, soundName, rectXExp.entries,
            //  rectYExp.entries, items)
            int[] ptrOffsets = { 0x00, 0x34, 0xA4, 0xA8, 0xDC, 0xE0, 0xE4, 0xE8, 0xEC,
                                 0xF4, 0xF8, 0xFC, 0x128, 0x130, 0x134 };
            foreach (int po in ptrOffsets)
            {
                uint v = ReadU32At(data, off + po, isBigEndian);
                if (v != 0 && v != 0xFFFFFFFF) { reason = $"ptr field +0x{po:X}=0x{v:X8} is neither null nor inline"; return false; }
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

            // --- statement_s.numEntries fields: each must be 0..1000 ---
            int visibleEntries = (int)ReadU32At(data, off + 0xF0, isBigEndian);
            int rectXEntries   = (int)ReadU32At(data, off + 0x124, isBigEndian);
            int rectYEntries   = (int)ReadU32At(data, off + 0x12C, isBigEndian);
            if (visibleEntries < 0 || visibleEntries > 1000) { reason = $"visibleExp.numEntries={visibleEntries}"; return false; }
            if (rectXEntries   < 0 || rectXEntries   > 1000) { reason = $"rectXExp.numEntries={rectXEntries}"; return false; }
            if (rectYEntries   < 0 || rectYEntries   > 1000) { reason = $"rectYExp.numEntries={rectYEntries}"; return false; }

            // --- statement_s.entries ptrs: 0 iff numEntries==0, else FFFFFFFF ---
            uint visibleEntriesPtr = ReadU32At(data, off + 0xF4, isBigEndian);
            uint rectXEntriesPtr   = ReadU32At(data, off + 0x128, isBigEndian);
            uint rectYEntriesPtr   = ReadU32At(data, off + 0x130, isBigEndian);
            if (!StatementPtrConsistent(visibleEntries, visibleEntriesPtr)) { reason = "visibleExp ptr/count mismatch"; return false; }
            if (!StatementPtrConsistent(rectXEntries,   rectXEntriesPtr))   { reason = "rectXExp ptr/count mismatch"; return false; }
            if (!StatementPtrConsistent(rectYEntries,   rectYEntriesPtr))   { reason = "rectYExp ptr/count mismatch"; return false; }

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

        // statement_s rule: numEntries=0 ↔ entries=null. Otherwise (count>0) entries must be FFFFFFFF (inline).
        private static bool StatementPtrConsistent(int numEntries, uint entriesPtr)
        {
            if (numEntries == 0) return entriesPtr == 0;
            return entriesPtr == 0xFFFFFFFF;
        }

        // Color component: 0..1 normalized (allow a tiny negative for fade effects, allow up to ~10 for HDR).
        private static bool IsPlausibleColor(byte[] data, int off, bool isBigEndian)
        {
            for (int i = 0; i < 4; i++)
            {
                float c = ReadF32At(data, off + i * 4, isBigEndian);
                if (float.IsNaN(c) || float.IsInfinity(c)) return false;
                if (c < -0.01f || c > 10.0f) return false;
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
