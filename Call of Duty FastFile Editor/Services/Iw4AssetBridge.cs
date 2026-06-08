using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.Services
{
    public sealed class Iw4BridgeResult
    {
        public List<RawFileNode> RawFileNodes { get; } = new();
        public List<LocalizedEntry> LocalizedEntries { get; } = new();
        public List<StringTable> StringTables { get; } = new();
        public List<WeaponAsset> Weapons { get; } = new();
        public List<TechSetAsset> TechSets { get; } = new();
        public List<MenuList> MenuLists { get; } = new();
        public List<StructuredDataDefAsset> StructuredDataDefs { get; } = new();
        public List<MaterialAsset> Materials { get; } = new();

        /// <summary>True when the IW4 walk read every asset body (no stop / no error).</summary>
        public bool Complete { get; set; }

        /// <summary>Number of localize-typed pool entries the zone declares (resolved or not).</summary>
        public int DeclaredLocalizeCount { get; set; }

        /// <summary>Number of material-typed pool entries the zone declares (resolved or not).</summary>
        public int DeclaredMaterialCount { get; set; }
    }

    /// <summary>
    /// Bridges <see cref="FastFileLib.Iw4.Iw4ZoneReader"/> (the MW2 PS3 pointer-following reader,
    /// ported from Jacob Schroeder's FastFile — https://github.com/jacob-schroeder/FastFile) to the
    /// editor's <see cref="RawFileNode"/> / <see cref="LocalizedEntry"/> models, so the editor reads
    /// rawfile/localize by following the IW pointer conventions instead of pattern-scanning for
    /// <c>FF FF FF FF</c> markers.
    ///
    /// The IW4 rawfile <c>Offset</c> (header) and <c>DataOffset</c> match what <c>RawFileScanner</c>
    /// produces, so the editor's in-place save path (which writes at <see cref="RawFileNode.CodeStartPosition"/>
    /// and rewrites the header at <see cref="RawFileNode.StartOfFileHeader"/>) keeps working unchanged.
    ///
    /// Beyond rawfile/localize, every other top-level asset type the IW4 walk reads is mapped to its
    /// editor model so the dedicated tabs (StringTables, Weapons, TechSets) and the Asset Pool list
    /// view are populated from the authoritative pointer-walk instead of pattern-scanning. The IW4
    /// top-level reader registry covers rawfile/localize/stringtable/menufile/structureddatadef/
    /// techset/weapon; image/material/xmodel are only sub-assets (the walk stops at a top-level image),
    /// so any zone whose full walk completes contains none of those as pool entries — menus and images
    /// stay on the existing processor path. Because the walk returns assets in pool order, the Nth
    /// weapon/stringtable/techset here lines up with the Nth pool record of that type, which is how the
    /// Asset Pool view matches names.
    /// </summary>
    public static class Iw4AssetBridge
    {
        /// <summary>
        /// Walks the zone with the IW4 reader. Returns null (caller keeps the pattern-scan result)
        /// if the walk doesn't fully complete — a partial walk would be missing assets past the stop.
        /// </summary>
        public static Iw4BridgeResult? TryRead(ZoneFile zone)
        {
            if (zone?.Data == null)
                return null;

            FastFileLib.Iw4.Iw4ZoneReadResult walk;
            try
            {
                walk = new FastFileLib.Iw4.Iw4ZoneReader(zone.Data).Read();
            }
            catch
            {
                return null;
            }

            // A complete walk replaces the pattern scanner wholesale; a partial walk (stopped at an
            // un-ported type) is still usable for any asset type it read IN FULL — see the caller.
            var result = new Iw4BridgeResult
            {
                Complete = walk.Error == null && walk.StoppedAtType == null,
                DeclaredLocalizeCount = walk.AssetList.Assets.Count(a => a.Type == FastFileLib.Iw4.XAssetType.Localize),
                DeclaredMaterialCount = walk.AssetList.Assets.Count(a => a.Type == FastFileLib.Iw4.XAssetType.Material),
            };
            var iw4MenuLists = new List<FastFileLib.Iw4.MenuList>();

            // Every top-level asset body's start offset. The byte spans of inline data (e.g. a
            // menuDef's serialized struct + items + strings) run from one body offset to the next in
            // stream order, so this sorted set lets us bound each menu's data without re-scanning.
            var bodyOffsets = new SortedSet<int>();

            foreach (var asset in walk.AssetList.Assets)
            {
                var body = asset.XAssetPtr?.Result;
                if (body is { Offset: > 0 })
                    bodyOffsets.Add(body.Offset);

                switch (body)
                {
                    case FastFileLib.Iw4.RawFile rf:
                        result.RawFileNodes.Add(ToRawFileNode(rf));
                        break;
                    case FastFileLib.Iw4.LocalizeEntry le:
                        result.LocalizedEntries.Add(ToLocalizedEntry(le));
                        break;
                    case FastFileLib.Iw4.StringTable st:
                        result.StringTables.Add(ToStringTable(st));
                        break;
                    case FastFileLib.Iw4.WeaponVariantDef wp:
                        result.Weapons.Add(ToWeaponAsset(wp, zone.Data));
                        break;
                    case FastFileLib.Iw4.MaterialTechniqueSet ts:
                        result.TechSets.Add(ToTechSet(ts));
                        break;
                    case FastFileLib.Iw4.Material mat:
                        result.Materials.Add(ToMaterial(mat));
                        break;
                    case FastFileLib.Iw4.MenuList ml:
                        iw4MenuLists.Add(ml);
                        break;
                    case FastFileLib.Iw4.StructuredDataDefSet sd:
                        result.StructuredDataDefs.Add(ToStructuredDataDef(sd));
                        break;
                }
            }

            // Add every menuDef's offset to the boundary set so consecutive menus inside one list
            // bound each other (the last menu in a list is bounded by the next top-level asset body).
            foreach (var ml in iw4MenuLists)
                foreach (var def in ResolveMenuDefs(ml))
                    if (def.Offset > 0)
                        bodyOffsets.Add(def.Offset);

            int zoneEnd = zone.Data.Length;
            foreach (var ml in iw4MenuLists)
                result.MenuLists.Add(ToMenuList(ml, bodyOffsets, zoneEnd));

            return result;
        }

        /// <summary>Resolved menuDefs of an IW4 menulist, in serialized (offset) order.</summary>
        private static List<FastFileLib.Iw4.MenuDef> ResolveMenuDefs(FastFileLib.Iw4.MenuList ml)
        {
            if (ml.Menus is not { IsResolved: true, Result: not null })
                return new List<FastFileLib.Iw4.MenuDef>();

            return ml.Menus.Result
                .Where(p => p is { IsResolved: true, Result: not null })
                .Select(p => p!.Result!)
                .OrderBy(m => m.Offset)
                .ToList();
        }

        private static MenuList ToMenuList(FastFileLib.Iw4.MenuList ml, SortedSet<int> boundaries, int zoneEnd)
        {
            var defs = ResolveMenuDefs(ml);

            var menus = new List<MenuDef>(defs.Count);
            foreach (var d in defs)
            {
                // End of this menu's inline data = the next serialized boundary after its start
                // (next menuDef or next asset body), falling back to the zone end.
                int end = NextBoundary(boundaries, d.Offset, zoneEnd);
                var menu = new MenuDef
                {
                    Window = new WindowDef { Name = d.Window?.Name ?? string.Empty, Rect = ToRect(d.Window?.Rect) },
                    ItemCount = d.ItemCount,
                    StartOffset = d.Offset,
                    EndOffset = end,
                };
                AddEditableValues(menu, d);
                AddItems(menu, d);
                menus.Add(menu);
            }

            int dataStart = menus.Count > 0 ? menus[0].StartOffset : ml.Offset;
            int dataEnd = menus.Count > 0 ? menus[^1].EndOffset : ml.Offset;

            return new MenuList
            {
                Name = ml.NamePtr is { IsResolved: true } ? ml.NamePtr.Result ?? string.Empty : string.Empty,
                MenuCount = ml.MenuCount,
                Menus = menus,
                StartOfFileHeader = ml.Offset,
                EndOfFileHeader = ml.Offset,
                DataStartOffset = dataStart,
                DataEndOffset = dataEnd,
                AdditionalData = "IW4 pointer-walk",
            };
        }

        /// <summary>Smallest boundary strictly greater than <paramref name="offset"/>, or <paramref name="zoneEnd"/>.</summary>
        private static int NextBoundary(SortedSet<int> boundaries, int offset, int zoneEnd)
        {
            foreach (int b in boundaries.GetViewBetween(offset + 1, int.MaxValue))
                return b;
            return zoneEnd;
        }

        // IW4 console menuDef_t starts with an embedded windowDef_t at +0x00 (dynamicFlags[4]).
        // These are the field byte offsets within that struct, used to make the rect/colors/itemCount
        // editable at their real zone positions (windowDef = 0xB0 bytes; menuDef = 0x2F0). Derived from
        // the lib's MenuReader read order — no byte-scanning needed since the IW4 walk already gave us
        // the menuDef's authoritative start offset and the decoded values.
        private const int WindowRectOffset = 0x04;
        private const int WindowForeColorOffset = 0x5C;
        private const int WindowBackColorOffset = 0x6C;
        private const int WindowBorderColorOffset = 0x7C;
        private const int WindowOutlineColorOffset = 0x8C;
        private const int WindowDisableColorOffset = 0x9C;
        private const int MenuItemCountOffset = 0xB8;

        private static void AddEditableValues(MenuDef menu, FastFileLib.Iw4.MenuDef d)
        {
            int start = d.Offset;
            var w = d.Window;
            if (w == null)
                return;

            if (w.Rect != null)
                menu.EditableValues.Add(MenuValue.CreateRect("rect", w.Rect.X, w.Rect.Y, w.Rect.W, w.Rect.H, start + WindowRectOffset));

            menu.EditableValues.Add(MenuValue.CreateColor("foreColor", ColorToFloats(w.ForeColor), start + WindowForeColorOffset));
            menu.EditableValues.Add(MenuValue.CreateColor("backColor", ColorToFloats(w.BackColor), start + WindowBackColorOffset));
            menu.EditableValues.Add(MenuValue.CreateColor("borderColor", ColorToFloats(w.BorderColor), start + WindowBorderColorOffset));
            menu.EditableValues.Add(MenuValue.CreateColor("outlineColor", ColorToFloats(w.OutlineColor), start + WindowOutlineColorOffset));
            menu.EditableValues.Add(MenuValue.CreateColor("disableColor", ColorToFloats(w.DisableColor), start + WindowDisableColorOffset));
            menu.EditableValues.Add(MenuValue.CreateInt("itemCount", d.ItemCount, start + MenuItemCountOffset));
        }

        // Vec4 is stored as four consecutive floats (A,R,G,B order, per the reader's ReadVec4); the
        // editor writes them back in the same order, so editing round-trips.
        private static float[] ColorToFloats(FastFileLib.Iw4.Vec4 c) => new[] { c.A, c.R, c.G, c.B };

        private static RectDef? ToRect(FastFileLib.Iw4.RectangleDef? r)
            => r == null ? null : new RectDef
            {
                X = r.X, Y = r.Y, W = r.W, H = r.H,
                HorzAlign = r.HorzAlign, VertAlign = r.VertAlign,
            };

        // Map the IW4-parsed itemDefs (the menuReader fully walks items[]) into the editor's MenuDef.Items
        // so the Menus tab can list each item's type / text / dvar / rect. Read-only — for display only.
        private static void AddItems(MenuDef menu, FastFileLib.Iw4.MenuDef d)
        {
            if (d.Items is not { IsResolved: true, Result: not null })
                return;

            foreach (var ip in d.Items.Result)
            {
                var it = ip is { IsResolved: true, Result: not null } ? ip.Result : null;
                if (it == null)
                    continue;

                menu.Items.Add(new ItemDef
                {
                    Window = new WindowDef { Name = it.Window?.Name ?? string.Empty, Rect = ToRect(it.Window?.Rect) },
                    Type = it.Type,
                    DataType = it.DataType,
                    Text = S(it.Text),
                    Dvar = S(it.Dvar),
                    DvarTest = S(it.DvarTest),
                    EnableDvar = S(it.EnableDvar),
                });
            }
        }

        private static RawFileNode ToRawFileNode(FastFileLib.Iw4.RawFile rf)
        {
            byte[] onDisk = rf.BufferPtr?.Result ?? Array.Empty<byte>();
            byte[] data = rf.CompressedLen > 0
                ? CompressionHelper.DecompressZlib(onDisk)
                : onDisk;

            return new RawFileNode
            {
                FileName = rf.Name,
                StartOfFileHeader = rf.Offset,
                HeaderSize = 16,                 // MW2 16-byte rawfile header
                MaxSize = rf.Len,                // uncompressed length
                IsCompressed = rf.CompressedLen > 0,
                CompressedSize = rf.CompressedLen,
                CodeStartPosition = rf.DataOffset,
                RawFileBytes = data,
                RawFileContent = Encoding.UTF8.GetString(data),
                RawFileEndPosition = rf.DataOffset + rf.OnDiskSize + 1, // +1 trailing null
                PatternIndexPosition = rf.Offset,
                AdditionalData = "IW4 pointer-walk",
            };
        }

        private static LocalizedEntry ToLocalizedEntry(FastFileLib.Iw4.LocalizeEntry le)
        {
            // Zone localize layout: [valuePtr][namePtr] then any *inline* value / key C strings.
            // (A key stored as an Offset pointer lives in a shared block — it's resolved into le.Name
            // by the reader but doesn't occupy bytes in this entry, so it doesn't extend the entry.)
            int end = le.Offset + 8; // the two pointers

            bool valueInline = le.ValuePtr is { Kind: FastFileLib.Iw4.PointerKind.Inline };
            bool keyInline = le.NamePtr is { Kind: FastFileLib.Iw4.PointerKind.Inline };

            int valueOffset = valueInline ? le.ValuePtr!.SourceOffset : -1;
            int keyOffset = keyInline ? le.NamePtr!.SourceOffset : -1;

            if (valueInline && valueOffset >= 0)
                end = Math.Max(end, valueOffset + Encoding.UTF8.GetByteCount(le.Value ?? string.Empty) + 1);
            if (keyInline && keyOffset >= 0)
                end = Math.Max(end, keyOffset + Encoding.ASCII.GetByteCount(le.Name ?? string.Empty) + 1);

            return new LocalizedEntry
            {
                Key = le.Name,
                LocalizedText = le.Value,
                StartOfFileHeader = le.Offset,
                StartOfFileData = valueOffset >= 0 ? valueOffset : le.Offset + 8,
                KeyStartOffset = keyOffset,
                EndOfFileData = end,
                EndOfFileHeader = end,
                AdditionalData = "IW4 pointer-walk",
            };
        }

        private static StringTable ToStringTable(FastFileLib.Iw4.StringTable st)
        {
            // Map the resolved 8-byte cells (string pointer + hash) to the editor's (offset, text)
            // list. Inline cell strings carry their zone position in the pointer's SourceOffset;
            // shared (offset-pointer) strings don't resolve in the reader, so they fall back to 0.
            var cells = st.Strings
                .Select(c => (Offset: c.StringPtr is { SourceOffset: >= 0 } p ? p.SourceOffset : 0,
                              Text: c.String ?? string.Empty))
                .ToList();

            return new StringTable
            {
                TableName = st.Name,
                ColumnCount = st.ColumnCount,
                RowCount = st.RowCount,
                Cells = cells,
                StartOfFileHeader = st.Offset,
                AdditionalData = "IW4 pointer-walk",
            };
        }

        private static string S(FastFileLib.Iw4.ZonePointer<string>? p)
            => p is { IsResolved: true } ? p.Result ?? string.Empty : string.Empty;

        /// <summary>Big-endian (PS3) int32 read with bounds checking; returns -1 if out of range.</summary>
        private static int ReadBE(byte[] data, int offset)
            => (data != null && offset >= 0 && offset + 4 <= data.Length)
                ? (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]
                : -1;

        /// <summary>Big-endian (PS3) float read; returns 0 if out of range.</summary>
        private static float ReadBEFloat(byte[] data, int offset)
        {
            int bits = ReadBE(data, offset);
            return bits == -1 && !(data != null && offset >= 0 && offset + 4 <= data.Length)
                ? 0f
                : BitConverter.Int32BitsToSingle(bits);
        }

        // Authoritative IW4 enum value lists (weapType_t / weapClass_t from OpenAssetTools; the
        // rest are the standard IW4 lineage enums). The WeaponDef's enum cluster is the 8-int block
        // the reader stores as PlayerAnimTypeThroughStance: [0]=playerAnimType, [1]=weapType,
        // [2]=weapClass, [3]=penetrateType, [4]=impactType(default), [5]=inventoryType, [6]=fireType,
        // [7]=clipType. Validated against patch_mp.ff (model1887 → bullet/spread shotgun; airdrop
        // marker → grenade/grenade/item). Impact for display uses the per-variant impactType override.
        private static readonly string[] Iw4WeapType = { "bullet", "grenade", "projectile", "riotshield" };
        private static readonly string[] Iw4WeapClass =
            { "rifle", "sniper", "mg", "smg", "spread (shotgun)", "pistol", "grenade", "rocketlauncher", "turret", "throwingknife", "non-player", "item" };
        private static readonly string[] Iw4PenetrateType = { "none", "small", "medium", "large" };
        private static readonly string[] Iw4InventoryType = { "primary", "offhand", "item", "altmode", "exclusive", "scavenger" };
        private static readonly string[] Iw4FireType = { "fullauto", "singleshot", "burst2", "burst3", "burst4", "doublebarrel" };
        private static readonly string[] Iw4ImpactType =
        {
            "none", "bullet small", "bullet large", "bullet ap", "bullet xtreme", "shotgun",
            "grenade bounce", "grenade explode", "rifle grenade", "rocket explode",
            "rocket explode xtreme", "projectile dud", "mortar shell", "tank shell",
        };

        private static string EnumName(string[] table, int v) => v >= 0 && v < table.Length ? table[v] : v.ToString();

        // IW4 WeaponDef field byte offsets (validated against patch_mp.ff PS3 — see comments below).
        // Same layout PC/PS3 up through these fields (the PC/PS3 size delta is the bool-flag tail).
        private const int WeaponDefIStartAmmo = 0x208;
        private const int WeaponDefIMaxAmmo = 0x21C;
        private const int WeaponDefShotCount = 0x220;
        private const int WeaponDefDamage = 0x230;
        private const int WeaponDefPlayerDamage = 0x234;
        private const int WeaponDefMeleeDamage = 0x238;
        // Damage falloff (offsets need typed struct sizing — shorts/bools shift the later region).
        private const int WeaponDefMinDamage = 0x598;
        private const int WeaponDefMinPlayerDamage = 0x59C;
        private const int WeaponDefMaxDamageRange = 0x5A0;
        private const int WeaponDefMinDamageRange = 0x5A4;

        private static WeaponAsset ToWeaponAsset(FastFileLib.Iw4.WeaponVariantDef wp, byte[] zoneData)
        {
            // The IW4 walk is the correct MW2 weapon reader. The enum cluster + ammo/damage ints live
            // in regions the ported reader stores as opaque blocks, but at known byte offsets (from the
            // OpenAssetTools IW4 WeaponDef, validated against patch_mp.ff: model1887 → bullet/spread,
            // 8-pellet shotCount, damage 35/pellet, melee 135; akimbo doubles reserve ammo). They're
            // read straight from the zone (big-endian) at WeaponDef.Offset + field offset.
            var def = wp.WeaponDefPtr is { IsResolved: true } ? wp.WeaponDefPtr.Result : null;

            int damage = -1, maxAmmo = -1, startAmmo = -1, shotCount = -1, playerDamage = -1, meleeDamage = -1;
            int minDamage = -1, minPlayerDamage = -1;
            float maxDamageRange = 0, minDamageRange = 0;
            if (def != null)
            {
                int b = def.Offset;
                damage = ReadBE(zoneData, b + WeaponDefDamage);
                playerDamage = ReadBE(zoneData, b + WeaponDefPlayerDamage);
                meleeDamage = ReadBE(zoneData, b + WeaponDefMeleeDamage);
                maxAmmo = ReadBE(zoneData, b + WeaponDefIMaxAmmo);
                startAmmo = ReadBE(zoneData, b + WeaponDefIStartAmmo);
                shotCount = ReadBE(zoneData, b + WeaponDefShotCount);
                minDamage = ReadBE(zoneData, b + WeaponDefMinDamage);
                minPlayerDamage = ReadBE(zoneData, b + WeaponDefMinPlayerDamage);
                maxDamageRange = ReadBEFloat(zoneData, b + WeaponDefMaxDamageRange);
                minDamageRange = ReadBEFloat(zoneData, b + WeaponDefMinDamageRange);
            }

            var weapon = new WeaponAsset
            {
                InternalName = wp.InternalName,
                DisplayName = S(wp.DisplayNamePtr),
                ClipSize = wp.iClipSize,
                FireTime = wp.iFireTime,
                AdsTransInTime = wp.iAdsTransInTime,
                AdsZoomFov = wp.fAdsZoomFov,
                Damage = damage,
                MinDamage = minDamage,
                MaxAmmo = maxAmmo,
                StartOffset = wp.Offset,
                WeaponDefOffset = def?.Offset ?? 0,
                EndOffset = 0,
                IsStructuredView = true,
                AdditionalData = "IW4 pointer-walk",
                // Impact comes from the per-variant impactType (the WeaponDef enum block has no
                // impactType field; e.g. model1887 → shotgun).
                ImpactName = EnumName(Iw4ImpactType, (int)wp.impactType),
            };

            // The WeaponDef enum cluster the reader stores as the 8-int PlayerAnimTypeThroughStance
            // block: [0]=playerAnimType, [1]=weapType, [2]=weapClass, [3]=penetrateType,
            // [4]=inventoryType, [5]=fireType, [6]=offhandClass, [7]=stance (OAT IW4 order).
            var anim = def?.PlayerAnimTypeThroughStance;
            if (anim != null && anim.Length >= 8)
            {
                weapon.TypeName = EnumName(Iw4WeapType, anim[1]);
                weapon.ClassName = EnumName(Iw4WeapClass, anim[2]);
                weapon.PenetrateName = EnumName(Iw4PenetrateType, anim[3]);
                weapon.InventoryName = EnumName(Iw4InventoryType, anim[4]);
                weapon.FireTypeName = EnumName(Iw4FireType, anim[5]);
            }

            var d = weapon.DetailFields;
            d.Add(("Variant", ""));
            d.Add(("internalName", wp.InternalName));
            d.Add(("displayName", S(wp.DisplayNamePtr)));
            d.Add(("altWeaponName", S(wp.szAltWeaponName)));
            d.Add(("type", weapon.TypeName));
            d.Add(("class", weapon.ClassName));
            d.Add(("fireType", weapon.FireTypeName));
            d.Add(("penetrateType", weapon.PenetrateName));
            d.Add(("impactType", weapon.ImpactName));
            d.Add(("inventoryType", weapon.InventoryName));
            if (def != null)
            {
                d.Add(("damage (max)", damage.ToString()));
                d.Add(("minDamage", minDamage.ToString()));
                d.Add(("playerDamage (max)", playerDamage.ToString()));
                d.Add(("minPlayerDamage", minPlayerDamage.ToString()));
                d.Add(("maxDamageRange", maxDamageRange.ToString("0.#")));
                d.Add(("minDamageRange", minDamageRange.ToString("0.#")));
                d.Add(("meleeDamage", meleeDamage.ToString()));
                d.Add(("shotCount", shotCount.ToString()));
                d.Add(("startAmmo", startAmmo.ToString()));
                d.Add(("maxAmmo (reserve)", maxAmmo.ToString()));
            }
            d.Add(("clipSize", wp.iClipSize.ToString()));
            d.Add(("fireTime", $"{wp.iFireTime} ms"));
            d.Add(("adsTransInTime", $"{wp.iAdsTransInTime} ms"));
            d.Add(("adsTransOutTime", $"{wp.iAdsTransOutTime} ms"));
            d.Add(("adsZoomFov", wp.fAdsZoomFov.ToString("0.###")));
            d.Add(("penetrateMultiplier", wp.fPenetrateMultiplier.ToString("0.###")));
            d.Add(("accuracyGraphKnots", wp.accuracyGraphKnotCount.ToString()));

            if (def != null)
            {
                d.Add(("WeaponDef", ""));
                d.Add(("arcs (L/R/T/B)", $"{def.LeftArc:0.#} / {def.RightArc:0.#} / {def.TopArc:0.#} / {def.BottomArc:0.#}"));
                d.Add(("accuracy", def.Accuracy.ToString("0.####")));
                d.Add(("aiSpread", def.AiSpread.ToString("0.###")));
                d.Add(("playerSpread", def.PlayerSpread.ToString("0.###")));
                d.Add(("minTurnSpeed", $"{def.MinTurnSpeed[0]:0.#} / {def.MinTurnSpeed[1]:0.#}"));
                d.Add(("maxTurnSpeed", $"{def.MaxTurnSpeed[0]:0.#} / {def.MaxTurnSpeed[1]:0.#}"));
                d.Add(("pitchConvergenceTime", def.PitchConvergenceTime.ToString("0.###")));
                d.Add(("yawConvergenceTime", def.YawConvergenceTime.ToString("0.###")));
                d.Add(("suppressTime", def.SuppressTime.ToString("0.###")));
                d.Add(("maxRange", def.MaxRange.ToString("0.#")));
                d.Add(("useHintString", S(def.UseHintString)));
                d.Add(("dropHintString", S(def.DropHintString)));
                d.Add(("scriptName", S(def.ScriptName)));
                d.Add(("accuracyGraphName0", S(def.AccuracyGraphName0)));
                d.Add(("accuracyGraphName1", S(def.AccuracyGraphName1)));
                d.Add(("fireRumble", S(def.FireRumble)));
                d.Add(("meleeImpactRumble", S(def.MeleeImpactRumble)));

                string flags = FlagsToString(def.BooleanFlags);
                if (flags.Length > 0)
                    d.Add(("flags", flags));
            }

            // Drop section headers whose section turned out empty / fields that are blank strings
            // would clutter the view; keep all here — the detail form renders blanks as "—".
            return weapon;
        }

        private static string FlagsToString(FastFileLib.Iw4.WeaponBooleanFlags? flags)
        {
            if (flags == null) return string.Empty;
            var names = flags.GetType().GetFields()
                .Where(f => f.FieldType == typeof(bool) && f.GetValue(flags) is true)
                .Select(f => f.Name);
            return string.Join(", ", names);
        }

        // Canonical IW4 (IW-lineage) MaterialTechniqueType slot names. The techset's techniques[]
        // array is indexed by technique type, so a non-null slot N means "this material set defines
        // the technique of type N". The reader doesn't dereference the (shared, offset-pointer)
        // technique bodies, but the slot index alone yields the technique's well-known type name.
        // Order is the standard IW4 enum; PS3's 37-slot variant matches this prefix.
        private static readonly string[] Iw4TechniqueTypeNames =
        {
            "depth prepass", "build float z", "build shadowmap depth", "build shadowmap color",
            "unlit", "emissive", "emissive dfog", "emissive shadow", "emissive shadow dfog",
            "lit", "lit dfog", "lit sun", "lit sun dfog", "lit sun shadow", "lit sun shadow dfog",
            "lit spot", "lit spot dfog", "lit spot shadow", "lit spot shadow dfog",
            "lit omni", "lit omni dfog", "lit omni shadow", "lit omni shadow dfog",
            "lit instanced", "lit instanced dfog", "lit instanced sun", "lit instanced sun dfog",
            "lit instanced sun shadow", "lit instanced sun shadow dfog",
            "lit instanced spot", "lit instanced spot dfog", "lit instanced spot shadow",
            "lit instanced spot shadow dfog", "lit instanced omni", "lit instanced omni dfog",
            "lit instanced omni shadow", "lit instanced omni shadow dfog",
        };

        private static string TechniqueSlotName(int index)
            => index >= 0 && index < Iw4TechniqueTypeNames.Length ? Iw4TechniqueTypeNames[index] : $"technique[{index}]";

        private static TechSetAsset ToTechSet(FastFileLib.Iw4.MaterialTechniqueSet ts)
        {
            // The reader reads the technique pointers but not the (shared) technique bodies, so the
            // per-slot name comes from the slot index (= technique type), not the body. Active slots
            // are non-null pointers.
            var slots = ts.Techniques ?? Array.Empty<FastFileLib.Iw4.ZonePointer<FastFileLib.Iw4.MaterialTechnique>>();
            var techniques = new TechniqueInfo[slots.Length];
            int active = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                bool present = slots[i] is { Kind: not FastFileLib.Iw4.PointerKind.Null };
                techniques[i] = new TechniqueInfo
                {
                    IsPresent = present,
                    Name = present ? TechniqueSlotName(i) : string.Empty,
                };
                if (present) active++;
            }

            return new TechSetAsset
            {
                Name = ts.Name,
                WorldVertFormat = (byte)ts.WorldVertexFormat,
                HasBeenUploaded = ts.HasBeenUploaded,
                ActiveTechniqueCount = active,
                Techniques = techniques,
                StartOffset = ts.Offset,
                EndOffset = 0,
                AdditionalData = "IW4 pointer-walk",
            };
        }

        private static MaterialAsset ToMaterial(FastFileLib.Iw4.Material m)
        {
            var mat = new MaterialAsset
            {
                Name = m.Info?.Name ?? string.Empty,
                TextureCount = m.TextureCount,
                ConstantCount = m.ConstantCount,
                StateBitsCount = m.StateBitsCount,
                StartOfFileHeader = m.Offset,
                EndOffset = 0,
                IsStructuredView = true,
                AdditionalData = "IW4 pointer-walk",
            };

            // Technique set / texture table / constants resolve only when stored inline; shared
            // ones are offset (block) pointers the reader doesn't dereference, so they stay empty
            // and only the root counts above are shown.
            var ts = m.TechniqueSet is { IsResolved: true, Result: not null } ? m.TechniqueSet.Result : null;
            mat.TechniqueSetName = ts?.Name ?? string.Empty;
            if (ts?.Techniques != null)
                for (int i = 0; i < ts.Techniques.Length; i++)
                    if (ts.Techniques[i] is { Kind: not FastFileLib.Iw4.PointerKind.Null })
                        mat.Techniques.Add(TechniqueSlotName(i));

            if (m.TextureTable is { IsResolved: true, Result: not null } table)
                foreach (var t in table.Result)
                {
                    string img =
                        t.Info?.Image is { IsResolved: true, Result: not null } gi ? gi.Result.Name :
                        t.Info?.Water is { IsResolved: true } ? "<water>" : "<shared>";
                    mat.Textures.Add($"{t.Semantic} : {img}");
                }

            if (m.ConstantTable is { IsResolved: true, Result: not null } consts)
                foreach (var c in consts.Result)
                    mat.Constants.Add(
                        $"{c.Name} = ({c.Literal.A:0.###}, {c.Literal.R:0.###}, {c.Literal.G:0.###}, {c.Literal.B:0.###})");

            return mat;
        }

        // ---- StructuredDataDef (IW4 raw/mp/*.def layout dump) ----

        private static StructuredDataDefAsset ToStructuredDataDef(FastFileLib.Iw4.StructuredDataDefSet sd)
        {
            var defs = sd.DefsPtr is { IsResolved: true, Result: not null }
                ? sd.DefsPtr.Result
                : Array.Empty<FastFileLib.Iw4.StructuredDataDef>();

            return new StructuredDataDefAsset
            {
                Name = sd.Name,
                Offset = sd.Offset,
                DefCount = sd.DefCount,
                EnumCount = defs.Sum(d => d.EnumCount),
                StructCount = defs.Sum(d => d.StructCount),
                DumpText = RenderStructuredDataDump(sd, defs),
            };
        }

        /// <summary>Renders a StructuredDataType (the spec's tagged union) to a readable type string.</summary>
        private static string TypeString(FastFileLib.Iw4.StructuredDataType? t)
        {
            if (t == null) return "?";
            return t.Type switch
            {
                FastFileLib.Iw4.StructuredDataTypeCategory.DataInt => "int",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataByte => "byte",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataBool => "bool",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataString => $"string[{t.UnionValue}]",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataEnum => $"enum[{t.UnionValue}]",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataStruct => $"struct[{t.UnionValue}]",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataIndexedArray => $"indexedArray[{t.UnionValue}]",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataEnumArray => $"enumArray[{t.UnionValue}]",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataFloat => "float",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataShort => "short",
                FastFileLib.Iw4.StructuredDataTypeCategory.DataCount => "count",
                _ => $"type{(int)t.Type}",
            };
        }

        private static T[] Resolve<T>(FastFileLib.Iw4.ZonePointer<T[]>? p)
            => p is { IsResolved: true, Result: not null } ? p.Result : Array.Empty<T>();

        private static string RenderStructuredDataDump(
            FastFileLib.Iw4.StructuredDataDefSet sd, FastFileLib.Iw4.StructuredDataDef[] defs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// {sd.Name}");
            sb.AppendLine($"// StructuredDataDefSet  —  defCount = {sd.DefCount}");
            sb.AppendLine();

            for (int di = 0; di < defs.Length; di++)
            {
                var def = defs[di];
                sb.AppendLine($"def #{di}   version={def.Version}   formatChecksum=0x{def.FormatChecksum:X8}   size={def.Size}");

                var enums = Resolve(def.EnumsPtr);
                sb.AppendLine($"  enums ({enums.Length}):");
                for (int ei = 0; ei < enums.Length; ei++)
                {
                    var entries = Resolve(enums[ei].EntriesPtr);
                    sb.AppendLine($"    enum[{ei}]  ({entries.Length} entries):");
                    foreach (var entry in entries)
                        sb.AppendLine($"      {S(entry.StringPtr)} = {entry.Index}");
                }

                var structs = Resolve(def.StructsPtr);
                sb.AppendLine($"  structs ({structs.Length}):");
                for (int si = 0; si < structs.Length; si++)
                {
                    var st = structs[si];
                    var props = Resolve(st.PropertiesPtr);
                    sb.AppendLine($"    struct[{si}]  size={st.Size}  bitOffset={st.BitOffset}  ({props.Length} properties):");
                    foreach (var prop in props)
                        sb.AppendLine($"      +0x{prop.Offset:X}  {S(prop.NamePtr)} : {TypeString(prop.Type)}");
                }

                var indexedArrays = Resolve(def.IndexedArraysPtr);
                if (indexedArrays.Length > 0)
                {
                    sb.AppendLine($"  indexedArrays ({indexedArrays.Length}):");
                    for (int k = 0; k < indexedArrays.Length; k++)
                    {
                        var ia = indexedArrays[k];
                        sb.AppendLine($"    indexedArray[{k}]  size={ia.ArraySize}  element={TypeString(ia.ElementType)}  elementSize={ia.ElementSize}");
                    }
                }

                var enumedArrays = Resolve(def.EnumedArraysPtr);
                if (enumedArrays.Length > 0)
                {
                    sb.AppendLine($"  enumedArrays ({enumedArrays.Length}):");
                    for (int k = 0; k < enumedArrays.Length; k++)
                    {
                        var ea = enumedArrays[k];
                        sb.AppendLine($"    enumArray[{k}]  enum={ea.EnumIndex}  element={TypeString(ea.ElementType)}  elementSize={ea.ElementSize}");
                    }
                }

                sb.AppendLine($"  rootType: {TypeString(def.RootType)}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
