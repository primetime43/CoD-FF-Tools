using System.Collections.Generic;

namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Represents an XAnim (animation) asset from a zone file.
    ///
    /// Structure for CoD4/WaW:
    /// struct XAnimParts {
    ///   const char *name;              // 4 bytes - FF FF FF FF if inline
    ///   unsigned __int16 dataByteCount;
    ///   unsigned __int16 dataShortCount;
    ///   unsigned __int16 dataIntCount;
    ///   unsigned __int16 randomDataByteCount;
    ///   unsigned __int16 randomDataIntCount;
    ///   unsigned __int16 numframes;
    ///   bool bLoop;
    ///   bool bDelta;
    ///   char boneCount[12];
    ///   char notifyCount;
    ///   char assetType;
    ///   bool pad;
    ///   unsigned int randomDataShortCount;
    ///   unsigned int indexCount;
    ///   float framerate;
    ///   float frequency;
    ///   ScriptString *names;           // 4 bytes - pointer
    ///   char *dataByte;                // 4 bytes - pointer
    ///   __int16 *dataShort;            // 4 bytes - pointer
    ///   int *dataInt;                  // 4 bytes - pointer
    ///   __int16 *randomDataShort;      // 4 bytes - pointer
    ///   char *randomDataByte;          // 4 bytes - pointer
    ///   int *randomDataInt;            // 4 bytes - pointer
    ///   XAnimIndices indices;          // 4 bytes - pointer
    ///   XAnimNotifyInfo *notify;       // 4 bytes - pointer
    ///   XAnimDeltaPart *deltaPart;     // 4 bytes - pointer
    /// };
    /// </summary>
    public class XAnimParts
    {
        /// <summary>
        /// Animation name/path.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Number of data bytes.
        /// </summary>
        public ushort DataByteCount { get; set; }

        /// <summary>
        /// Number of data shorts.
        /// </summary>
        public ushort DataShortCount { get; set; }

        /// <summary>
        /// Number of data ints.
        /// </summary>
        public ushort DataIntCount { get; set; }

        /// <summary>
        /// Number of random data bytes.
        /// </summary>
        public ushort RandomDataByteCount { get; set; }

        /// <summary>
        /// Number of random data ints.
        /// </summary>
        public ushort RandomDataIntCount { get; set; }

        /// <summary>
        /// Number of animation frames.
        /// </summary>
        public ushort NumFrames { get; set; }

        /// <summary>
        /// Whether the animation loops.
        /// </summary>
        public bool IsLooping { get; set; }

        /// <summary>
        /// Whether the animation has delta data.
        /// </summary>
        public bool HasDelta { get; set; }

        /// <summary>
        /// Bone counts array (12 bytes).
        /// </summary>
        public byte[] BoneCounts { get; set; } = new byte[12];

        /// <summary>
        /// Number of notify events.
        /// </summary>
        public byte NotifyCount { get; set; }

        /// <summary>
        /// Asset type identifier.
        /// </summary>
        public byte AssetType { get; set; }

        /// <summary>
        /// Number of random data shorts.
        /// </summary>
        public uint RandomDataShortCount { get; set; }

        /// <summary>
        /// Index count.
        /// </summary>
        public uint IndexCount { get; set; }

        /// <summary>
        /// Animation framerate (frames per second).
        /// </summary>
        public float Framerate { get; set; }

        /// <summary>
        /// Animation frequency.
        /// </summary>
        public float Frequency { get; set; }

        /// <summary>
        /// Total bone count (sum of all boneCount entries).
        /// </summary>
        public int TotalBoneCount => CalculateTotalBoneCount();

        /// <summary>
        /// Animation duration in seconds.
        /// </summary>
        public float Duration => Framerate > 0 ? NumFrames / Framerate : 0;

        /// <summary>
        /// Start offset in the zone file.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// End offset in the zone file.
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// Additional parsing information.
        /// </summary>
        public string AdditionalData { get; set; } = string.Empty;

        /// <summary>
        /// List of bone names used by this animation.
        /// Resolved from script string indices.
        /// </summary>
        public List<string> BoneNames { get; set; } = new List<string>();

        /// <summary>
        /// Offset of the names pointer in the zone file (for debugging).
        /// </summary>
        public int NamesPointerOffset { get; set; }

        /// <summary>
        /// Offset where the animation data starts (after bone name indices).
        /// </summary>
        public int DataStartOffset { get; set; }

        /// <summary>
        /// Offset of the dataByte array in the zone file.
        /// </summary>
        public int DataByteOffset { get; set; }

        /// <summary>
        /// Offset of the dataShort array in the zone file.
        /// </summary>
        public int DataShortOffset { get; set; }

        /// <summary>
        /// Offset of the dataInt array in the zone file.
        /// </summary>
        public int DataIntOffset { get; set; }

        /// <summary>
        /// Offset of the indices data in the zone file.
        /// </summary>
        public int IndicesOffset { get; set; }

        /// <summary>
        /// Offset of notify data in the zone file.
        /// </summary>
        public int NotifyOffset { get; set; }

        /// <summary>
        /// Offset of delta part data in the zone file.
        /// </summary>
        public int DeltaPartOffset { get; set; }

        /// <summary>
        /// Whether animation data offsets have been calculated.
        /// </summary>
        public bool HasDataOffsets { get; set; }

        private int CalculateTotalBoneCount()
        {
            int total = 0;
            if (BoneCounts != null)
            {
                foreach (var count in BoneCounts)
                    total += count;
            }
            return total;
        }

        /// <summary>
        /// Gets a summary of the animation properties.
        /// </summary>
        public string GetSummary()
        {
            return $"{NumFrames} frames, {Framerate:F1} fps, {Duration:F2}s, {TotalBoneCount} bones" +
                   (IsLooping ? ", loops" : "") +
                   (HasDelta ? ", delta" : "");
        }
    }
}
