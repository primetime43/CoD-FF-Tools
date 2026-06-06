// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Logic/Zone/ZoneReader.cs (ParseHeader / ParseXAssetList / ReadAsset).
//
// Adaptation: the reference reader assumes every asset type has a body reader. Here only
// a subset is ported (see XAssetReaderRegistry), so ReadAsset throws
// Iw4UnsupportedTypeException at the first un-ported type; we catch it, stop, and return
// the header + script strings + full asset-type list + the bodies resolved so far.
// =============================================================================

namespace FastFileLib.Iw4;

/// <summary>Outcome of an <see cref="Iw4ZoneReader"/> walk.</summary>
public sealed class Iw4ZoneReadResult
{
    public XFile Header { get; init; } = new();
    public XAssetList AssetList { get; init; } = new();
    public ZoneBlockLayout? Layout { get; init; }
    /// <summary>First asset type that halted the body walk (no body reader ported); null if the walk completed.</summary>
    public XAssetType? StoppedAtType { get; init; }
    public int StoppedAtIndex { get; init; } = -1;
    /// <summary>Set if the walk failed for a reason other than an un-ported type (e.g. a genuine desync/parse error).</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Reads an inflated IW4 (MW2 PS3) zone the engine's way, via the ported
/// <see cref="ZoneReadContext"/> deferred-resolution engine. The XFile header, script
/// strings, and asset-pool type list are always complete; asset bodies are read in pool
/// order until the first type without a ported reader.
/// </summary>
public sealed class Iw4ZoneReader
{
    private readonly byte[] _buffer;
    private int _position;

    public Iw4ZoneReader(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public Iw4ZoneReadResult Read()
    {
        _position = 0;
        var header = ParseHeader();
        var (assetList, stoppedType, stoppedIndex, error) = ParseXAssetList();

        return new Iw4ZoneReadResult
        {
            Header = header,
            AssetList = assetList,
            Layout = new ZoneBlockLayout(header.Size, header.BlockSize),
            StoppedAtType = stoppedType,
            StoppedAtIndex = stoppedIndex,
            Error = error,
        };
    }

    private XFile ParseHeader()
    {
        var context = new ZoneReadContext(_buffer, _position);

        int headerSize = FastFileConstants.GetZoneHeaderSize(GameVersion.MW2, isXbox360: false, isPC: false, isWii: false);
        int blockCount = (headerSize - 8 - 16) / 4;

        var header = new XFile
        {
            Size = context.ReadInt32(),
            ExternalSize = context.ReadInt32(),
            BlockSize = new int[blockCount],
        };
        for (int i = 0; i < blockCount; i++)
            header.BlockSize[i] = context.ReadInt32();

        _position = context.Position;
        return header;
    }

    private (XAssetList list, XAssetType? stoppedType, int stoppedIndex, string? error) ParseXAssetList()
    {
        var context = new ZoneReadContext(_buffer, _position);
        var assetList = ReadXAssetList(ref context);

        XAssetType? stoppedType = null;
        int stoppedIndex = -1;
        string? error = null;

        try
        {
            context.ResolveQueued();
        }
        catch (Exception ex)
        {
            var unsupported = FindUnsupported(ex);
            if (unsupported != null)
            {
                stoppedType = unsupported.AssetType;
                stoppedIndex = unsupported.AssetIndex;
            }
            else
            {
                // surface the innermost cause (the engine wraps each resolver failure)
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                error = inner.Message;
            }
        }

        _position = context.Position;
        return (assetList, stoppedType, stoppedIndex, error);
    }

    private static XAssetList ReadXAssetList(ref ZoneReadContext context)
    {
        var scriptStringCount = context.ReadInt32();
        var scriptStringsPtr = context.ReadPointer<ZonePointer<string?>[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<ZonePointer<string?>[]> pointer) =>
            {
                var pointers = ReadScriptStringPointers(ref pointerContext, scriptStringCount);
                pointer.SetResult(pointers);

                foreach (var scriptStringPointer in pointers)
                {
                    if (scriptStringPointer.Kind == PointerKind.Inline)
                    {
                        var scriptString = pointerContext.ReadPointerValue(
                            scriptStringPointer,
                            (ref ZoneReadContext c) => (string?)c.ReadCString());
                        scriptStringPointer.SetResult(scriptString);
                        continue;
                    }

                    scriptStringPointer.SetResult(default);
                }
            });

        var assetCount = context.ReadInt32();
        var assetsPtr = context.ReadPointer<XAsset[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<XAsset[]> pointer) =>
            {
                var assets = ReadAssets(ref pointerContext, assetCount);
                pointer.SetResult(assets);
            });

        return new XAssetList
        {
            ScriptStringCount = scriptStringCount,
            ScriptStringsPtr = scriptStringsPtr,
            AssetCount = assetCount,
            AssetsPtr = assetsPtr,
        };
    }

    private static ZonePointer<string?>[] ReadScriptStringPointers(ref ZoneReadContext context, int count)
    {
        var pointers = new ZonePointer<string?>[Math.Max(0, count)];
        for (var i = 0; i < pointers.Length; i++)
            pointers[i] = context.ReadPointer<string?>();
        return pointers;
    }

    private static XAsset[] ReadAssets(ref ZoneReadContext context, int count)
    {
        var assets = new XAsset[Math.Max(0, count)];
        for (var i = 0; i < assets.Length; i++)
            assets[i] = ReadAsset(ref context, i);
        return assets;
    }

    private static XAsset ReadAsset(ref ZoneReadContext context, int index)
    {
        var type = (XAssetType)context.ReadInt32();
        var asset = new XAsset { Type = type };

        asset.XAssetPtr = context.ReadPointer<BaseAsset>(
            (ref ZoneReadContext pointerContext, ZonePointer<BaseAsset> pointer) =>
            {
                if (!XAssetReaderRegistry.TryGetReader(type, out var reader))
                    throw new Iw4UnsupportedTypeException(type, index, pointerContext.Position);

                var value = reader(ref pointerContext);
                pointer.SetResult(value);
            });

        return asset;
    }

    private static Iw4UnsupportedTypeException? FindUnsupported(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
            if (e is Iw4UnsupportedTypeException u)
                return u;
        return null;
    }
}
