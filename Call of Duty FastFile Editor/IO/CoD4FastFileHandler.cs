using Call_of_Duty_FastFile_Editor.GameDefinitions;
using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.IO
{
    public class CoD4FastFileHandler : FastFileHandlerBase
    {
        protected override byte[] HeaderBytes => FastFileConstants.UnsignedHeaderBytes;
        protected override byte[] VersionBytes => CoD4Definition.VersionBytes;
    }
}
