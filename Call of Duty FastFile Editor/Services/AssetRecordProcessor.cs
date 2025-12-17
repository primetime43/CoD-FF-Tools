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
                        // For unsupported asset types, we can't parse them directly
                        // Record the first unsupported asset index so pattern matching fallback will run
                        if (structureParsingStoppedAtIndex < 0)
                        {
                            structureParsingStoppedAtIndex = i;
                            // Use the last SUCCESSFULLY parsed asset's end offset
                            if (indexOfLastAssetRecordParsed >= 0 && zoneAssetRecords[indexOfLastAssetRecordParsed].AssetRecordEndOffset > 0)
                            {
                                lastStructureParsedEndOffset = zoneAssetRecords[indexOfLastAssetRecordParsed].AssetRecordEndOffset;
                            }
                            Debug.WriteLine($"[AssetRecordProcessor] First unsupported asset type '{assetTypeName}' at index {i}. Last parsed asset end: 0x{lastStructureParsedEndOffset:X}. Will use pattern matching for remaining assets.");
                        }
                        // Continue to skip all unsupported assets
                        continue;
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
                            assetRecordMethod = $"Structure-based ({gameDefinition.ShortName})";
                            node.AdditionalData = assetRecordMethod;
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
                            assetRecordMethod = $"Structure-based ({gameDefinition.ShortName})";
                            entry.AdditionalData = assetRecordMethod;
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
                            assetRecordMethod = $"Structure-based ({gameDefinition.ShortName})";
                            menuList.AdditionalData = assetRecordMethod;
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
                            assetRecordMethod = $"Structure-based ({gameDefinition.ShortName})";
                            material.AdditionalData = assetRecordMethod;
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
                            assetRecordMethod = $"Structure-based ({gameDefinition.ShortName})";
                            techSet.AdditionalData = assetRecordMethod;
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

                        // If parsing failed at the exact offset, search forward to find the next valid XAnim
                        if (xanim == null && startingOffset > 0)
                        {
                            Debug.WriteLine($"[AssetRecordProcessor] XAnim parse failed at 0x{startingOffset:X}, searching forward for valid header...");
                            // Search a larger range (2MB) since XAnims may be scattered with large gaps
                            xanim = FindNextXAnim(zoneData, startingOffset + 1, 2000000, gameDefinition);
                            if (xanim != null)
                            {
                                Debug.WriteLine($"[AssetRecordProcessor] Found XAnim '{xanim.Name}' by forward search at 0x{xanim.StartOffset:X}");
                            }
                            else
                            {
                                // Forward search failed - trigger pattern matching fallback for remaining XAnims
                                Debug.WriteLine($"[AssetRecordProcessor] Forward search failed at 0x{startingOffset:X}, triggering pattern matching fallback for remaining XAnims");
                                if (structureParsingStoppedAtIndex < 0)
                                {
                                    structureParsingStoppedAtIndex = i;
                                    lastStructureParsedEndOffset = startingOffset;
                                }
                                // Skip remaining XAnims in the main loop - let pattern matching handle them
                                continue;
                            }
                        }

                        if (xanim != null)
                        {
                            assetRecordMethod = $"Structure-based ({gameDefinition.ShortName})";
                            xanim.AdditionalData = assetRecordMethod;
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
                            assetRecordMethod = $"Structure-based ({gameDefinition.ShortName})";
                            stringTable.AdditionalData = assetRecordMethod;
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
                    else if (gameDefinition.IsWeaponType(assetTypeValue))
                    {
                        // Weapons are handled in the pattern matching fallback section
                        // because they typically appear after unsupported asset types
                        // and we can't calculate their exact offset from the asset pool
                        Debug.WriteLine($"[AssetRecordProcessor] Weapon at index {i} will be handled by pattern matching fallback");
                        if (structureParsingStoppedAtIndex < 0)
                        {
                            structureParsingStoppedAtIndex = i;
                        }
                        continue;
                    }
                    else if (gameDefinition.IsImageType(assetTypeValue))
                    {
                        // Images are handled in the pattern matching fallback section
                        // because they typically appear after unsupported asset types
                        // and we can't calculate their exact offset from the asset pool
                        Debug.WriteLine($"[AssetRecordProcessor] Image at index {i} will be handled by pattern matching fallback");
                        if (structureParsingStoppedAtIndex < 0)
                        {
                            structureParsingStoppedAtIndex = i;
                        }
                        continue;
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
                            parseMethod = $"Structure-based sequential ({gameDefinition.ShortName})";
                        }
                    }

                    // If structure-based parsing failed or we need to find the first one, use pattern matching
                    if (node == null)
                    {
                        node = RawFileParser.ExtractSingleRawFileNodeWithPattern(zoneData, currentOffset, gameDefinition);
                        if (node != null)
                        {
                            parseMethod = "Pattern matching";
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
                    int consecutiveFailures = 0;
                    const int maxConsecutiveFailures = 50; // Stop after 50 consecutive failed markers

                    while (localizesParsed < remainingLocalizes && localizeSearchOffset < maxSearchEnd)
                    {
                        // Find the next localize marker
                        int markerOffset = FindFirstLocalizeMarker(zoneData, localizeSearchOffset, maxSearchEnd);

                        if (markerOffset < 0)
                            break;

                        // Try to parse the localize entry at this marker
                        var (entry, nextOffset) = gameDefinition.ParseLocalizedEntry(zoneData, markerOffset);

                        if (entry != null && nextOffset > markerOffset)
                        {
                            entry.AdditionalData = "Pattern matching";
                            result.LocalizedEntries.Add(entry);
                            localizesParsed++;
                            consecutiveFailures = 0; // Reset on success
                            // Continue searching from after this entry
                            localizeSearchOffset = nextOffset;
                        }
                        else
                        {
                            // Marker found but parsing failed - skip past this marker and continue searching
                            consecutiveFailures++;
                            if (consecutiveFailures >= maxConsecutiveFailures)
                                break;
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

                        techSet.AdditionalData = "Pattern matching";
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

                        menuList.AdditionalData = "Pattern matching";
                        result.MenuLists.Add(menuList);
                        menuFilesParsed++;
                        Debug.WriteLine($"[AssetRecordProcessor] Pattern matched menufile #{menuFilesParsed}: '{menuList.Name}' at 0x{menuList.StartOfFileHeader:X}");

                        // Move past this menulist to find the next one
                        menuFileSearchOffset = menuList.DataEndOffset;
                    }

                    Debug.WriteLine($"[AssetRecordProcessor] Pattern matching found {menuFilesParsed} menufiles");
                }

                // For xanims, use pattern matching to find them
                // Count from index 0 because XAnims may appear anywhere in the asset pool
                int expectedXAnimCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    0, gameDefinition.XAnimAssetType);
                int alreadyParsedXAnims = result.XAnims.Count;
                int remainingXAnims = expectedXAnimCount - alreadyParsedXAnims;

                if (remainingXAnims > 0)
                {
                    // Get names of already parsed XAnims to avoid duplicates
                    var existingXAnimNames = new HashSet<string>(
                        result.XAnims.Select(x => x.Name),
                        StringComparer.OrdinalIgnoreCase);

                    // Use pattern matching to find xanims
                    // Search the entire zone since XAnims can be scattered with large gaps between them
                    // Start from AssetPoolEndOffset to ensure we find all XAnims
                    int xanimSearchOffset = openedFastFile.OpenedFastFileZone.AssetPoolEndOffset;
                    int xanimsParsed = 0;
                    int searchChunkSize = 2000000; // Search in 2MB chunks for faster scanning

                    while (xanimsParsed < remainingXAnims && xanimSearchOffset < zoneData.Length - 100)
                    {
                        var xanim = FindNextXAnim(zoneData, xanimSearchOffset, searchChunkSize, gameDefinition);

                        if (xanim != null)
                        {
                            // Skip duplicates
                            if (existingXAnimNames.Contains(xanim.Name))
                            {
                                xanimSearchOffset = xanim.EndOffset;
                                continue;
                            }

                            existingXAnimNames.Add(xanim.Name);
                            xanim.AdditionalData = "Pattern matching";
                            result.XAnims.Add(xanim);
                            xanimsParsed++;

                            // Move past this xanim to find the next one
                            xanimSearchOffset = xanim.EndOffset;
                        }
                        else
                        {
                            // No XAnim found in this chunk - continue searching from end of chunk
                            // XAnims may be scattered with gaps larger than the search chunk
                            int nextSearchOffset = xanimSearchOffset + searchChunkSize;
                            if (nextSearchOffset >= zoneData.Length - 100)
                                break;
                            xanimSearchOffset = nextSearchOffset;
                        }
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

                        stringTable.AdditionalData = "Pattern matching";
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
                            assetRecord.AdditionalData = "Pattern matching";
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

                // For weapons, use pattern matching to find them
                int expectedWeaponCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    0, gameDefinition.WeaponAssetType); // Count from index 0 since weapons may appear early
                int alreadyParsedWeapons = result.Weapons.Count;
                int remainingWeapons = expectedWeaponCount - alreadyParsedWeapons;

                Debug.WriteLine($"[AssetRecordProcessor] Expected {expectedWeaponCount} weapons, already parsed {alreadyParsedWeapons}, remaining {remainingWeapons}");

                if (remainingWeapons > 0)
                {
                    // Get indices of unparsed weapon asset records
                    var weaponIndices = new List<int>();
                    for (int i = 0; i < zoneAssetRecords.Count; i++)
                    {
                        int assetType = GetAssetTypeValue(openedFastFile, zoneAssetRecords[i]);
                        if (gameDefinition.IsWeaponType(assetType) &&
                            string.IsNullOrEmpty(zoneAssetRecords[i].Name))
                        {
                            weaponIndices.Add(i);
                            Debug.WriteLine($"[AssetRecordProcessor] Found unparsed weapon at index {i}");
                        }
                    }
                    Debug.WriteLine($"[AssetRecordProcessor] Found {weaponIndices.Count} unparsed weapon records in asset pool");

                    // Use pattern matching to find weapons
                    // Search the entire zone in large chunks
                    // Start from AssetPoolEndOffset to ensure we find all weapons, not searchStartOffset
                    // which may skip weapons if other assets were parsed after them
                    int weaponSearchOffset = openedFastFile.OpenedFastFileZone.AssetPoolEndOffset;
                    int weaponsParsed = 0;
                    int searchChunkSize = 1000000; // Search in 1MB chunks
                    var foundWeaponNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    Debug.WriteLine($"[AssetRecordProcessor] Starting weapon search from 0x{weaponSearchOffset:X}, looking for {remainingWeapons} weapons");

                    while (weaponsParsed < remainingWeapons && weaponSearchOffset < zoneData.Length - 100)
                    {
                        var weapon = FindNextWeapon(zoneData, weaponSearchOffset, searchChunkSize, gameDefinition);

                        if (weapon != null)
                        {
                            // Skip duplicates
                            if (foundWeaponNames.Contains(weapon.InternalName))
                            {
                                Debug.WriteLine($"[AssetRecordProcessor] Skipping duplicate weapon '{weapon.InternalName}'");
                                weaponSearchOffset = weapon.EndOffset;
                                continue;
                            }

                            foundWeaponNames.Add(weapon.InternalName);
                            weapon.AdditionalData = "Pattern matching";
                            result.Weapons.Add(weapon);

                            // Update the corresponding asset record if we have one
                            if (weaponsParsed < weaponIndices.Count)
                            {
                                int recordIndex = weaponIndices[weaponsParsed];
                                Debug.WriteLine($"[AssetRecordProcessor] Updating asset record at index {recordIndex} with weapon '{weapon.InternalName}'");
                                var assetRecord = zoneAssetRecords[recordIndex];
                                assetRecord.AssetRecordEndOffset = weapon.EndOffset;
                                assetRecord.Name = weapon.InternalName;
                                assetRecord.Content = weapon.GetSummary();
                                assetRecord.AdditionalData = "Pattern matching";
                                assetRecord.HeaderStartOffset = weapon.StartOffset;
                                assetRecord.HeaderEndOffset = weapon.StartOffset + weapon.HeaderSize;
                                assetRecord.AssetDataStartPosition = weapon.StartOffset;
                                assetRecord.AssetDataEndOffset = weapon.EndOffset;
                                zoneAssetRecords[recordIndex] = assetRecord;
                            }

                            weaponsParsed++;
                            Debug.WriteLine($"[AssetRecordProcessor] Pattern matched weapon #{weaponsParsed}: '{weapon.InternalName}' at 0x{weapon.StartOffset:X}, EndOffset=0x{weapon.EndOffset:X}");

                            // Move past this weapon's HEADER to find the next one
                            // WeaponDef header is 0x9AC bytes, then inline strings follow
                            // Don't jump to EndOffset as that can skip over many weapons
                            // Instead, continue searching from right after the header + estimated inline data
                            int nextSearchStart = weapon.StartOffset + weapon.HeaderSize + 128; // Skip header + estimated inline strings
                            Debug.WriteLine($"[AssetRecordProcessor] Next weapon search from 0x{nextSearchStart:X} (not EndOffset 0x{weapon.EndOffset:X})");
                            weaponSearchOffset = nextSearchStart;
                        }
                        else
                        {
                            // No weapon found in this chunk - continue searching from where we left off
                            // Use overlap to avoid missing weapons at chunk boundaries
                            int nextSearchOffset = weaponSearchOffset + searchChunkSize - 0x1000; // 4KB overlap
                            if (nextSearchOffset <= weaponSearchOffset)
                                nextSearchOffset = weaponSearchOffset + searchChunkSize;

                            if (nextSearchOffset >= zoneData.Length - 100)
                            {
                                Debug.WriteLine($"[AssetRecordProcessor] Reached end of zone at 0x{weaponSearchOffset:X}, stopping weapon search");
                                break;
                            }
                            Debug.WriteLine($"[AssetRecordProcessor] No weapon in chunk at 0x{weaponSearchOffset:X}, continuing from 0x{nextSearchOffset:X}");
                            weaponSearchOffset = nextSearchOffset;
                        }
                    }

                    Debug.WriteLine($"[AssetRecordProcessor] Pattern matching found {weaponsParsed} weapons");
                }

                // For images, use pattern matching to find them
                int expectedImageCount = CountExpectedAssetType(openedFastFile, zoneAssetRecords,
                    0, gameDefinition.ImageAssetType);
                int alreadyParsedImages = result.Images.Count;
                int remainingImages = expectedImageCount - alreadyParsedImages;

                Debug.WriteLine($"[AssetRecordProcessor] Expected {expectedImageCount} images, already parsed {alreadyParsedImages}, remaining {remainingImages}");

                if (remainingImages > 0)
                {
                    // Get indices of unparsed image asset records
                    var imageIndices = new List<int>();
                    for (int i = 0; i < zoneAssetRecords.Count; i++)
                    {
                        int assetType = GetAssetTypeValue(openedFastFile, zoneAssetRecords[i]);
                        if (gameDefinition.IsImageType(assetType) &&
                            string.IsNullOrEmpty(zoneAssetRecords[i].Name))
                        {
                            imageIndices.Add(i);
                            Debug.WriteLine($"[AssetRecordProcessor] Found unparsed image at index {i}");
                        }
                    }
                    Debug.WriteLine($"[AssetRecordProcessor] Found {imageIndices.Count} unparsed image records in asset pool");

                    // Use pattern matching to find images
                    int imageSearchOffset = openedFastFile.OpenedFastFileZone.AssetPoolEndOffset;
                    int imagesParsed = 0;
                    int searchChunkSize = 1000000; // Search in 1MB chunks
                    var foundImageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    Debug.WriteLine($"[AssetRecordProcessor] Starting image search from 0x{imageSearchOffset:X}, looking for {remainingImages} images");

                    while (imagesParsed < remainingImages && imageSearchOffset < zoneData.Length - 100)
                    {
                        var image = ImageParser.FindAndParseImage(zoneData, imageSearchOffset, searchChunkSize);

                        if (image != null)
                        {
                            // Skip duplicates
                            if (foundImageNames.Contains(image.Name))
                            {
                                Debug.WriteLine($"[AssetRecordProcessor] Skipping duplicate image '{image.Name}'");
                                imageSearchOffset = image.EndOffset;
                                continue;
                            }

                            foundImageNames.Add(image.Name);
                            image.AdditionalData = "Pattern matching";
                            result.Images.Add(image);

                            // Update the corresponding asset record if we have one
                            if (imagesParsed < imageIndices.Count)
                            {
                                int recordIndex = imageIndices[imagesParsed];
                                Debug.WriteLine($"[AssetRecordProcessor] Updating asset record at index {recordIndex} with image '{image.Name}'");
                                var assetRecord = zoneAssetRecords[recordIndex];
                                assetRecord.AssetRecordEndOffset = image.EndOffset;
                                assetRecord.Name = image.Name;
                                assetRecord.Content = $"{image.Resolution}, {image.FormattedSize}";
                                assetRecord.AdditionalData = "Pattern matching";
                                assetRecord.HeaderStartOffset = image.StartOffset;
                                assetRecord.HeaderEndOffset = image.EndOffset;
                                assetRecord.AssetDataStartPosition = image.StartOffset;
                                assetRecord.AssetDataEndOffset = image.EndOffset;
                                zoneAssetRecords[recordIndex] = assetRecord;
                            }

                            imagesParsed++;
                            Debug.WriteLine($"[AssetRecordProcessor] Pattern matched image #{imagesParsed}: '{image.Name}' ({image.Resolution}) at 0x{image.StartOffset:X}");

                            // Move past this image to find the next one
                            imageSearchOffset = image.EndOffset;
                        }
                        else
                        {
                            // No image found in this chunk - continue searching
                            int nextSearchOffset = imageSearchOffset + searchChunkSize - 0x100; // 256 byte overlap
                            if (nextSearchOffset <= imageSearchOffset)
                                nextSearchOffset = imageSearchOffset + searchChunkSize;

                            if (nextSearchOffset >= zoneData.Length - 100)
                            {
                                Debug.WriteLine($"[AssetRecordProcessor] Reached end of zone at 0x{imageSearchOffset:X}, stopping image search");
                                break;
                            }
                            Debug.WriteLine($"[AssetRecordProcessor] No image in chunk at 0x{imageSearchOffset:X}, continuing from 0x{nextSearchOffset:X}");
                            imageSearchOffset = nextSearchOffset;
                        }
                    }

                    Debug.WriteLine($"[AssetRecordProcessor] Pattern matching found {imagesParsed} images");
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
            if (fastFile.IsCod5File && fastFile.IsXbox360)
                return (int)record.AssetType_COD5_Xbox360;
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
        /// Keys are typically in SCREAMING_SNAKE_CASE (e.g., RANK_BGEN_FULL_N).
        /// Only ASCII uppercase letters, digits, and underscores are allowed.
        /// Must match the validation in CoD5GameDefinition.IsValidLocalizeKey to avoid
        /// false positives that would accumulate consecutive failures and break the search loop.
        /// </summary>
        private static bool IsValidLocalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 3 || key.Length > 150)
                return false;

            // Must start with an uppercase ASCII letter (A-Z)
            char first = key[0];
            if (!(first >= 'A' && first <= 'Z'))
                return false;

            int uppercaseCount = 0;
            int underscoreCount = 0;
            int consecutiveSameChar = 1;
            char prevChar = '\0';

            foreach (char c in key)
            {
                bool isUppercase = (c >= 'A' && c <= 'Z');
                bool isDigit = (c >= '0' && c <= '9');
                bool isUnderscore = (c == '_');

                // Only allow uppercase letters, digits, and underscores
                if (!isUppercase && !isDigit && !isUnderscore)
                    return false;

                if (isUppercase) uppercaseCount++;
                if (isUnderscore) underscoreCount++;

                // Check for excessive repeated characters (e.g., "WWWWW")
                if (c == prevChar)
                {
                    consecutiveSameChar++;
                    if (consecutiveSameChar > 3)
                        return false;
                }
                else
                {
                    consecutiveSameChar = 1;
                }
                prevChar = c;
            }

            // Must have at least one underscore (keys are SCREAMING_SNAKE_CASE)
            if (underscoreCount == 0)
                return false;

            // Must have at least 2 uppercase letters
            if (uppercaseCount < 2)
                return false;

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
        /// Finds the next XAnim by structure-based search first, then pattern matching as fallback.
        /// Validates XAnim structure characteristics before attempting full parse to avoid slow false positives.
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
                if (zoneData[pos] != 0xFF || zoneData[pos + 1] != 0xFF ||
                    zoneData[pos + 2] != 0xFF || zoneData[pos + 3] != 0xFF)
                    continue;

                // Quick structural validation BEFORE calling ParseXAnim to avoid slow false positives
                // XAnim structure (CoD4/WaW):
                // 0x00: name pointer (FF FF FF FF) - already matched
                // 0x0E: numframes (2 bytes BE)
                // 0x2C: framerate (4 bytes float BE)

                // Check we have enough data for the header
                if (pos + 0x34 > zoneData.Length)
                    continue;

                // Quick validation: Check numframes at 0x0E (should be reasonable: 1-50000)
                ushort numframes = (ushort)((zoneData[pos + 0x0E] << 8) | zoneData[pos + 0x0F]);
                if (numframes == 0 || numframes > 50000)
                    continue;

                // Quick validation: Check framerate is a valid IEEE 754 float
                // Valid framerates: 0.1 to 120 fps (game animations typically 15-60 fps)
                // Note: Valid floats like 30.0 (0x41F00000) have bytes that look like ASCII,
                // so we can't filter by byte values - only validate the actual float value
                byte[] floatBytes = new byte[4];
                floatBytes[0] = zoneData[pos + 0x2F];
                floatBytes[1] = zoneData[pos + 0x2E];
                floatBytes[2] = zoneData[pos + 0x2D];
                floatBytes[3] = zoneData[pos + 0x2C];
                float framerate = BitConverter.ToSingle(floatBytes, 0);

                // Skip if framerate is invalid (NaN, infinity, or out of reasonable range)
                // This filters out most text data which produces extreme float values
                if (float.IsNaN(framerate) || float.IsInfinity(framerate) || framerate < 0.1f || framerate > 120f)
                    continue;

                // Additional validation: dataByteCount, dataShortCount, dataIntCount should be reasonable
                ushort dataByteCount = (ushort)((zoneData[pos + 0x04] << 8) | zoneData[pos + 0x05]);
                ushort dataShortCount = (ushort)((zoneData[pos + 0x06] << 8) | zoneData[pos + 0x07]);
                ushort dataIntCount = (ushort)((zoneData[pos + 0x08] << 8) | zoneData[pos + 0x09]);

                // These counts should be reasonable for animation data
                if (dataByteCount > 50000 || dataShortCount > 50000 || dataIntCount > 50000)
                    continue;

                // Passed quick validation - now try full parse
                var xanim = gameDefinition.ParseXAnim(zoneData, pos);
                if (xanim != null)
                {
                    // Validate the animation name looks valid before accepting
                    if (IsValidXAnimName(xanim.Name))
                    {
                        Debug.WriteLine($"[AssetRecordProcessor] Found XAnim '{xanim.Name}' at 0x{pos:X}");
                        return xanim;
                    }
                    else
                    {
                        Debug.WriteLine($"[AssetRecordProcessor] Rejected XAnim with invalid name '{xanim.Name}' at 0x{pos:X}");
                    }
                }
            }

            Debug.WriteLine($"[AssetRecordProcessor] No XAnim found in search range");
            return null;
        }

        /// <summary>
        /// Validates that a string looks like a valid XAnim animation name.
        /// Valid names are lowercase with underscores, digits allowed, typically 3-80 characters.
        /// Examples: "viewmodel_default_idle", "mp_panzerschreck_fire", "zombie_run_v1"
        /// </summary>
        private static bool IsValidXAnimName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 100)
                return false;

            // Must start with a letter (animation names start with lowercase letters)
            char first = name[0];
            if (first < 'a' || first > 'z')
                return false;

            // Count valid characters - must be all lowercase letters, digits, or underscores
            int validChars = 0;
            int underscoreCount = 0;
            foreach (char c in name)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    validChars++;
                }
                else if (c == '_')
                {
                    underscoreCount++;
                    validChars++;
                }
                else
                {
                    // Invalid character found (uppercase, special chars, non-ASCII)
                    return false;
                }
            }

            // All characters must be valid
            if (validChars != name.Length)
                return false;

            // Most valid animation names have at least one underscore
            // Names like "idle" without underscores are rare but valid
            // Names with too many underscores in a row are suspicious
            if (name.Contains("__"))
                return false;

            return true;
        }

        /// <summary>
        /// Finds the next Weapon by searching for weapon internal names (strings ending with _mp or _sp).
        /// When a potential weapon name is found, validates it by checking for the WeaponDef structure header
        /// at approximately 0x9AC bytes before the name.
        /// WeaponDef header is 0x9AC bytes (2476 bytes) for WaW.
        /// </summary>
        private static WeaponAsset? FindNextWeapon(byte[] zoneData, int startOffset, int maxSearchBytes, IGameDefinition gameDefinition)
        {
            Debug.WriteLine($"[AssetRecordProcessor] Searching for Weapon from 0x{startOffset:X}, max {maxSearchBytes} bytes, zoneData.Length=0x{zoneData.Length:X}");

            // Weapon header is 0x9AC bytes (2476 bytes)
            const int WEAPON_HEADER_SIZE = 0x9AC;

            // Bounds check
            if (startOffset < 0 || startOffset >= zoneData.Length - 10)
            {
                Debug.WriteLine($"[AssetRecordProcessor] Weapon search startOffset 0x{startOffset:X} is out of bounds (zoneData.Length=0x{zoneData.Length:X})");
                return null;
            }

            int endOffset = Math.Min(startOffset + maxSearchBytes, zoneData.Length - 10);
            Debug.WriteLine($"[AssetRecordProcessor] Weapon search endOffset=0x{endOffset:X}");

            // STRING-FIRST APPROACH: Search for weapon name strings ending with _mp or _sp
            // Then validate by checking for the WeaponDef header 0x9AC bytes before
            for (int pos = startOffset; pos < endOffset; pos++)
            {
                // Only match START of strings - byte before must be 0x00 (null terminator) or 0xFF (padding)
                if (pos > 0)
                {
                    byte prevByte = zoneData[pos - 1];
                    if (prevByte != 0x00 && prevByte != 0xFF)
                        continue;
                }

                // Must start with a lowercase letter (weapon internal names start lowercase)
                byte firstChar = zoneData[pos];
                if (firstChar < 'a' || firstChar > 'z')
                    continue;

                // Read the potential weapon name string
                string candidate = ReadNullTerminatedStringAt(zoneData, pos);

                // Basic validation - weapon names are typically 5-40 characters
                if (candidate.Length < 5 || candidate.Length > 50)
                    continue;

                // Must only contain valid weapon name characters
                if (!candidate.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                    continue;

                string lower = candidate.ToLowerInvariant();

                // Must end with _mp or _sp (weapon naming convention)
                // This is the only reliable text-based filter - actual validation happens via structure
                if (!lower.EndsWith("_mp") && !lower.EndsWith("_sp"))
                    continue;

                // Calculate where the header should be (0x9AC bytes before the string, minus padding)
                // Search backwards from the string to find the header
                int searchStart = Math.Max(0, pos - WEAPON_HEADER_SIZE - 64);
                int searchEnd = Math.Max(0, pos - WEAPON_HEADER_SIZE + 64);

                for (int headerPos = searchEnd; headerPos >= searchStart; headerPos--)
                {
                    // Check for 0xFFFFFFFF at position 0 (szInternalName pointer)
                    uint ptr1 = ReadUInt32BE(zoneData, headerPos);
                    if (ptr1 != 0xFFFFFFFF && ptr1 != 0xFFFFFFFE)
                        continue;

                    // Verify the inline data at headerPos + 0x9AC matches our string position
                    int expectedDataOffset = headerPos + WEAPON_HEADER_SIZE;

                    // Skip padding to find where string should start
                    int dataOffset = expectedDataOffset;
                    int maxPadding = 64;
                    int padCount = 0;
                    while (dataOffset < zoneData.Length - 1 && padCount < maxPadding &&
                           (zoneData[dataOffset] == 0x00 || zoneData[dataOffset] == 0xFF))
                    {
                        dataOffset++;
                        padCount++;
                    }

                    // Check if this header points to our candidate string
                    if (dataOffset != pos)
                        continue;

                    Debug.WriteLine($"[AssetRecordProcessor] Found weapon string '{candidate}' at 0x{pos:X}, header at 0x{headerPos:X}");

                    // Try to parse the weapon at this header position
                    var weapon = gameDefinition.ParseWeapon(zoneData, headerPos);
                    if (weapon != null)
                    {
                        Debug.WriteLine($"[AssetRecordProcessor] Successfully parsed weapon '{weapon.InternalName}' at header 0x{headerPos:X}");
                        return weapon;
                    }
                    else
                    {
                        Debug.WriteLine($"[AssetRecordProcessor] Failed to parse weapon at header 0x{headerPos:X}");
                    }
                }
            }

            Debug.WriteLine($"[AssetRecordProcessor] No Weapon found in search range");
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
