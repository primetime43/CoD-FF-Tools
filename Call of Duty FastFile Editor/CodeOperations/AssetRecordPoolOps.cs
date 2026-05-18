using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.CodeOperations
{
    public class AssetRecordPoolOps
    {
        public static void AddRawFileAssetRecordToPool(ZoneFile currentZone, string zoneFilePath)
        {
            if (currentZone.AssetPoolStartOffset < 0 || currentZone.AssetPoolEndOffset < 0)
            {
                throw new InvalidOperationException("Asset pool offsets are not properly set.");
            }

            byte[] newRecord = new byte[8] { 0x00, 0x00, 0x00, 0x22, 0xFF, 0xFF, 0xFF, 0xFF };

            // AssetCount lives at a different offset on each platform (48/52/56-byte headers)
            // and is stored LE on PC, BE on console/Wii. Use library dispatcher.
            var parentFf = currentZone.ParentFastFile;
            bool isPC = parentFf?.IsPC ?? false;
            int assetRecordCountOffset = FastFileConstants.GetAssetCountOffset(
                parentFf?.GameVersionEnum ?? GameVersion.Unknown,
                parentFf?.IsXbox360 ?? false,
                isPC,
                parentFf?.IsWii ?? false);

            currentZone.ModifyZoneFile(fs =>
            {
                long insertPosition = currentZone.AssetPoolStartOffset;
                long originalLength = fs.Length;

                fs.Seek(insertPosition, SeekOrigin.Begin);
                byte[] tailBuffer = new byte[originalLength - insertPosition];
                fs.Read(tailBuffer, 0, tailBuffer.Length);

                fs.SetLength(originalLength + newRecord.Length);

                fs.Seek(insertPosition + newRecord.Length, SeekOrigin.Begin);
                fs.Write(tailBuffer, 0, tailBuffer.Length);

                fs.Seek(insertPosition, SeekOrigin.Begin);
                fs.Write(newRecord, 0, newRecord.Length);

                // Read existing AssetCount, increment, write back — endianness per platform.
                fs.Seek(assetRecordCountOffset, SeekOrigin.Begin);
                byte[] countBytes = new byte[4];
                fs.Read(countBytes, 0, countBytes.Length);
                uint currentCount = isPC
                    ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(countBytes)
                    : System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(countBytes);
                uint newCount = currentCount + 1;
                if (isPC)
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(countBytes, newCount);
                else
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(countBytes, newCount);
                fs.Seek(assetRecordCountOffset, SeekOrigin.Begin);
                fs.Write(countBytes, 0, countBytes.Length);
            });

            // Update the AssetPoolEndOffset by the new record length.
            currentZone.AssetPoolEndOffset += newRecord.Length;

            // Instead of manually adjusting the in-memory asset records list,
            // simply re-parse the asset pool from the updated zone file.
            currentZone.ParseAssetPool();
        }
    }
}