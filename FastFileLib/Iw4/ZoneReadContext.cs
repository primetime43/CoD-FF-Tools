// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Logic/Zone/ZoneReadContext.cs — the stream reader + the
// inline-pointer deferred-resolution queue (ResolveQueued) that walks asset
// bodies in the engine's order. This is what makes nested types (e.g. stringtable)
// read correctly, unlike a naive linear reader.
//
// Adaptations (only): ReadVec4 dropped (unused by the ported readers); the catch
// `when` filters additionally let Iw4UnsupportedTypeException propagate unwrapped,
// so the top level can stop cleanly at the first asset type without a body reader.
// =============================================================================

namespace FastFileLib.Iw4;

internal delegate T ZoneValueReader<T>(ref ZoneReadContext context);

internal delegate void ZonePointerResolver<T>(ref ZoneReadContext context, ZonePointer<T> pointer);

internal interface IQueuedZonePointerResolver
{
    string Name { get; }
    void Resolve(ref ZoneReadContext context);
}

internal sealed class QueuedZonePointerResolver<T>(
    ZonePointer<T> pointer,
    ZonePointerResolver<T> resolver) : IQueuedZonePointerResolver
{
    public string Name => typeof(T).Name;

    public void Resolve(ref ZoneReadContext context)
    {
        var start = context.Position;
        Memory.ResolvePointer(pointer, context.Position);

        try
        {
            resolver(ref context, pointer);
            pointer.SetSourceSpan(start, context.Position - start);
            context.Trace?.Invoke(Name, start, context.Position);
        }
        catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
        {
            throw new InvalidDataException(
                $"Failed to resolve inline pointer for {typeof(T).Name}; raw=0x{pointer.Raw:X8}, offset=0x{pointer.Offset:X8}, current zone offset=0x{context.Position:X8} ({context.Position:N0}).",
                ex);
        }
    }
}

internal ref struct ZoneReadContext
{
    private readonly ReadOnlySpan<byte> _span;
    private readonly List<IQueuedZonePointerResolver> _inlineResolvers = new();
    private readonly List<IQueuedZonePointerResolver> _deferredResolvers = new();
    private bool _deferInlinePointers;
    public Action<string, int, int>? Trace { get; set; }

    public ZoneReadContext(ReadOnlySpan<byte> span, int position)
    {
        _span = span;
        Position = position;
    }

    public int Position;

    public ReadOnlySpan<byte> Span => _span;

    public bool PushInlinePointerDeferral(bool deferInlinePointers = true)
    {
        var previous = _deferInlinePointers;
        _deferInlinePointers = deferInlinePointers;
        return previous;
    }

    public void RestoreInlinePointerDeferral(bool deferInlinePointers)
    {
        _deferInlinePointers = deferInlinePointers;
    }

    public int ReadInt32()
    {
        EnsureAvailable(4, "Int32");
        return _span.ReadInt32(ref Position);
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(2, "UInt16");
        return _span.ReadUInt16(ref Position);
    }

    public uint ReadUInt32()
    {
        EnsureAvailable(4, "UInt32");
        return _span.ReadUInt32(ref Position);
    }

    public ulong ReadUInt64()
    {
        EnsureAvailable(8, "UInt64");
        return _span.ReadUInt64(ref Position);
    }

    public float ReadFloat()
    {
        EnsureAvailable(4, "Float");
        return _span.ReadFloat(ref Position);
    }

    public byte ReadByte()
    {
        EnsureAvailable(1, "Byte");
        return _span.ReadByte(ref Position);
    }

    public Vec4 ReadVec4()
    {
        EnsureAvailable(16, "Vec4");
        return _span.ReadVec4(ref Position);
    }

    public bool ReadBool()
    {
        var start = Position;
        try
        {
            EnsureAvailable(1, "Boolean");
            return _span.ReadBool(ref Position);
        }
        catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
        {
            throw ReadFailure("Boolean", start, ex);
        }
    }

    public string ReadCString()
    {
        var start = Position;
        try
        {
            return _span.ReadCStringAt(ref Position);
        }
        catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
        {
            throw ReadFailure("CString", start, ex);
        }
    }

    public string ReadAlignedCString(int alignment = 4)
    {
        var value = ReadCString();
        AlignPosition(alignment);
        return value;
    }

    public string ReadString(int length)
    {
        EnsureAvailable(length, $"String[{length}]");
        return _span.ReadString(ref Position, length);
    }

    public byte[] ReadBytes(int length)
    {
        EnsureAvailable(length, $"Byte[{length}]");
        return _span.Read(ref Position, length);
    }

    public ZonePointer<T> ReadPointer<T>()
    {
        EnsureAvailable(4, $"Pointer<{typeof(T).Name}>");
        return Memory.ReadPointer<T>(_span, ref Position);
    }

    public ZonePointer<T> ReadPointer<T>(ZoneValueReader<T> reader)
    {
        return ReadPointer<T>((ref ZoneReadContext context, ZonePointer<T> pointer) =>
        {
            var value = context.ReadPointerValue(pointer, reader);
            pointer.SetResult(value);
        });
    }

    public ZonePointer<T> ReadPointer<T>(ZonePointerResolver<T> resolver)
    {
        var pointer = ReadPointer<T>();
        ResolvePointer(pointer, resolver);
        return pointer;
    }

    public ZonePointer<T> ReadInlinePointer<T>(ZonePointerResolver<T> resolver)
    {
        var pointer = ReadPointer<T>();
        ResolveInlinePointer(pointer, resolver);
        return pointer;
    }

    public void ResolvePointer<T>(ZonePointer<T> pointer, ZonePointerResolver<T> resolver)
    {
        if (pointer.IsResolved)
            return;

        try
        {
            switch (pointer.Kind)
            {
                case PointerKind.Null:
                    pointer.SetResult(default);
                    break;
                case PointerKind.Inline:
                    AddInlineResolver(pointer, resolver);
                    break;
                case PointerKind.Offset:
                    pointer.SetResult(default);
                    break;
            }
        }
        catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
        {
            throw PointerFailure(pointer, typeof(T).Name, ex);
        }
    }

    public void ResolveInlinePointer<T>(ZonePointer<T> pointer, ZonePointerResolver<T> resolver)
    {
        if (pointer.IsResolved)
            return;

        try
        {
            switch (pointer.Kind)
            {
                case PointerKind.Null:
                case PointerKind.Offset:
                    pointer.SetResult(default);
                    break;
                case PointerKind.Inline:
                    AddInlineResolver(pointer, resolver);
                    break;
            }
        }
        catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
        {
            throw PointerFailure(pointer, typeof(T).Name, ex);
        }
    }

    public void ResolveInlinePointerDeferred<T>(ZonePointer<T> pointer, ZonePointerResolver<T> resolver)
    {
        if (pointer.IsResolved)
            return;

        try
        {
            switch (pointer.Kind)
            {
                case PointerKind.Null:
                case PointerKind.Offset:
                    pointer.SetResult(default);
                    break;
                case PointerKind.Inline:
                    _deferredResolvers.Add(new QueuedZonePointerResolver<T>(pointer, resolver));
                    break;
            }
        }
        catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
        {
            throw PointerFailure(pointer, typeof(T).Name, ex);
        }
    }

    public void ResolvePointerDeferred<T>(ZonePointer<T> pointer, ZonePointerResolver<T> resolver)
    {
        if (pointer.IsResolved)
            return;

        try
        {
            switch (pointer.Kind)
            {
                case PointerKind.Null:
                    pointer.SetResult(default);
                    break;
                case PointerKind.Inline:
                    _deferredResolvers.Add(new QueuedZonePointerResolver<T>(pointer, resolver));
                    break;
                case PointerKind.Offset:
                    pointer.SetResult(default);
                    break;
            }
        }
        catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
        {
            throw PointerFailure(pointer, typeof(T).Name, ex);
        }
    }

    internal void ResolveInlinePointerNow<T>(ZonePointer<T> pointer, ZonePointerResolver<T> resolver)
    {
        if (pointer.IsResolved)
            return;

        switch (pointer.Kind)
        {
            case PointerKind.Null:
                pointer.SetResult(default);
                return;
            case PointerKind.Offset:
                pointer.SetResult(default);
                return;
            case PointerKind.Inline:
                break;
        }

        var start = Position;
        Memory.ResolvePointer(pointer, Position);

        try
        {
            resolver(ref this, pointer);
            pointer.SetSourceSpan(start, Position - start);
            Trace?.Invoke(typeof(T).Name, start, Position);
        }
        catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
        {
            throw PointerFailure(pointer, typeof(T).Name, ex);
        }
    }

    public T ReadPointerValue<T>(ZonePointer<T> pointer, ZoneValueReader<T> reader)
    {
        var start = Position;
        Memory.ResolvePointer(pointer, Position);

        if (pointer.Kind != PointerKind.Offset)
        {
            try
            {
                var value = reader(ref this);
                pointer.SetSourceSpan(start, Position - start);
                return value;
            }
            catch (Exception ex) when (ex is not Iw4UnsupportedTypeException && ex is not InvalidDataException { InnerException: not null })
            {
                throw PointerFailure(pointer, typeof(T).Name, ex);
            }
        }

        return default!;
    }

    public void ResolveQueued()
    {
        var resolvedCount = 0;

        while (_inlineResolvers.Count > 0 || _deferredResolvers.Count > 0)
        {
            if (++resolvedCount > 1_000_000)
            {
                throw new InvalidDataException(
                    $"Stopped resolving inline zone pointers after {resolvedCount:N0} entries at zone offset 0x{Position:X8} ({Position:N0}); remaining queued pointers: {_inlineResolvers.Count:N0}, deferred pointers: {_deferredResolvers.Count:N0}.");
            }

            if (_inlineResolvers.Count == 0)
            {
                _inlineResolvers.AddRange(_deferredResolvers);
                _deferredResolvers.Clear();
            }

            var resolver = _inlineResolvers[0];
            _inlineResolvers.RemoveAt(0);

            var olderSiblingCount = _inlineResolvers.Count;
            var olderDeferredCount = _deferredResolvers.Count;
            resolver.Resolve(ref this);

            var nestedCount = _inlineResolvers.Count - olderSiblingCount;
            var nestedDeferredCount = _deferredResolvers.Count - olderDeferredCount;
            var deferredInsertIndex = 0;
            if (nestedCount > 0 && olderSiblingCount > 0)
            {
                var nestedResolvers = _inlineResolvers.GetRange(olderSiblingCount, nestedCount);
                _inlineResolvers.RemoveRange(olderSiblingCount, nestedCount);

                _inlineResolvers.InsertRange(0, nestedResolvers);
                deferredInsertIndex = nestedResolvers.Count;
            }

            if (nestedDeferredCount > 0)
            {
                var nestedDeferredResolvers = _deferredResolvers.GetRange(olderDeferredCount, nestedDeferredCount);
                _deferredResolvers.RemoveRange(olderDeferredCount, nestedDeferredCount);

                _inlineResolvers.InsertRange(deferredInsertIndex, nestedDeferredResolvers);
            }
        }
    }

    public void PromoteDeferredPointers()
    {
        if (_deferredResolvers.Count == 0)
            return;

        _inlineResolvers.InsertRange(0, _deferredResolvers);
        _deferredResolvers.Clear();
    }

    private void AddInlineResolver<T>(ZonePointer<T> pointer, ZonePointerResolver<T> resolver)
    {
        var queuedResolver = new QueuedZonePointerResolver<T>(pointer, resolver);
        if (_deferInlinePointers)
            _deferredResolvers.Add(queuedResolver);
        else
            _inlineResolvers.Add(queuedResolver);
    }

    public void AlignPosition(int alignment)
    {
        if (alignment <= 0)
            throw new InvalidDataException($"Cannot align zone position with invalid alignment {alignment:N0}.");

        var remainder = Position % alignment;
        if (remainder == 0)
            return;

        Position += alignment - remainder;
    }

    private void EnsureAvailable(int length, string operation)
    {
        if (length < 0)
            throw new InvalidDataException($"Cannot read {operation} with negative length {length:N0} at zone offset 0x{Position:X8} ({Position:N0}).");

        if (Position < 0 || Position + length > _span.Length)
            throw new InvalidDataException($"Cannot read {operation} ({length:N0} byte(s)) at zone offset 0x{Position:X8} ({Position:N0}); zone length is 0x{_span.Length:X8} ({_span.Length:N0}).");
    }

    private static InvalidDataException ReadFailure(string operation, int position, Exception innerException)
    {
        return new InvalidDataException($"Failed to read {operation} at zone offset 0x{position:X8} ({position:N0}).", innerException);
    }

    private InvalidDataException PointerFailure<T>(ZonePointer<T> pointer, string typeName, Exception innerException)
    {
        return new InvalidDataException(
            $"Failed to resolve {pointer.Kind} pointer for {typeName}; raw=0x{pointer.Raw:X8}, offset=0x{pointer.Offset:X8}, current zone offset=0x{Position:X8} ({Position:N0}).",
            innerException);
    }
}
