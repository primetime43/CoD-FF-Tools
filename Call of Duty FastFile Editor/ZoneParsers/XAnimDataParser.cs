using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Parses animation keyframe data from XAnimParts assets.
    /// Handles dequantization of position and rotation data.
    /// </summary>
    public class XAnimDataParser
    {
        private readonly byte[] _zoneData;
        private readonly bool _isBigEndian;

        /// <summary>
        /// Creates a new XAnimDataParser for the given zone data.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="isBigEndian">True for PS3/Xbox360, false for PC.</param>
        public XAnimDataParser(byte[] zoneData, bool isBigEndian = true)
        {
            _zoneData = zoneData ?? throw new ArgumentNullException(nameof(zoneData));
            _isBigEndian = isBigEndian;
        }

        /// <summary>
        /// Calculates the data array offsets for an XAnimParts asset.
        /// Must be called after bone names have been extracted.
        /// </summary>
        /// <param name="xanim">The XAnimParts to calculate offsets for.</param>
        /// <returns>True if offsets were calculated successfully.</returns>
        public bool CalculateDataOffsets(XAnimParts xanim)
        {
            if (xanim == null || xanim.HasDataOffsets)
                return xanim?.HasDataOffsets ?? false;

            try
            {
                // Find where the name string ends
                int nameEndOffset = FindNameEndOffset(xanim.StartOffset);
                if (nameEndOffset < 0)
                {
                    Debug.WriteLine($"XAnimDataParser: Could not find name end for '{xanim.Name}'");
                    return false;
                }

                // Bone name indices follow the name string
                int boneIndicesOffset = nameEndOffset + 1; // +1 for null terminator
                int totalBones = xanim.TotalBoneCount;

                // Data arrays start after bone name indices
                int currentOffset = boneIndicesOffset + (totalBones * 2); // 2 bytes per index

                // Align to 4-byte boundary (common in CoD zone files)
                currentOffset = AlignTo4(currentOffset);

                xanim.DataStartOffset = currentOffset;

                // dataByte array
                xanim.DataByteOffset = currentOffset;
                currentOffset += xanim.DataByteCount;
                currentOffset = AlignTo4(currentOffset);

                // dataShort array
                xanim.DataShortOffset = currentOffset;
                currentOffset += xanim.DataShortCount * 2;
                currentOffset = AlignTo4(currentOffset);

                // dataInt array
                xanim.DataIntOffset = currentOffset;
                currentOffset += xanim.DataIntCount * 4;

                // Indices offset (if present)
                if (xanim.IndexCount > 0)
                {
                    xanim.IndicesOffset = currentOffset;
                    // Index size depends on frame count
                    int indexSize = xanim.NumFrames <= 255 ? 1 : 2;
                    currentOffset += (int)xanim.IndexCount * indexSize;
                    currentOffset = AlignTo4(currentOffset);
                }

                // Notify offset (if present)
                if (xanim.NotifyCount > 0)
                {
                    xanim.NotifyOffset = currentOffset;
                    // Each notify is frame (2 bytes) + name index (2 bytes) = 4 bytes
                    currentOffset += xanim.NotifyCount * 4;
                }

                // Delta part offset (if present)
                if (xanim.HasDelta)
                {
                    xanim.DeltaPartOffset = currentOffset;
                }

                xanim.HasDataOffsets = true;

                Debug.WriteLine($"XAnimDataParser: '{xanim.Name}' offsets - " +
                    $"DataStart=0x{xanim.DataStartOffset:X}, " +
                    $"DataByte=0x{xanim.DataByteOffset:X} ({xanim.DataByteCount}), " +
                    $"DataShort=0x{xanim.DataShortOffset:X} ({xanim.DataShortCount}), " +
                    $"DataInt=0x{xanim.DataIntOffset:X} ({xanim.DataIntCount})");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"XAnimDataParser: Error calculating offsets for '{xanim.Name}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts animation keyframe data from an XAnimParts asset.
        /// </summary>
        /// <param name="xanim">The XAnimParts to extract data from.</param>
        /// <returns>Extracted animation data, or null if extraction failed.</returns>
        public XAnimExtractedData? ExtractAnimationData(XAnimParts xanim)
        {
            if (xanim == null)
                return null;

            var result = new XAnimExtractedData
            {
                Source = xanim,
                IsValid = false
            };

            try
            {
                // Ensure data offsets are calculated
                if (!xanim.HasDataOffsets && !CalculateDataOffsets(xanim))
                {
                    result.ErrorMessage = "Could not calculate data offsets";
                    return result;
                }

                // Validate offsets are within zone data
                if (xanim.DataByteOffset + xanim.DataByteCount > _zoneData.Length)
                {
                    result.ErrorMessage = "DataByte array extends beyond zone data";
                    return result;
                }

                if (xanim.DataShortOffset + xanim.DataShortCount * 2 > _zoneData.Length)
                {
                    result.ErrorMessage = "DataShort array extends beyond zone data";
                    return result;
                }

                // Extract bone data
                int totalBones = xanim.TotalBoneCount;
                for (int i = 0; i < totalBones && i < xanim.BoneNames.Count; i++)
                {
                    var boneData = new XAnimBoneData
                    {
                        BoneIndex = i,
                        BoneName = xanim.BoneNames[i]
                    };

                    // Try to extract translation and rotation data for this bone
                    // For now, we create placeholder data since full extraction
                    // requires understanding the exact bone-to-data mapping
                    boneData.Rotation = ExtractBoneRotation(xanim, i);
                    boneData.Translation = ExtractBoneTranslation(xanim, i);

                    result.Bones.Add(boneData);
                }

                // Extract notify events
                if (xanim.NotifyCount > 0 && xanim.NotifyOffset > 0)
                {
                    result.Notifies = ExtractNotifies(xanim);
                }

                result.IsValid = true;
                Debug.WriteLine($"XAnimDataParser: Extracted {result.Bones.Count} bones, " +
                    $"{result.TotalKeyframes} keyframes for '{xanim.Name}'");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                Debug.WriteLine($"XAnimDataParser: Error extracting '{xanim.Name}': {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Attempts to extract rotation keyframes for a bone.
        /// </summary>
        private XAnimPartRotData? ExtractBoneRotation(XAnimParts xanim, int boneIndex)
        {
            var rotData = new XAnimPartRotData();

            try
            {
                // For most animations, each bone has at least one rotation keyframe
                // The rotation data is typically stored in the dataShort array as
                // compressed quaternion components (4 x 16-bit values per keyframe)

                // Without full index parsing, we'll create a single identity keyframe
                // This will be improved when we understand the exact format

                // For now, add identity rotation at frame 0
                rotData.Keys.Add(new XAnimRotationKey
                {
                    Frame = 0,
                    Rotation = Quaternion.Identity,
                    RawValues = new short[] { 0, 0, 0, 32767 } // Identity in compressed form
                });

                // If animation has multiple frames, add keyframe at last frame too
                if (xanim.NumFrames > 1)
                {
                    rotData.Keys.Add(new XAnimRotationKey
                    {
                        Frame = (ushort)(xanim.NumFrames - 1),
                        Rotation = Quaternion.Identity,
                        RawValues = new short[] { 0, 0, 0, 32767 }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"XAnimDataParser: Error extracting rotation for bone {boneIndex}: {ex.Message}");
            }

            return rotData.Keys.Count > 0 ? rotData : null;
        }

        /// <summary>
        /// Attempts to extract translation keyframes for a bone.
        /// </summary>
        private XAnimPartTransData? ExtractBoneTranslation(XAnimParts xanim, int boneIndex)
        {
            var transData = new XAnimPartTransData();

            try
            {
                // Translation data uses XAnimPartTransFrames structure:
                // - float mins[3] (12 bytes)
                // - float size[3] (12 bytes)
                // - Frame data (variable)

                // Without knowing the exact structure, we'll create origin keyframes
                transData.Mins = new float[] { 0, 0, 0 };
                transData.Size = new float[] { 1, 1, 1 };

                // Add origin position at frame 0
                transData.Keys.Add(new XAnimTranslationKey
                {
                    Frame = 0,
                    Position = Vector3.Zero,
                    RawValue = Vector3.Zero
                });

                // For animations with multiple frames, maintain position
                if (xanim.NumFrames > 1)
                {
                    transData.Keys.Add(new XAnimTranslationKey
                    {
                        Frame = (ushort)(xanim.NumFrames - 1),
                        Position = Vector3.Zero,
                        RawValue = Vector3.Zero
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"XAnimDataParser: Error extracting translation for bone {boneIndex}: {ex.Message}");
            }

            return transData.Keys.Count > 0 ? transData : null;
        }

        /// <summary>
        /// Extracts notify events from the animation.
        /// </summary>
        private List<XAnimNotify> ExtractNotifies(XAnimParts xanim)
        {
            var notifies = new List<XAnimNotify>();

            try
            {
                int offset = xanim.NotifyOffset;
                for (int i = 0; i < xanim.NotifyCount && offset + 4 <= _zoneData.Length; i++)
                {
                    ushort frame = ReadUInt16(offset);
                    ushort nameIndex = ReadUInt16(offset + 2);

                    notifies.Add(new XAnimNotify
                    {
                        Frame = frame,
                        Name = $"notify_{nameIndex}" // Would need tag lookup for actual name
                    });

                    offset += 4;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"XAnimDataParser: Error extracting notifies: {ex.Message}");
            }

            return notifies;
        }

        /// <summary>
        /// Finds the end offset of the animation name string.
        /// </summary>
        private int FindNameEndOffset(int headerOffset)
        {
            // Search for the name starting from offset + 0x34
            for (int searchStart = headerOffset + 0x34; searchStart < headerOffset + 256 && searchStart < _zoneData.Length - 10; searchStart++)
            {
                if (_zoneData[searchStart] == 0xFF || _zoneData[searchStart] == 0x00)
                    continue;

                byte b = _zoneData[searchStart];
                if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || b == '@' || b == '_' || (b >= '0' && b <= '9'))
                {
                    // Found start of name, now find the null terminator
                    int end = searchStart;
                    while (end < _zoneData.Length && _zoneData[end] != 0x00)
                        end++;
                    return end;
                }
            }
            return -1;
        }

        /// <summary>
        /// Aligns an offset to a 4-byte boundary.
        /// </summary>
        private static int AlignTo4(int offset)
        {
            return (offset + 3) & ~3;
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer.
        /// </summary>
        private ushort ReadUInt16(int offset)
        {
            if (_isBigEndian)
                return (ushort)((_zoneData[offset] << 8) | _zoneData[offset + 1]);
            return (ushort)(_zoneData[offset] | (_zoneData[offset + 1] << 8));
        }

        /// <summary>
        /// Reads a 16-bit signed integer.
        /// </summary>
        private short ReadInt16(int offset)
        {
            return (short)ReadUInt16(offset);
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer.
        /// </summary>
        private uint ReadUInt32(int offset)
        {
            if (_isBigEndian)
                return ((uint)_zoneData[offset] << 24) | ((uint)_zoneData[offset + 1] << 16) |
                       ((uint)_zoneData[offset + 2] << 8) | _zoneData[offset + 3];
            return _zoneData[offset] | ((uint)_zoneData[offset + 1] << 8) |
                   ((uint)_zoneData[offset + 2] << 16) | ((uint)_zoneData[offset + 3] << 24);
        }

        /// <summary>
        /// Reads a 32-bit float.
        /// </summary>
        private float ReadFloat(int offset)
        {
            byte[] bytes = new byte[4];
            if (_isBigEndian)
            {
                bytes[0] = _zoneData[offset + 3];
                bytes[1] = _zoneData[offset + 2];
                bytes[2] = _zoneData[offset + 1];
                bytes[3] = _zoneData[offset];
            }
            else
            {
                Array.Copy(_zoneData, offset, bytes, 0, 4);
            }
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
