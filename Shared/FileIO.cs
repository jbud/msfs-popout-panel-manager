﻿using System;
using System.IO;

namespace MSFSPopoutPanelManager.Shared
{
    public class FileIo
    {
        public static string GetUserDataFilePath()
        {
#if DEBUG || DEBUGTOUCHPANEL
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MSFS Pop Out Panel Manager Debug");
#elif RELEASETOUCHPANEL
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MSFS Pop Out Panel Manager Touch Panel");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MSFS Pop Out Panel Manager");
#endif
        }

        public static string GetErrorLogFilePath()
        {
#if DEBUG || DEBUGTOUCHPANEL
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager Debug\LogFiles\error.log");
#elif RELEASETOUCHPANEL
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager Touch Panel\LogFiles\error.log");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager\LogFiles\error.log");
#endif
        }

        public static string GetDebugLogFilePath()
        {
#if DEBUG || DEBUGTOUCHPANEL
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager Debug\LogFiles\debug.log");
#elif RELEASETOUCHPANEL
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager Touch Panel\LogFiles\debug.log");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager\LogFiles\debug.log");
#endif
        }

        public static string GetInfoLogFilePath()
        {
#if DEBUG || DEBUGTOUCHPANEL
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager Debug\LogFiles\info.log");
#elif RELEASETOUCHPANEL
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager Touch Panel\LogFiles\info.log");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"MSFS Pop Out Panel Manager\LogFiles\info.log");
#endif
        }
    }
}
