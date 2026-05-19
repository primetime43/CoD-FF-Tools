namespace Call_of_Duty_FastFile_Editor.IO
{
    /// <summary>
    /// Factory marker for MW2 FastFiles. Decompress logic is shared via the base
    /// class; save/recompress is handled by <see cref="FastFileSaveService"/>.
    /// </summary>
    public class MW2FastFileHandler : FastFileHandlerBase
    {
    }
}
