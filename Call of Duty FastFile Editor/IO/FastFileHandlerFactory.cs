using Call_of_Duty_FastFile_Editor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Call_of_Duty_FastFile_Editor.IO
{
    public static class FastFileHandlerFactory
    {
        public static IFastFileHandler GetHandler(FastFile file)
        {
            bool isXbox360 = file.IsXbox360;

            if (file.IsCod5File)
                return new CoD5FastFileHandler(isXbox360);
            if (file.IsCod4File)
                return new CoD4FastFileHandler(isXbox360);
            if (file.IsMW2File)
                return new MW2FastFileHandler();
            throw new NotSupportedException("Unknown or unsupported game.");
        }
    }
}
