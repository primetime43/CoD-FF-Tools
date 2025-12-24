using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Constants;

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

                // Update the AssetRecordCount field in the header.
                int assetRecordCountOffset = ZoneFileHeaderConstants.AssetRecordCountOffset;
                fs.Seek(assetRecordCountOffset, SeekOrigin.Begin);
                byte[] countBytes = new byte[4];
                fs.Read(countBytes, 0, countBytes.Length);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(countBytes);
                }
                uint currentCount = BitConverter.ToUInt32(countBytes, 0);
                uint newCount = currentCount + 1;
                byte[] newCountBytes = BitConverter.GetBytes(newCount);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(newCountBytes);
                }
                fs.Seek(assetRecordCountOffset, SeekOrigin.Begin);
                fs.Write(newCountBytes, 0, newCountBytes.Length);
            });

            // Update the AssetPoolEndOffset by the new record length.
            currentZone.AssetPoolEndOffset += newRecord.Length;

            // Instead of manually adjusting the in-memory asset records list,
            // simply re-parse the asset pool from the updated zone file.
            currentZone.ParseAssetPool();
        }
    }
}