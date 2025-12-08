using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using Call_of_Duty_FastFile_Editor.ZoneParsers;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Base class providing common parsing functionality for all game definitions.
    /// Game-specific implementations can override methods as needed.
    /// </summary>
    public abstract class GameDefinitionBase : IGameDefinition
    {
        public abstract string GameName { get; }
        public abstract string ShortName { get; }
        public abstract int VersionValue { get; }
        public abstract int PCVersionValue { get; }
        public abstract byte[] VersionBytes { get; }
        public abstract byte RawFileAssetType { get; }
        public abstract byte LocalizeAssetType { get; }
        public virtual byte MenuFileAssetType => 0; // Default 0 means not supported
        public abstract byte XAnimAssetType { get; }

        public virtual bool IsRawFileType(int assetType) => assetType == RawFileAssetType;
        public virtual bool IsLocalizeType(int assetType) => assetType == LocalizeAssetType;
        public virtual bool IsMenuFileType(int assetType) => MenuFileAssetType != 0 && assetType == MenuFileAssetType;
        public virtual bool IsXAnimType(int assetType) => assetType == XAnimAssetType;
        public virtual bool IsMaterialType(int assetType) => false; // Override in game-specific definitions
        public virtual bool IsTechSetType(int assetType) => false; // Override in game-specific definitions
        public virtual bool IsSupportedAssetType(int assetType) => IsRawFileType(assetType) || IsLocalizeType(assetType) || IsMenuFileType(assetType) || IsXAnimType(assetType);
        public abstract string GetAssetTypeName(int assetType);

        /// <summary>
        /// Default rawfile structure for CoD4/CoD5:
        /// [FF FF FF FF] [4-byte size BE] [FF FF FF FF] [null-terminated name] [data]
        /// </summary>
        public virtual RawFileNode? ParseRawFile(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseRawFile at offset 0x{offset:X}");

            // Ensure enough bytes for header (12 bytes minimum)
            if (offset > zoneData.Length - 12)
            {
                Debug.WriteLine($"[{ShortName}] Not enough bytes for header at 0x{offset:X}");
                return null;
            }

            // Read and validate first marker (should be 0xFFFFFFFF)
            uint marker1 = ReadUInt32BE(zoneData, offset);
            if (marker1 != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[{ShortName}] Unexpected marker1 at 0x{offset:X}: 0x{marker1:X}");
                return null;
            }

            // Read data length (size of the file data)
            int dataLength = (int)ReadUInt32BE(zoneData, offset + 4);
            if (dataLength < 0)
            {
                Debug.WriteLine($"[{ShortName}] Negative dataLength: {dataLength} at 0x{offset + 4:X}");
                return null;
            }

            // Read and validate second marker (should be 0xFFFFFFFF)
            uint marker2 = ReadUInt32BE(zoneData, offset + 8);
            if (marker2 != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[{ShortName}] Unexpected marker2 at 0x{offset + 8:X}: 0x{marker2:X}");
                return null;
            }

            var node = new RawFileNode
            {
                StartOfFileHeader = offset,
                MaxSize = dataLength
            };

            // Read null-terminated filename after header
            int fileNameOffset = offset + 12;
            string fileName = ReadNullTerminatedString(zoneData, fileNameOffset);
            node.FileName = fileName;

            // Calculate filename byte length including null terminator
            // Use Length, not UTF8.GetByteCount - we read byte-by-byte, each byte = one char
            int nameByteCount = fileName.Length + 1;
            int fileDataOffset = fileNameOffset + nameByteCount;

            // Check if data fits in zone, or if we need to read truncated data
            // Some rawfiles (e.g., embedded Bink videos) may have sizes larger than available data
            int availableData = zoneData.Length - fileDataOffset;
            int actualDataLength = Math.Min(dataLength, availableData);

            if (dataLength > availableData)
            {
                Debug.WriteLine($"[{ShortName}] Rawfile '{fileName}' claims {dataLength} bytes but only {availableData} available (truncated/external data)");
            }
            else
            {
                Debug.WriteLine($"[{ShortName}] Found rawfile: '{fileName}' size={dataLength}");
            }

            // Read file data (may be truncated for large embedded files)
            if (actualDataLength > 0)
            {
                byte[] rawBytes = new byte[actualDataLength];
                Array.Copy(zoneData, fileDataOffset, rawBytes, 0, actualDataLength);
                node.RawFileBytes = rawBytes;
                node.RawFileContent = Encoding.UTF8.GetString(rawBytes);

                // Calculate end position based on CLAIMED size for next asset calculation
                // but cap it to zone length
                node.RawFileEndPosition = Math.Min(fileDataOffset + dataLength + 1, zoneData.Length);
            }
            else
            {
                Debug.WriteLine($"[{ShortName}] No data available for rawfile");
                node.RawFileBytes = Array.Empty<byte>();
                node.RawFileContent = string.Empty;
                node.RawFileEndPosition = fileDataOffset;
            }

            return node;
        }

        /// <summary>
        /// Default localize structure:
        /// [FF FF FF FF FF FF FF FF] [null-terminated value] [null-terminated key]
        /// </summary>
        public virtual (LocalizedEntry? entry, int nextOffset) ParseLocalizedEntry(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseLocalizedEntry at offset 0x{offset:X}");

            // Check for 8-byte marker
            if (offset + 8 > zoneData.Length)
            {
                return (null, offset);
            }

            // Validate marker (FF FF FF FF FF FF FF FF)
            bool validMarker = true;
            for (int i = 0; i < 8; i++)
            {
                if (zoneData[offset + i] != 0xFF)
                {
                    validMarker = false;
                    break;
                }
            }

            if (!validMarker)
            {
                Debug.WriteLine($"[{ShortName}] Invalid localize marker at 0x{offset:X}");
                return (null, offset);
            }

            int currentOffset = offset + 8;

            // Read localized value (null-terminated)
            string localizedValue = ReadNullTerminatedString(zoneData, currentOffset);
            // Use Length, not UTF8.GetByteCount - we read byte-by-byte, each byte = one char
            currentOffset += localizedValue.Length + 1;

            // Track where key starts for in-place patching
            int keyStartOffset = currentOffset;

            // Read key/reference (null-terminated)
            string key = ReadNullTerminatedString(zoneData, currentOffset);
            currentOffset += key.Length + 1;

            var entry = new LocalizedEntry
            {
                Key = key,
                LocalizedText = localizedValue,
                StartOfFileHeader = offset,
                EndOfFileHeader = currentOffset,
                KeyStartOffset = keyStartOffset
            };

            Debug.WriteLine($"[{ShortName}] Found localize: key='{key}'");

            return (entry, currentOffset);
        }

        /// <summary>
        /// Default menufile parsing using MenuListParser.
        /// </summary>
        public virtual MenuList? ParseMenuFile(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseMenuFile at offset 0x{offset:X}");
            return MenuListParser.ParseMenuList(zoneData, offset, isBigEndian: true);
        }

        /// <summary>
        /// Default material parsing using MaterialParser.
        /// </summary>
        public virtual MaterialAsset? ParseMaterial(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseMaterial at offset 0x{offset:X}");
            return MaterialParser.ParseMaterial(zoneData, offset, isBigEndian: true);
        }

        /// <summary>
        /// Default techset parsing using TechSetParser.
        /// </summary>
        public virtual TechSetAsset? ParseTechSet(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseTechSet at offset 0x{offset:X}");
            return TechSetParser.ParseTechSet(zoneData, offset, isBigEndian: true);
        }

        /// <summary>
        /// Default XAnim parsing.
        ///
        /// XAnimParts structure for CoD4/WaW (88 bytes header):
        /// [FF FF FF FF] - name pointer (inline)
        /// [2 bytes] dataByteCount
        /// [2 bytes] dataShortCount
        /// [2 bytes] dataIntCount
        /// [2 bytes] randomDataByteCount
        /// [2 bytes] randomDataIntCount
        /// [2 bytes] numframes
        /// [1 byte] bLoop
        /// [1 byte] bDelta
        /// [12 bytes] boneCount array
        /// [1 byte] notifyCount
        /// [1 byte] assetType
        /// [1 byte] pad
        /// [1 byte] padding
        /// [4 bytes] randomDataShortCount
        /// [4 bytes] indexCount
        /// [4 bytes] framerate (float)
        /// [4 bytes] frequency (float)
        /// [4 bytes] names pointer
        /// [4 bytes] dataByte pointer
        /// [4 bytes] dataShort pointer
        /// [4 bytes] dataInt pointer
        /// [4 bytes] randomDataShort pointer
        /// [4 bytes] randomDataByte pointer
        /// [4 bytes] randomDataInt pointer
        /// [4 bytes] indices pointer
        /// [4 bytes] notify pointer
        /// [4 bytes] deltaPart pointer
        /// [name string\0]
        /// </summary>
        public virtual XAnimParts? ParseXAnim(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseXAnim at offset 0x{offset:X}");

            // First try structure-based parsing at exact offset (with debug output)
            var result = TryParseXAnimStructure(zoneData, offset, debugOutput: true);
            if (result != null)
            {
                return result;
            }

            // If exact position failed, search forward for a valid XAnim header
            for (int searchOffset = offset + 1; searchOffset < Math.Min(offset + 256, zoneData.Length - 88); searchOffset++)
            {
                result = TryParseXAnimStructure(zoneData, searchOffset, debugOutput: false);
                if (result != null)
                {
                    Debug.WriteLine($"[{ShortName}] Found XAnim at adjusted offset 0x{searchOffset:X} (was 0x{offset:X})");
                    return result;
                }
            }

            Debug.WriteLine($"[{ShortName}] Failed to parse XAnim at 0x{offset:X}");
            return null;
        }

        /// <summary>
        /// Attempts to parse an XAnim at the exact given offset.
        /// </summary>
        private XAnimParts? TryParseXAnimStructure(byte[] zoneData, int offset, bool debugOutput = false)
        {
            // Need at least 88 bytes for the header structure
            if (offset + 88 > zoneData.Length)
            {
                if (debugOutput) Debug.WriteLine($"[{ShortName}] XAnim: Not enough bytes at 0x{offset:X}");
                return null;
            }

            // Read the name pointer (should be 0xFFFFFFFF for inline)
            uint namePointer = ReadUInt32BE(zoneData, offset);
            if (namePointer != 0xFFFFFFFF && namePointer != 0x00000000)
            {
                if (debugOutput) Debug.WriteLine($"[{ShortName}] XAnim: Invalid name pointer 0x{namePointer:X8} at 0x{offset:X}");
                return null;
            }

            // Read header fields (big-endian for PS3/Xbox 360)
            // XAnimParts structure - verified from actual zone file hex dump:
            // 0x00: name pointer (4 bytes) - 0xFFFFFFFF if inline
            // 0x04: dataByteCount (2 bytes)
            // 0x06: dataShortCount (2 bytes)
            // 0x08: dataIntCount (2 bytes)
            // 0x0A: randomDataByteCount (2 bytes)
            // 0x0C: randomDataIntCount (2 bytes)
            // 0x0E: numframes (2 bytes)
            // 0x10: bLoop (1 byte)
            // 0x11: bDelta (1 byte)
            // 0x12-0x1D: boneCount[12] (12 bytes)
            // 0x1E: notifyCount (1 byte)
            // 0x1F: assetType (1 byte)
            // 0x20: randomDataShortCount (4 bytes)
            // 0x24: indexCount (4 bytes)
            // 0x28: unknown/padding (4 bytes) - observed as 0x00000000
            // 0x2C: framerate (4 bytes) - e.g., 0x41F00000 = 30.0 fps
            // 0x30: frequency (4 bytes) - e.g., 0x3FC00000 = 1.5
            // 0x34+: pointers (dataByte, dataShort, dataInt, randomDataShort, randomDataByte, randomDataInt, indices, notify, deltaPart)
            // 0x58+: name string (when name pointer is 0xFFFFFFFF)

            ushort dataByteCount = ReadUInt16BE(zoneData, offset + 0x04);
            ushort dataShortCount = ReadUInt16BE(zoneData, offset + 0x06);
            ushort dataIntCount = ReadUInt16BE(zoneData, offset + 0x08);
            ushort randomDataByteCount = ReadUInt16BE(zoneData, offset + 0x0A);
            ushort randomDataIntCount = ReadUInt16BE(zoneData, offset + 0x0C);
            ushort numframes = ReadUInt16BE(zoneData, offset + 0x0E);
            bool bLoop = zoneData[offset + 0x10] != 0;
            bool bDelta = zoneData[offset + 0x11] != 0;

            // Read bone count array (12 bytes at offset 0x12)
            byte[] boneCount = new byte[12];
            Array.Copy(zoneData, offset + 0x12, boneCount, 0, 12);

            byte notifyCount = zoneData[offset + 0x1E];
            byte assetType = zoneData[offset + 0x1F];

            uint randomDataShortCount = ReadUInt32BE(zoneData, offset + 0x20);
            uint indexCount = ReadUInt32BE(zoneData, offset + 0x24);
            // Skip 0x28 (unknown/padding field)
            float framerate = ReadFloatBE(zoneData, offset + 0x2C);  // Fixed: was 0x28
            float frequency = ReadFloatBE(zoneData, offset + 0x30);  // Fixed: was 0x2C

            if (debugOutput)
            {
                Debug.WriteLine($"[{ShortName}] XAnim at 0x{offset:X}: namePtr=0x{namePointer:X8}, frames={numframes}, fps={framerate:F1}, dataByte={dataByteCount}, dataShort={dataShortCount}, dataInt={dataIntCount}");
                // Dump raw bytes at key offsets for debugging
                string bytesAt2C = $"{zoneData[offset + 0x2C]:X2} {zoneData[offset + 0x2D]:X2} {zoneData[offset + 0x2E]:X2} {zoneData[offset + 0x2F]:X2}";
                Debug.WriteLine($"[{ShortName}] XAnim: Raw bytes at 0x2C (framerate): {bytesAt2C}");
            }

            // Validate: framerate should be reasonable (1-120 fps typically)
            // Also check for NaN and infinity
            if (float.IsNaN(framerate) || float.IsInfinity(framerate) || framerate < 0.1f || framerate > 1000f)
            {
                if (debugOutput) Debug.WriteLine($"[{ShortName}] XAnim: Invalid framerate {framerate} at 0x{offset:X}");
                return null;
            }

            // Validate: numframes should be reasonable
            if (numframes == 0 || numframes > 100000)
            {
                if (debugOutput) Debug.WriteLine($"[{ShortName}] XAnim: Invalid numframes {numframes} at 0x{offset:X}");
                return null;
            }

            // After the fixed header fields (ending at 0x34), there are pointer fields.
            // Based on hex dump analysis:
            // - 0x34-0x57: Pointer fields (9 pointers * 4 bytes = 36 bytes, mix of 0xFFFFFFFF and 0x00000000)
            // - 0x58+: Name string (when name pointer is 0xFFFFFFFF)
            // We search for the name starting from offset + 0x34

            string name = "";
            int nameOffset = 0;

            // Search for the name string starting from offset + 0x34 (after fixed fields)
            // Name is typically around offset 0x58 (88 bytes from header start)
            for (int searchStart = offset + 0x34; searchStart < offset + 256 && searchStart < zoneData.Length - 10; searchStart++)
            {
                // Skip 0xFF and 0x00 bytes (pointer placeholders and nulls)
                if (zoneData[searchStart] == 0xFF || zoneData[searchStart] == 0x00)
                    continue;

                // Check if this looks like a valid name start (lowercase letter or 'p' for prefix)
                byte b = zoneData[searchStart];
                if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z'))
                {
                    string candidate = ReadNullTerminatedString(zoneData, searchStart);
                    if (!string.IsNullOrEmpty(candidate) && candidate.Length >= 3 && candidate.Length <= 128)
                    {
                        // Check if it looks like an animation name (contains underscore and valid chars)
                        bool looksValid = candidate.Contains('_') &&
                                          candidate.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '/' || c == '\\');
                        if (looksValid)
                        {
                            name = candidate;
                            nameOffset = searchStart;
                            if (debugOutput) Debug.WriteLine($"[{ShortName}] XAnim: Found name '{name}' at 0x{nameOffset:X} (header at 0x{offset:X})");
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                if (debugOutput) Debug.WriteLine($"[{ShortName}] XAnim: Could not find valid name at 0x{offset:X}");
                return null;
            }

            Debug.WriteLine($"[{ShortName}] XAnim header at 0x{offset:X}: name='{name}', frames={numframes}, fps={framerate:F1}, bones={boneCount.Sum(b => (int)b)}");

            // Calculate end offset (header + name + null terminator)
            // The actual data follows but we're just parsing the header for now
            int endOffset = nameOffset + name.Length + 1;

            // Skip past any inline data based on the counts
            // This is a simplified calculation - actual size depends on data alignment
            int estimatedDataSize =
                dataByteCount +
                (dataShortCount * 2) +
                (dataIntCount * 4) +
                randomDataByteCount +
                (int)(randomDataShortCount * 2) +
                (randomDataIntCount * 4);

            // Add some buffer for alignment and additional structures
            endOffset += estimatedDataSize;

            return new XAnimParts
            {
                Name = name,
                DataByteCount = dataByteCount,
                DataShortCount = dataShortCount,
                DataIntCount = dataIntCount,
                RandomDataByteCount = randomDataByteCount,
                RandomDataIntCount = randomDataIntCount,
                NumFrames = numframes,
                IsLooping = bLoop,
                HasDelta = bDelta,
                BoneCounts = boneCount,
                NotifyCount = notifyCount,
                AssetType = assetType,
                RandomDataShortCount = randomDataShortCount,
                IndexCount = indexCount,
                Framerate = framerate,
                Frequency = frequency,
                StartOffset = offset,
                EndOffset = endOffset,
                AdditionalData = $"{ShortName} structure-based parse; {numframes} frames at {framerate:F1} fps"
            };
        }

        #region Helper Methods

        protected static ushort ReadUInt16BE(byte[] data, int offset)
        {
            if (offset + 2 > data.Length) return 0;
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        protected static float ReadFloatBE(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) return 0;
            byte[] bytes = new byte[4];
            bytes[0] = data[offset + 3];
            bytes[1] = data[offset + 2];
            bytes[2] = data[offset + 1];
            bytes[3] = data[offset];
            return BitConverter.ToSingle(bytes, 0);
        }

        protected static uint ReadUInt32BE(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) return 0;
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) |
                          (data[offset + 2] << 8) | data[offset + 3]);
        }

        protected static int ReadInt32BE(byte[] data, int offset)
        {
            return (int)ReadUInt32BE(data, offset);
        }

        protected static string ReadNullTerminatedString(byte[] data, int offset)
        {
            var sb = new StringBuilder();
            while (offset < data.Length && data[offset] != 0x00)
            {
                sb.Append((char)data[offset]);
                offset++;
            }
            return sb.ToString();
        }

        #endregion
    }
}
