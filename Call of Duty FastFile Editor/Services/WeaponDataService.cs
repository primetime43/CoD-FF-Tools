using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using System;

namespace Call_of_Duty_FastFile_Editor.Services
{
    /// <summary>
    /// Service for reading and writing weapon field values with platform-aware endianness.
    /// Supports PC (little-endian) and console (big-endian) platforms.
    /// </summary>
    public class WeaponDataService
    {
        private readonly byte[] _zoneData;
        private readonly IGameDefinition _gameDefinition;
        private readonly bool _isLittleEndian;
        private readonly string _gameShortName;

        /// <summary>
        /// Creates a new WeaponDataService.
        /// </summary>
        /// <param name="zoneData">The zone file data buffer.</param>
        /// <param name="gameDefinition">The game definition for platform detection.</param>
        public WeaponDataService(byte[] zoneData, IGameDefinition gameDefinition)
        {
            _zoneData = zoneData ?? throw new ArgumentNullException(nameof(zoneData));
            _gameDefinition = gameDefinition ?? throw new ArgumentNullException(nameof(gameDefinition));
            _isLittleEndian = gameDefinition.IsPC;
            _gameShortName = gameDefinition.ShortName.ToUpperInvariant();

            // Normalize game short name
            if (_gameShortName.Contains("COD5") || _gameShortName.Contains("WAW"))
                _gameShortName = "COD5";
            else if (_gameShortName.Contains("COD4"))
                _gameShortName = "COD4";
            else if (_gameShortName.Contains("MW2"))
                _gameShortName = "MW2";
        }

        /// <summary>
        /// Gets the current game's short name for field offset lookups.
        /// </summary>
        public string GameShortName => _gameShortName;

        /// <summary>
        /// Gets whether the platform uses little-endian byte order.
        /// </summary>
        public bool IsLittleEndian => _isLittleEndian;

        /// <summary>
        /// Gets the platform name for display.
        /// </summary>
        public string Platform => _gameDefinition.Platform;

        /// <summary>
        /// Reads a field value from the weapon data at the specified base offset.
        /// </summary>
        /// <param name="field">The field definition.</param>
        /// <param name="weaponBaseOffset">The base offset of the weapon in zone data.</param>
        /// <param name="alignmentAdjust">Alignment adjustment for this weapon.</param>
        /// <returns>The field value as an object, or null if the field is not available.</returns>
        public object? ReadFieldValue(WeaponFieldDefinition field, int weaponBaseOffset, int alignmentAdjust = 0)
        {
            int fieldOffset = field.GetOffset(_gameShortName);
            if (fieldOffset < 0)
                return null;

            int absoluteOffset = weaponBaseOffset + alignmentAdjust + fieldOffset;

            if (absoluteOffset < 0 || absoluteOffset + GetFieldSize(field.FieldType) > _zoneData.Length)
                return null;

            return field.FieldType switch
            {
                WeaponFieldType.Int32 => ReadInt32(absoluteOffset),
                WeaponFieldType.UInt32 => ReadUInt32(absoluteOffset),
                WeaponFieldType.Int16 => ReadInt16(absoluteOffset),
                WeaponFieldType.UInt16 => ReadUInt16(absoluteOffset),
                WeaponFieldType.Byte => _zoneData[absoluteOffset],
                WeaponFieldType.Float => ReadFloat(absoluteOffset),
                WeaponFieldType.Bool => ReadInt32(absoluteOffset) != 0,
                WeaponFieldType.Enum => ReadInt32(absoluteOffset),
                _ => null
            };
        }

        /// <summary>
        /// Writes a field value to the weapon data at the specified base offset.
        /// </summary>
        /// <param name="field">The field definition.</param>
        /// <param name="weaponBaseOffset">The base offset of the weapon in zone data.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="alignmentAdjust">Alignment adjustment for this weapon.</param>
        /// <returns>True if the write was successful, false otherwise.</returns>
        public bool WriteFieldValue(WeaponFieldDefinition field, int weaponBaseOffset, object value, int alignmentAdjust = 0)
        {
            if (field.IsReadOnly)
                return false;

            int fieldOffset = field.GetOffset(_gameShortName);
            if (fieldOffset < 0)
                return false;

            int absoluteOffset = weaponBaseOffset + alignmentAdjust + fieldOffset;

            if (absoluteOffset < 0 || absoluteOffset + GetFieldSize(field.FieldType) > _zoneData.Length)
                return false;

            try
            {
                switch (field.FieldType)
                {
                    case WeaponFieldType.Int32:
                        WriteInt32(absoluteOffset, Convert.ToInt32(value));
                        break;
                    case WeaponFieldType.UInt32:
                        WriteUInt32(absoluteOffset, Convert.ToUInt32(value));
                        break;
                    case WeaponFieldType.Int16:
                        WriteInt16(absoluteOffset, Convert.ToInt16(value));
                        break;
                    case WeaponFieldType.UInt16:
                        WriteUInt16(absoluteOffset, Convert.ToUInt16(value));
                        break;
                    case WeaponFieldType.Byte:
                        _zoneData[absoluteOffset] = Convert.ToByte(value);
                        break;
                    case WeaponFieldType.Float:
                        WriteFloat(absoluteOffset, Convert.ToSingle(value));
                        break;
                    case WeaponFieldType.Bool:
                        WriteInt32(absoluteOffset, Convert.ToBoolean(value) ? 1 : 0);
                        break;
                    case WeaponFieldType.Enum:
                        WriteInt32(absoluteOffset, Convert.ToInt32(value));
                        break;
                    default:
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects alignment adjustment for a weapon at the given offset.
        /// Some weapons have 6-byte FF pattern instead of 8-byte, requiring a 2-byte adjustment.
        /// </summary>
        /// <param name="weaponOffset">The starting offset of the weapon.</param>
        /// <returns>The alignment adjustment (0 or 2).</returns>
        public int DetectAlignmentAdjust(int weaponOffset)
        {
            if (weaponOffset + 8 > _zoneData.Length)
                return 0;

            int ffCount = 0;
            for (int i = 0; i < 8 && _zoneData[weaponOffset + i] == 0xFF; i++)
            {
                ffCount++;
            }

            // If exactly 6 FFs (not 8), the actual data starts 2 bytes later
            return ffCount == 6 ? 2 : 0;
        }

        #region Read Helpers

        private int ReadInt32(int offset)
        {
            if (_isLittleEndian)
            {
                return _zoneData[offset] |
                       (_zoneData[offset + 1] << 8) |
                       (_zoneData[offset + 2] << 16) |
                       (_zoneData[offset + 3] << 24);
            }
            else
            {
                return (_zoneData[offset] << 24) |
                       (_zoneData[offset + 1] << 16) |
                       (_zoneData[offset + 2] << 8) |
                       _zoneData[offset + 3];
            }
        }

        private uint ReadUInt32(int offset)
        {
            return (uint)ReadInt32(offset);
        }

        private short ReadInt16(int offset)
        {
            if (_isLittleEndian)
            {
                return (short)(_zoneData[offset] | (_zoneData[offset + 1] << 8));
            }
            else
            {
                return (short)((_zoneData[offset] << 8) | _zoneData[offset + 1]);
            }
        }

        private ushort ReadUInt16(int offset)
        {
            return (ushort)ReadInt16(offset);
        }

        private float ReadFloat(int offset)
        {
            byte[] bytes = new byte[4];
            if (_isLittleEndian)
            {
                bytes[0] = _zoneData[offset];
                bytes[1] = _zoneData[offset + 1];
                bytes[2] = _zoneData[offset + 2];
                bytes[3] = _zoneData[offset + 3];
            }
            else
            {
                bytes[0] = _zoneData[offset + 3];
                bytes[1] = _zoneData[offset + 2];
                bytes[2] = _zoneData[offset + 1];
                bytes[3] = _zoneData[offset];
            }
            return BitConverter.ToSingle(bytes, 0);
        }

        #endregion

        #region Write Helpers

        private void WriteInt32(int offset, int value)
        {
            if (_isLittleEndian)
            {
                _zoneData[offset] = (byte)value;
                _zoneData[offset + 1] = (byte)(value >> 8);
                _zoneData[offset + 2] = (byte)(value >> 16);
                _zoneData[offset + 3] = (byte)(value >> 24);
            }
            else
            {
                _zoneData[offset] = (byte)(value >> 24);
                _zoneData[offset + 1] = (byte)(value >> 16);
                _zoneData[offset + 2] = (byte)(value >> 8);
                _zoneData[offset + 3] = (byte)value;
            }
        }

        private void WriteUInt32(int offset, uint value)
        {
            WriteInt32(offset, (int)value);
        }

        private void WriteInt16(int offset, short value)
        {
            if (_isLittleEndian)
            {
                _zoneData[offset] = (byte)value;
                _zoneData[offset + 1] = (byte)(value >> 8);
            }
            else
            {
                _zoneData[offset] = (byte)(value >> 8);
                _zoneData[offset + 1] = (byte)value;
            }
        }

        private void WriteUInt16(int offset, ushort value)
        {
            WriteInt16(offset, (short)value);
        }

        private void WriteFloat(int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (_isLittleEndian)
            {
                _zoneData[offset] = bytes[0];
                _zoneData[offset + 1] = bytes[1];
                _zoneData[offset + 2] = bytes[2];
                _zoneData[offset + 3] = bytes[3];
            }
            else
            {
                _zoneData[offset] = bytes[3];
                _zoneData[offset + 1] = bytes[2];
                _zoneData[offset + 2] = bytes[1];
                _zoneData[offset + 3] = bytes[0];
            }
        }

        #endregion

        #region Helpers

        private static int GetFieldSize(WeaponFieldType fieldType)
        {
            return fieldType switch
            {
                WeaponFieldType.Int32 => 4,
                WeaponFieldType.UInt32 => 4,
                WeaponFieldType.Int16 => 2,
                WeaponFieldType.UInt16 => 2,
                WeaponFieldType.Byte => 1,
                WeaponFieldType.Float => 4,
                WeaponFieldType.Bool => 4,
                WeaponFieldType.Enum => 4,
                _ => 4
            };
        }

        #endregion
    }
}
