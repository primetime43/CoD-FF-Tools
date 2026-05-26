using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Tests for FastFileInfo.DetectGameFromZoneData / IsZoneDataPC's fallback path —
/// retail PC zones use per-zone MemAlloc1 values that aren't in our magic table,
/// so the magic-value fast path returns Unknown / false on them. The fallback
/// uses ZoneSize endianness + 0xFFFFFFFF marker positions instead.
/// </summary>
public class ZoneDetectionTests
{
    [Fact]
    public void DetectGame_Mw2Pc_WithNonMagicMemAlloc_ReturnsMw2()
    {
        // Real MW2 PC patch_mp.zone uses BlockSizeTemp = 0x020C (not the magic 0x03B4).
        // Detection must fall through to the layout-shape path and identify 56-byte LE
        // as MW2 PC.
        byte[] zone = BuildSyntheticZone(headerSize: 0x38, isLE: true, blockSizeTemp: 0x020Cu, blockSizeVertex: 0);

        Assert.Equal(GameVersion.MW2, FastFileInfo.DetectGameFromZoneData(zone));
        Assert.True(FastFileInfo.IsZoneDataPC(zone));
    }

    [Fact]
    public void DetectGame_WaWWii_WithNonMagicMemAlloc_ReturnsWaW()
    {
        // 56-byte BE layout = WaW Wii (since Wii is BE and MW2 PC is LE).
        byte[] zone = BuildSyntheticZone(headerSize: 0x38, isLE: false, blockSizeTemp: 0x12345u, blockSizeVertex: 0);

        Assert.Equal(GameVersion.WaW, FastFileInfo.DetectGameFromZoneData(zone));
        Assert.False(FastFileInfo.IsZoneDataPC(zone));
    }

    [Fact]
    public void DetectGame_Mw2Xbox360_WithNonMagicMemAlloc_ReturnsMw2()
    {
        // 48-byte BE = MW2 Xbox 360 only (no BlockSizeVertex slot, asset table @ 0x30).
        byte[] zone = BuildSyntheticZone(headerSize: 0x30, isLE: false, blockSizeTemp: 0x99u, blockSizeVertex: 0);

        Assert.Equal(GameVersion.MW2, FastFileInfo.DetectGameFromZoneData(zone));
        Assert.False(FastFileInfo.IsZoneDataPC(zone));
    }

    [Fact]
    public void DetectGame_WaWPc_WithNonMagicMemAlloc_ReturnsWaW()
    {
        // 52-byte LE = CoD4/WaW PC. The fallback defaults to WaW since it's the more
        // common PC modding target — CoD4 PC retail mostly uses the magic value.
        byte[] zone = BuildSyntheticZone(headerSize: 0x34, isLE: true, blockSizeTemp: 0xABCDu, blockSizeVertex: 0);

        Assert.Equal(GameVersion.WaW, FastFileInfo.DetectGameFromZoneData(zone));
        Assert.True(FastFileInfo.IsZoneDataPC(zone));
    }

    [Fact]
    public void DetectGame_KnownMagicConstants_StillTakeFastPath()
    {
        // Regression: the fallback must not break existing magic-value matches.
        // Build a zone with BlockSizeTemp = MW2 PS3 magic (0x03B4) at offset 0x08 BE,
        // and a layout that *would* otherwise be ambiguous. Magic match wins.
        byte[] zone = BuildSyntheticZone(headerSize: 0x34, isLE: false, blockSizeTemp: 0x03B4u, blockSizeVertex: 0);

        Assert.Equal(GameVersion.MW2, FastFileInfo.DetectGameFromZoneData(zone));
        Assert.False(FastFileInfo.IsZoneDataPC(zone));
    }

    [Fact]
    public void IsZonePC_AmbiguousZoneSize_FallsThroughToMagicOnly()
    {
        // A zone too small for the ZoneSize plausibility check to be meaningful — both
        // BE and LE readings of ZoneSize are tiny. Detection should not crash and
        // should return false when there's no signal at all.
        byte[] zone = new byte[64];
        // Leave it all zeros — ZoneSize = 0 in both directions = neither plausible.

        Assert.False(FastFileInfo.IsZoneDataPC(zone));
        // DetectGameFromZoneData should also gracefully return Unknown
        Assert.Equal(GameVersion.Unknown, FastFileInfo.DetectGameFromZoneData(zone));
    }

    /// <summary>
    /// Builds a minimal zone byte array of the given header size and endianness,
    /// with valid ZoneSize, AssetCount, and 0xFFFFFFFF marker placeholders so the
    /// header-shape detection has something to find.
    /// </summary>
    private static byte[] BuildSyntheticZone(int headerSize, bool isLE, uint blockSizeTemp, uint blockSizeVertex)
    {
        // Pad to 64KB so ZoneSize ≈ data length matches what real zones do.
        const int totalSize = 0x10000;
        var zone = new byte[totalSize];

        void Write(int offset, uint value)
        {
            if (isLE)
            {
                zone[offset]     = (byte)(value & 0xFF);
                zone[offset + 1] = (byte)((value >> 8) & 0xFF);
                zone[offset + 2] = (byte)((value >> 16) & 0xFF);
                zone[offset + 3] = (byte)((value >> 24) & 0xFF);
            }
            else
            {
                zone[offset]     = (byte)((value >> 24) & 0xFF);
                zone[offset + 1] = (byte)((value >> 16) & 0xFF);
                zone[offset + 2] = (byte)((value >> 8) & 0xFF);
                zone[offset + 3] = (byte)(value & 0xFF);
            }
        }

        // ZoneSize = content size (everything after the header). Roughly matches file
        // length minus some padding — within the plausibility tolerance.
        Write(0x00, (uint)(totalSize - headerSize - 0x100));
        Write(0x08, blockSizeTemp);

        // Lay out the XAssetList placeholders for the chosen header size.
        int scriptStringsPtrOffset, assetCountOffset, assetsPtrOffset;
        switch (headerSize)
        {
            case 0x30: // MW2 Xbox 360
                scriptStringsPtrOffset = 0x24;
                assetCountOffset       = 0x28;
                assetsPtrOffset        = 0x2C;
                break;
            case 0x34: // PS3-style 52-byte
                Write(0x20, blockSizeVertex);
                scriptStringsPtrOffset = 0x28;
                assetCountOffset       = 0x2C;
                assetsPtrOffset        = 0x30;
                break;
            case 0x38: // Wii / MW2 PC 56-byte
                Write(0x20, blockSizeVertex);
                scriptStringsPtrOffset = 0x2C;
                assetCountOffset       = 0x30;
                assetsPtrOffset        = 0x34;
                break;
            default: throw new ArgumentException($"unsupported header size 0x{headerSize:X}");
        }

        zone[scriptStringsPtrOffset]     = 0xFF;
        zone[scriptStringsPtrOffset + 1] = 0xFF;
        zone[scriptStringsPtrOffset + 2] = 0xFF;
        zone[scriptStringsPtrOffset + 3] = 0xFF;
        Write(assetCountOffset, 1u);
        zone[assetsPtrOffset]     = 0xFF;
        zone[assetsPtrOffset + 1] = 0xFF;
        zone[assetsPtrOffset + 2] = 0xFF;
        zone[assetsPtrOffset + 3] = 0xFF;

        return zone;
    }
}
