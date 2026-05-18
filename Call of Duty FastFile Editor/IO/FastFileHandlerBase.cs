using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.IO
{
    /// <summary>
    /// Base FastFile handler that delegates all compression/decompression to FastFileLib so the
    /// editor and CLI share the same code paths. Per-game subclasses exist only as markers for
    /// the factory; they no longer carry their own format-specific logic.
    /// </summary>
    public abstract class FastFileHandlerBase : IFastFileHandler
    {
        /// <summary>The game this handler targets — used to pick the right library compressor.</summary>
        protected abstract GameVersion Game { get; }

        public virtual void Decompress(string inputFilePath, string outputFilePath)
        {
            FastFileProcessor.Decompress(inputFilePath, outputFilePath);
        }

        public virtual void Recompress(string ffFilePath, string zoneFilePath, FastFile openedFastFile)
        {
            string platform = openedFastFile.IsPC ? "PC"
                            : openedFastFile.IsWii ? "Wii"
                            : openedFastFile.IsXbox360 ? "Xbox360"
                            : "PS3";

            FastFileProcessor.Recompress(
                zoneFilePath,
                ffFilePath,
                Game,
                platform,
                openedFastFile.IsSigned,
                openedFastFile.FfFilePath);
        }
    }
}
