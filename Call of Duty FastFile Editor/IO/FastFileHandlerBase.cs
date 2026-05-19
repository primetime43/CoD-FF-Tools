using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.IO
{
    /// <summary>
    /// Base FastFile handler — currently just carries the open-time Decompress glue.
    /// Save/recompress was moved to <see cref="FastFileSaveService"/> so every save
    /// flow funnels through one platform-aware code path. The per-game subclasses
    /// (CoD4/CoD5/MW2) remain as factory markers but no longer carry behavior.
    /// </summary>
    public abstract class FastFileHandlerBase : IFastFileHandler
    {
        public virtual void Decompress(string inputFilePath, string outputFilePath)
        {
            FastFileProcessor.Decompress(inputFilePath, outputFilePath);
        }
    }
}
