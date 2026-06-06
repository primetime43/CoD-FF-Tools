using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastFileLib.Iw4;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Tests the IW4 (MW2 PS3) zone reader (<see cref="Iw4ZoneReader"/>), the faithful port of
/// Jacob Schroeder's read pipeline + deferred-resolution engine
/// (https://github.com/jacob-schroeder/FastFile).
///
/// Uses a hand-built synthetic zone (no copyrighted FF): a 52-byte big-endian header, one
/// script string, then a 2-asset pool (RawFile, Localize) with their inline bodies. This
/// exercises ParseHeader, script-string resolution, the asset-pool walk, the body readers,
/// and the deferred-resolution ordering end to end.
/// </summary>
public class Iw4ZoneReaderTests
{
    [Fact]
    public void ReadsHeader_ScriptStrings_Pool_AndFlatBodies()
    {
        var zone = BuildSyntheticZone();

        var r = new Iw4ZoneReader(zone).Read();

        // script strings
        Assert.Equal(new string?[] { "hi" }, r.AssetList.ScriptStrings);

        // asset pool (always complete)
        var assets = r.AssetList.Assets;
        Assert.Equal(2, assets.Length);
        Assert.Equal(XAssetType.RawFile, assets[0].Type);
        Assert.Equal(XAssetType.Localize, assets[1].Type);

        // both bodies are flat types with readers, so the walk completes
        Assert.Null(r.StoppedAtType);
        Assert.Null(r.Error);

        var raw = Assert.IsType<RawFile>(assets[0].XAssetPtr!.Result);
        Assert.Equal("raw", raw.Name);
        Assert.Equal(5, raw.Len);
        // offsets the editor's save path relies on: header struct at 0x4B, body at 0x5F
        Assert.Equal(0x4B, raw.Offset);
        Assert.Equal(0x5F, raw.DataOffset);
        Assert.Equal(5, raw.OnDiskSize);

        var loc = Assert.IsType<LocalizeEntry>(assets[1].XAssetPtr!.Result);
        Assert.Equal("VAL", loc.Value);
        Assert.Equal("KEY", loc.Name); // localize "name" is the reference key
    }

    [Fact]
    public void BodyWalk_StopsAtFirstTypeWithoutReader()
    {
        // XModel has no ported body reader, so an XModel at asset #0 halts the walk cleanly.
        var zone = BuildSyntheticZone(firstAssetType: (int)XAssetType.XModel);

        var r = new Iw4ZoneReader(zone).Read();

        Assert.Equal(XAssetType.XModel, r.AssetList.Assets[0].Type);
        Assert.Equal(XAssetType.XModel, r.StoppedAtType);
        Assert.Equal(0, r.StoppedAtIndex);
        Assert.Null(r.Error);
        // the unread asset never got a resolved body
        Assert.False(r.AssetList.Assets[0].XAssetPtr!.IsResolved);
    }

    /// <summary>
    /// Builds: [52-byte BE header][scriptStrings: 1 inline ptr + "hi\0"]
    ///         [pool: firstAssetType(-1), Localize(-1)]
    ///         [rawfile body: namePtr(-1), compLen 0, len 5, bufPtr(-1), "raw\0", 5 bytes]
    ///         [localize body: valuePtr(-1), namePtr(-1), "VAL\0", "KEY\0"]
    /// </summary>
    private static byte[] BuildSyntheticZone(int firstAssetType = (int)XAssetType.RawFile)
    {
        var b = new List<byte>();

        void I32(int v)
        {
            b.Add((byte)(v >> 24)); b.Add((byte)(v >> 16)); b.Add((byte)(v >> 8)); b.Add((byte)v);
        }
        void Cstr(string s)
        {
            b.AddRange(Encoding.ASCII.GetBytes(s));
            b.Add(0);
        }

        // XFile header (Size patched at the end), ExternalSize, 7 block sizes
        I32(0);          // 0x00 Size (placeholder)
        I32(0);          // 0x04 ExternalSize
        I32(0);          // 0x08 TEMP
        I32(0);          // 0x0C PHYSICAL
        I32(0);          // 0x10 RUNTIME
        I32(0);          // 0x14 VIRTUAL
        I32(100);        // 0x18 LARGE
        I32(0);          // 0x1C CALLBACK
        I32(0);          // 0x20 VERTEX

        // XAssetList header
        I32(1);          // 0x24 scriptStringCount
        I32(-1);         // 0x28 scriptStringsPtr (inline)
        I32(2);          // 0x2C assetCount
        I32(-1);         // 0x30 assetsPtr (inline)

        // inline: script string pointer array (1) + the string
        I32(-1);         // ptr[0] inline
        Cstr("hi");

        // asset pool: [type][ptr] x 2
        I32(firstAssetType); I32(-1);
        I32((int)XAssetType.Localize); I32(-1);

        // rawfile body (only reached when firstAssetType is RawFile)
        I32(-1);         // namePtr inline
        I32(0);          // compressedLen
        I32(5);          // len
        I32(-1);         // bufferPtr inline
        Cstr("raw");     // name
        b.AddRange(new byte[] { 1, 2, 3, 4, 5 }); // 5-byte buffer

        // localize body
        I32(-1);         // valuePtr inline
        I32(-1);         // namePtr inline
        Cstr("VAL");     // value
        Cstr("KEY");     // reference key

        var arr = b.ToArray();
        // patch Size (offset 0) = total length, big-endian
        arr[0] = (byte)(arr.Length >> 24);
        arr[1] = (byte)(arr.Length >> 16);
        arr[2] = (byte)(arr.Length >> 8);
        arr[3] = (byte)arr.Length;
        return arr;
    }
}
