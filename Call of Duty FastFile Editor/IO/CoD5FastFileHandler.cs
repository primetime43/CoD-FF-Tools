using Call_of_Duty_FastFile_Editor.GameDefinitions;
using FastFileLib;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.IO
{
    public class CoD5FastFileHandler : FastFileHandlerBase
    {
        private readonly bool _isXbox360;

        public CoD5FastFileHandler(bool isXbox360 = false)
        {
            _isXbox360 = isXbox360;
        }

        // Xbox 360 uses signed header (IWff0100), PS3 uses unsigned (IWffu100)
        protected override byte[] HeaderBytes => _isXbox360
            ? FastFileConstants.SignedHeaderBytes
            : FastFileConstants.UnsignedHeaderBytes;
        protected override byte[] VersionBytes => CoD5Definition.VersionBytes;
    }
}
