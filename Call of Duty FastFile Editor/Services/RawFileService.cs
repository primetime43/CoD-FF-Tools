using System.IO.Compression;
using System.Text;
using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.UI;
using Call_of_Duty_FastFile_Editor.Services.IO;
using Call_of_Duty_FastFile_Editor.GameDefinitions;
using FastFileLib;
using static Call_of_Duty_FastFile_Editor.Models.FastFile;

namespace Call_of_Duty_FastFile_Editor.Services
{
    public class RawFileService : IRawFileService
    {
        /// <inheritdoc/>
        public void AppendNewRawFile(string zoneFilePath, string filePath, int expectedSize)
        {
            // Adjust the raw file entry from disk.
            byte[] newEntryBytes = AdjustRawFileEntry(filePath, expectedSize);
            ZoneFile currentZone = RawFileNode.CurrentZone;
            int insertPosition = currentZone.AssetPoolEndOffset;

            currentZone.ModifyZoneFile(fs =>
            {
                long originalLength = fs.Length;
                // Read tail data from the insertion point.
                fs.Seek(insertPosition, SeekOrigin.Begin);
                byte[] tailBuffer = new byte[originalLength - insertPosition];
                fs.Read(tailBuffer, 0, tailBuffer.Length);
                // Extend the file length.
                fs.SetLength(originalLength + newEntryBytes.Length);
                // Shift tail data forward.
                fs.Seek(insertPosition + newEntryBytes.Length, SeekOrigin.Begin);
                fs.Write(tailBuffer, 0, tailBuffer.Length);
                // Write the adjusted new entry.
                fs.Seek(insertPosition, SeekOrigin.Begin);
                fs.Write(newEntryBytes, 0, newEntryBytes.Length);
            });

            // Read the current zone size.
            uint currentZoneSize = ZoneFileIO.ReadZoneFileSize(zoneFilePath);
            // Add the size of the injected entry.
            uint newZoneSize = currentZoneSize + (uint)newEntryBytes.Length;
            // Write the new size back to the zone file header.
            ZoneFileIO.WriteZoneFileSize(zoneFilePath, newZoneSize);
            // Also update the in-memory zone header information.
            currentZone.LoadData();
            currentZone.ReadHeaderFields();
        }

        /// <inheritdoc/>
        public void InjectPlainFile(string zoneFilePath, string filePath, string gamePath)
        {
            // Read the plain file content
            byte[] fileContent = File.ReadAllBytes(filePath);
            int contentSize = fileContent.Length;

            // Build the raw file entry with header:
            // - 4 bytes: first marker (0xFFFFFFFF)
            // - 4 bytes: data size (big-endian)
            // - 4 bytes: second marker (0xFFFFFFFF)
            // - N bytes: filename + null terminator
            // - M bytes: file content
            byte[] fileNameBytes = Encoding.ASCII.GetBytes(gamePath);
            int headerSize = 12 + fileNameBytes.Length + 1; // 12 bytes markers/size + filename + null
            int totalSize = headerSize + contentSize;

            byte[] newEntry = new byte[totalSize];

            // Write first marker (0xFFFFFFFF)
            newEntry[0] = 0xFF;
            newEntry[1] = 0xFF;
            newEntry[2] = 0xFF;
            newEntry[3] = 0xFF;

            // Write data size (big-endian)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
                newEntry.AsSpan(4, 4),
                (uint)contentSize
            );

            // Write second marker (0xFFFFFFFF)
            newEntry[8] = 0xFF;
            newEntry[9] = 0xFF;
            newEntry[10] = 0xFF;
            newEntry[11] = 0xFF;

            // Write filename
            Array.Copy(fileNameBytes, 0, newEntry, 12, fileNameBytes.Length);
            // Null terminator is already 0x00 from array initialization

            // Write content
            Array.Copy(fileContent, 0, newEntry, headerSize, contentSize);

            // Now inject the entry into the zone file
            ZoneFile currentZone = RawFileNode.CurrentZone;
            int insertPosition = currentZone.AssetPoolEndOffset;

            currentZone.ModifyZoneFile(fs =>
            {
                long originalLength = fs.Length;
                // Read tail data from the insertion point.
                fs.Seek(insertPosition, SeekOrigin.Begin);
                byte[] tailBuffer = new byte[originalLength - insertPosition];
                fs.Read(tailBuffer, 0, tailBuffer.Length);
                // Extend the file length.
                fs.SetLength(originalLength + newEntry.Length);
                // Shift tail data forward.
                fs.Seek(insertPosition + newEntry.Length, SeekOrigin.Begin);
                fs.Write(tailBuffer, 0, tailBuffer.Length);
                // Write the new entry.
                fs.Seek(insertPosition, SeekOrigin.Begin);
                fs.Write(newEntry, 0, newEntry.Length);
            });

            // Update the zone file size header.
            uint currentZoneSize = ZoneFileIO.ReadZoneFileSize(zoneFilePath);
            uint newZoneSize = currentZoneSize + (uint)newEntry.Length;
            ZoneFileIO.WriteZoneFileSize(zoneFilePath, newZoneSize);

            // Refresh zone data and header.
            currentZone.LoadData();
            currentZone.ReadHeaderFields();
        }

        /// <inheritdoc/>
        public void AdjustRawFileNodeSize(string zoneFilePath, RawFileNode rawFileNode, int newSize)
        {
            int oldSize = rawFileNode.MaxSize;
            if (newSize <= oldSize)
            {
                MessageBox.Show("The new size must be greater than the current size.",
                                "Invalid Size", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Create the new content: copy the existing data then pad with zeros.
            byte[] currentContent = rawFileNode.RawFileBytes;
            byte[] newContent = new byte[newSize];
            Array.Copy(currentContent, newContent, currentContent.Length);
            // The remaining bytes in newContent are already 0.

            // Use the same rebuild approach as IncreaseSize
            IncreaseSize(zoneFilePath, rawFileNode, newContent);
        }

        /// <summary>
        /// Adjusts a raw file entry read from disk so that its header's size field (at offset 4)
        /// matches the expected data size. It uses the known header structure:
        ///   Bytes 0-3: first marker (0xFFFFFFFF)
        ///   Bytes 4-7: data size (to be updated)
        ///   Bytes 8-11: second marker (0xFFFFFFFF)
        ///   Bytes 12 to N: null-terminated filename, then file data.
        /// The method pads or trims the data portion so that its length equals the expected size.
        /// Finally, it returns the reassembled entry.
        /// </summary>
        /// <param name="filePath">Full path to the file being injected (which already contains its header).</param>
        /// <param name="expectedSize">The expected size for the file’s data portion (RawFileNode.MaxSize).</param>
        /// <returns>An adjusted raw file entry as a byte array.</returns>
        private byte[] AdjustRawFileEntry(string filePath, int expectedSize)
        {
            // Read the entire file from disk.
            byte[] entry = File.ReadAllBytes(filePath);
            if (entry.Length < 12)
                throw new Exception("File too short to contain a valid header.");

            // The header structure is:
            // - Bytes 0-3: first marker (0xFFFFFFFF)
            // - Bytes 4-7: data size (which we'll update)
            // - Bytes 8-11: second marker (0xFFFFFFFF)
            // - Bytes 12: start of filename (null terminated)
            int fileNameStart = 12;
            int fileNameEnd = fileNameStart;
            while (fileNameEnd < entry.Length && entry[fileNameEnd] != 0x00)
            {
                fileNameEnd++;
            }
            if (fileNameEnd == entry.Length)
                throw new Exception("Filename in header is not null-terminated.");
            fileNameEnd++; // Include the null terminator.
            int headerLength = fileNameEnd; // The entire header is from offset 0 to fileNameEnd.

            // Extract header.
            byte[] header = new byte[headerLength];
            Array.Copy(entry, header, headerLength);

            // Data portion starts at headerLength.
            int currentDataSize = entry.Length - headerLength;
            byte[] data = new byte[expectedSize];
            if (currentDataSize < expectedSize)
            {
                // Copy available data and pad with zeros.
                Array.Copy(entry, headerLength, data, 0, currentDataSize);
            }
            else
            {
                // Otherwise, take exactly expectedSize bytes.
                Array.Copy(entry, headerLength, data, 0, expectedSize);
            }

            // Write the expectedSize directly as big‑endian into header[4..8)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
              header.AsSpan(4, 4),
              (uint)expectedSize
            );

            // Reassemble and return the adjusted raw file entry.
            byte[] newEntry = new byte[header.Length + data.Length];
            Buffer.BlockCopy(header, 0, newEntry, 0, header.Length);
            Buffer.BlockCopy(data, 0, newEntry, header.Length, data.Length);
            return newEntry;
        }

        /// <inheritdoc/>
        public void ExportRawFile(RawFileNode exportedRawFile, string fileExtension)
        {
            using var save = new SaveFileDialog
            {
                Title = "Export File (With Header for Re-injection)",
                FileName = SanitizeFileName(exportedRawFile.FileName),
                Filter = $"{fileExtension.TrimStart('.').ToUpper()} Files (*{fileExtension})|*{fileExtension}|All Files (*.*)|*.*"
            };

            if (save.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                byte[] zoneData = RawFileNode.CurrentZone.Data;
                int start = exportedRawFile.StartOfFileHeader;
                int length = exportedRawFile.RawFileEndPosition - start;
                byte[] slice = zoneData.Skip(start).Take(length).ToArray();

                File.WriteAllBytes(save.FileName, slice);

                MessageBox.Show(
                    $"File successfully exported to:\n\n{save.FileName}\n\n" +
                    "Note: This file includes the zone header and can be re-injected.",
                    "Export Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export file: {ex.Message}",
                    "Export Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <inheritdoc/>
        public void ExportRawFileContentOnly(RawFileNode exportedRawFile, string fileExtension)
        {
            using var save = new SaveFileDialog
            {
                Title = "Export Content Only",
                FileName = SanitizeFileName(exportedRawFile.FileName),
                Filter = $"{fileExtension.TrimStart('.').ToUpper()} Files (*{fileExtension})|*{fileExtension}|All Files (*.*)|*.*"
            };

            if (save.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                // Export only the actual content (RawFileBytes), not the header
                byte[] contentOnly = exportedRawFile.RawFileBytes;

                File.WriteAllBytes(save.FileName, contentOnly);

                MessageBox.Show(
                    $"File content successfully exported to:\n\n{save.FileName}\n\n" +
                    "Note: This file contains only the script content without zone header.",
                    "Export Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export file: {ex.Message}",
                    "Export Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <inheritdoc/>
        public void IncreaseSize(string zoneFilePath, RawFileNode rawFileNode, byte[] newContent)
        {
            int oldSize = rawFileNode.MaxSize;
            int newSize = newContent.Length;
            if (newSize <= oldSize)
            {
                UpdateFileContent(zoneFilePath, rawFileNode, newContent);
                return;
            }

            ZoneFile currentZone = RawFileNode.CurrentZone;

            // Rebuild the zone fresh using ZoneBuilder - same approach as FF Compiler
            // This ensures all headers and structures are correctly calculated
            byte[] originalZone = File.ReadAllBytes(zoneFilePath);
            GameVersion gameVersion = GetGameVersionFromZone();

            // Extract all raw files from the current zone
            var allRawFiles = ExtractRawFilesFromZone(originalZone);

            // Replace the modified file's content
            for (int i = 0; i < allRawFiles.Count; i++)
            {
                if (allRawFiles[i].Name.Equals(rawFileNode.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    allRawFiles[i] = new FastFileLib.Models.RawFile(rawFileNode.FileName, newContent);
                    break;
                }
            }

            // Build a new zone using ZoneBuilder
            var builder = new ZoneBuilder(gameVersion, "patch_mp");
            builder.AddRawFiles(allRawFiles);
            byte[] newZone = builder.Build();

            // Write the new zone to disk
            File.WriteAllBytes(zoneFilePath, newZone);

            // Update in-memory node properties
            rawFileNode.MaxSize = newSize;
            rawFileNode.RawFileBytes = newContent;
            rawFileNode.RawFileContent = Encoding.Default.GetString(newContent);

            // Refresh zone data from disk
            currentZone.LoadData();
            currentZone.ReadHeaderFields();
        }

        /// <summary>
        /// Extracts all raw files from a zone file.
        /// </summary>
        private static List<FastFileLib.Models.RawFile> ExtractRawFilesFromZone(byte[] zoneData)
        {
            var rawFiles = new List<FastFileLib.Models.RawFile>();
            var validExtensions = new[] { ".cfg", ".gsc", ".atr", ".csc", ".rmb", ".arena", ".vision", ".txt", ".str", ".menu" };
            var foundOffsets = new HashSet<int>();

            foreach (var ext in validExtensions)
            {
                byte[] pattern = Encoding.ASCII.GetBytes(ext + "\0");

                for (int i = 0; i <= zoneData.Length - pattern.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (zoneData[i + j] != pattern[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (!match) continue;

                    // Search backwards for FF FF FF FF marker
                    int markerEnd = i - 1;
                    while (markerEnd >= 4)
                    {
                        if (zoneData[markerEnd] == 0xFF &&
                            zoneData[markerEnd - 1] == 0xFF &&
                            zoneData[markerEnd - 2] == 0xFF &&
                            zoneData[markerEnd - 3] == 0xFF)
                            break;
                        markerEnd--;
                        if (i - markerEnd > 300)
                        {
                            markerEnd = -1;
                            break;
                        }
                    }

                    if (markerEnd < 4) continue;
                    if (zoneData[markerEnd + 1] == 0x00) continue;

                    int sizeOffset = markerEnd - 7;
                    if (sizeOffset < 0) continue;

                    int headerOffset = sizeOffset - 4;
                    if (headerOffset < 0) continue;
                    if (foundOffsets.Contains(headerOffset)) continue;

                    // Read size (big-endian)
                    int size = (zoneData[sizeOffset] << 24) |
                              (zoneData[sizeOffset + 1] << 16) |
                              (zoneData[sizeOffset + 2] << 8) |
                              zoneData[sizeOffset + 3];

                    if (size <= 0 || size > 10_000_000) continue;

                    // Read filename
                    int nameStart = markerEnd + 1;
                    int nameEnd = nameStart;
                    while (nameEnd < zoneData.Length && zoneData[nameEnd] != 0)
                        nameEnd++;

                    if (nameEnd <= nameStart) continue;

                    string name = Encoding.ASCII.GetString(zoneData, nameStart, nameEnd - nameStart);
                    if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;

                    int dataOffset = nameEnd + 1;
                    if (dataOffset + size > zoneData.Length) continue;

                    // Extract data
                    byte[] data = new byte[size];
                    Array.Copy(zoneData, dataOffset, data, 0, size);

                    rawFiles.Add(new FastFileLib.Models.RawFile(name, data));
                    foundOffsets.Add(headerOffset);
                }
            }

            return rawFiles;
        }

        /// <summary>
        /// Gets the FastFileLib.GameVersion based on the currently opened FastFile.
        /// Uses centralized detection from FastFileLib for consistency across all tools.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no zone is loaded or game version cannot be detected.</exception>
        private static GameVersion GetGameVersionFromZone()
        {
            var zone = RawFileNode.CurrentZone;
            if (zone == null)
                throw new InvalidOperationException("No zone file is currently loaded. Cannot determine game version.");

            // Primary detection: Use FastFileLib's centralized zone detection
            // This reads MemAlloc1 values from the zone header which is the most reliable method
            if (zone.Data != null && zone.Data.Length >= 12)
            {
                var detected = FastFileInfo.DetectGameFromZoneData(zone.Data);
                if (detected != GameVersion.Unknown)
                    return detected;
            }

            // Fallback: Try detecting from zone file path
            if (!string.IsNullOrEmpty(zone.FilePath) && File.Exists(zone.FilePath))
            {
                var detected = FastFileInfo.DetectGameFromZone(zone.FilePath);
                if (detected != GameVersion.Unknown)
                    return detected;
            }

            // Final fallback: Use parent FastFile header info
            var parentFastFile = zone.ParentFastFile;
            if (parentFastFile != null)
            {
                if (parentFastFile.IsMW2File)
                    return GameVersion.MW2;
                if (parentFastFile.IsCod4File)
                    return GameVersion.CoD4;
                if (parentFastFile.IsCod5File)
                    return GameVersion.WaW;
            }

            // If we reach here, detection completely failed - this is a critical error
            throw new InvalidOperationException(
                "Unable to detect game version from the zone file. " +
                "The zone header may be corrupted or the file format is not recognized. " +
                "Expected MemAlloc1 values: CoD4=0x0F70, WaW=0x10B0, MW2=0x03B4");
        }

        /// <inheritdoc/>
        public void RenameRawFile(TreeView filesTreeView, string ffFilePath, string zoneFilePath, List<RawFileNode> rawFileNodes, FastFile openedFastFile)
        {
            try
            {
                if (filesTreeView.SelectedNode?.Tag is RawFileNode selectedFileNode)
                {
                    var rawFileNode = rawFileNodes.FirstOrDefault(node => node.PatternIndexPosition == selectedFileNode.PatternIndexPosition);
                    if (rawFileNode != null)
                    {
                        // Prompt the user for a new file name.
                        string newFileName = PromptForNewFileName(rawFileNode.FileName);
                        if (string.IsNullOrWhiteSpace(newFileName))
                        {
                            MessageBox.Show("Rename operation was canceled or an invalid name was provided.",
                                "Rename Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        // The header structure is: 4 bytes (marker), then 4 bytes (size), then 4 bytes (marker),
                        // then the file name (in ASCII, null-terminated).
                        const int fixedHeaderSize = 12;
                        int fileNameStartPosition = rawFileNode.StartOfFileHeader + fixedHeaderSize;

                        // Get the old file name length (including null terminator in zone file).
                        int oldNameLengthWithNull = rawFileNode.FileName.Length + 1;
                        // Get the new file name as bytes (without null terminator, we'll add it).
                        byte[] newFileNameBytes = rawFileNode.GetFileNameBytes(newFileName);
                        int newNameLengthWithNull = newFileNameBytes.Length + 1;

                        // Compute the byte difference between the new and old name lengths (including null terminators).
                        int byteDifference = newNameLengthWithNull - oldNameLengthWithNull;

                        // Read the entire zone file into memory.
                        byte[] zoneFileData = File.ReadAllBytes(zoneFilePath);

                        // Create a backup before modifying.
                        CreateBackup(zoneFilePath);

                        if (byteDifference == 0)
                        {
                            // Overwrite directly if the lengths are identical.
                            Array.Copy(newFileNameBytes, 0, zoneFileData, fileNameStartPosition, newFileNameBytes.Length);
                            // Write null terminator
                            zoneFileData[fileNameStartPosition + newFileNameBytes.Length] = 0x00;
                        }
                        else if (byteDifference < 0)
                        {
                            // New name is shorter - shift data left.
                            // Calculate where data after old filename starts (after null terminator).
                            int oldDataStart = fileNameStartPosition + oldNameLengthWithNull;
                            int newDataStart = fileNameStartPosition + newNameLengthWithNull;
                            int shiftLength = zoneFileData.Length - oldDataStart;

                            // Shift the remainder of the zone data left.
                            Array.Copy(zoneFileData, oldDataStart, zoneFileData, newDataStart, shiftLength);

                            // Write new filename and null terminator.
                            Array.Copy(newFileNameBytes, 0, zoneFileData, fileNameStartPosition, newFileNameBytes.Length);
                            zoneFileData[fileNameStartPosition + newFileNameBytes.Length] = 0x00;

                            // Truncate the zone file data array.
                            zoneFileData = zoneFileData.Take(zoneFileData.Length + byteDifference).ToArray();
                        }
                        else // byteDifference > 0
                        {
                            // New name is longer - shift data right.
                            int originalLength = zoneFileData.Length;
                            // Resize the array to make room for extra bytes.
                            Array.Resize(ref zoneFileData, originalLength + byteDifference);

                            // Calculate where data after old filename starts (after null terminator).
                            int oldDataStart = fileNameStartPosition + oldNameLengthWithNull;
                            int newDataStart = fileNameStartPosition + newNameLengthWithNull;
                            int shiftLength = originalLength - oldDataStart;

                            // Shift the remainder of the zone data right.
                            Array.Copy(zoneFileData, oldDataStart, zoneFileData, newDataStart, shiftLength);

                            // Write new filename and null terminator.
                            Array.Copy(newFileNameBytes, 0, zoneFileData, fileNameStartPosition, newFileNameBytes.Length);
                            zoneFileData[fileNameStartPosition + newFileNameBytes.Length] = 0x00;
                        }

                        // Write the modified zone file back to disk.
                        File.WriteAllBytes(zoneFilePath, zoneFileData);

                        // Update the zone file size header if the filename length changed.
                        if (byteDifference != 0)
                        {
                            uint currentZoneSize = ZoneFileIO.ReadZoneFileSize(zoneFilePath);
                            uint newZoneSize = (uint)((int)currentZoneSize + byteDifference);
                            ZoneFileIO.WriteZoneFileSize(zoneFilePath, newZoneSize);

                            // Refresh zone data and header fields.
                            RawFileNode.CurrentZone.LoadData();
                            RawFileNode.CurrentZone.ReadHeaderFields();
                        }

                        // Save the old file name for notification.
                        string oldFileName = rawFileNode.FileName;
                        // Update the renamed file's FileName property.
                        rawFileNode.FileName = newFileName;
                        // Note: Computed properties (CodeStartPosition, CodeEndPosition, RawFileEndPosition) will reflect the new name length.

                        // Adjust the StartOfFileHeader for all subsequent raw file nodes.
                        foreach (var node in rawFileNodes)
                        {
                            if (node.StartOfFileHeader > rawFileNode.StartOfFileHeader)
                            {
                                node.StartOfFileHeader += byteDifference;
                            }
                        }

                        // Update the TreeView node text.
                        UpdateTreeViewNodeText(filesTreeView, rawFileNode.PatternIndexPosition, newFileName);

                        MessageBox.Show($"File successfully renamed from '{oldFileName}' to '{newFileName}'.",
                            "Rename Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Selected node does not match any file entry nodes.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("No node is selected or the selected node does not have a valid position.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename file: {ex.Message}", "Rename Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <inheritdoc/>
        public void SaveZoneRawFileChanges(TreeView filesTreeView, string ffFilePath, string zoneFilePath, List<RawFileNode> rawFileNodes, string updatedText, FastFile openedFastFile)
        {
            try
            {
                if (filesTreeView.SelectedNode?.Tag is RawFileNode rawFileNode)
                {
                    SaveFileNode(
                      ffFilePath,
                      zoneFilePath,
                      rawFileNode,
                      updatedText,
                      openedFastFile.OpenedFastFileHeader
                    );
                }
                else
                {
                    MessageBox.Show(
                      "No node is selected or the selected node does not have a valid RawFileNode.",
                      "Error",
                      MessageBoxButtons.OK,
                      MessageBoxIcon.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <inheritdoc/>
        public void UpdateFileContent(string zoneFilePath, RawFileNode rawFileNode, byte[] newContent)
        {
            System.Diagnostics.Debug.WriteLine($"[UpdateFileContent] File='{rawFileNode.FileName}', IsCompressed={rawFileNode.IsCompressed}, HeaderSize={rawFileNode.HeaderSize}");

            // Handle internally compressed raw files (MW2 PS3 format)
            if (rawFileNode.IsCompressed)
            {
                UpdateCompressedFileContent(zoneFilePath, rawFileNode, newContent);
                return;
            }

            // Fallback: Try to detect MW2 compressed format if not already detected
            if (TryDetectAndUpdateCompression(rawFileNode))
            {
                UpdateCompressedFileContent(zoneFilePath, rawFileNode, newContent);
                return;
            }

            System.Diagnostics.Debug.WriteLine("[UpdateFileContent] Using uncompressed update");
            if (newContent.Length > rawFileNode.MaxSize)
            {
                throw new ArgumentException(
                    $"New content size ({newContent.Length} bytes) exceeds the maximum allowed size ({rawFileNode.MaxSize} bytes) for file '{rawFileNode.FileName}'."
                );
            }

            try
            {
                RawFileNode.CurrentZone.ModifyZoneFile(fs =>
                {
                    fs.Seek(rawFileNode.CodeStartPosition, SeekOrigin.Begin);
                    fs.Write(newContent, 0, newContent.Length);

                    if (newContent.Length < rawFileNode.MaxSize)
                    {
                        var padding = new byte[rawFileNode.MaxSize - newContent.Length];
                        fs.Write(padding, 0, padding.Length);
                    }
                });

                rawFileNode.RawFileBytes = newContent;
                rawFileNode.RawFileContent = System.Text.Encoding.Default.GetString(newContent);
            }
            catch (IOException ioEx)
            {
                throw new IOException(
                    $"Failed to update content for raw file '{rawFileNode.FileName}': {ioEx.Message}",
                    ioEx
                );
            }
        }

        /// <summary>
        /// Attempts to detect MW2 compressed format for a raw file node.
        /// Updates the node's properties if compression is detected.
        /// </summary>
        /// <returns>True if compression was detected and properties were updated</returns>
        private static bool TryDetectAndUpdateCompression(RawFileNode rawFileNode)
        {
            byte[]? zoneData = RawFileNode.CurrentZone?.Data;
            if (zoneData == null)
                return false;

            int headerOffset = rawFileNode.StartOfFileHeader;

            // Try both the current offset and 4 bytes back (pattern matching may be off for 16-byte headers)
            int[] offsetsToTry = { headerOffset, headerOffset - 4 };

            foreach (int tryOffset in offsetsToTry)
            {
                if (tryOffset < 0 || tryOffset + FastFileLib.FastFileConstants.RawFileHeaderSize_MW2_Compressed > zoneData.Length)
                    continue;

                // Check for MW2 16-byte header format: [FFFFFFFF][compLen][uncompLen][FFFFFFFF]
                bool hasFirstMarker = zoneData[tryOffset..].Take(4).SequenceEqual(FastFileLib.FastFileConstants.RawFileMarker);
                bool hasSecondMarker = zoneData[(tryOffset + 12)..].Take(4).SequenceEqual(FastFileLib.FastFileConstants.RawFileMarker);

                if (!hasFirstMarker || !hasSecondMarker)
                    continue;

                // Read lengths from header
                int compressedLen = FastFileLib.FastFileConstants.ReadBigEndianInt32(zoneData, tryOffset + 4);
                int uncompressedLen = FastFileLib.FastFileConstants.ReadBigEndianInt32(zoneData, tryOffset + 8);

                // Validate: compressed length should be positive, reasonable, and different from uncompressed
                if (compressedLen <= 0 || compressedLen >= 10_000_000 || compressedLen == uncompressedLen)
                    continue;

                // Find data offset: header(16) + filename + null
                int filenameStart = tryOffset + FastFileLib.FastFileConstants.RawFileHeaderSize_MW2_Compressed;
                int filenameEnd = filenameStart;
                while (filenameEnd < zoneData.Length && zoneData[filenameEnd] != 0)
                    filenameEnd++;
                int dataOffset = filenameEnd + 1;

                // Verify data starts with zlib header
                if (!FastFileLib.FastFileConstants.HasZlibHeader(zoneData, dataOffset))
                    continue;

                // Found compression - update node properties
                System.Diagnostics.Debug.WriteLine($"[TryDetectCompression] Detected compression at 0x{tryOffset:X}");
                rawFileNode.StartOfFileHeader = tryOffset;
                rawFileNode.IsCompressed = true;
                rawFileNode.CompressedSize = compressedLen;
                rawFileNode.HeaderSize = FastFileLib.FastFileConstants.RawFileHeaderSize_MW2_Compressed;
                rawFileNode.MaxSize = uncompressedLen;
                rawFileNode.CodeStartPosition = dataOffset;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the content of an internally compressed raw file (MW2 format).
        /// Re-compresses the data and updates the header fields accordingly.
        /// </summary>
        private void UpdateCompressedFileContent(string zoneFilePath, RawFileNode rawFileNode, byte[] newContent)
        {
            // Compress the new content using zlib
            byte[] compressedContent = FastFileLib.CompressionHelper.CompressZlib(newContent);

            // Check if compressed content fits in the original slot
            if (compressedContent.Length > rawFileNode.CompressedSize)
            {
                throw new ArgumentException(
                    $"Compressed content size ({compressedContent.Length} bytes) exceeds the original compressed slot size ({rawFileNode.CompressedSize} bytes) for file '{rawFileNode.FileName}'.\n" +
                    $"The zone file needs to be rebuilt to accommodate larger content."
                );
            }

            try
            {
                RawFileNode.CurrentZone.ModifyZoneFile(fs =>
                {
                    // MW2 16-byte header format:
                    // [FF FF FF FF] [compressedLen BE] [len BE] [FF FF FF FF] [name\0] [compressed data]
                    // offset 0        offset 4          offset 8   offset 12

                    // Update compressedLen field at StartOfFileHeader + 4 (big-endian)
                    fs.Seek(rawFileNode.StartOfFileHeader + 4, SeekOrigin.Begin);
                    byte[] compressedLenBytes = new byte[4];
                    compressedLenBytes[0] = (byte)(compressedContent.Length >> 24);
                    compressedLenBytes[1] = (byte)(compressedContent.Length >> 16);
                    compressedLenBytes[2] = (byte)(compressedContent.Length >> 8);
                    compressedLenBytes[3] = (byte)(compressedContent.Length);
                    fs.Write(compressedLenBytes, 0, 4);

                    // Update len (uncompressed length) field at StartOfFileHeader + 8 (big-endian)
                    byte[] lenBytes = new byte[4];
                    lenBytes[0] = (byte)(newContent.Length >> 24);
                    lenBytes[1] = (byte)(newContent.Length >> 16);
                    lenBytes[2] = (byte)(newContent.Length >> 8);
                    lenBytes[3] = (byte)(newContent.Length);
                    fs.Write(lenBytes, 0, 4);

                    // Write compressed data at CodeStartPosition
                    fs.Seek(rawFileNode.CodeStartPosition, SeekOrigin.Begin);
                    fs.Write(compressedContent, 0, compressedContent.Length);

                    // Pad with zeros if new compressed data is smaller than original
                    if (compressedContent.Length < rawFileNode.CompressedSize)
                    {
                        var padding = new byte[rawFileNode.CompressedSize - compressedContent.Length];
                        fs.Write(padding, 0, padding.Length);
                    }
                });

                // Update node properties
                rawFileNode.RawFileBytes = newContent;
                rawFileNode.RawFileContent = System.Text.Encoding.Default.GetString(newContent);
                rawFileNode.MaxSize = newContent.Length;
                rawFileNode.CompressedSize = compressedContent.Length;
            }
            catch (IOException ioEx)
            {
                throw new IOException(
                    $"Failed to update compressed content for raw file '{rawFileNode.FileName}': {ioEx.Message}",
                    ioEx
                );
            }
        }

        /// <summary>
        /// Creates a backup of the specified file.
        /// </summary>
        /// <param name="filePath">The path of the file to backup.</param>
        private void CreateBackup(string filePath)
        {
            string backupPath = $"{filePath}.backup";
            try
            {
                File.Copy(filePath, backupPath, overwrite: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create backup: {ex.Message}", "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Updates the TreeView node's text based on the PatternIndexPosition.
        /// </summary>
        /// <param name="filesTreeView">The TreeView control.</param>
        /// <param name="patternIndexPosition">The PatternIndexPosition of the RawFileNode.</param>
        /// <param name="newFileName">The new file name to set.</param>
        private void UpdateTreeViewNodeText(TreeView filesTreeView, int patternIndexPosition, string newFileName)
        {
            foreach (TreeNode node in filesTreeView.Nodes)
            {
                if (node.Tag is RawFileNode rfn && rfn.PatternIndexPosition == patternIndexPosition)
                {
                    node.Text = newFileName;
                    break; // Exit the loop once the node is found and updated
                }
            }
        }


        /// <summary>
        /// Prompts the user to enter a new file name.
        /// </summary>
        /// <param name="currentName">The current file name.</param>
        /// <returns>The new file name entered by the user.</returns>
        private string PromptForNewFileName(string currentName)
        {
            using (RenameDialog renameDialog = new RenameDialog(currentName))
            {
                if (renameDialog.ShowDialog() == DialogResult.OK)
                {
                    return renameDialog.NewFileName;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Updates the content of a specific raw file within the Zone File.
        /// </summary>
        /// <param name="ffFilePath">The file path to the Fast File (.ff) being edited.</param>
        /// <param name="zoneFilePath">The file path to the decompressed Zone File (.zone) corresponding to the Fast File.</param>
        /// <param name="rawFileNode">The <see cref="RawFileNode"/> object representing the raw file to be updated.</param>
        /// <param name="updatedText">The new content for the raw file, as edited by the user.</param>
        /// <param name="headerInfo">An instance of <see cref="FastFileHeader"/> containing header information of the Fast File.</param>
        /// <exception cref="ArgumentException">Thrown when the updated content size exceeds the original maximum size of the raw file.</exception>
        /// <exception cref="IOException">Thrown when file read/write operations fail.</exception>
        /// <remarks>
        /// This method performs the following operations:
        /// <list type="number">
        ///     <item>Converts the updated text to a byte array using ASCII encoding.</item>
        ///     <item>Validates that the size of the updated content does not exceed the original maximum size.</item>
        ///     <item>Creates a backup of the Zone File before making any changes.</item>
        ///     <item>Updates the Zone File with the new content at the specified position.</item>
        ///     <item>Updates the in-memory <see cref="RawFileNode"/> with the new content.</item>
        ///     <item>Notifies the user of the successful save operation to the raw file.</item>
        /// </list>
        /// </remarks>
        private void SaveFileNode(string ffFilePath, string zoneFilePath, RawFileNode rawFileNode, string updatedText, FastFileHeader headerInfo)
        {
            byte[] updatedBytes = Encoding.UTF8.GetBytes(updatedText);
            int updatedSize = updatedBytes.Length;
            int originalSize = rawFileNode.MaxSize;

            // If new content exceeds the current slot, offer to resize
            if (updatedSize > originalSize)
            {
                var result = MessageBox.Show(
                    $"Content is {updatedSize} bytes (max {originalSize}).\n" +
                    "Do you want to expand the slot to fit?",
                    "Resize Raw File Slot",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // Backup zone file before rebuilding
                        CreateBackup(zoneFilePath);

                        // Rebuild the zone with the new content directly
                        // This uses the same approach as raw file injection
                        IncreaseSize(zoneFilePath, rawFileNode, updatedBytes);
                        rawFileNode.RawFileContent = updatedText;

                        MessageBox.Show(
                            $"Raw File '{rawFileNode.FileName}' saved successfully (zone rebuilt).",
                            "Saved",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Asterisk);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to save raw file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;
                }
                else
                {
                    // User declined—abort save
                    return;
                }
            }

            try
            {
                // Backup zone file before writing
                CreateBackup(zoneFilePath);

                // Now write the content (will pad with zeros if smaller than MaxSize)
                UpdateFileContent(zoneFilePath, rawFileNode, updatedBytes);
                rawFileNode.RawFileContent = updatedText;

                MessageBox.Show(
                    $"Raw File '{rawFileNode.FileName}' saved successfully.",
                    "Saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Asterisk);
            }
            catch (ArgumentException argEx) when (argEx.Message.Contains("compressed") || argEx.Message.Contains("Compressed"))
            {
                // Compressed content exceeds slot - offer to rebuild zone
                var result = MessageBox.Show(
                    $"{argEx.Message}\n\nDo you want to rebuild the zone to accommodate the new content?\n\n" +
                    "(Note: This will convert the file to uncompressed format)",
                    "Rebuild Zone",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // Rebuild the zone with the new content
                        // This creates an uncompressed version which MW2 can also read
                        IncreaseSize(zoneFilePath, rawFileNode, updatedBytes);
                        rawFileNode.RawFileContent = updatedText;
                        rawFileNode.IsCompressed = false; // Mark as no longer compressed after rebuild

                        MessageBox.Show(
                            $"Raw File '{rawFileNode.FileName}' saved successfully (zone rebuilt with uncompressed format).",
                            "Saved",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Asterisk);
                    }
                    catch (Exception rebuildEx)
                    {
                        MessageBox.Show($"Failed to rebuild zone: {rebuildEx.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (IOException ioEx)
            {
                MessageBox.Show($"Failed to save raw file: {ioEx.Message}", "IO Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Replaces invalid filename chars with underscores.
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }
    }
}
