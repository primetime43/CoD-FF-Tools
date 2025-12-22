using System;
using System.Collections.Generic;
using System.Numerics;

namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Represents a position (translation) keyframe.
    /// </summary>
    public class XAnimTranslationKey
    {
        /// <summary>
        /// Frame number for this keyframe.
        /// </summary>
        public ushort Frame { get; set; }

        /// <summary>
        /// Position value (dequantized from mins/size).
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Raw quantized value before dequantization (for debugging).
        /// </summary>
        public Vector3 RawValue { get; set; }
    }

    /// <summary>
    /// Represents a rotation keyframe.
    /// </summary>
    public class XAnimRotationKey
    {
        /// <summary>
        /// Frame number for this keyframe.
        /// </summary>
        public ushort Frame { get; set; }

        /// <summary>
        /// Rotation quaternion (normalized).
        /// </summary>
        public Quaternion Rotation { get; set; }

        /// <summary>
        /// Raw 16-bit compressed values (for debugging).
        /// </summary>
        public short[] RawValues { get; set; } = new short[4];
    }

    /// <summary>
    /// Translation frame data with quantization parameters.
    /// Based on XAnimPartTransFrames structure.
    /// </summary>
    public class XAnimPartTransData
    {
        /// <summary>
        /// Minimum values for dequantization (world space origin).
        /// </summary>
        public float[] Mins { get; set; } = new float[3];

        /// <summary>
        /// Size values for dequantization (world space scale).
        /// </summary>
        public float[] Size { get; set; } = new float[3];

        /// <summary>
        /// List of translation keyframes.
        /// </summary>
        public List<XAnimTranslationKey> Keys { get; set; } = new List<XAnimTranslationKey>();

        /// <summary>
        /// Dequantizes a value using the stored mins and size.
        /// Formula: position = mins + (quantized / maxQuantValue) * size
        /// </summary>
        /// <param name="quantized">The quantized value (0-65535 for 16-bit).</param>
        /// <param name="axis">Axis index (0=X, 1=Y, 2=Z).</param>
        /// <param name="is16Bit">True for 16-bit quantization, false for 8-bit.</param>
        /// <returns>Dequantized world-space value.</returns>
        public float Dequantize(int quantized, int axis, bool is16Bit = true)
        {
            float maxValue = is16Bit ? 65535.0f : 255.0f;
            return Mins[axis] + (quantized / maxValue) * Size[axis];
        }
    }

    /// <summary>
    /// Rotation data for a bone.
    /// </summary>
    public class XAnimPartRotData
    {
        /// <summary>
        /// List of rotation keyframes.
        /// </summary>
        public List<XAnimRotationKey> Keys { get; set; } = new List<XAnimRotationKey>();
    }

    /// <summary>
    /// Animation data for a single bone.
    /// </summary>
    public class XAnimBoneData
    {
        /// <summary>
        /// Bone index in the skeleton.
        /// </summary>
        public int BoneIndex { get; set; }

        /// <summary>
        /// Bone name (resolved from script string).
        /// </summary>
        public string BoneName { get; set; } = string.Empty;

        /// <summary>
        /// Translation/position data for this bone.
        /// </summary>
        public XAnimPartTransData? Translation { get; set; }

        /// <summary>
        /// Rotation data for this bone.
        /// </summary>
        public XAnimPartRotData? Rotation { get; set; }

        /// <summary>
        /// Whether this bone has any keyframe data.
        /// </summary>
        public bool HasData => (Translation?.Keys.Count > 0) || (Rotation?.Keys.Count > 0);
    }

    /// <summary>
    /// Animation notify event.
    /// </summary>
    public class XAnimNotify
    {
        /// <summary>
        /// Frame when the notify occurs.
        /// </summary>
        public ushort Frame { get; set; }

        /// <summary>
        /// Notify name/type.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Delta movement data for the root bone.
    /// Used for animations that have root motion.
    /// </summary>
    public class XAnimDeltaData
    {
        /// <summary>
        /// Delta translations per frame.
        /// </summary>
        public List<XAnimTranslationKey> Translations { get; set; } = new List<XAnimTranslationKey>();

        /// <summary>
        /// Delta rotations per frame.
        /// </summary>
        public List<XAnimRotationKey> Rotations { get; set; } = new List<XAnimRotationKey>();
    }

    /// <summary>
    /// Complete extracted animation data for an XAnimParts asset.
    /// </summary>
    public class XAnimExtractedData
    {
        /// <summary>
        /// Reference to the original XAnimParts.
        /// </summary>
        public XAnimParts Source { get; set; } = null!;

        /// <summary>
        /// Per-bone animation data.
        /// </summary>
        public List<XAnimBoneData> Bones { get; set; } = new List<XAnimBoneData>();

        /// <summary>
        /// Notify events.
        /// </summary>
        public List<XAnimNotify> Notifies { get; set; } = new List<XAnimNotify>();

        /// <summary>
        /// Delta (root motion) data if present.
        /// </summary>
        public XAnimDeltaData? DeltaData { get; set; }

        /// <summary>
        /// Whether extraction was successful.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if extraction failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Total number of keyframes extracted.
        /// </summary>
        public int TotalKeyframes
        {
            get
            {
                int count = 0;
                foreach (var bone in Bones)
                {
                    if (bone.Translation != null)
                        count += bone.Translation.Keys.Count;
                    if (bone.Rotation != null)
                        count += bone.Rotation.Keys.Count;
                }
                return count;
            }
        }
    }

    /// <summary>
    /// Helper class for quaternion decompression.
    /// CoD uses 16-bit signed integers to store quaternion components.
    /// </summary>
    public static class QuaternionCompression
    {
        /// <summary>
        /// Maximum value for 16-bit quaternion component.
        /// </summary>
        private const float MaxQuatValue = 32767.0f;

        /// <summary>
        /// Decompresses a quaternion from four 16-bit signed integers.
        /// </summary>
        /// <param name="x">X component (-32768 to 32767).</param>
        /// <param name="y">Y component (-32768 to 32767).</param>
        /// <param name="z">Z component (-32768 to 32767).</param>
        /// <param name="w">W component (-32768 to 32767).</param>
        /// <returns>Normalized quaternion.</returns>
        public static Quaternion Decompress(short x, short y, short z, short w)
        {
            float fx = x / MaxQuatValue;
            float fy = y / MaxQuatValue;
            float fz = z / MaxQuatValue;
            float fw = w / MaxQuatValue;

            return Quaternion.Normalize(new Quaternion(fx, fy, fz, fw));
        }

        /// <summary>
        /// Decompresses a quaternion from three 16-bit signed integers.
        /// The fourth component (W) is reconstructed from the constraint |q| = 1.
        /// </summary>
        /// <param name="x">X component.</param>
        /// <param name="y">Y component.</param>
        /// <param name="z">Z component.</param>
        /// <returns>Normalized quaternion with reconstructed W.</returns>
        public static Quaternion DecompressSmallest3(short x, short y, short z)
        {
            float fx = x / MaxQuatValue;
            float fy = y / MaxQuatValue;
            float fz = z / MaxQuatValue;

            // Reconstruct W: w = sqrt(1 - x^2 - y^2 - z^2)
            float wSquared = 1.0f - (fx * fx + fy * fy + fz * fz);
            float fw = wSquared > 0 ? MathF.Sqrt(wSquared) : 0;

            return Quaternion.Normalize(new Quaternion(fx, fy, fz, fw));
        }
    }
}
