namespace Call_of_Duty_FastFile_Editor.IO
{
    /// <summary>
    /// Per-game FF I/O handler. Currently only carries the open-time Decompress step;
    /// save/recompress now goes through <see cref="FastFileSaveService"/> instead, so
    /// every save flow uses the same platform-aware path.
    /// </summary>
    public interface IFastFileHandler
    {
        void Decompress(string inputFilePath, string outputFilePath);
    }
}
