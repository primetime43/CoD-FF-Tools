using System;
using System.IO;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.Export
{
    /// <summary>
    /// Writes SEAnim animation files.
    /// Based on specification from https://github.com/SE2Dev/SEAnim-Docs
    /// </summary>
    public class SEAnimWriter : IDisposable
    {
        private const string Magic = "SEAnim";
        private const ushort Version = 0x0001;
        private const ushort HeaderSize = 0x1C; // 28 bytes

        private readonly BinaryWriter _writer;
        private readonly SEAnimAnimation _animation;
        private bool _disposed;

        /// <summary>
        /// Creates a new SEAnimWriter for the specified animation.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="animation">The animation data to write.</param>
        public SEAnimWriter(Stream stream, SEAnimAnimation animation)
        {
            _writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            _animation = animation;
        }

        /// <summary>
        /// Writes the complete SEAnim file.
        /// </summary>
        public void Write()
        {
            WriteMagicAndVersion();
            WriteHeader();
            WriteBoneNames();
            // No bone animation modifiers for now
            WriteBoneData();
            WriteNotes();
        }

        /// <summary>
        /// Writes the magic string and version.
        /// </summary>
        private void WriteMagicAndVersion()
        {
            // Magic: "SEAnim" (6 bytes)
            _writer.Write(Encoding.ASCII.GetBytes(Magic));

            // Version: 0x0001 (2 bytes, little-endian)
            _writer.Write(Version);
        }

        /// <summary>
        /// Writes the SEAnim header (28 bytes).
        /// </summary>
        private void WriteHeader()
        {
            var presenceFlags = _animation.GetPresenceFlags();
            var propertyFlags = _animation.HighPrecision
                ? SEAnimPropertyFlags.HighPrecision
                : SEAnimPropertyFlags.None;

            // headerSize: uint16_t (2 bytes)
            _writer.Write(HeaderSize);

            // animType: uint8_t (1 byte)
            _writer.Write((byte)_animation.AnimType);

            // animFlags: uint8_t (1 byte)
            _writer.Write((byte)_animation.Flags);

            // dataPresenceFlags: uint8_t (1 byte)
            _writer.Write((byte)presenceFlags);

            // dataPropertyFlags: uint8_t (1 byte)
            _writer.Write((byte)propertyFlags);

            // reserved1: uint8_t[2] (2 bytes)
            _writer.Write((byte)0);
            _writer.Write((byte)0);

            // framerate: float (4 bytes)
            _writer.Write(_animation.Framerate);

            // frameCount: uint32_t (4 bytes)
            _writer.Write(_animation.FrameCount);

            // boneCount: uint32_t (4 bytes)
            _writer.Write((uint)_animation.Bones.Count);

            // boneAnimModifierCount: uint8_t (1 byte)
            _writer.Write((byte)0);

            // reserved2: uint8_t[3] (3 bytes)
            _writer.Write((byte)0);
            _writer.Write((byte)0);
            _writer.Write((byte)0);

            // noteCount: uint32_t (4 bytes)
            _writer.Write((uint)_animation.Notes.Count);
        }

        /// <summary>
        /// Writes all bone names as null-terminated strings.
        /// </summary>
        private void WriteBoneNames()
        {
            foreach (var bone in _animation.Bones)
            {
                WriteNullTerminatedString(bone.Name);
            }
        }

        /// <summary>
        /// Writes bone keyframe data for all bones.
        /// </summary>
        private void WriteBoneData()
        {
            var presenceFlags = _animation.GetPresenceFlags();

            foreach (var bone in _animation.Bones)
            {
                // Bone flags (1 byte)
                _writer.Write((byte)bone.Flags);

                // Location keyframes (if presence flag set)
                if (presenceFlags.HasFlag(SEAnimPresenceFlags.BoneLocation))
                {
                    WriteFrameCount(bone.LocationKeys.Count);
                    foreach (var key in bone.LocationKeys)
                    {
                        WriteFrame(key.Frame);
                        WriteVector3(key.Location);
                    }
                }

                // Rotation keyframes (if presence flag set)
                if (presenceFlags.HasFlag(SEAnimPresenceFlags.BoneRotation))
                {
                    WriteFrameCount(bone.RotationKeys.Count);
                    foreach (var key in bone.RotationKeys)
                    {
                        WriteFrame(key.Frame);
                        WriteQuaternion(key.Rotation);
                    }
                }

                // Scale keyframes (if presence flag set)
                if (presenceFlags.HasFlag(SEAnimPresenceFlags.BoneScale))
                {
                    WriteFrameCount(bone.ScaleKeys.Count);
                    foreach (var key in bone.ScaleKeys)
                    {
                        WriteFrame(key.Frame);
                        WriteVector3(key.Scale);
                    }
                }
            }
        }

        /// <summary>
        /// Writes animation notes.
        /// </summary>
        private void WriteNotes()
        {
            foreach (var note in _animation.Notes)
            {
                WriteFrame(note.Frame);
                WriteNullTerminatedString(note.Name);
            }
        }

        /// <summary>
        /// Writes a null-terminated string.
        /// </summary>
        private void WriteNullTerminatedString(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            _writer.Write(bytes);
            _writer.Write((byte)0); // Null terminator
        }

        /// <summary>
        /// Writes a frame index using variable-width encoding based on frame count.
        /// </summary>
        private void WriteFrame(uint frame)
        {
            if (_animation.FrameCount <= 0xFF)
            {
                _writer.Write((byte)frame);
            }
            else if (_animation.FrameCount <= 0xFFFF)
            {
                _writer.Write((ushort)frame);
            }
            else
            {
                _writer.Write(frame);
            }
        }

        /// <summary>
        /// Writes a keyframe count using variable-width encoding.
        /// </summary>
        private void WriteFrameCount(int count)
        {
            if (_animation.FrameCount <= 0xFF)
            {
                _writer.Write((byte)count);
            }
            else if (_animation.FrameCount <= 0xFFFF)
            {
                _writer.Write((ushort)count);
            }
            else
            {
                _writer.Write((uint)count);
            }
        }

        /// <summary>
        /// Writes a Vector3 (location or scale).
        /// </summary>
        private void WriteVector3(System.Numerics.Vector3 vec)
        {
            if (_animation.HighPrecision)
            {
                _writer.Write((double)vec.X);
                _writer.Write((double)vec.Y);
                _writer.Write((double)vec.Z);
            }
            else
            {
                _writer.Write(vec.X);
                _writer.Write(vec.Y);
                _writer.Write(vec.Z);
            }
        }

        /// <summary>
        /// Writes a Quaternion (rotation) in X, Y, Z, W order.
        /// </summary>
        private void WriteQuaternion(System.Numerics.Quaternion quat)
        {
            if (_animation.HighPrecision)
            {
                _writer.Write((double)quat.X);
                _writer.Write((double)quat.Y);
                _writer.Write((double)quat.Z);
                _writer.Write((double)quat.W);
            }
            else
            {
                _writer.Write(quat.X);
                _writer.Write(quat.Y);
                _writer.Write(quat.Z);
                _writer.Write(quat.W);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _writer?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Helper class for exporting XAnim to SEAnim format.
    /// </summary>
    public static class SEAnimExporter
    {
        /// <summary>
        /// Exports an XAnimParts to an SEAnim file.
        /// </summary>
        /// <param name="xanim">The XAnimParts to export.</param>
        /// <param name="filePath">The output file path.</param>
        /// <param name="zoneData">Optional zone data for keyframe extraction.</param>
        /// <returns>True if export succeeded, false otherwise.</returns>
        public static bool Export(Models.XAnimParts xanim, string filePath, byte[]? zoneData = null)
        {
            try
            {
                var animation = SEAnimAnimation.FromXAnim(xanim, zoneData);

                using (var stream = File.Create(filePath))
                using (var writer = new SEAnimWriter(stream, animation))
                {
                    writer.Write();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Exports an XAnimParts to an SEAnim file with detailed error information.
        /// </summary>
        /// <param name="xanim">The XAnimParts to export.</param>
        /// <param name="filePath">The output file path.</param>
        /// <param name="zoneData">Optional zone data for keyframe extraction.</param>
        /// <param name="error">Error message if export fails.</param>
        /// <returns>True if export succeeded, false otherwise.</returns>
        public static bool Export(Models.XAnimParts xanim, string filePath, byte[]? zoneData, out string? error)
        {
            error = null;

            try
            {
                var animation = SEAnimAnimation.FromXAnim(xanim, zoneData);

                using (var stream = File.Create(filePath))
                using (var writer = new SEAnimWriter(stream, animation))
                {
                    writer.Write();
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
