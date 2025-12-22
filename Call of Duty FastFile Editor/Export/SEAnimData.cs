using System;
using System.Collections.Generic;
using System.Numerics;

namespace Call_of_Duty_FastFile_Editor.Export
{
    /// <summary>
    /// Animation type for SEAnim bones.
    /// </summary>
    public enum SEAnimType : byte
    {
        /// <summary>Bones are set to their absolute position/rotation.</summary>
        Absolute = 0,
        /// <summary>Bones are added to the base pose.</summary>
        Additive = 1,
        /// <summary>Bones are relative to a reference frame.</summary>
        Relative = 2,
        /// <summary>First bone contains delta movement data.</summary>
        Delta = 3
    }

    /// <summary>
    /// Animation flags for SEAnim.
    /// </summary>
    [Flags]
    public enum SEAnimFlags : byte
    {
        None = 0,
        /// <summary>Animation should loop.</summary>
        Looped = 0x01
    }

    /// <summary>
    /// Data presence flags indicating which data blocks exist.
    /// </summary>
    [Flags]
    public enum SEAnimPresenceFlags : byte
    {
        None = 0,
        /// <summary>Bone location keyframes present.</summary>
        BoneLocation = 0x01,
        /// <summary>Bone rotation keyframes present.</summary>
        BoneRotation = 0x02,
        /// <summary>Bone scale keyframes present.</summary>
        BoneScale = 0x04,
        /// <summary>Note data present.</summary>
        Note = 0x40,
        /// <summary>Custom data block present.</summary>
        Custom = 0x80
    }

    /// <summary>
    /// Data property flags for SEAnim.
    /// </summary>
    [Flags]
    public enum SEAnimPropertyFlags : byte
    {
        None = 0,
        /// <summary>Use double precision (8 bytes) for vectors and quaternions.</summary>
        HighPrecision = 0x01
    }

    /// <summary>
    /// Bone flags in SEAnim.
    /// </summary>
    public enum SEAnimBoneFlags : byte
    {
        /// <summary>Normal bone.</summary>
        Default = 0,
        /// <summary>Cosmetic bone (not essential for gameplay).</summary>
        Cosmetic = 1
    }

    /// <summary>
    /// Represents a location keyframe.
    /// </summary>
    public class SEAnimLocationKey
    {
        public uint Frame { get; set; }
        public Vector3 Location { get; set; }

        public SEAnimLocationKey(uint frame, Vector3 location)
        {
            Frame = frame;
            Location = location;
        }

        public SEAnimLocationKey(uint frame, float x, float y, float z)
        {
            Frame = frame;
            Location = new Vector3(x, y, z);
        }
    }

    /// <summary>
    /// Represents a rotation keyframe.
    /// </summary>
    public class SEAnimRotationKey
    {
        public uint Frame { get; set; }
        public Quaternion Rotation { get; set; }

        public SEAnimRotationKey(uint frame, Quaternion rotation)
        {
            Frame = frame;
            Rotation = rotation;
        }

        public SEAnimRotationKey(uint frame, float x, float y, float z, float w)
        {
            Frame = frame;
            Rotation = new Quaternion(x, y, z, w);
        }
    }

    /// <summary>
    /// Represents a scale keyframe.
    /// </summary>
    public class SEAnimScaleKey
    {
        public uint Frame { get; set; }
        public Vector3 Scale { get; set; }

        public SEAnimScaleKey(uint frame, Vector3 scale)
        {
            Frame = frame;
            Scale = scale;
        }

        public SEAnimScaleKey(uint frame, float x, float y, float z)
        {
            Frame = frame;
            Scale = new Vector3(x, y, z);
        }
    }

    /// <summary>
    /// Represents a bone with its keyframe data.
    /// </summary>
    public class SEAnimBone
    {
        /// <summary>
        /// Bone name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Bone flags (default or cosmetic).
        /// </summary>
        public SEAnimBoneFlags Flags { get; set; } = SEAnimBoneFlags.Default;

        /// <summary>
        /// Location keyframes for this bone.
        /// </summary>
        public List<SEAnimLocationKey> LocationKeys { get; set; } = new List<SEAnimLocationKey>();

        /// <summary>
        /// Rotation keyframes for this bone.
        /// </summary>
        public List<SEAnimRotationKey> RotationKeys { get; set; } = new List<SEAnimRotationKey>();

        /// <summary>
        /// Scale keyframes for this bone.
        /// </summary>
        public List<SEAnimScaleKey> ScaleKeys { get; set; } = new List<SEAnimScaleKey>();

        /// <summary>
        /// Whether this bone has any keyframe data.
        /// </summary>
        public bool HasData => LocationKeys.Count > 0 || RotationKeys.Count > 0 || ScaleKeys.Count > 0;
    }

    /// <summary>
    /// Represents a note (event) at a specific frame.
    /// </summary>
    public class SEAnimNote
    {
        public uint Frame { get; set; }
        public string Name { get; set; } = string.Empty;

        public SEAnimNote(uint frame, string name)
        {
            Frame = frame;
            Name = name;
        }
    }

    /// <summary>
    /// Represents a complete SEAnim animation.
    /// </summary>
    public class SEAnimAnimation
    {
        /// <summary>
        /// Animation type (absolute, additive, relative, delta).
        /// </summary>
        public SEAnimType AnimType { get; set; } = SEAnimType.Absolute;

        /// <summary>
        /// Animation flags.
        /// </summary>
        public SEAnimFlags Flags { get; set; } = SEAnimFlags.None;

        /// <summary>
        /// Whether to use high precision (double) for vectors/quaternions.
        /// </summary>
        public bool HighPrecision { get; set; } = false;

        /// <summary>
        /// Animation framerate in frames per second.
        /// </summary>
        public float Framerate { get; set; } = 30.0f;

        /// <summary>
        /// Total number of frames in the animation.
        /// </summary>
        public uint FrameCount { get; set; }

        /// <summary>
        /// List of bones with their animation data.
        /// </summary>
        public List<SEAnimBone> Bones { get; set; } = new List<SEAnimBone>();

        /// <summary>
        /// List of animation notes/events.
        /// </summary>
        public List<SEAnimNote> Notes { get; set; } = new List<SEAnimNote>();

        /// <summary>
        /// Creates a new SEAnimAnimation.
        /// </summary>
        public SEAnimAnimation()
        {
        }

        /// <summary>
        /// Creates an SEAnimAnimation from XAnimParts data with extracted keyframes.
        /// </summary>
        /// <param name="xanim">The XAnimParts to convert.</param>
        /// <param name="zoneData">Optional zone data for keyframe extraction.</param>
        /// <returns>An SEAnimAnimation with extracted or identity keyframes.</returns>
        public static SEAnimAnimation FromXAnim(Models.XAnimParts xanim, byte[]? zoneData = null)
        {
            var anim = new SEAnimAnimation
            {
                AnimType = xanim.HasDelta ? SEAnimType.Delta : SEAnimType.Absolute,
                Flags = xanim.IsLooping ? SEAnimFlags.Looped : SEAnimFlags.None,
                Framerate = float.IsNaN(xanim.Framerate) ? 30.0f : xanim.Framerate,
                FrameCount = xanim.NumFrames > 0 ? (uint)xanim.NumFrames : 1u
            };

            // Try to extract actual keyframe data if zone data is provided
            Models.XAnimExtractedData? extractedData = null;
            if (zoneData != null)
            {
                var parser = new ZoneParsers.XAnimDataParser(zoneData, isBigEndian: true);
                extractedData = parser.ExtractAnimationData(xanim);
            }

            // Convert extracted data or create identity keyframes
            if (extractedData?.IsValid == true && extractedData.Bones.Count > 0)
            {
                // Use extracted keyframe data
                foreach (var boneData in extractedData.Bones)
                {
                    var bone = new SEAnimBone { Name = boneData.BoneName };

                    // Convert rotation keyframes
                    if (boneData.Rotation != null)
                    {
                        foreach (var key in boneData.Rotation.Keys)
                        {
                            bone.RotationKeys.Add(new SEAnimRotationKey(key.Frame, key.Rotation));
                        }
                    }

                    // Convert translation keyframes
                    if (boneData.Translation != null)
                    {
                        foreach (var key in boneData.Translation.Keys)
                        {
                            bone.LocationKeys.Add(new SEAnimLocationKey(key.Frame, key.Position));
                        }
                    }

                    // Ensure at least one keyframe per bone
                    if (bone.RotationKeys.Count == 0)
                        bone.RotationKeys.Add(new SEAnimRotationKey(0, Quaternion.Identity));
                    if (bone.LocationKeys.Count == 0)
                        bone.LocationKeys.Add(new SEAnimLocationKey(0, Vector3.Zero));

                    anim.Bones.Add(bone);
                }

                // Add notes from extracted data
                foreach (var notify in extractedData.Notifies)
                {
                    anim.Notes.Add(new SEAnimNote(notify.Frame, notify.Name));
                }
            }
            else
            {
                // Fallback: Add bones with identity keyframes
                foreach (var boneName in xanim.BoneNames)
                {
                    var bone = new SEAnimBone { Name = boneName };

                    // Add identity rotation at frame 0 (no rotation)
                    bone.RotationKeys.Add(new SEAnimRotationKey(0, Quaternion.Identity));

                    // Add identity location at frame 0 (origin)
                    bone.LocationKeys.Add(new SEAnimLocationKey(0, Vector3.Zero));

                    anim.Bones.Add(bone);
                }
            }

            // If no bones were extracted, add a placeholder root bone
            if (anim.Bones.Count == 0)
            {
                var root = new SEAnimBone { Name = "root" };
                root.RotationKeys.Add(new SEAnimRotationKey(0, Quaternion.Identity));
                root.LocationKeys.Add(new SEAnimLocationKey(0, Vector3.Zero));
                anim.Bones.Add(root);
            }

            return anim;
        }

        /// <summary>
        /// Calculates the presence flags based on bone data.
        /// </summary>
        public SEAnimPresenceFlags GetPresenceFlags()
        {
            SEAnimPresenceFlags flags = SEAnimPresenceFlags.None;

            foreach (var bone in Bones)
            {
                if (bone.LocationKeys.Count > 0)
                    flags |= SEAnimPresenceFlags.BoneLocation;
                if (bone.RotationKeys.Count > 0)
                    flags |= SEAnimPresenceFlags.BoneRotation;
                if (bone.ScaleKeys.Count > 0)
                    flags |= SEAnimPresenceFlags.BoneScale;
            }

            if (Notes.Count > 0)
                flags |= SEAnimPresenceFlags.Note;

            return flags;
        }
    }
}
