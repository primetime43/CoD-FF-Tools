// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Logic/Memory.cs
// =============================================================================

namespace FastFileLib.Iw4;

internal static class Memory
{
    public static ZonePointer<T> ReadPointer<T>(ReadOnlySpan<byte> span, ref int position)
    {
        int raw = span.ReadInt32(ref position);
        return new ZonePointer<T>(raw);
    }

    public static void ResolvePointer(Pointer ptr, int position)
    {
        if (ptr.Kind == PointerKind.Null)
            return;

        if (ptr.Kind == PointerKind.Inline)
            ptr.SetOffset(position);
    }
}
