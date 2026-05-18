using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Field-by-field deserializer for MW2 (IW4) menuDef_t binaries, ported from OpenAssetTools'
    /// generated content loader (src/ZoneLoading/Game/IW4 + src/ZoneCode/Game/IW4/XAssets/menuDef_t.txt).
    ///
    /// Why this exists: signature-scanning for windowDef_t headers can't distinguish menus from
    /// their inline itemDef_s items (both start with the same windowDef_t). Faithful field-by-field
    /// walking is the only reliable way to compute each menu's exact end offset and to recover
    /// real window.name strings from inline data.
    ///
    /// Per-platform layout differences:
    /// - PC: dynamicFlags[1], cursorItem[1], cursorPos[1], textRect[1], menuTransition[1] (per slot),
    ///       has openSoundExp + closeSoundExp pointers
    /// - Console (Xbox 360 / PS3): dynamicFlags[4], cursorItem[4], cursorPos[4], textRect[4],
    ///       menuTransition[4] (per slot), no openSoundExp / closeSoundExp
    /// </summary>
    public class Iw4MenuDeserializer
    {
        private readonly ZoneStreamReader _r;
        private readonly bool _isConsole;
        private readonly bool _isBigEndian;

        // EventType enum (1-byte field at end of MenuEventHandler binary, 4-byte aligned)
        private const byte EVENT_UNCONDITIONAL = 0x0;
        private const byte EVENT_IF = 0x1;
        private const byte EVENT_ELSE = 0x2;
        private const byte EVENT_SET_LOCAL_VAR_BOOL = 0x3;
        private const byte EVENT_SET_LOCAL_VAR_INT = 0x4;
        private const byte EVENT_SET_LOCAL_VAR_FLOAT = 0x5;
        private const byte EVENT_SET_LOCAL_VAR_STRING = 0x6;

        // expDataType enum (Operand.dataType)
        private const int VAL_INT = 0x0;
        private const int VAL_FLOAT = 0x1;
        private const int VAL_STRING = 0x2;
        private const int VAL_FUNCTION = 0x3;

        // expressionEntryType enum
        private const int EET_OPERATOR = 0x0;
        private const int EET_OPERAND = 0x1;

        // itemDef_s.type values that switch the typeData union variant
        private const int ITEM_TYPE_TEXT = 0x0;
        private const int ITEM_TYPE_EDITFIELD = 0x4;
        private const int ITEM_TYPE_LISTBOX = 0x6;
        private const int ITEM_TYPE_NUMERICFIELD = 0x9;
        private const int ITEM_TYPE_SLIDER = 0xA;
        private const int ITEM_TYPE_YESNO = 0xB;
        private const int ITEM_TYPE_MULTI = 0xC;
        private const int ITEM_TYPE_DVARENUM = 0xD;
        private const int ITEM_TYPE_BIND = 0xE;
        private const int ITEM_TYPE_VALIDFILEFIELD = 0x10;
        private const int ITEM_TYPE_DECIMALFIELD = 0x11;
        private const int ITEM_TYPE_UPREDITFIELD = 0x12;
        private const int ITEM_TYPE_NEWS_TICKER = 0x14;
        private const int ITEM_TYPE_TEXT_SCROLL = 0x15;
        private const int ITEM_TYPE_EMAILFIELD = 0x16;
        private const int ITEM_TYPE_PASSWORDFIELD = 0x17;

        public Iw4MenuDeserializer(byte[] zoneData, int startOffset, bool isConsole, bool isBigEndian)
        {
            _r = new ZoneStreamReader(zoneData, startOffset, isBigEndian);
            _isConsole = isConsole;
            _isBigEndian = isBigEndian;
        }

        public int Position => _r.Position;

        /// <summary>
        /// Reads a single menuDef_t at the current stream position and returns a populated MenuDef.
        /// Advances the stream past all of the menu's binary + inline data. Returns null if the
        /// binary doesn't look like a menuDef_t (e.g. windowDef_t.name pointer isn't FFFFFFFF or 0).
        /// </summary>
        public MenuDef ReadMenuDef(int menuIndex)
        {
            int menuStart = _r.Position;

            try
            {
                var menu = new MenuDef { StartOffset = menuStart, Window = new WindowDef() };

                // === menuDef_t binary struct ===
                // window
                uint windowNamePtr = ReadWindowDefBinary(menu);

                // Validate this is plausibly a menuDef_t start: window.name must be inline or null
                if (windowNamePtr != 0xFFFFFFFF && windowNamePtr != 0)
                {
                    Debug.WriteLine($"[Iw4MenuDeserializer] menu[{menuIndex}] @ 0x{menuStart:X}: window.name not FFFFFFFF or 0 (got 0x{windowNamePtr:X8}), not a menuDef_t");
                    _r.Position = menuStart;
                    return null;
                }

                uint windowGroupPtr = _r.PeekWindowGroupPointerFromStart(menuStart, _isConsole);
                uint fontPtr = _r.ReadUInt32();
                menu.Fullscreen = _r.ReadInt32();
                menu.ItemCount = _r.ReadInt32();
                menu.FontIndex = _r.ReadInt32();
                _r.Skip(4 * CursorItemCount); // cursorItem[N]
                menu.FadeCycle = _r.ReadInt32();
                menu.FadeClamp = _r.ReadFloat();
                menu.FadeAmount = _r.ReadFloat();
                menu.FadeInAmount = _r.ReadFloat();
                menu.BlurRadius = _r.ReadFloat();
                uint onOpenPtr = _r.ReadUInt32();
                uint onCloseRequestPtr = _r.ReadUInt32();
                uint onClosePtr = _r.ReadUInt32();
                uint onESCPtr = _r.ReadUInt32();
                uint onKeyPtr = _r.ReadUInt32();
                uint visibleExpPtr = _r.ReadUInt32();
                uint allowedBindingPtr = _r.ReadUInt32();
                uint soundNamePtr = _r.ReadUInt32();
                menu.ImageTrack = _r.ReadInt32();
                menu.FocusColor = ReadColor();
                uint rectXExpPtr = _r.ReadUInt32();
                uint rectYExpPtr = _r.ReadUInt32();
                uint rectWExpPtr = _r.ReadUInt32();
                uint rectHExpPtr = _r.ReadUInt32();
                uint openSoundExpPtr = 0, closeSoundExpPtr = 0;
                if (!_isConsole)
                {
                    openSoundExpPtr = _r.ReadUInt32();
                    closeSoundExpPtr = _r.ReadUInt32();
                }
                uint itemsPtr = _r.ReadUInt32();
                _r.Skip(MenuTransitionStructSize * TransitionElementCount); // scaleTransition
                _r.Skip(MenuTransitionStructSize * TransitionElementCount); // alphaTransition
                _r.Skip(MenuTransitionStructSize * TransitionElementCount); // xTransition
                _r.Skip(MenuTransitionStructSize * TransitionElementCount); // yTransition
                uint expressionDataPtr = _r.ReadUInt32();

                int binaryEnd = _r.Position;
                Debug.WriteLine($"[Iw4MenuDeserializer] menu[{menuIndex}] binary @ 0x{menuStart:X}..0x{binaryEnd:X} ({binaryEnd - menuStart} bytes), itemCount={menu.ItemCount}");

                // === Inline data (reorder per OAT spec): expressionData, window, font, onOpen,
                //     onClose, onCloseRequest, onESC, onKey, visibleExp, allowedBinding, soundName,
                //     rectXExp, rectYExp, rectWExp, rectHExp, openSoundExp, closeSoundExp, items
                // ===
                if (IsInline(expressionDataPtr)) LoadExpressionSupportingData();
                if (IsInline(windowNamePtr))
                {
                    menu.Window.Name = _r.ReadInlineString();
                }
                if (IsInline(windowGroupPtr))
                {
                    menu.Window.Group = _r.ReadInlineString();
                }
                if (IsInline(fontPtr)) _r.ReadInlineString(); // font name string (not stored)
                if (IsInline(onOpenPtr)) LoadMenuEventHandlerSet();
                if (IsInline(onClosePtr)) LoadMenuEventHandlerSet();
                if (IsInline(onCloseRequestPtr)) LoadMenuEventHandlerSet();
                if (IsInline(onESCPtr)) LoadMenuEventHandlerSet();
                if (IsInline(onKeyPtr)) LoadItemKeyHandlerList();
                if (IsInline(visibleExpPtr)) LoadStatement();
                if (IsInline(allowedBindingPtr)) _r.ReadInlineString();
                if (IsInline(soundNamePtr)) _r.ReadInlineString();
                if (IsInline(rectXExpPtr)) LoadStatement();
                if (IsInline(rectYExpPtr)) LoadStatement();
                if (IsInline(rectWExpPtr)) LoadStatement();
                if (IsInline(rectHExpPtr)) LoadStatement();
                if (!_isConsole)
                {
                    if (IsInline(openSoundExpPtr)) LoadStatement();
                    if (IsInline(closeSoundExpPtr)) LoadStatement();
                }
                if (IsInline(itemsPtr)) LoadItemsArray(menu.ItemCount);

                menu.EndOffset = _r.Position;
                Debug.WriteLine($"[Iw4MenuDeserializer] menu[{menuIndex}] end @ 0x{menu.EndOffset:X} (inline data {menu.EndOffset - binaryEnd} bytes)");
                return menu;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Iw4MenuDeserializer] menu[{menuIndex}] @ 0x{menuStart:X} failed: {ex.Message}");
                _r.Position = menuStart;
                return null;
            }
        }

        // ===== Sizes that depend on platform =====
        private int WindowDefBinarySize => _isConsole ? 0xB0 : 0xA4;
        private int CursorItemCount => _isConsole ? 4 : 1;
        private int TransitionElementCount => _isConsole ? 4 : 1;
        private const int MenuTransitionStructSize = 28; // 4 ints + 3 floats

        /// <summary>True for FFFFFFFF (inline data follows). Zero means null.</summary>
        private static bool IsInline(uint ptr) => ptr == 0xFFFFFFFF;

        // ===== windowDef_t binary read =====

        /// <summary>
        /// Reads the windowDef_t binary into menu.Window. Returns the name pointer value so the
        /// caller can determine whether to load the inline name string later.
        /// </summary>
        private uint ReadWindowDefBinary(MenuDef menu)
        {
            int windowStart = _r.Position;
            uint namePtr = _r.ReadUInt32();

            // rect
            menu.Window.Rect = new RectDef
            {
                X = _r.ReadFloat(),
                Y = _r.ReadFloat(),
                W = _r.ReadFloat(),
                H = _r.ReadFloat(),
                HorzAlign = _r.ReadByte(),
                VertAlign = _r.ReadByte(),
            };
            _r.Skip(2); // rect padding

            // rectClient
            menu.Window.RectClient = new RectDef
            {
                X = _r.ReadFloat(),
                Y = _r.ReadFloat(),
                W = _r.ReadFloat(),
                H = _r.ReadFloat(),
                HorzAlign = _r.ReadByte(),
                VertAlign = _r.ReadByte(),
            };
            _r.Skip(2); // rectClient padding

            uint groupPtr = _r.ReadUInt32();
            menu.Window.Style = _r.ReadInt32();
            menu.Window.Border = _r.ReadInt32();
            menu.Window.OwnerDraw = _r.ReadInt32();
            menu.Window.OwnerDrawFlags = _r.ReadInt32();
            menu.Window.BorderSize = _r.ReadFloat();
            menu.Window.StaticFlags = _r.ReadInt32();
            // dynamicFlags[1] on PC, [4] on console
            int dynamicFlagsCount = _isConsole ? 4 : 1;
            menu.Window.DynamicFlags = new int[dynamicFlagsCount];
            for (int i = 0; i < dynamicFlagsCount; i++) menu.Window.DynamicFlags[i] = _r.ReadInt32();
            menu.Window.NextTime = _r.ReadInt32();
            menu.Window.ForeColor = ReadColor();
            menu.Window.BackColor = ReadColor();
            menu.Window.BorderColor = ReadColor();
            menu.Window.OutlineColor = ReadColor();
            menu.Window.DisableColor = ReadColor();
            _r.ReadUInt32(); // background (Material *) — opaque pointer, ignore

            // Stash group pointer where the caller can fetch it later (we want to load it
            // alongside name in the reorder phase). Cheapest path: stuff it into Window.Group
            // as a placeholder we'll either overwrite with the inline string or reset.
            menu.Window.Group = groupPtr == 0xFFFFFFFF ? "__INLINE__" : null;

            int windowEnd = _r.Position;
            Debug.Assert(windowEnd - windowStart == WindowDefBinarySize,
                $"windowDef_t read {windowEnd - windowStart} bytes, expected {WindowDefBinarySize}");

            return namePtr;
        }

        private float[] ReadColor()
        {
            return new[] { _r.ReadFloat(), _r.ReadFloat(), _r.ReadFloat(), _r.ReadFloat() };
        }

        // ===== Inline data loaders (skip-walks; we don't store everything, just advance the cursor) =====

        /// <summary>Reads MenuEventHandlerSet binary (8 bytes) + handlers array + each handler.</summary>
        private void LoadMenuEventHandlerSet()
        {
            int count = _r.ReadInt32();
            uint handlersPtr = _r.ReadUInt32();

            if (!IsInline(handlersPtr) || count <= 0 || count > 1000)
                return;

            // Array of MenuEventHandler pointers (each FFFFFFFF for inline)
            var handlerPtrs = new uint[count];
            for (int i = 0; i < count; i++) handlerPtrs[i] = _r.ReadUInt32();

            for (int i = 0; i < count; i++)
            {
                if (!IsInline(handlerPtrs[i])) continue;
                LoadMenuEventHandler();
            }
        }

        /// <summary>Reads MenuEventHandler binary (8 bytes: 4-byte union ptr + 1-byte type + 3 pad)
        /// then loads inline data per eventType.</summary>
        private void LoadMenuEventHandler()
        {
            uint dataPtr = _r.ReadUInt32(); // EventData union ptr
            byte eventType = _r.ReadByte();
            _r.Skip(3); // pad to 4-byte boundary

            switch (eventType)
            {
                case EVENT_UNCONDITIONAL:
                    if (IsInline(dataPtr)) _r.ReadInlineString(); // unconditionalScript
                    break;
                case EVENT_IF:
                    if (IsInline(dataPtr)) LoadConditionalScript();
                    break;
                case EVENT_ELSE:
                    if (IsInline(dataPtr)) LoadMenuEventHandlerSet();
                    break;
                case EVENT_SET_LOCAL_VAR_BOOL:
                case EVENT_SET_LOCAL_VAR_INT:
                case EVENT_SET_LOCAL_VAR_FLOAT:
                case EVENT_SET_LOCAL_VAR_STRING:
                    if (IsInline(dataPtr)) LoadSetLocalVarData();
                    break;
                default:
                    Debug.WriteLine($"[Iw4MenuDeserializer] Unknown EventType=0x{eventType:X} at 0x{_r.Position - 4:X}");
                    break;
            }
        }

        /// <summary>ConditionalScript binary (8 bytes) + reorder: eventExpression, eventHandlerSet.</summary>
        private void LoadConditionalScript()
        {
            uint eventHandlerSetPtr = _r.ReadUInt32();
            uint eventExpressionPtr = _r.ReadUInt32();
            // Reorder per OAT spec: eventExpression FIRST, then eventHandlerSet
            if (IsInline(eventExpressionPtr)) LoadStatement();
            if (IsInline(eventHandlerSetPtr)) LoadMenuEventHandlerSet();
        }

        /// <summary>SetLocalVarData binary (8 bytes) + localVarName string + expression Statement_s.</summary>
        private void LoadSetLocalVarData()
        {
            uint localVarNamePtr = _r.ReadUInt32();
            uint expressionPtr = _r.ReadUInt32();
            if (IsInline(localVarNamePtr)) _r.ReadInlineString();
            if (IsInline(expressionPtr)) LoadStatement();
        }

        /// <summary>Statement_s binary (28 bytes) + entries array + supportingData if inline.
        /// lastResult is part of the binary (the OAT spec marks it as "never loaded" meaning
        /// the field stays zero in the in-game struct, NOT that the bytes are absent from zone).</summary>
        private void LoadStatement()
        {
            int numEntries = _r.ReadInt32();
            uint entriesPtr = _r.ReadUInt32();
            uint supportingDataPtr = _r.ReadUInt32();
            _r.ReadInt32(); // lastExecuteTime
            // Operand lastResult: 4 (dataType) + 8 (union, max sizeof) — total 12 bytes per the
            // 4-byte alignment of the enum field. operandInternalDataUnion holds an int(4),
            // float(4), ExpressionString(4), or Statement_s*(4) — but the union itself is 4-byte
            // aligned with 4 bytes of trailing padding for the parent Statement_s, totaling 12.
            _r.Skip(12);

            if (IsInline(entriesPtr) && numEntries > 0 && numEntries <= 10000)
            {
                for (int i = 0; i < numEntries; i++) LoadExpressionEntry();
            }
            if (IsInline(supportingDataPtr)) LoadExpressionSupportingData();
        }

        /// <summary>expressionEntry binary (16 bytes: 4 type + 12 union) + inline data per type.</summary>
        private void LoadExpressionEntry()
        {
            int type = _r.ReadInt32();
            int unionBinaryStart = _r.Position;

            if (type == EET_OPERAND)
            {
                // Operand binary: 4 (dataType) + 8 (union — max member is ExpressionString or Statement_s*)
                int dataType = _r.ReadInt32();
                uint unionPtr = _r.ReadUInt32();
                _r.Skip(4); // union padding to 8 bytes
                // inline data depends on dataType
                switch (dataType)
                {
                    case VAL_STRING:
                        // ExpressionString inline — 4-byte string ptr + inline string
                        if (IsInline(unionPtr)) _r.ReadInlineString();
                        break;
                    case VAL_FUNCTION:
                        // Statement_s* — inline statement
                        if (IsInline(unionPtr)) LoadStatement();
                        break;
                    // VAL_INT / VAL_FLOAT: just 4 bytes inline in the union, no extra data
                }
            }
            else
            {
                // EET_OPERATOR: 4-byte op, no extra inline data. But the union slot is still 12 bytes.
                _r.Skip(12);
            }

            int consumed = _r.Position - unionBinaryStart;
            if (consumed < 12) _r.Skip(12 - consumed);
        }

        /// <summary>ExpressionSupportingData binary (12 bytes: UIFunctionList + StaticDvarList + StringList)
        /// + inline data for each sub-list.</summary>
        private void LoadExpressionSupportingData()
        {
            // UIFunctionList: count + pointer
            int functionCount = _r.ReadInt32();
            uint functionsPtr = _r.ReadUInt32();
            // StaticDvarList: count + pointer
            int dvarCount = _r.ReadInt32();
            uint dvarsPtr = _r.ReadUInt32();
            // StringList: count + pointer
            int stringCount = _r.ReadInt32();
            uint stringsPtr = _r.ReadUInt32();

            if (IsInline(functionsPtr) && functionCount > 0 && functionCount <= 1000)
            {
                // Array of Statement_s* pointers
                var ptrs = new uint[functionCount];
                for (int i = 0; i < functionCount; i++) ptrs[i] = _r.ReadUInt32();
                for (int i = 0; i < functionCount; i++)
                {
                    if (IsInline(ptrs[i])) LoadStatement();
                }
            }
            if (IsInline(dvarsPtr) && dvarCount > 0 && dvarCount <= 1000)
            {
                var ptrs = new uint[dvarCount];
                for (int i = 0; i < dvarCount; i++) ptrs[i] = _r.ReadUInt32();
                for (int i = 0; i < dvarCount; i++)
                {
                    if (IsInline(ptrs[i])) LoadStaticDvar();
                }
            }
            if (IsInline(stringsPtr) && stringCount > 0 && stringCount <= 10000)
            {
                // Array of string pointers — each FFFFFFFF followed by inline string
                var ptrs = new uint[stringCount];
                for (int i = 0; i < stringCount; i++) ptrs[i] = _r.ReadUInt32();
                for (int i = 0; i < stringCount; i++)
                {
                    if (IsInline(ptrs[i])) _r.ReadInlineString();
                }
            }
        }

        /// <summary>StaticDvar binary (8 bytes) + dvarName string.</summary>
        private void LoadStaticDvar()
        {
            _r.ReadUInt32();                // dvar (never loaded — runtime ptr)
            uint dvarNamePtr = _r.ReadUInt32();
            if (IsInline(dvarNamePtr)) _r.ReadInlineString();
        }

        /// <summary>ItemKeyHandler linked list: each node is 12 bytes (key + action ptr + next ptr).
        /// Walk while next is FFFFFFFF (inline).</summary>
        private void LoadItemKeyHandlerList()
        {
            while (true)
            {
                _r.ReadInt32();             // key
                uint actionPtr = _r.ReadUInt32();
                uint nextPtr = _r.ReadUInt32();
                if (IsInline(actionPtr)) LoadMenuEventHandlerSet();
                if (!IsInline(nextPtr)) break;
            }
        }

        /// <summary>items array: itemCount × itemDef_s* pointer, then each inline itemDef_s.</summary>
        private void LoadItemsArray(int itemCount)
        {
            if (itemCount <= 0 || itemCount > 1000) return;

            var ptrs = new uint[itemCount];
            for (int i = 0; i < itemCount; i++) ptrs[i] = _r.ReadUInt32();

            for (int i = 0; i < itemCount; i++)
            {
                if (IsInline(ptrs[i])) LoadItemDef();
            }
        }

        /// <summary>Reads an itemDef_s binary + recursively all its inline data.</summary>
        private void LoadItemDef()
        {
            int itemStart = _r.Position;

            // window
            var dummyMenu = new MenuDef { Window = new WindowDef() };
            uint itemWindowNamePtr = ReadWindowDefBinary(dummyMenu);
            uint itemWindowGroupPtr = dummyMenu.Window.Group == "__INLINE__" ? 0xFFFFFFFFu : 0u;

            // textRect[N]
            int textRectCount = _isConsole ? 4 : 1;
            _r.Skip(textRectCount * 20); // rectDef_s = 20 bytes (16 floats + horz/vert + 2 pad)

            int type = _r.ReadInt32();
            int dataType = _r.ReadInt32();
            _r.ReadInt32(); // alignment
            _r.ReadInt32(); // fontEnum
            _r.ReadInt32(); // textAlignMode
            _r.ReadFloat(); // textalignx
            _r.ReadFloat(); // textaligny
            _r.ReadFloat(); // textscale
            _r.ReadInt32(); // textStyle
            _r.ReadInt32(); // gameMsgWindowIndex
            _r.ReadInt32(); // gameMsgWindowMode
            uint textPtr = _r.ReadUInt32();
            _r.ReadInt32(); // itemFlags
            _r.ReadUInt32(); // parent (never loaded)
            uint mouseEnterTextPtr = _r.ReadUInt32();
            uint mouseExitTextPtr = _r.ReadUInt32();
            uint mouseEnterPtr = _r.ReadUInt32();
            uint mouseExitPtr = _r.ReadUInt32();
            uint actionPtr = _r.ReadUInt32();
            uint acceptPtr = _r.ReadUInt32();
            uint onFocusPtr = _r.ReadUInt32();
            uint leaveFocusPtr = _r.ReadUInt32();
            uint dvarPtr = _r.ReadUInt32();
            uint dvarTestPtr = _r.ReadUInt32();
            uint onKeyPtr = _r.ReadUInt32();
            uint enableDvarPtr = _r.ReadUInt32();
            uint localVarPtr = _r.ReadUInt32();
            _r.ReadInt32(); // dvarFlags
            _r.ReadUInt32(); // focusSound (snd_alias_list_t * — opaque, skip)
            _r.ReadFloat(); // special
            int cursorPosCount = _isConsole ? 4 : 1;
            _r.Skip(cursorPosCount * 4);
            uint typeDataPtr = _r.ReadUInt32(); // itemDefData_t union
            _r.ReadInt32(); // imageTrack
            int floatExpressionCount = _r.ReadInt32();
            uint floatExpressionsPtr = _r.ReadUInt32();
            uint visibleExpPtr = _r.ReadUInt32();
            uint disabledExpPtr = _r.ReadUInt32();
            uint textExpPtr = _r.ReadUInt32();
            uint materialExpPtr = _r.ReadUInt32();
            ReadColor(); // glowColor
            _r.ReadByte(); // decayActive
            _r.Skip(3); // pad
            _r.ReadInt32(); // fxBirthTime
            _r.ReadInt32(); // fxLetterTime
            _r.ReadInt32(); // fxDecayStartTime
            _r.ReadInt32(); // fxDecayDuration
            _r.ReadInt32(); // lastSoundPlayedTime

            // === itemDef_s inline data (per OAT spec) ===
            if (IsInline(itemWindowNamePtr)) _r.ReadInlineString();
            if (IsInline(itemWindowGroupPtr)) _r.ReadInlineString();
            if (IsInline(mouseEnterTextPtr)) LoadMenuEventHandlerSet();
            if (IsInline(mouseExitTextPtr)) LoadMenuEventHandlerSet();
            if (IsInline(mouseEnterPtr)) LoadMenuEventHandlerSet();
            if (IsInline(mouseExitPtr)) LoadMenuEventHandlerSet();
            if (IsInline(actionPtr)) LoadMenuEventHandlerSet();
            if (IsInline(acceptPtr)) LoadMenuEventHandlerSet();
            if (IsInline(onFocusPtr)) LoadMenuEventHandlerSet();
            if (IsInline(leaveFocusPtr)) LoadMenuEventHandlerSet();
            if (IsInline(textPtr)) _r.ReadInlineString();
            if (IsInline(dvarPtr)) _r.ReadInlineString();
            if (IsInline(dvarTestPtr)) _r.ReadInlineString();
            if (IsInline(onKeyPtr)) LoadItemKeyHandlerList();
            if (IsInline(enableDvarPtr)) _r.ReadInlineString();
            if (IsInline(localVarPtr)) _r.ReadInlineString();

            if (IsInline(typeDataPtr)) LoadItemTypeData(type);

            if (IsInline(floatExpressionsPtr) && floatExpressionCount > 0 && floatExpressionCount <= 1000)
            {
                // ItemFloatExpression array: each is 8 bytes (int target + Statement_s *)
                var exprPtrs = new uint[floatExpressionCount];
                for (int i = 0; i < floatExpressionCount; i++)
                {
                    _r.ReadInt32(); // target
                    exprPtrs[i] = _r.ReadUInt32();
                }
                for (int i = 0; i < floatExpressionCount; i++)
                {
                    if (IsInline(exprPtrs[i])) LoadStatement();
                }
            }

            if (IsInline(visibleExpPtr)) LoadStatement();
            if (IsInline(disabledExpPtr)) LoadStatement();
            if (IsInline(textExpPtr)) LoadStatement();
            if (IsInline(materialExpPtr)) LoadStatement();

            Debug.WriteLine($"[Iw4MenuDeserializer]   item @ 0x{itemStart:X}..0x{_r.Position:X} type={type}");
        }

        /// <summary>itemDefData_t (union) — variant determined by item.type per OAT spec.</summary>
        private void LoadItemTypeData(int itemType)
        {
            switch (itemType)
            {
                case ITEM_TYPE_LISTBOX:
                    LoadListBoxDef();
                    break;
                case ITEM_TYPE_TEXT:
                case ITEM_TYPE_EDITFIELD:
                case ITEM_TYPE_NUMERICFIELD:
                case ITEM_TYPE_SLIDER:
                case ITEM_TYPE_YESNO:
                case ITEM_TYPE_BIND:
                case ITEM_TYPE_VALIDFILEFIELD:
                case ITEM_TYPE_DECIMALFIELD:
                case ITEM_TYPE_UPREDITFIELD:
                case ITEM_TYPE_EMAILFIELD:
                case ITEM_TYPE_PASSWORDFIELD:
                    LoadEditFieldDef();
                    break;
                case ITEM_TYPE_MULTI:
                    LoadMultiDef();
                    break;
                case ITEM_TYPE_DVARENUM:
                    _r.ReadInlineString(); // enumDvarName
                    break;
                case ITEM_TYPE_NEWS_TICKER:
                    LoadNewsTickerDef();
                    break;
                case ITEM_TYPE_TEXT_SCROLL:
                    LoadTextScrollDef();
                    break;
                default:
                    // Unknown types — no inline data per spec ("set condition data never;")
                    break;
            }
        }

        /// <summary>listBoxDef_s binary + inline onDoubleClick + selectIcon Material*.</summary>
        private void LoadListBoxDef()
        {
            _r.ReadInt32(); // mousePos
            // startPos/endPos: [1] on PC, [4] on console per #ifndef PC
            int posCount = _isConsole ? 4 : 1;
            _r.Skip(posCount * 4); // startPos
            _r.Skip(posCount * 4); // endPos
            _r.ReadInt32(); // drawPadding (console only? actually the OAT struct shows it always)
            _r.ReadFloat(); // elementWidth
            _r.ReadFloat(); // elementHeight
            _r.ReadInt32(); // elementStyle
            _r.ReadInt32(); // numColumns
            _r.Skip(16 * 16); // columnInfo[16] (each 16 bytes)
            uint onDoubleClickPtr = _r.ReadUInt32();
            _r.ReadInt32(); // notselectable
            _r.ReadInt32(); // noScrollBars
            _r.ReadInt32(); // usePaging
            _r.Skip(16);    // selectBorder vec4
            _r.ReadUInt32(); // selectIcon (Material*) — opaque, no inline load

            if (IsInline(onDoubleClickPtr)) LoadMenuEventHandlerSet();
        }

        /// <summary>editFieldDef_s — 32 bytes binary, no inline data.</summary>
        private void LoadEditFieldDef()
        {
            _r.Skip(32); // 4 floats + 4 ints
        }

        /// <summary>multiDef_s binary + inline dvarList[] and dvarStr[] strings.</summary>
        private void LoadMultiDef()
        {
            // 32 dvarList* + 32 dvarStr* + 32 dvarValue (float) + count + strDef
            var dvarListPtrs = new uint[32];
            var dvarStrPtrs = new uint[32];
            for (int i = 0; i < 32; i++) dvarListPtrs[i] = _r.ReadUInt32();
            for (int i = 0; i < 32; i++) dvarStrPtrs[i] = _r.ReadUInt32();
            _r.Skip(32 * 4); // dvarValue (32 floats)
            int count = _r.ReadInt32();
            _r.ReadInt32(); // strDef

            if (count > 0 && count <= 32)
            {
                for (int i = 0; i < count; i++)
                {
                    if (IsInline(dvarListPtrs[i])) _r.ReadInlineString();
                }
                for (int i = 0; i < count; i++)
                {
                    if (IsInline(dvarStrPtrs[i])) _r.ReadInlineString();
                }
            }
        }

        /// <summary>newsTickerDef_s — 28 bytes binary (per IW4_Assets.h), no inline data.</summary>
        private void LoadNewsTickerDef()
        {
            _r.Skip(28);
        }

        /// <summary>textScrollDef_s — 4 bytes binary, no inline data.</summary>
        private void LoadTextScrollDef()
        {
            _r.Skip(4);
        }
    }

    /// <summary>
    /// Cursor over a byte[] with BE/LE endianness awareness. All ReadXxx methods advance Position
    /// by the type's size; Skip advances without reading. Designed for walking IW4 zone payloads.
    /// </summary>
    public class ZoneStreamReader
    {
        private readonly byte[] _data;
        private readonly bool _isBigEndian;

        public int Position { get; set; }
        public int Length => _data.Length;
        public byte[] Data => _data;

        public ZoneStreamReader(byte[] data, int startOffset, bool isBigEndian)
        {
            _data = data;
            Position = startOffset;
            _isBigEndian = isBigEndian;
        }

        public void Skip(int bytes) => Position += bytes;

        public byte ReadByte() => _data[Position++];

        public uint ReadUInt32()
        {
            uint v;
            if (_isBigEndian)
            {
                v = (uint)((_data[Position] << 24) | (_data[Position + 1] << 16) | (_data[Position + 2] << 8) | _data[Position + 3]);
            }
            else
            {
                v = (uint)(_data[Position] | (_data[Position + 1] << 8) | (_data[Position + 2] << 16) | (_data[Position + 3] << 24));
            }
            Position += 4;
            return v;
        }

        public int ReadInt32() => (int)ReadUInt32();

        public float ReadFloat()
        {
            byte[] bytes;
            if (_isBigEndian)
                bytes = new[] { _data[Position + 3], _data[Position + 2], _data[Position + 1], _data[Position] };
            else
                bytes = new[] { _data[Position], _data[Position + 1], _data[Position + 2], _data[Position + 3] };
            Position += 4;
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>Reads a null-terminated ASCII string at the current position, advancing past the null terminator.</summary>
        public string ReadInlineString()
        {
            int start = Position;
            while (Position < _data.Length && _data[Position] != 0) Position++;
            string s = Encoding.ASCII.GetString(_data, start, Position - start);
            if (Position < _data.Length) Position++; // consume null
            return s;
        }

        /// <summary>Peek the groupPtr field at a known offset within a windowDef_t starting at windowStart.</summary>
        public uint PeekWindowGroupPointerFromStart(int windowStart, bool isConsole)
        {
            // windowDef_t layout: name(4) + rect(20) + rectClient(20) + group(4) at offset 0x2C
            int groupOffset = windowStart + 4 + 20 + 20;
            int savedPos = Position;
            Position = groupOffset;
            uint v = ReadUInt32();
            Position = savedPos;
            return v;
        }
    }
}
