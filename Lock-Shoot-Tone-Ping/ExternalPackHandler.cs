using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lock_Shoot_Tone_Ping
{
    internal class ExternalPackHandler
    {
        public ExternalPackHandler(string Root)
        {
            string FileModName = Plugin.I.GetFileModName();
            Root = $"{Root}\\{FileModName}\\Packs";
            if (!Directory.Exists(Root))
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Error, "External packs Folder Not Found [In External Pack Handler]. Replacement cannot therefore be generated");
                return;
            }


        }
    }
}
