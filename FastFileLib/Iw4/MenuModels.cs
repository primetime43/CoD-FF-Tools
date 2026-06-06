// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports the menu model classes:
//   FastFile.Models/Assets/Menufile/MenuList.cs
//   FastFile.Models/Assets/Menu/MenuDef.cs
//   FastFile.Models/Assets/Menu/Elements/MenuElements.cs
//   FastFile.Models/Assets/Menu/Enums/*.cs
// PS3 array sizes (the `#if !PC` branches). Material / SndAliasList / Dvar are
// placeholders — for a UI zone those are referenced (offset pointers), not inline,
// so their readers never fire (see MenuReader's MaterialReader stub).
// =============================================================================

namespace FastFileLib.Iw4;

// ---- placeholder types (referenced as pointers, never resolved inline here) ----
// (Material is a real ported type — see MaterialModels.cs.)
public sealed class SndAliasList { }
public sealed class Dvar { }

// ---- enums (only the values the reader branches on need to be named) ----
public enum ExpDataType
{
    VAL_INT = 0x0,
    VAL_FLOAT = 0x1,
    VAL_STRING = 0x2,
    VAL_FUNCTION = 0x3,
}

public enum OperationEnum { OP_NOOP = 0x0 }
public enum ItemFloatExpressionTarget { ITEM_FLOATEXP_TGT_RECT_X = 0x0 }
public enum TransitionType { TRANS_INACTIVE = 0x0, TRANS_LERP = 0x1 }
public enum TriggerType { TRIGGER_NONE = 0x0, TRIGGER_CLOSEMENU = 0x1 }

// ---- elements ----
public class RectangleDef
{
    public float X, Y, W, H;
    public byte HorzAlign, VertAlign;
    public ushort AlignmentPadding;
}

public class Window
{
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public RectangleDef? Rect { get; set; }
    public RectangleDef? RectClient { get; set; }
    public ZonePointer<string>? GroupPtr { get; set; }
    public int Style, Border, OwnerDraw, OwnerDrawFlags;
    public float BorderSize;
    public int StaticFlags;
    public int[] DynamicFlags { get; set; } = new int[4];
    public int NextTime;
    public Vec4 ForeColor, BackColor, BorderColor, OutlineColor, DisableColor;
    public ZonePointer<Material>? Background { get; set; }
}

public class ExpressionString { public ZonePointer<string>? StringPtr { get; set; } }

public class OperandInternalData
{
    public int IntVal;
    public float FloatVal;
    public ZonePointer<ExpressionString>? StringVal { get; set; }
    public ZonePointer<Statement>? Function { get; set; }
}

public class Operand { public ExpDataType DataType; public OperandInternalData? Internals { get; set; } }
public class EntryInternalData { public OperationEnum Op; public Operand? Operand { get; set; } }
public class ExpressionEntry { public int Type; public EntryInternalData? Data { get; set; } }

public class SetLocalVarData
{
    public ZonePointer<string>? LocalVarName { get; set; }
    public ZonePointer<Statement>? Expression { get; set; }
}

public class ConditionalScript
{
    public ZonePointer<MenuEventHandlerSet>? EventHandlerSet { get; set; }
    public ZonePointer<Statement>? EventExpression { get; set; }
}

public class Statement
{
    public int NumEntries;
    public ZonePointer<ExpressionEntry[]>? Entries { get; set; }
    public ZonePointer<ExpressionSupportingData>? SupportingData { get; set; }
    public int LastExecuteTime;
    public Operand? LastResult { get; set; }
}

public class ItemFloatExpression
{
    public ItemFloatExpressionTarget Target;
    public ZonePointer<Statement>? Expression { get; set; }
}

public class EventData
{
    public int Raw;
    public ZonePointer<string>? UnconditionalScript { get; set; }
    public ZonePointer<ConditionalScript>? ConditionalScript { get; set; }
    public ZonePointer<MenuEventHandlerSet>? ElseScript { get; set; }
    public ZonePointer<SetLocalVarData>? SetLocalVarData { get; set; }
}

public class MenuEventHandler
{
    public EventData? EventData { get; set; }
    public byte EventType, EventTypePadding0, EventTypePadding1, EventTypePadding2;
}

public class MenuEventHandlerSet
{
    public int EventHandlerCount;
    public ZonePointer<ZonePointer<MenuEventHandler>[]>? EventHandlers { get; set; }
}

public class ItemKeyHandler
{
    public int Key;
    public ZonePointer<MenuEventHandlerSet>? Action { get; set; }
    public ZonePointer<ItemKeyHandler>? Next { get; set; }
}

public class NewsTickerDef { public int FeedId, Speed, Spacing, LastTime, Start, End; public float X; }
public class ColumnInfo { public int Pos, Width, MaxChars, Alignment; }

public class ListBoxDef
{
    public int[] StartPos { get; set; } = new int[4];
    public int[] EndPos { get; set; } = new int[4];
    public int DrawPadding;
    public float ElementWidth, ElementHeight;
    public int ElementStyle, NumColumns;
    public ColumnInfo[] ColumnInfo { get; set; } = new ColumnInfo[16];
    public ZonePointer<MenuEventHandlerSet>? DoubleClick { get; set; }
    public int NotSelectable, NoScrollbars, UsePaging;
    public Vec4 SelectBorder;
    public ZonePointer<Material>? SelectIcon { get; set; }
}

public class EditFieldDef
{
    public float MinVal, MaxVal, DefVal, Range;
    public int MaxChars, MaxCharsGotoNext, MaxPaintChars, PaintOffset;
}

public class MultiDef
{
    public ZonePointer<string>[] DvarList { get; set; } = new ZonePointer<string>[32];
    public ZonePointer<string>[] DvarStr { get; set; } = new ZonePointer<string>[32];
    public float[] DvarValue { get; set; } = new float[32];
    public int Count, StrDef;
}

public class TextScrollDef { public int StartTime; }

public class ItemDefData
{
    public int Raw;
    public ZonePointer<ListBoxDef>? ListBox { get; set; }
    public ZonePointer<EditFieldDef>? EditField { get; set; }
    public ZonePointer<MultiDef>? Multi { get; set; }
    public ZonePointer<string>? EnumDvarName { get; set; }
    public ZonePointer<NewsTickerDef>? NewsTicker { get; set; }
    public ZonePointer<TextScrollDef>? TextScroll { get; set; }
    public ZonePointer<ItemDefRawData>? Data { get; set; }
}

public class ItemDefRawData { public int[] Words { get; set; } = new int[8]; }

public class ItemDef
{
    public Window? Window { get; set; }
    public RectangleDef[] TextRect { get; set; } = new RectangleDef[4];
    public int Type, DataType, Align, FontEnum, TextAlignMode;
    public float TextAlignX, TextAlignY, TextScale;
    public int TextStyle, GameMsgWindowIndex, GameMsgWindowMode;
    public ZonePointer<string>? Text { get; set; }
    public int TextSaveGameInfo;
    public ZonePointer<MenuDef>? Parent { get; set; }
    public ZonePointer<MenuEventHandlerSet>? MouseEnterText { get; set; }
    public ZonePointer<MenuEventHandlerSet>? MouseExitText { get; set; }
    public ZonePointer<MenuEventHandlerSet>? MouseEnter { get; set; }
    public ZonePointer<MenuEventHandlerSet>? MouseExit { get; set; }
    public ZonePointer<MenuEventHandlerSet>? Action { get; set; }
    public ZonePointer<MenuEventHandlerSet>? Accept { get; set; }
    public ZonePointer<MenuEventHandlerSet>? OnFocus { get; set; }
    public ZonePointer<MenuEventHandlerSet>? LeaveFocus { get; set; }
    public ZonePointer<string>? Dvar { get; set; }
    public ZonePointer<string>? DvarTest { get; set; }
    public ZonePointer<ItemKeyHandler>? OnKey { get; set; }
    public ZonePointer<string>? EnableDvar { get; set; }
    public int DvarFlags;
    public ZonePointer<SndAliasList>? FocusSound { get; set; }
    public float Special;
    public int[] CursorPos { get; set; } = new int[4];
    public ItemDefData? TypeData { get; set; }
    public int ImageTrack, FloatExpressionCount;
    public ZonePointer<ItemFloatExpression[]>? FloatExpressions { get; set; }
    public ZonePointer<Statement>? VisibleExp { get; set; }
    public ZonePointer<Statement>? DisabledExp { get; set; }
    public ZonePointer<Statement>? TextExp { get; set; }
    public ZonePointer<Statement>? MaterialExp { get; set; }
    public Vec4 GlowColor;
    public bool DecayActive;
    public byte DecayActivePadding0, DecayActivePadding1, DecayActivePadding2;
    public int FxBirthTime, FxLetterTime, FxDecayStartTime, FxDecayDuration, LastSoundPlayedTime;
}

public class StaticDvar
{
    public ZonePointer<Dvar>? Dvar { get; set; }
    public ZonePointer<string>? DvarName { get; set; }
}

public class StaticDvarList
{
    public int NumStaticDvars;
    public ZonePointer<ZonePointer<StaticDvar>[]>? StaticDvars { get; set; }
}

public class UIFunctionList
{
    public int TotalFunctions;
    public ZonePointer<ZonePointer<Statement>[]>? Functions { get; set; }
}

public class StringList
{
    public int TotalStrings;
    public ZonePointer<ZonePointer<string>[]>? Strings { get; set; }
}

public class ExpressionSupportingData
{
    public UIFunctionList? UiFunctions { get; set; }
    public StaticDvarList? StaticDvarList { get; set; }
    public StringList? UiStrings { get; set; }
}

public class MenuTransition
{
    public TransitionType TransitionType;
    public int TargetField, StartTime;
    public float StartVal, EndVal, Time;
    public TriggerType EndTriggerType;
}

public class MenuDef : BaseAsset
{
    public MenuDef() : base(XAssetType.Menu) { }
    public Window? Window { get; set; }
    public ZonePointer<string>? FontPtr { get; set; }
    public int Fullscreen, ItemCount, FontIndex;
    public int[] CursorItems { get; set; } = new int[4];
    public int FadeCycle;
    public float FadeClamp, FadeAmount, FadeInAmount, BlurRadius;
    public ZonePointer<MenuEventHandlerSet>? OnOpen { get; set; }
    public ZonePointer<MenuEventHandlerSet>? OnRequestClose { get; set; }
    public ZonePointer<MenuEventHandlerSet>? OnClose { get; set; }
    public ZonePointer<MenuEventHandlerSet>? OnEsc { get; set; }
    public ZonePointer<ItemKeyHandler>? ExecKeys { get; set; }
    public ZonePointer<Statement>? VisibleExp { get; set; }
    public ZonePointer<string>? AllowedBinding { get; set; }
    public ZonePointer<string>? SoundName { get; set; }
    public int ImageTrack;
    public Vec4 FocusColor;
    public ZonePointer<Statement>? RectXExp { get; set; }
    public ZonePointer<Statement>? RectYExp { get; set; }
    public ZonePointer<Statement>? RectHExp { get; set; }
    public ZonePointer<Statement>? RectWExp { get; set; }
    public ZonePointer<ZonePointer<ItemDef>[]>? Items { get; set; }
    public MenuTransition[] ScaleTransition { get; set; } = new MenuTransition[4];
    public MenuTransition[] AlphaTransition { get; set; } = new MenuTransition[4];
    public MenuTransition[] XTransition { get; set; } = new MenuTransition[4];
    public MenuTransition[] YTransition { get; set; } = new MenuTransition[4];
    public ZonePointer<ExpressionSupportingData>? ExpressionData { get; set; }

    public override string? GetDisplayName => Window?.Name ?? string.Empty;
}

public class MenuList : BaseAsset
{
    public MenuList() : base(XAssetType.MenuFile) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public int MenuCount;
    public ZonePointer<ZonePointer<MenuDef>[]>? Menus { get; set; }

    public override string? GetDisplayName => NamePtr is { IsResolved: true } ? NamePtr.Result : string.Empty;
}
