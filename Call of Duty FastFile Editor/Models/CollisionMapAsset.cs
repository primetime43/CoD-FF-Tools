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
        /// Number of brushes
        /// </summary>
        public int BrushCount { get; set; }

        /// <summary>
        /// Number of submodels (cmodel_t)
        /// </summary>
        public int SubModelCount { get; set; }

        /// <summary>
        /// Number of collision vertices
        /// </summary>
        public int VertexCount { get; set; }

        /// <summary>
        /// Number of collision triangles
        /// </summary>
        public int TriangleCount { get; set; }

        /// <summary>
        /// List of collision materials (dmaterial_t)
        /// </summary>
        public List<ClipMapMaterial> Materials { get; set; } = new List<ClipMapMaterial>();

        /// <summary>
        /// List of static models referenced in the collision map
        /// </summary>
        public List<ClipMapStaticModel> StaticModels { get; set; } = new List<ClipMapStaticModel>();

        /// <summary>
        /// Parsing method used (Structure-based or Pattern matching)
        /// </summary>
        public string AdditionalData { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a collision material (dmaterial_t).
    /// Structure: 64-byte name + 4-byte surface flags + 4-byte content flags = 72 bytes total
    /// </summary>
    public class ClipMapMaterial
    {
        /// <summary>
        /// Material name (up to 64 characters)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Surface flags (affects footsteps, impacts, etc.)
        /// </summary>
        public uint SurfaceFlags { get; set; }

        /// <summary>
        /// Content flags (solid, water, ladder, etc.)
        /// </summary>
        public uint ContentFlags { get; set; }

        /// <summary>
        /// Offset in zone file where this material was found
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Gets a human-readable description of the content flags
        /// </summary>
        public string ContentFlagsDescription => GetContentFlagsDescription();

        /// <summary>
        /// Gets a human-readable description of the surface flags
        /// </summary>
        public string SurfaceFlagsDescription => GetSurfaceFlagsDescription();

        private string GetContentFlagsDescription()
        {
            var flags = new List<string>();
            if ((ContentFlags & 0x1) != 0) flags.Add("SOLID");
            if ((ContentFlags & 0x2) != 0) flags.Add("LAVA");
            if ((ContentFlags & 0x4) != 0) flags.Add("WATER");
            if ((ContentFlags & 0x10) != 0) flags.Add("PLAYERCLIP");
            if ((ContentFlags & 0x20) != 0) flags.Add("MONSTERCLIP");
            if ((ContentFlags & 0x80) != 0) flags.Add("VEHICLECLIP");
            if ((ContentFlags & 0x400) != 0) flags.Add("LADDER");
            if ((ContentFlags & 0x1000000) != 0) flags.Add("TRIGGER");
            if ((ContentFlags & 0x4000000) != 0) flags.Add("MANTLE");
            if ((ContentFlags & 0x8000000) != 0) flags.Add("NOSIGHT");

            return flags.Count > 0 ? string.Join(" | ", flags) : $"0x{ContentFlags:X8}";
        }

        private string GetSurfaceFlagsDescription()
        {
            var flags = new List<string>();
            // Surface type (lower bits)
            int surfType = (int)(SurfaceFlags & 0x1F);
            string[] surfTypes = { "DEFAULT", "BARK", "BRICK", "CARPET", "CLOTH", "CONCRETE", "DIRT", "FLESH",
                                   "FOLIAGE", "GLASS", "GRASS", "GRAVEL", "ICE", "METAL", "MUD", "PAPER",
                                   "PLASTER", "ROCK", "SAND", "SNOW", "WATER", "WOOD", "ASPHALT", "CERAMIC" };
            if (surfType < surfTypes.Length)
                flags.Add(surfTypes[surfType]);

            if ((SurfaceFlags & 0x100) != 0) flags.Add("NODAMAGE");
            if ((SurfaceFlags & 0x200) != 0) flags.Add("SLICK");
            if ((SurfaceFlags & 0x400) != 0) flags.Add("SKY");
            if ((SurfaceFlags & 0x800) != 0) flags.Add("NOIMPACT");
            if ((SurfaceFlags & 0x1000) != 0) flags.Add("NOMARKS");
            if ((SurfaceFlags & 0x4000) != 0) flags.Add("NODRAW");
            if ((SurfaceFlags & 0x10000) != 0) flags.Add("NODLIGHT");

            return flags.Count > 0 ? string.Join(" | ", flags) : $"0x{SurfaceFlags:X8}";
        }
    }

    /// <summary>
    /// Represents a static model in the collision map (cStaticModel_s).
    /// </summary>
    public class ClipMapStaticModel
    {
        /// <summary>
        /// Name/path of the XModel
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Origin X coordinate
        /// </summary>
        public float OriginX { get; set; }

        /// <summary>
        /// Origin Y coordinate
        /// </summary>
        public float OriginY { get; set; }

        /// <summary>
        /// Origin Z coordinate
        /// </summary>
        public float OriginZ { get; set; }

        /// <summary>
        /// Offset in zone file where this static model was found
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Gets the origin as a formatted string
        /// </summary>
        public string OriginString => $"({OriginX:F2}, {OriginY:F2}, {OriginZ:F2})";
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

        /// <summary>
        /// Reads a big-endian 32-bit unsigned integer.
        /// </summary>
        private static uint ReadUInt32BE(byte[] data, int offset)
        {
            if (offset + 4 > data.Length)
                return 0;

            return (uint)((data[offset] << 24)
                 | (data[offset + 1] << 16)
                 | (data[offset + 2] << 8)
                 | data[offset + 3]);
        }

        /// <summary>
        /// Reads a big-endian single-precision float.
        /// </summary>
        private static float ReadFloatBE(byte[] data, int offset)
        {
            if (offset + 4 > data.Length)
                return 0f;

            byte[] bytes = new byte[4];
            bytes[3] = data[offset];
            bytes[2] = data[offset + 1];
            bytes[1] = data[offset + 2];
            bytes[0] = data[offset + 3];
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Parses collision materials (dmaterial_t) from zone data.
        /// dmaterial_t structure: 64-byte name + 4-byte surface flags + 4-byte content flags = 72 bytes
        /// Materials are stored in a contiguous array.
        /// </summary>
        public static List<ClipMapMaterial> ParseMaterials(byte[] data, bool isPC = false)
        {
            var materials = new List<ClipMapMaterial>();
            if (data == null || data.Length < 72)
                return materials;

            // Find the materials array by looking for a sequence of valid dmaterial_t structures
            int arrayStart = FindMaterialArrayStart(data, isPC);
            if (arrayStart < 0)
                return materials;

            // Parse all materials in the contiguous array
            int offset = arrayStart;
            while (offset + 72 <= data.Length)
            {
                var material = TryParseMaterialAt(data, offset, isPC);
                if (material == null)
                    break; // End of array

                materials.Add(material);
                offset += 72;
            }

            return materials;
        }

        /// <summary>
        /// Finds the start of the dmaterial_t array by looking for known material prefixes
        /// and validating that we have a contiguous array of valid structures.
        /// </summary>
        private static int FindMaterialArrayStart(byte[] data, bool isPC)
        {
            // Known material name prefixes that appear in CoD games
            string[] knownPrefixes = { "mtl_", "mc/", "wc/", "gfx_", "clip", "mantle" };

            for (int i = 0; i < data.Length - 72 * 3; i++)
            {
                // Check if this offset starts with a known material prefix
                bool hasKnownPrefix = false;
                foreach (var prefix in knownPrefixes)
                {
                    if (i + prefix.Length >= data.Length)
                        continue;

                    bool match = true;
                    for (int j = 0; j < prefix.Length && match; j++)
                    {
                        if (data[i + j] != prefix[j])
                            match = false;
                    }
                    if (match)
                    {
                        hasKnownPrefix = true;
                        break;
                    }
                }

                if (!hasKnownPrefix)
                    continue;

                // Validate this is the start of a valid material
                var firstMaterial = TryParseMaterialAt(data, i, isPC);
                if (firstMaterial == null)
                    continue;

                // Check if we can find more valid materials at 72-byte intervals
                int validCount = 1;
                int checkOffset = i + 72;
                while (checkOffset + 72 <= data.Length && validCount < 5)
                {
                    var nextMaterial = TryParseMaterialAt(data, checkOffset, isPC);
                    if (nextMaterial == null)
                        break;
                    validCount++;
                    checkOffset += 72;
                }

                // Require at least 3 consecutive valid materials to confirm we found the array
                if (validCount >= 3)
                {
                    // Walk backwards to find the actual start of the array
                    int arrayStart = i;
                    int backOffset = i - 72;
                    while (backOffset >= 0)
                    {
                        var prevMaterial = TryParseMaterialAt(data, backOffset, isPC);
                        if (prevMaterial == null)
                            break;
                        arrayStart = backOffset;
                        backOffset -= 72;
                    }
                    return arrayStart;
                }
            }

            return -1;
        }

        /// <summary>
        /// Attempts to parse a dmaterial_t structure at the given offset.
        /// Uses strict validation to avoid false positives.
        /// </summary>
        private static ClipMapMaterial TryParseMaterialAt(byte[] data, int offset, bool isPC)
        {
            if (offset + 72 > data.Length || offset < 0)
                return null;

            // Read the 64-byte name field
            int nameLen = 0;
            while (nameLen < 64 && data[offset + nameLen] != 0)
            {
                byte c = data[offset + nameLen];
                // Valid material name characters: alphanumeric, underscore, slash, dot, minus
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') || c == '_' || c == '/' || c == '.' || c == '-'))
                    return null;
                nameLen++;
            }

            // Name must be at least 3 characters
            if (nameLen < 3)
                return null;

            // Rest of the 64-byte name field should be null padding
            for (int i = offset + nameLen; i < offset + 64; i++)
            {
                if (data[i] != 0)
                    return null;
            }

            string name = Encoding.ASCII.GetString(data, offset, nameLen);

            // Read flags based on endianness
            uint surfaceFlags, contentFlags;
            if (isPC)
            {
                surfaceFlags = (uint)(data[offset + 64] | (data[offset + 65] << 8) |
                                     (data[offset + 66] << 16) | (data[offset + 67] << 24));
                contentFlags = (uint)(data[offset + 68] | (data[offset + 69] << 8) |
                                     (data[offset + 70] << 16) | (data[offset + 71] << 24));
            }
            else
            {
                surfaceFlags = ReadUInt32BE(data, offset + 64);
                contentFlags = ReadUInt32BE(data, offset + 68);
            }

            // Surface flags lower 5 bits are surface type (0-23 are valid)
            uint surfaceType = surfaceFlags & 0x1F;
            if (surfaceType > 23)
                return null;

            return new ClipMapMaterial
            {
                Name = name,
                SurfaceFlags = surfaceFlags,
                ContentFlags = contentFlags,
                Offset = offset
            };
        }

        /// <summary>
        /// Parses a full ClipMapAsset from zone data, including map ents and materials.
        /// </summary>
        public static ClipMapAsset ParseClipMap(ZoneFile zone, bool isPC = false)
        {
            if (zone?.Data == null)
                return null;

            var clipMap = new ClipMapAsset();

            // Parse map entities
            clipMap.MapEnts = ParseMapEnts(zone);

            // Parse materials
            clipMap.Materials = ParseMaterials(zone.Data, isPC);
            clipMap.MaterialCount = clipMap.Materials.Count;

            // Try to find the map name from the map ents (worldspawn entity)
            if (clipMap.MapEnts?.Entities != null)
            {
                var worldspawn = clipMap.MapEnts.Entities.FirstOrDefault(e =>
                    e.ClassName.Equals("worldspawn", StringComparison.OrdinalIgnoreCase));
                if (worldspawn != null && worldspawn.Properties.TryGetValue("classname", out var _))
                {
                    // The map name is typically the zone name
                    clipMap.Name = "clipMap";
                }
            }

            clipMap.AdditionalData = $"Materials: {clipMap.MaterialCount}, Entities: {clipMap.MapEnts?.Entities.Count ?? 0}";

            return clipMap;
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
