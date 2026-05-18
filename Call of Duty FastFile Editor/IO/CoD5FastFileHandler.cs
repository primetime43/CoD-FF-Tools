using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.IO
{
    public class CoD5FastFileHandler : FastFileHandlerBase
    {
        protected override GameVersion Game => GameVersion.WaW;
    }
}
