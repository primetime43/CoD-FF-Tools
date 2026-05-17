using Call_of_Duty_FastFile_Editor.Models;
using System.Collections.Generic;
using System.Windows.Forms;
using static Call_of_Duty_FastFile_Editor.Models.FastFile;

namespace Call_of_Duty_FastFile_Editor.Services
{
    public interface IRawFileService
    {
        /// <summary>
        /// Exports the given raw file node to an external file with the specified extension.
        /// Includes the zone header for re-injection compatibility.
        /// </summary>
        /// <param name="node">The raw file node to export.</param>
        /// <param name="extension">The file extension to use for the exported file (including the leading dot).</param>
        void ExportRawFile(RawFileNode node, string extension);

        /// <summary>
        /// Exports only the content of the raw file (without zone header) for external editing.
        /// </summary>
        /// <param name="node">The raw file node to export.</param>
        /// <param name="extension">The file extension to use for the exported file.</param>
        void ExportRawFileContentOnly(RawFileNode node, string extension);

        /// <summary>
        /// Overwrites the content of a raw file inside the zone file, padding or trimming as needed.
        /// </summary>
        /// <param name="zoneFilePath">Path to the decompressed zone file.</param>
        /// <param name="node">The raw file node whose content will be updated.</param>
        /// <param name="newContent">The new byte content to write into the raw file slot.</param>
        void UpdateFileContent(string zoneFilePath, RawFileNode node, byte[] newContent);

        /// <summary>
        /// Saves any pending edits for the selected raw file back into the zone file and recompresses it into the Fast File.
        /// </summary>
        /// <param name="filesTreeView">The TreeView control containing the raw file nodes.</param>
        /// <param name="ffFilePath">Path to the original Fast File (.ff).</param>
        /// <param name="zoneFilePath">Path to the decompressed Zone File (.zone).</param>
        /// <param name="rawFileNodes">The collection of all raw file nodes in the zone.</param>
        /// <param name="updatedText">The updated text content for the raw file.</param>
        /// <param name="openedFastFile">The FastFile instance representing the opened .ff.</param>
        void SaveZoneRawFileChanges(
            TreeView filesTreeView,
            string ffFilePath,
            string zoneFilePath,
            List<RawFileNode> rawFileNodes,
            string updatedText,
            FastFile openedFastFile);

        /// <summary>
        /// Renames a raw file entry inside the zone by updating its header and refreshing the TreeView label.
        /// </summary>
        /// <param name="filesTreeView">The TreeView control listing raw file entries.</param>
        /// <param name="ffFilePath">Path to the Fast File (.ff) being edited.</param>
        /// <param name="zoneFilePath">Path to the decompressed Zone File (.zone).</param>
        /// <param name="rawFileNodes">The list of raw file nodes.</param>
        /// <param name="openedFastFile">The FastFile instance for context.</param>
        void RenameRawFile(
            TreeView filesTreeView,
            string ffFilePath,
            string zoneFilePath,
            List<RawFileNode> rawFileNodes,
            FastFile openedFastFile);

        /// <summary>
        /// Increases the size of an existing raw file entry by shifting subsequent data and padding with zeros.
        /// </summary>
        /// <param name="zoneFilePath">Path to the decompressed Zone File (.zone).</param>
        /// <param name="rawFileNode">The raw file node to resize.</param>
        /// <param name="newContent">The new content whose length determines the new size.</param>
        void IncreaseSize(string zoneFilePath, RawFileNode rawFileNode, byte[] newContent);


        /// <summary>
        /// Adjusts the maximum size of a raw file node by padding with zeros or shifting data.
        /// </summary>
        /// <param name="zoneFilePath">Path to the decompressed Zone File (.zone).</param>
        /// <param name="rawFileNode">The raw file node whose size is to be changed.</param>
        /// <param name="newSize">The desired new size in bytes.</param>
        void AdjustRawFileNodeSize(string zoneFilePath, RawFileNode rawFileNode, int newSize);
    }
}
