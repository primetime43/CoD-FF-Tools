using Call_of_Duty_FastFile_Editor.GameDefinitions;
using FastFileLib;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.IO
{
    public class CoD5FastFileHandler : FastFileHandlerBase
    {
        protected override byte[] HeaderBytes => FastFileConstants.UnsignedHeaderBytes;
        protected override byte[] VersionBytes => CoD5Definition.VersionBytes;
    }
}
