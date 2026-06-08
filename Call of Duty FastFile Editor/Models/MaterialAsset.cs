namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Represents a Material asset from the zone file.
    /// Materials define how surfaces are rendered, linking textures to shaders.
    /// </summary>
    public class MaterialAsset : IAssetRecordUpdatable
    {
        /// <summary>
        /// Name of the material.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Number of textures used by this material.
        /// </summary>
        public int TextureCount { get; set; }

        /// <summary>
        /// Number of shader constants.
        /// </summary>
        public int ConstantCount { get; set; }

        /// <summary>
        /// State bits count.
        /// </summary>
        public int StateBitsCount { get; set; }

        /// <summary>
        /// Name of the technique set used by this material.
        /// </summary>
        public string TechniqueSetName { get; set; } = string.Empty;

        /// <summary>
        /// Offset where the asset header starts in the zone file.
        /// </summary>
        public int StartOfFileHeader { get; set; }

        /// <summary>
        /// Offset where the asset data ends in the zone file.
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// Position where the file header ends (implements IAssetRecordUpdatable).
        /// </summary>
        public int EndOfFileHeader => EndOffset;

        /// <summary>
        /// Additional parsing information (also used as the "source" — e.g.
        /// "Structure-based (CoD4)" for the pattern scan, "IW4 pointer-walk" for the
        /// MW2 PS3 reader).
        /// </summary>
        public string AdditionalData { get; set; } = string.Empty;

        // --- Optional detail, populated only by the IW4 (MW2 PS3) pointer-walk reader.
        //     The pattern scan leaves these empty (it only recovers the name). ---

        /// <summary>Per-texture lines: "semantic : image" (image is "&lt;shared&gt;" when the
        /// image is an offset pointer the reader doesn't dereference).</summary>
        public List<string> Textures { get; } = new();

        /// <summary>Shader constant lines: "name = (x, y, z, w)".</summary>
        public List<string> Constants { get; } = new();

        /// <summary>Active technique-set slot names (technique type per non-null slot).</summary>
        public List<string> Techniques { get; } = new();

        /// <summary>True when the rich detail above came from the IW4 pointer-walk.</summary>
        public bool IsStructuredView { get; set; }

        public void UpdateAssetRecord(ref ZoneAssetRecord record)
        {
            record.Name = Name;
            record.AssetRecordEndOffset = EndOffset;
            record.Content = $"Textures: {TextureCount}, TechSet: {TechniqueSetName}";
        }
    }
}
