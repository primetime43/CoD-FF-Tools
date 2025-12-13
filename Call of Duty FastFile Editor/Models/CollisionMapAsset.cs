using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Represents the collision map (clipMap_t) asset for Call of Duty games.
    /// Reference: https://codresearch.dev/index.php/Collision_Map_Asset_(WaW)
    ///
    /// The collision map contains:
    /// - Map entity strings (spawn points, triggers, etc.)
    /// - Plane data for collision detection
    /// - Static model references
    /// - Brush geometry
    /// - BSP tree structure
    /// - Triangle collision data
    /// </summary>
    public class ClipMapAsset
    {
        /// <summary>
        /// The name of this collision map (e.g., "maps/mp/mp_castle.d3dbsp")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Start offset in the zone file
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// End offset in the zone file
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// Whether this is a multiplayer or singleplayer collision map
        /// </summary>
        public bool IsMultiplayer { get; set; }

        /// <summary>
        /// The map entity string data (contains spawn points, triggers, etc.)
        /// </summary>
        public MapEntsData MapEnts { get; set; }

        /// <summary>
        /// Checksum for map verification (mapcrc dvar)
        /// </summary>
        public uint Checksum { get; set; }

        /// <summary>
        /// Number of collision planes
        /// </summary>
        public int PlaneCount { get; set; }

        /// <summary>
        /// Number of static models in the collision map
        /// </summary>
        public int StaticModelCount { get; set; }

        /// <summary>
        /// Number of materials
        /// </summary>
        public int MaterialCount { get; set; }

        /// <summary>
        /// Number of brush sides
        /// </summary>
        public int BrushSideCount { get; set; }

        /// <summary>
        /// Number of BSP nodes
        /// </summary>
        public int NodeCount { get; set; }

        /// <summary>
        /// Number of BSP leafs
        /// </summary>
        public int LeafCount { get; set; }

        /// <summary>
        /// Parsing method used (Structure-based or Pattern matching)
        /// </summary>
        public string AdditionalData { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contains the map entity string data parsed from the collision map.
    /// This is the text data that defines entities like spawn points, triggers, etc.
    /// </summary>
    public class MapEntsData
    {
        /// <summary>
        /// Offset where the map ents size field is located
        /// </summary>
        public int SizeOffset { get; set; }

        /// <summary>
        /// Size of the map ents data in bytes
        /// </summary>
        public int DataSize { get; set; }

        /// <summary>
        /// Offset where the map ents string data starts
        /// </summary>
        public int DataStartOffset { get; set; }

        /// <summary>
        /// The raw map ents string (for export/display)
        /// </summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>
        /// Parsed entities from the map ents string
        /// </summary>
        public List<MapEntity> Entities { get; set; } = new List<MapEntity>();
    }

    /// <summary>
    /// Represents a single entity from the map ents string.
    /// Entities are defined as key-value pairs within braces.
    /// </summary>
    public class MapEntity
    {
        /// <summary>
        /// The file offset where this entity's '{' was found
        /// </summary>
        public int SourceOffset { get; set; }

        /// <summary>
        /// The classname of this entity (e.g., "worldspawn", "mp_dm_spawn", "trigger_multiple")
        /// </summary>
        public string ClassName => Properties.TryGetValue("classname", out var cn) ? cn : "(unknown)";

        /// <summary>
        /// Stores all key-value pairs for this entity
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a display name for this entity
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (Properties.TryGetValue("targetname", out var targetName))
                    return $"{ClassName} ({targetName})";
                if (Properties.TryGetValue("target", out var target))
                    return $"{ClassName} -> {target}";
                return ClassName;
            }
        }
    }

    /// <summary>
    /// Parser for collision map entity data.
    /// </summary>
    public static class ClipMapParser
    {
        private static readonly Regex KeyValuePattern = new Regex(
            @"^""([^""]+)""\s+""([^""]*)""$",
            RegexOptions.Compiled);

        /// <summary>
        /// Attempts to find and parse map entity data from a zone file.
        /// </summary>
        public static MapEntsData ParseMapEnts(ZoneFile zone)
        {
            if (zone?.Data == null)
                return null;

            int offset = FindMapEntsOffset(zone.Data);
            if (offset < 0)
                return null;

            return ParseMapEntsAtOffset(zone.Data, offset);
        }

        /// <summary>
        /// Parses map entity data from a known offset.
        /// Format: [4 bytes BE size] [size bytes of ASCII text]
        /// </summary>
        public static MapEntsData ParseMapEntsAtOffset(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 4 > data.Length)
                return null;

            // Read size as big-endian 32-bit integer
            int size = ReadInt32BE(data, offset);
            if (size <= 0 || offset + 4 + size > data.Length)
                return null;

            int dataStart = offset + 4;

            // Extract the raw text
            string rawText = Encoding.ASCII.GetString(data, dataStart, size).TrimEnd('\0');

            var mapEnts = new MapEntsData
            {
                SizeOffset = offset,
                DataSize = size,
                DataStartOffset = dataStart,
                RawText = rawText,
                Entities = ParseEntitiesFromText(rawText, dataStart)
            };

            return mapEnts;
        }

        /// <summary>
        /// Parses entity definitions from map ents text.
        /// </summary>
        private static List<MapEntity> ParseEntitiesFromText(string text, int baseOffset)
        {
            var entities = new List<MapEntity>();
            MapEntity current = null;
            var lineBuffer = new StringBuilder();
            bool insideEntity = false;
            int charIndex = 0;

            foreach (char c in text)
            {
                if (c == '{')
                {
                    current = new MapEntity { SourceOffset = baseOffset + charIndex };
                    insideEntity = true;
                    lineBuffer.Clear();
                }
                else if (c == '}')
                {
                    if (current != null && current.Properties.Count > 0)
                        entities.Add(current);
                    current = null;
                    insideEntity = false;
                }
                else if (c == '\r' || c == '\n')
                {
                    if (insideEntity && current != null)
                    {
                        string line = lineBuffer.ToString().Trim();
                        if (line.Length > 0)
                            ParseKeyValueLine(line, current);
                    }
                    lineBuffer.Clear();
                }
                else if (insideEntity)
                {
                    lineBuffer.Append(c);
                }

                charIndex++;
            }

            return entities;
        }

        /// <summary>
        /// Parses a "key" "value" line and adds it to the entity.
        /// </summary>
        private static void ParseKeyValueLine(string line, MapEntity entity)
        {
            var match = KeyValuePattern.Match(line);
            if (match.Success)
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                entity.Properties[key] = value;
            }
        }

        /// <summary>
        /// Finds the offset of map entity data by searching for valid patterns.
        /// </summary>
        private static int FindMapEntsOffset(byte[] data)
        {
            // Search for runs of 0xFF bytes which typically precede map data
            var ffRuns = FindFFRuns(data, minLength: 32);

            foreach (int runOffset in ffRuns)
            {
                // Search in a window around each FF run
                int windowStart = Math.Max(0, runOffset - 512);
                int windowEnd = Math.Min(data.Length - 4, runOffset + 512);

                for (int i = windowStart; i <= windowEnd; i++)
                {
                    int size = ReadInt32BE(data, i);
                    if (size <= 0 || size > 10000000) // Sanity check: max 10MB
                        continue;

                    int dataStart = i + 4;
                    if (dataStart + size > data.Length)
                        continue;

                    // Quick validation: look for '{' in first 256 bytes
                    int checkEnd = Math.Min(dataStart + 256, dataStart + size);
                    bool foundBrace = false;
                    for (int p = dataStart; p < checkEnd; p++)
                    {
                        if (data[p] == '{')
                        {
                            foundBrace = true;
                            break;
                        }
                    }

                    if (!foundBrace)
                        continue;

                    // Validate by attempting to parse
                    var testData = ParseMapEntsAtOffset(data, i);
                    if (testData?.Entities.Count > 0)
                        return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds offsets where runs of 0xFF bytes begin.
        /// </summary>
        private static List<int> FindFFRuns(byte[] data, int minLength)
        {
            var runs = new List<int>();
            int i = 0;

            while (i < data.Length)
            {
                if (data[i] == 0xFF)
                {
                    int start = i;
                    int count = 0;
                    while (i < data.Length && data[i] == 0xFF)
                    {
                        count++;
                        i++;
                    }
                    if (count >= minLength)
                        runs.Add(start);
                }
                else
                {
                    i++;
                }
            }

            return runs;
        }

        /// <summary>
        /// Reads a big-endian 32-bit integer.
        /// </summary>
        private static int ReadInt32BE(byte[] data, int offset)
        {
            if (offset + 4 > data.Length)
                return -1;

            return (data[offset] << 24)
                 | (data[offset + 1] << 16)
                 | (data[offset + 2] << 8)
                 | data[offset + 3];
        }
    }

    #region Legacy Support - Keep for backward compatibility

    /// <summary>
    /// Legacy static class for backward compatibility.
    /// Use ClipMapParser instead for new code.
    /// </summary>
    public static class Collision_Map_Operations
    {
        public static List<MapEntity> ParseMapEntsAtOffset(ZoneFile zone, int offset)
        {
            var result = ClipMapParser.ParseMapEntsAtOffset(zone?.Data, offset);
            return result?.Entities ?? new List<MapEntity>();
        }

        public static int FindCollision_Map_DataOffsetViaFF(ZoneFile zone)
        {
            if (zone?.Data == null)
                return -1;

            var mapEnts = ClipMapParser.ParseMapEnts(zone);
            return mapEnts?.SizeOffset ?? -1;
        }

        public static (int mapSize, int offsetOfSize)? GetMapDataSizeAndOffset(ZoneFile zone, List<MapEntity> entities)
        {
            if (zone?.Data == null || entities == null || entities.Count == 0)
                return null;

            int minOffset = int.MaxValue;
            foreach (var ent in entities)
            {
                if (ent.SourceOffset < minOffset)
                    minOffset = ent.SourceOffset;
            }

            int sizeOffset = minOffset - 4;
            if (sizeOffset < 0 || sizeOffset + 4 > zone.Data.Length)
                return null;

            int mapSize = (zone.Data[sizeOffset] << 24)
                        | (zone.Data[sizeOffset + 1] << 16)
                        | (zone.Data[sizeOffset + 2] << 8)
                        | zone.Data[sizeOffset + 3];

            if (mapSize <= 0 || mapSize > zone.Data.Length)
                return null;

            return (mapSize, sizeOffset);
        }
    }

    #endregion
}
