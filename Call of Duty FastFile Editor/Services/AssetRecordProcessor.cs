using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.ZoneParsers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Call_of_Duty_FastFile_Editor.Services
{
    public static class AssetRecordProcessor
    {
        /// <summary>
        /// Processes the given zone asset records, extracting various asset types
        /// and returns them in a ZoneAssetRecords object.
        /// Uses the game-specific parser from IGameDefinition for structure-based parsing,
        /// then falls back to pattern matching for rawfiles that appear after unsupported asset types.
        /// </summary>
        /// <param name="openedFastFile">The FastFile object containing the zone data.</param>
        /// <param name="zoneAssetRecords">The list of asset records extracted from the zone.</param>
        /// <param name="forcePatternMatching">If true, skip structure-based parsing and use pattern matching only.</param>
        /// <returns>A ZoneAssetRecords object containing updated asset lists and records.</returns>
        public static AssetRecordCollection ProcessAssetRecords(
            FastFile openedFastFile,
            List<ZoneAssetRecord> zoneAssetRecords,
            bool forcePatternMatching = false
        )
        {
            // Get the appropriate game definition for this FastFile
            IGameDefinition gameDefinition = GameDefinitionFactory.GetDefinition(openedFastFile);
            Debug.WriteLine($"[AssetRecordProcessor] Using game definition: {gameDefinition.ShortName}");

            // Create the result container.
            AssetRecordCollection result = new AssetRecordCollection();

            // Keep track of the index and end offset of the last successfully parsed asset record.
            int indexOfLastAssetRecordParsed = 0;
            int previousRecordEndOffset = 0;
            string assetRecordMethod = string.Empty;
            int structureParsingStoppedAtIndex = -1;
            int lastStructureParsedEndOffset = 0;

            // Zone file data
            byte[] zoneData = openedFastFile.OpenedFastFileZone.Data;

            // Log the starting offset for debugging
            int assetPoolEndOffset = openedFastFile.OpenedFastFileZone.AssetPoolEndOffset;
            Debug.WriteLine($"[AssetRecordProcessor] AssetPoolEndOffset = 0x{assetPoolEndOffset:X}");
            if (assetPoolEndOffset + 16 <= zoneData.Length)
            {
                string first16Bytes = BitConverter.ToString(zoneData, assetPoolEndOffset, 16).Replace("-", " ");
                Debug.WriteLine($"[AssetRecordProcessor] First 16 bytes at AssetPoolEndOffset: {first16Bytes}");
            }

            // If forcePatternMatching is true, skip structure-based parsing entirely
            if (forcePatternMatching)
            {
                structureParsingStoppedAtIndex = 0;
                goto PatternMatchingFallback;
            }

            // Loop through each asset record.
            for (int i = 0; i < zoneAssetRecords.Count; i++)
            {
                try
                {
                    // Get the asset type value based on game
                    int assetTypeValue = GetAssetTypeValue(openedFastFile, zoneAssetRecords[i]);
                    string assetTypeName = gameDefinition.GetAssetTypeName(assetTypeValue);

                    // Check if this asset type is supported
                    bool isSupported = gameDefinition.IsSupportedAssetType(assetTypeValue);

                    if (!isSupported)
                    {
                        // Stop structure-based processing - we can't determine the size of unsupported assets
                        Debug.WriteLine($"[AssetRecordProcessor] Stopping structure-based parsing at index {i}: unsupported asset type '{assetTypeName}'. Will use pattern matching for remaining rawfiles.");
                        structureParsingStoppedAtIndex = i;
                        if (i > 0 && zoneAssetRecords[i - 1].AssetRecordEndOffset > 0)
                        {
                            lastStructureParsedEndOffset = zoneAssetRecords[i - 1].AssetRecordEndOffset;
                        }
                        goto PatternMatchingFallback;
                    }

                    // For all records except the first, update previousRecordEndOffset.
                    if (i > 0)
                        previousRecordEndOffset = zoneAssetRecords[i - 1].AssetRecordEndOffset;

                    Debug.WriteLine($"Processing record index {i} ({assetTypeName}), previousRecordEndOffset: {previousRecordEndOffset}");

                    // Determine the starting offset for the current record
                    int startingOffset = DetermineStartingOffset(openedFastFile, zoneAssetRecords, i, previousRecordEndOffset, indexOfLastAssetRecordParsed);

                    // Use game-specific parser based on asset type
                    if (gameDefinition.IsRawFileType(assetTypeValue))
                    {
                        // Parse rawfile using game-specific parser
                        RawFileNode? node = gameDefinition.ParseRawFile(zoneData, startingOffset);

                        if (node != null)
                        {
                            assetRecordMethod = $"Raw file parsed using {gameDefinition.ShortName} structure-based parser.";
                            result.RawFileNodes.Add(node);
                            UpdateAssetRecord(zoneAssetRecords, i, node, assetRecordMethod);
                            indexOfLastAssetRecordParsed = i;
                        }
                        else
                        {
                            Debug.WriteLine($"[AssetRecordProcessor] Failed to parse rawfile at index {i}, offset 0x{startingOffset:X}. Stopping.");
                            structureParsingStoppedAtIndex = i;
                            lastStructureParsedEndOffset = startingOffset;
                            goto PatternMatchingFallback;
                        }
                    }
                    else if (gameDefinition.IsLocalizeType(assetTypeValue))
                    {
                        // Parse localize using game-specific parser
                        var (entry, nextOffset) = gameDefinition.ParseLocalizedEntry(zoneData, startingOffset);

                        if (entry != null)
                        {
                            assetRecordMethod = $"Localized asset parsed using {gameDefinition.ShortName} structure-based parser.";
                            result.LocalizedEntries.Add(entry);

                            // Update the asset record with localize info
                            var assetRecord = zoneAssetRecords[i];
                            assetRecord.AssetRecordEndOffset = nextOffset;
                            assetRecord.Name = entry.Key;
                            assetRecord.Content = entry.LocalizedText;
                            assetRecord.AdditionalData = assetRecordMethod;
                            zoneAssetRecords[i] = assetRecord;

                            indexOfLastAssetRecordParsed = i;
                        }
                        else
                        {
                            // Localize parsing failed - fall back to pattern matching for remaining rawfiles
                            Debug.WriteLine($"[AssetRecordProcessor] Failed to parse localize at index {i}, offset 0x{startingOffset:X}. Will use pattern matching for remaining rawfiles.");
                            structureParsingStoppedAtIndex = i;
                            lastStructureParsedEndOffset = startingOffset;
                            goto PatternMatchingFallback;
                        }
                    }
                    else if (gameDefinition.IsMenuFileType(assetTypeValue))
                    {
                        // Parse menufile using game-specific parser
                        MenuList? menuList = gameDefinition.ParseMenuFile(zoneData, startingOffset);

                        if (menuList != null)
                        {
                            assetRecordMethod = $"MenuList parsed using {gameDefinition.ShortName} structure-based parser.";
                            result.MenuLists.Add(menuList);

                            // Update the asset record with menufile info
                            var assetRecord = zoneAssetRecords[i];
                            assetRecord.AssetRecordEndOffset = menuList.DataEndOffset;
                            assetRecord.Name = menuList.Name;
                            assetRecord.Content = $"{menuList.MenuCount} menus";
                            assetRecord.AdditionalData = assetRecordMethod;
                            zoneAssetRecords[i] = assetRecord;

                            indexOfLastAssetRecordParsed = i;
                        }
                        else
                        {
                            // MenuFile parsing failed
                            Debug.WriteLine($"[AssetRecordProcessor] Failed to parse menufile at index {i}, offset 0x{startingOffset:X}. Stopping.");
                            structureParsingStoppedAtIndex = i;
                            lastStructureParsedEndOffset = startingOffset;
                            goto PatternMatchingFallback;
                        }
                    }
                    else if (gameDefinition.IsMaterialType(assetTypeValue))
                    {
                        // Parse material using game-specific parser
                        MaterialAsset? material = gameDefinition.ParseMaterial(zoneData, startingOffset);

                        if (material != null)
                        {
                            assetRecordMethod = $"Material parsed using {gameDefinition.ShortName} structure-based parser.";
                            result.Materials.Add(material);

                            // Update the asset record with material info
                            var assetRecord = zoneAssetRecords[i];
                            assetRecord.AssetRecordEndOffset = material.EndOffset;
                            assetRecord.Name = material.Name;
                            assetRecord.Content = $"Material: {material.Name}";
                            assetRecord.AdditionalData = assetRecordMethod;
                            zoneAssetRecords[i] = assetRecord;

                            indexOfLastAssetRecordParsed = i;
                        }
                        else
                        {
                            // Material parsing failed - continue to next asset
                            Debug.WriteLine($"[AssetRecordProcessor] Failed to parse material at index {i}, offset 0x{startingOffset:X}. Continuing.");
                            // Don't stop - materials are complex, just skip
                        }
                    }
                    else if (gameDefinition.IsTechSetType(assetTypeValue))
                    {
                        // Parse techset using game-specific parser
                        TechSetAsset? techSet = gameDefinition.ParseTechSet(zoneData, startingOffset);

                        if (techSet != null)
                        {
                            assetRecordMethod = $"TechSet parsed using {gameDefinition.ShortName} structure-based parser.";
                            result.TechSets.Add(techSet);

                            // Update the asset record with techset info
                            var assetRecord = zoneAssetRecords[i];
                            assetRecord.AssetRecordEndOffset = techSet.EndOffset;
                            assetRecord.Name = techSet.Name;
                            assetRecord.Content = $"TechSet: {techSet.Name}";
                            assetRecord.AdditionalData = assetRecordMethod;
                            zoneAssetRecords[i] = assetRecord;

                            indexOfLastAssetRecordParsed = i;
                        }
                        else
                        {
                            // TechSet parsing failed - continue to next asset
                            Debug.WriteLine($"[AssetRecordProcessor] Failed to parse techset at index {i}, offset 0x{startingOffset:X}. Continuing.");
                            // Don't stop - techsets are complex, just skip
                        }
                    }
                    else if (gameDefinition.IsXAnimType(assetTypeValue))
                    {
                        // Parse xanim using game-specific parser
                        XAnimParts? xanim = gameDefinition.ParseXAnim(zoneData, startingOffset);

                        if (xanim != null)
                        {
                            assetRecordMethod = $"XAnim parsed using {gameDefinition.ShortName} structure-based parser.";
                            result.XAnims.Add(xanim);

                            // Update the asset record with xanim info
                            var assetRecord = zoneAssetRecords[i];
                            assetRecord.AssetRecordEndOffset = xanim.EndOffset;
                            assetRecord.Name = xanim.Name;
                            assetRecord.Content = xanim.GetSummary();
                            assetRecord.AdditionalData = assetRecordMethod;
                            zoneAssetRecords[i] = assetRecord;

                            indexOfLastAssetRecordParsed = i;
                        }
                        else
                        {
                            // XAnim parsing failed - continue to next asset
                            Debug.WriteLine($"[AssetRecordProcessor] Failed to parse xanim at index {i}, offset 0x{startingOffset:X}. Continuing.");
                            // Don't stop - xanims are complex, just skip
                        }
                    }
                    else if (gameDefinition.IsStringTableType(assetTypeValue))
                    {
                        // Parse stringtable using game-specific parser
                        StringTable? stringTable = gameDefinition.ParseStringTable(zoneData, startingOffset);

                        if (stringTable != null)
                        {
                            assetRecordMethod = $"StringTable parsed using {gameDefinition.ShortName} structure-based parser.";
                            result.StringTables.Add(stringTable);

                            // Update the asset record with stringtable info
                            var assetRecord = zoneAssetRecords[i];
                            assetRecord.AssetRecordEndOffset = stringTable.DataEndPosition;
                            assetRecord.Name = stringTable.TableName;
                            assetRecord.Content = $"{stringTable.RowCount}x{stringTable.ColumnCount} ({stringTable.Cells?.Count ?? 0} cells)";
                            assetRecord.AdditionalData = assetRecordMethod;
                            zoneAssetRecords[i] = assetRecord;

                            indexOfLastAssetRecordParsed = i;
                        }
                        else
                        {
                            // StringTable parsing failed - continue to next asset
                            Debug.WriteLine($"[AssetRecordProcessor] Failed to parse stringtable at index {i}, offset 0x{startingOffset:X}. Continuing.");
                            // Don't stop - stringtables are complex, just skip
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to process asset record at index {i}: {ex.Message}. Stopping.");
                    break;
                }
            }

        PatternMatchingFallback:
            // If structure-based parsing stopped early due to unsupported assets,
            // use pattern matching to find remaining rawfiles
            if (structureParsingStoppedAtIndex >= 0)
            {
                Debug.WriteLine($"[AssetRecordProcessor] Starting pattern matching fallback from offset 0x{lastStructureParsedEndOffset:X}");

                int searchStartOffset = lastStructureParsedEndOffset > 0
                    ? lastStructureParsedEndOffset
                    : openedFastFile.OpenedFastFileZone.AssetPoolEndOffset;

                // Get already found rawfile names to avoid duplicates
                var existingFileNames = new HashSet<string>(
                    result.RawFileNodes.Select(n => n.FileName),
                    StringComparer.OrdinalIgnoreCase);

                // Count expected rawfiles from the asset pool
                int expectedRawFileCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    structureParsingStoppedAtIndex, gameDefinition.RawFileAssetType);
                int alreadyParsedRawFiles = result.RawFileNodes.Count;
                int remainingRawFiles = expectedRawFileCount - alreadyParsedRawFiles;

                Debug.WriteLine($"[AssetRecordProcessor] Expected {expectedRawFileCount} rawfiles, already parsed {alreadyParsedRawFiles}, remaining {remainingRawFiles}");

                int currentOffset = searchStartOffset;
                int rawFilesParsed = 0;
                bool needPatternMatchForFirst = true;

                while (rawFilesParsed < remainingRawFiles && currentOffset < zoneData.Length)
                {
                    RawFileNode? node = null;
                    string parseMethod = "";

                    // If we have a known offset (from previous file), try structure-based parsing first
                    if (!needPatternMatchForFirst)
                    {
                        node = gameDefinition.ParseRawFile(zoneData, currentOffset);
                        if (node != null)
                        {
                            parseMethod = $"Raw file parsed using {gameDefinition.ShortName} structure-based parser (fallback sequential).";
                        }
                    }

                    // If structure-based parsing failed or we need to find the first one, use pattern matching
                    if (node == null)
                    {
                        node = RawFileParser.ExtractSingleRawFileNodeWithPattern(zoneData, currentOffset, gameDefinition);
                        if (node != null)
                        {
                            parseMethod = "Raw file parsed using pattern matching (fallback).";
                        }
                    }

                    if (node == null)
                    {
                        // No more rawfiles found
                        Debug.WriteLine($"[AssetRecordProcessor] No rawfile found at offset 0x{currentOffset:X}, stopping");
                        break;
                    }

                    // Check if we already have this file from structure-based parsing
                    if (!existingFileNames.Contains(node.FileName))
                    {
                        node.AdditionalData = parseMethod;
                        result.RawFileNodes.Add(node);
                        existingFileNames.Add(node.FileName);
                        rawFilesParsed++;
                        Debug.WriteLine($"[AssetRecordProcessor] Fallback parsed rawfile #{rawFilesParsed}: '{node.FileName}' at offset 0x{node.StartOfFileHeader:X}");
                    }

                    // Move past this file - next iteration can try structure-based parsing
                    currentOffset = node.RawFileEndPosition;
                    needPatternMatchForFirst = false; // We now have a known offset for next file
                }

                Debug.WriteLine($"[AssetRecordProcessor] Fallback found {rawFilesParsed} additional rawfiles");

                // For localized entries, use the asset pool to know exactly how many to expect
                // Then parse them sequentially (NOT pattern scanning the entire zone)
                int expectedLocalizeCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    structureParsingStoppedAtIndex, gameDefinition.LocalizeAssetType);
                int alreadyParsedLocalizes = result.LocalizedEntries.Count;
                int remainingLocalizes = expectedLocalizeCount - alreadyParsedLocalizes;

                Debug.WriteLine($"[AssetRecordProcessor] Expected {expectedLocalizeCount} localizes, already parsed {alreadyParsedLocalizes}, remaining {remainingLocalizes}");

                if (remainingLocalizes > 0)
                {
                    // WaW localize entries are NOT stored consecutively - they're scattered throughout the zone
                    // Use pattern matching to find each entry independently
                    int localizeSearchOffset = searchStartOffset;
                    int localizesParsed = 0;
                    int maxSearchEnd = zoneData.Length;

                    Debug.WriteLine($"[AssetRecordProcessor] Searching for {remainingLocalizes} localize entries using pattern matching from 0x{localizeSearchOffset:X}");

                    while (localizesParsed < remainingLocalizes && localizeSearchOffset < maxSearchEnd)
                    {
                        // Find the next localize marker
                        int markerOffset = FindFirstLocalizeMarker(zoneData, localizeSearchOffset, maxSearchEnd);

                        if (markerOffset < 0)
                        {
                            Debug.WriteLine($"[AssetRecordProcessor] No more localize markers found after 0x{localizeSearchOffset:X}");
                            break;
                        }

                        // Try to parse the localize entry at this marker
                        var (entry, nextOffset) = gameDefinition.ParseLocalizedEntry(zoneData, markerOffset);

                        if (entry != null && nextOffset > markerOffset)
                        {
                            result.LocalizedEntries.Add(entry);
                            localizesParsed++;
                            Debug.WriteLine($"[AssetRecordProcessor] Parsed localize #{localizesParsed}: '{entry.Key}' at 0x{markerOffset:X}");
                            // Continue searching from after this entry
                            localizeSearchOffset = nextOffset;
                        }
                        else
                        {
                            // Marker found but parsing failed - skip past this marker and continue searching
                            Debug.WriteLine($"[AssetRecordProcessor] Marker at 0x{markerOffset:X} failed to parse, continuing search");
                            localizeSearchOffset = markerOffset + 8;
                        }
                    }

                    Debug.WriteLine($"[AssetRecordProcessor] Pattern matching found {localizesParsed} localized entries");
                }

                // For techsets, use pattern matching to find them
                int expectedTechSetCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    structureParsingStoppedAtIndex, gameDefinition.IsTechSetType);
                int alreadyParsedTechSets = result.TechSets.Count;
                int remainingTechSets = expectedTechSetCount - alreadyParsedTechSets;

                Debug.WriteLine($"[AssetRecordProcessor] Expected {expectedTechSetCount} techsets, already parsed {alreadyParsedTechSets}, remaining {remainingTechSets}");

                if (remainingTechSets > 0)
                {
                    // Use pattern matching to find techsets
                    int techSetSearchOffset = searchStartOffset;
                    int techSetsParsed = 0;

                    while (techSetsParsed < remainingTechSets && techSetSearchOffset < zoneData.Length)
                    {
                        var techSet = TechSetParser.FindNextTechSet(zoneData, techSetSearchOffset, 100000, isBigEndian: true);

                        if (techSet == null)
                        {
                            Debug.WriteLine($"[AssetRecordProcessor] No more techsets found after 0x{techSetSearchOffset:X}");
                            break;
                        }

                        result.TechSets.Add(techSet);
                        techSetsParsed++;
                        Debug.WriteLine($"[AssetRecordProcessor] Pattern matched techset #{techSetsParsed}: '{techSet.Name}' at 0x{techSet.StartOffset:X}");

                        // Move past this techset to find the next one
                        techSetSearchOffset = techSet.EndOffset;
                    }

                    Debug.WriteLine($"[AssetRecordProcessor] Pattern matching found {techSetsParsed} techsets");
                }

                // For menufiles, use pattern matching to find them
                // Note: Count from index 0 because menu files may appear before other asset types in the pool
                int expectedMenuFileCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    0, gameDefinition.MenuFileAssetType);
                int alreadyParsedMenuFiles = result.MenuLists.Count;
                int remainingMenuFiles = expectedMenuFileCount - alreadyParsedMenuFiles;

                Debug.WriteLine($"[AssetRecordProcessor] Expected {expectedMenuFileCount} menufiles, already parsed {alreadyParsedMenuFiles}, remaining {remainingMenuFiles}");

                if (remainingMenuFiles > 0)
                {
                    // Use pattern matching to find menufiles
                    // Start from asset pool end since menu files can appear anywhere after the pool
                    int menuFileSearchOffset = openedFastFile.OpenedFastFileZone.AssetPoolEndOffset;
                    int menuFilesParsed = 0;

                    while (menuFilesParsed < remainingMenuFiles && menuFileSearchOffset < zoneData.Length)
                    {
                        // Search up to 1MB or remaining file length for menu files
                        int maxSearchBytes = Math.Min(1000000, zoneData.Length - menuFileSearchOffset);
                        var menuList = FindNextMenuList(zoneData, menuFileSearchOffset, maxSearchBytes, isBigEndian: true);

                        if (menuList == null)
                        {
                            Debug.WriteLine($"[AssetRecordProcessor] No more menufiles found after 0x{menuFileSearchOffset:X}");
                            break;
                        }

                        result.MenuLists.Add(menuList);
                        menuFilesParsed++;
                        Debug.WriteLine($"[AssetRecordProcessor] Pattern matched menufile #{menuFilesParsed}: '{menuList.Name}' at 0x{menuList.StartOfFileHeader:X}");

                        // Move past this menulist to find the next one
                        menuFileSearchOffset = menuList.DataEndOffset;
                    }

                    Debug.WriteLine($"[AssetRecordProcessor] Pattern matching found {menuFilesParsed} menufiles");
                }

                // For xanims, use pattern matching to find them
                int expectedXAnimCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    structureParsingStoppedAtIndex, gameDefinition.XAnimAssetType);
                int alreadyParsedXAnims = result.XAnims.Count;
                int remainingXAnims = expectedXAnimCount - alreadyParsedXAnims;

                Debug.WriteLine($"[AssetRecordProcessor] Expected {expectedXAnimCount} xanims, already parsed {alreadyParsedXAnims}, remaining {remainingXAnims}");

                if (remainingXAnims > 0)
                {
                    // Use pattern matching to find xanims
                    int xanimSearchOffset = searchStartOffset;
                    int xanimsParsed = 0;

                    while (xanimsParsed < remainingXAnims && xanimSearchOffset < zoneData.Length)
                    {
                        var xanim = FindNextXAnim(zoneData, xanimSearchOffset, 500000, gameDefinition);

                        if (xanim == null)
                        {
                            Debug.WriteLine($"[AssetRecordProcessor] No more xanims found after 0x{xanimSearchOffset:X}");
                            break;
                        }

                        result.XAnims.Add(xanim);
                        xanimsParsed++;
                        Debug.WriteLine($"[AssetRecordProcessor] Pattern matched xanim #{xanimsParsed}: '{xanim.Name}' at 0x{xanim.StartOffset:X}");

                        // Move past this xanim to find the next one
                        xanimSearchOffset = xanim.EndOffset;
                    }

                    Debug.WriteLine($"[AssetRecordProcessor] Pattern matching found {xanimsParsed} xanims");
                }

                // For stringtables, use pattern matching to find them
                // Count from index 0 because StringTables may appear anywhere in the asset pool
                int expectedStringTableCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    0, gameDefinition.StringTableAssetType);
                int alreadyParsedStringTables = result.StringTables.Count;
                int remainingStringTables = expectedStringTableCount - alreadyParsedStringTables;

                Debug.WriteLine($"[AssetRecordProcessor] Expected {expectedStringTableCount} stringtables, already parsed {alreadyParsedStringTables}, remaining {remainingStringTables}");

                if (remainingStringTables > 0)
                {
                    // Get indices of unparsed stringtable asset records
                    // Search from index 0 because StringTables may appear before the index where parsing stopped
                    var stringTableIndices = new List<int>();
                    for (int i = 0; i < zoneAssetRecords.Count; i++)
                    {
                        int assetType = GetAssetTypeValue(openedFastFile, zoneAssetRecords[i]);
                        if (gameDefinition.IsStringTableType(assetType) &&
                            string.IsNullOrEmpty(zoneAssetRecords[i].Name))
                        {
                            stringTableIndices.Add(i);
                            Debug.WriteLine($"[AssetRecordProcessor] Found unparsed stringtable at index {i}");
                        }
                    }
                    Debug.WriteLine($"[AssetRecordProcessor] Found {stringTableIndices.Count} unparsed stringtable records in asset pool");

                    // Use pattern matching to find stringtables
                    int stringTableSearchOffset = searchStartOffset;
                    int stringTablesParsed = 0;

                    while (stringTablesParsed < remainingStringTables && stringTableSearchOffset < zoneData.Length)
                    {
                        // Use the existing pattern matching method from StringTable class
                        var stringTable = StringTable.FindSingleCsvStringTableWithPattern(
                            openedFastFile.OpenedFastFileZone, stringTableSearchOffset);

                        if (stringTable == null)
                        {
                            Debug.WriteLine($"[AssetRecordProcessor] No more stringtables found after 0x{stringTableSearchOffset:X}");
                            break;
                        }

                        result.StringTables.Add(stringTable);

                        // Update the corresponding asset record if we have one
                        if (stringTablesParsed < stringTableIndices.Count)
                        {
                            int recordIndex = stringTableIndices[stringTablesParsed];
                            Debug.WriteLine($"[AssetRecordProcessor] Updating asset record at index {recordIndex} with stringtable '{stringTable.TableName}'");
                            var assetRecord = zoneAssetRecords[recordIndex];
                            assetRecord.AssetRecordEndOffset = stringTable.DataEndPosition;
                            assetRecord.Name = stringTable.TableName;
                            assetRecord.Content = $"{stringTable.RowCount}x{stringTable.ColumnCount} ({stringTable.Cells?.Count ?? 0} cells)";
                            assetRecord.AdditionalData = "StringTable parsed using pattern matching (fallback).";
                            assetRecord.HeaderStartOffset = stringTable.StartOfFileHeader;
                            assetRecord.HeaderEndOffset = stringTable.EndOfFileHeader;
                            assetRecord.AssetDataStartPosition = stringTable.DataStartPosition;
                            assetRecord.AssetDataEndOffset = stringTable.DataEndPosition;
                            zoneAssetRecords[recordIndex] = assetRecord;
                            Debug.WriteLine($"[AssetRecordProcessor] Asset record {recordIndex} updated: Name='{assetRecord.Name}', AdditionalData='{assetRecord.AdditionalData}'");
                        }
                        else
                        {
                            Debug.WriteLine($"[AssetRecordProcessor] No asset record to update for stringtable '{stringTable.TableName}' (stringTablesParsed={stringTablesParsed}, indices count={stringTableIndices.Count})");
                        }

                        stringTablesParsed++;
                        Debug.WriteLine($"[AssetRecordProcessor] Pattern matched stringtable #{stringTablesParsed}: '{stringTable.TableName}' at 0x{stringTable.StartOfFileHeader:X}");

                        // Move past this stringtable to find the next one
                        stringTableSearchOffset = stringTable.DataEndPosition + 1;
                    }

                    Debug.WriteLine($"[AssetRecordProcessor] Pattern matching found {stringTablesParsed} stringtables");
                }
            }

            // Save the updated asset records into the result container.
            result.UpdatedRecords = zoneAssetRecords;
            return result;
        }

        /// <summary>
        /// Updates the zone asset record at the specified index with data from an asset that implements IAssetRecordUpdatable.
        /// Also sets an AdditionalData string for debugging purposes.
        /// </summary>
        /// <typeparam name="T">Type implementing IAssetRecordUpdatable.</typeparam>
        /// <param name="zoneAssetRecords">The list of asset records.</param>
        /// <param name="index">Index of the record to update.</param>
        /// <param name="record">Asset data used for updating.</param>
        /// <param name="assetRecordMethod">A descriptive message of the extraction method used.</param>
        private static void UpdateAssetRecord<T>(List<ZoneAssetRecord> zoneAssetRecords, int index, T record, string assetRecordMethod) where T : IAssetRecordUpdatable
        {
            // Retrieve the asset record at the given index.
            var assetRecord = zoneAssetRecords[index];
            // Update the asset record using the provided data.
            record.UpdateAssetRecord(ref assetRecord);
            // Store the extraction method message for debugging.
            assetRecord.AdditionalData = assetRecordMethod;
            // Write the updated asset record back into the list.
            zoneAssetRecords[index] = assetRecord;
            Debug.WriteLine($"Updated asset record at index {index}: {assetRecord.Name}");
        }

        /// <summary>
        /// Gets the asset type value from the record based on the game type.
        /// </summary>
        private static int GetAssetTypeValue(FastFile fastFile, ZoneAssetRecord record)
        {
            if (fastFile.IsCod4File)
                return (int)record.AssetType_COD4;
            if (fastFile.IsCod5File)
                return (int)record.AssetType_COD5;
            if (fastFile.IsMW2File)
                return (int)record.AssetType_MW2;
            return 0;
        }

        /// <summary>
        /// Determines the starting offset for parsing an asset record.
        /// </summary>
        private static int DetermineStartingOffset(
            FastFile fastFile,
            List<ZoneAssetRecord> records,
            int currentIndex,
            int previousRecordEndOffset,
            int indexOfLastParsed)
        {
            if (currentIndex == 0)
            {
                return fastFile.OpenedFastFileZone.AssetPoolEndOffset;
            }
            else if (previousRecordEndOffset > 0)
            {
                return previousRecordEndOffset;
            }
            else
            {
                return records[indexOfLastParsed].AssetRecordEndOffset;
            }
        }

        /// <summary>
        /// Counts the expected number of a specific asset type from the asset pool records.
        /// </summary>
        private static int CountExpectedAssetType(FastFile fastFile, List<ZoneAssetRecord> records, int startIndex, byte assetType)
        {
            int count = 0;
            for (int i = startIndex; i < records.Count; i++)
            {
                int recordAssetType = GetAssetTypeValue(fastFile, records[i]);
                if (recordAssetType == assetType)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Counts the expected number of assets matching a predicate from the asset pool records.
        /// </summary>
        private static int CountExpectedAssetType(FastFile fastFile, List<ZoneAssetRecord> records, int startIndex, Func<int, bool> assetTypePredicate)
        {
            int count = 0;
            for (int i = startIndex; i < records.Count; i++)
            {
                int recordAssetType = GetAssetTypeValue(fastFile, records[i]);
                if (assetTypePredicate(recordAssetType))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Finds the first valid localize marker within the specified range.
        /// A valid marker is either:
        /// - 8 consecutive 0xFF bytes (both value and key inline)
        /// - 4 non-0xFF bytes followed by 4 0xFF bytes (key only inline)
        /// Also validates the key looks like a proper localize key (SCREAMING_SNAKE_CASE).
        /// </summary>
        private static int FindFirstLocalizeMarker(byte[] data, int startOffset, int endOffset)
        {
            for (int pos = startOffset; pos <= endOffset - 8; pos++)
            {
                // Check if bytes 4-7 are FF (key pointer must be inline)
                if (data[pos + 4] != 0xFF || data[pos + 5] != 0xFF ||
                    data[pos + 6] != 0xFF || data[pos + 7] != 0xFF)
                {
                    continue;
                }

                // Check if first 4 bytes are also FF (both inline) or not
                bool valuePointerIsFF = data[pos] == 0xFF && data[pos + 1] == 0xFF &&
                                        data[pos + 2] == 0xFF && data[pos + 3] == 0xFF;

                // Verify there's valid data after the marker
                if (pos + 8 >= data.Length)
                    continue;

                byte nextByte = data[pos + 8];

                // Skip if still in padding (next byte is FF)
                if (nextByte == 0xFF)
                    continue;

                // If key-only (value not inline), next byte should be printable (start of key)
                if (!valuePointerIsFF && nextByte == 0x00)
                    continue;

                // Additional validation: try to read the key and check it looks like a localize key
                int keyOffset = pos + 8;
                if (valuePointerIsFF)
                {
                    // Skip the value string first to get to the key
                    while (keyOffset < data.Length && data[keyOffset] != 0x00)
                        keyOffset++;
                    keyOffset++; // Skip null terminator
                }

                // Read the key
                string key = ReadNullTerminatedStringAt(data, keyOffset);
                if (!IsValidLocalizeKey(key))
                {
                    continue; // Not a valid localize key, keep searching
                }

                // This looks like a valid localize marker
                return pos;
            }

            return -1; // Not found
        }

        /// <summary>
        /// Reads a null-terminated string from the data at the given offset.
        /// </summary>
        private static string ReadNullTerminatedStringAt(byte[] data, int offset)
        {
            var sb = new System.Text.StringBuilder();
            while (offset < data.Length && data[offset] != 0x00)
            {
                sb.Append((char)data[offset]);
                offset++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Validates that a string looks like a valid localization key.
        /// Keys are typically in SCREAMING_SNAKE_CASE but may vary by game.
        /// </summary>
        private static bool IsValidLocalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 2 || key.Length > 150)
                return false;

            // Must start with a letter
            if (!char.IsLetter(key[0]))
                return false;

            // Check all characters are valid (letters, digits, underscores)
            // Allow both upper and lower case for flexibility across game versions
            foreach (char c in key)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Finds the next MenuList by pattern matching.
        /// Searches for the MenuList header pattern: [FF FF FF FF] [menuCount] [FF FF FF FF] [name string]
        /// </summary>
        private static MenuList? FindNextMenuList(byte[] zoneData, int startOffset, int maxSearchBytes, bool isBigEndian)
        {
            Debug.WriteLine($"[AssetRecordProcessor] Searching for MenuList from 0x{startOffset:X}, max {maxSearchBytes} bytes");

            int endOffset = Math.Min(startOffset + maxSearchBytes, zoneData.Length - 16);

            for (int pos = startOffset; pos < endOffset; pos++)
            {
                // Look for the pattern: [FF FF FF FF] [4 bytes count] [FF FF FF FF]
                uint namePtr = ReadUInt32BE(zoneData, pos);
                if (namePtr != 0xFFFFFFFF)
                    continue;

                // Check if menus pointer at offset +8 is also 0xFFFFFFFF
                if (pos + 12 >= zoneData.Length)
                    continue;

                uint menusPtr = ReadUInt32BE(zoneData, pos + 8);
                if (menusPtr != 0xFFFFFFFF)
                    continue;

                // Read menu count
                int menuCount = (int)ReadUInt32BE(zoneData, pos + 4);
                if (menuCount < 0 || menuCount > 500)
                    continue;

                // Check for valid name string after header
                int nameOffset = pos + 12;
                if (nameOffset >= zoneData.Length)
                    continue;

                // First byte should be printable ASCII (start of path like 'u' for "ui/...")
                byte firstChar = zoneData[nameOffset];
                if (firstChar < 0x20 || firstChar > 0x7E)
                    continue;

                // Try to read and validate the name
                string name = ReadNullTerminatedStringAt(zoneData, nameOffset);
                if (!IsValidMenuFileName(name))
                    continue;

                Debug.WriteLine($"[AssetRecordProcessor] Found potential MenuList at 0x{pos:X}: name='{name}', count={menuCount}");

                // Try to parse it
                var menuList = MenuListParser.ParseMenuList(zoneData, pos, isBigEndian);
                if (menuList != null)
                {
                    Debug.WriteLine($"[AssetRecordProcessor] Successfully parsed MenuList '{menuList.Name}' with {menuList.Menus.Count} menus");
                    return menuList;
                }
            }

            Debug.WriteLine($"[AssetRecordProcessor] No MenuList found in search range");
            return null;
        }

        /// <summary>
        /// Validates that a string looks like a valid menu file name.
        /// Examples: "ui_mp/main.menu", "ui/scriptmenus/class.menu"
        /// </summary>
        private static bool IsValidMenuFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 5 || name.Length > 256)
                return false;

            // Should contain path separators and look like a menu path
            if (!name.Contains('/') && !name.Contains('\\'))
                return false;

            // Should end with .menu or contain "menu" in path
            if (!name.EndsWith(".menu", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("menu", StringComparison.OrdinalIgnoreCase))
                return false;

            // Check for valid path characters
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '/' && c != '\\' && c != '.' && c != '-')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Finds the next XAnim by pattern matching.
        /// Searches for the XAnim header pattern: [FF FF FF FF] followed by valid animation data.
        /// </summary>
        private static XAnimParts? FindNextXAnim(byte[] zoneData, int startOffset, int maxSearchBytes, IGameDefinition gameDefinition)
        {
            Debug.WriteLine($"[AssetRecordProcessor] Searching for XAnim from 0x{startOffset:X}, max {maxSearchBytes} bytes");

            // Bounds check - ensure startOffset is valid
            if (startOffset < 0 || startOffset >= zoneData.Length - 100)
            {
                Debug.WriteLine($"[AssetRecordProcessor] XAnim search startOffset 0x{startOffset:X} is out of bounds (zone size: 0x{zoneData.Length:X})");
                return null;
            }

            int endOffset = Math.Min(startOffset + maxSearchBytes, zoneData.Length - 100);

            for (int pos = startOffset; pos < endOffset; pos++)
            {
                // Look for the pattern: [FF FF FF FF] - name pointer (inline)
                uint namePtr = ReadUInt32BE(zoneData, pos);
                if (namePtr != 0xFFFFFFFF)
                    continue;

                // Try to parse it using the game definition's parser
                var xanim = gameDefinition.ParseXAnim(zoneData, pos);
                if (xanim != null)
                {
                    Debug.WriteLine($"[AssetRecordProcessor] Found XAnim '{xanim.Name}' at 0x{pos:X}");
                    return xanim;
                }
            }

            Debug.WriteLine($"[AssetRecordProcessor] No XAnim found in search range");
            return null;
        }

        private static uint ReadUInt32BE(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) return 0;
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) |
                          (data[offset + 2] << 8) | data[offset + 3]);
        }
    }
}
