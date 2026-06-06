namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Editor-side view model for a StructuredDataDefSet asset (MW2 / IW4).
    ///
    /// The structureddatadef asset stores the data-structure + enum layouts the game uses (defined
    /// under raw/mp/*.def). The original source format isn't shipped, so this is a read-only
    /// <b>dump</b> of the parsed layout: each def's enums (entry name = enum value), structs
    /// (property name : type @ byte offset), indexed/enumed arrays, and the root type. The IW4
    /// pointer-walk reader (<c>FastFileLib.Iw4.StructuredDataReader</c>) produces the structure;
    /// <c>Iw4AssetBridge</c> renders <see cref="DumpText"/> from it.
    /// </summary>
    public class StructuredDataDefAsset
    {
        /// <summary>DefSet name (e.g. <c>mp/playerconstantdata.def</c>).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Zone byte offset of the DefSet asset header.</summary>
        public int Offset { get; set; }

        /// <summary>Number of <c>StructuredDataDef</c>s in the set.</summary>
        public int DefCount { get; set; }

        /// <summary>Total enums across all defs (for the list summary).</summary>
        public int EnumCount { get; set; }

        /// <summary>Total structs across all defs (for the list summary).</summary>
        public int StructCount { get; set; }

        /// <summary>Pre-rendered, human-readable dump of the parsed layout (shown in the viewer).</summary>
        public string DumpText { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
