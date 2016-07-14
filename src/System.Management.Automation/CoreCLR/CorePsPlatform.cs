/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.IO;

#if CORECLR
// SMA.Environment is only available on CoreCLR
using SpecialFolder = System.Management.Automation.Environment.SpecialFolder;
#endif

namespace System.Management.Automation
{

    /// <summary>
    /// These are platform abstractions and platform specific implementations
    ///
    /// All these properties are calling into platform specific static classes, to make
    /// sure the platform implementations are switched at runtime (including pinvokes).
    /// </summary>
    public static class Platform
    {

        // Platform variables used to defined corresponding PowerShell built-in variables
        public static bool IsLinux
        {
            get
            {
                #if CORECLR
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
                #else
                return false;
                #endif
            }
        }

        //enum for selecting the xdgpaths
        public enum XDG_Type
        {
            // location to store configuration file
            CONFIG,
            // location for powershell modules
            MODULES,
            // location to store temporary files
            CACHE,
            // location to store data that application needs
            DATA,
            // default location
            DEFAULT
        }

        public static bool IsOSX
        {
            get
            {
                #if CORECLR
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
                #else
                return false;
                #endif
            }
        }

        public static bool IsWindows
        {
            get
            {
                #if CORECLR
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                #else
                return true;
                #endif
            }
        }

        public static bool IsCore
        {
            get
            {
                #if CORECLR
                return true;
                #else
                return false;
                #endif
            }
        }

        // format files
        internal static List<string> FormatFileNames = new List<string>
            {
                "Certificate.format.ps1xml",
                "Diagnostics.format.ps1xml",
                "DotNetTypes.format.ps1xml",
                "Event.format.ps1xml",
                "FileSystem.format.ps1xml",
                "Help.format.ps1xml",
                "HelpV3.format.ps1xml",
                "PowerShellCore.format.ps1xml",
                "PowerShellTrace.format.ps1xml",
                "Registry.format.ps1xml",
                "WSMan.format.ps1xml"
            };

        // function for choosing directory location of PowerShell for profile loading
        public static string SelectProductNameForDirectory (Platform.XDG_Type dirpath)
        {

            //TODO: XDG_DATA_DIRS implementation as per GitHub issue #1060

            string xdgconfighome = System.Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            string xdgdatahome = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            string xdgcachehome = System.Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            string xdgConfigHomeDefault =  Path.Combine ( System.Environment.GetEnvironmentVariable("HOME"), ".config", "powershell");
            string xdgDataHomeDefault = Path.Combine( System.Environment.GetEnvironmentVariable("HOME"), ".local", "share", "powershell");
            string xdgModuleDefault = Path.Combine ( xdgDataHomeDefault, "Modules");
            string xdgCacheDefault = Path.Combine (System.Environment.GetEnvironmentVariable("HOME"), ".cache", "powershell");

            switch (dirpath){
                case Platform.XDG_Type.CONFIG:
                    //the user has set XDG_CONFIG_HOME corrresponding to profile path
                    if (String.IsNullOrEmpty(xdgconfighome))
                    {
                        //xdg values have not been set
                        return xdgConfigHomeDefault;
                    }

                    else
                    {
                        return Path.Combine(xdgconfighome, "powershell");
                    }

                case Platform.XDG_Type.DATA:
                    //the user has set XDG_DATA_HOME corresponding to module path
                    if (String.IsNullOrEmpty(xdgdatahome)){

                        // create the xdg folder if needed
                        if (!Directory.Exists(xdgDataHomeDefault))
                        {
                            Directory.CreateDirectory(xdgDataHomeDefault);
                        }
                        return xdgDataHomeDefault;
                    }
                    else
                    {
                        return Path.Combine(xdgdatahome, "powershell");
                    }

                case Platform.XDG_Type.MODULES:
                    //the user has set XDG_DATA_HOME corresponding to module path
                    if (String.IsNullOrEmpty(xdgdatahome)){

                        //xdg values have not been set
                        if (!Directory.Exists(xdgModuleDefault)) //module folder not always guaranteed to exist
                        {
                            Directory.CreateDirectory(xdgModuleDefault);
                        }
                        return xdgModuleDefault;
                    }
                    else
                    {
                        return Path.Combine(xdgdatahome, "powershell", "Modules");
                    }

                case Platform.XDG_Type.CACHE:
                    //the user has set XDG_CACHE_HOME
                    if (String.IsNullOrEmpty(xdgcachehome))
                    {
                        //xdg values have not been set
                        if (!Directory.Exists(xdgCacheDefault)) //module folder not always guaranteed to exist
                        {
                            Directory.CreateDirectory(xdgCacheDefault);
                        }

                        return xdgCacheDefault;
                    }

                    else
                    {
                        if (!Directory.Exists(Path.Combine(xdgcachehome, "powershell")))
                        {
                            Directory.CreateDirectory(Path.Combine(xdgcachehome, "powershell"));
                        }

                        return Path.Combine(xdgcachehome, "powershell");
                    }

                case Platform.XDG_Type.DEFAULT:
                    //default for profile location
                    return xdgConfigHomeDefault;

                default:
                    //xdgConfigHomeDefault needs to be created in the edge case that we do not have the folder or it was deleted
                    //This folder is the default in the event of all other failures for data storage
                    if (!Directory.Exists(xdgConfigHomeDefault))
                    {
                        try {
                            Directory.CreateDirectory(xdgConfigHomeDefault);
                        }
                        catch{

                            Console.Error.WriteLine("Failed to create default data directory: " + xdgConfigHomeDefault);
                        }
                    }

                    return xdgConfigHomeDefault;
            }

        }

        // non-windows does not have reparse points
        internal static bool SupportsReparsePoints()
        {
            return IsWindows;
        }

        // non-windows does not support removing drives
        internal static bool SupportsRemoveDrive()
        {
            return IsWindows;
        }

        // Platform methods prefixed NonWindows are:
        // - non-windows by the definition of the IsWindows method above
        // - here, because porting to Linux and other operating systems
        //   should not move the original Windows code out of the module
        //   it belongs to, so this way the windows code can remain in it's
        //   original source file and only the non-windows code has been moved
        //   out here
        // - only to be used with the IsWindows feature query, and only if
        //   no other more specific feature query makes sense

        internal static bool NonWindowsIsHardLink(ref IntPtr handle)
        {
            return Unix.IsHardLink(ref handle);
        }

        internal static bool NonWindowsIsHardLink(FileSystemInfo fileInfo)
        {
            return Unix.IsHardLink(fileInfo);
        }

        internal static bool NonWindowsIsSymLink(FileSystemInfo fileInfo)
        {
            return Unix.IsSymLink(fileInfo);
        }

        internal static string NonWindowsInternalGetTarget(SafeFileHandle handle)
        {
            // SafeHandle is a Windows concept.  Use the string version instead.
            throw new PlatformNotSupportedException();
        }

        internal static string NonWindowsInternalGetTarget(string path)
        {
            return Unix.FollowSymLink(path);
        }

        internal static string NonWindowsGetUserFromPid(int path)
        {
            return Unix.GetUserFromPid(path);
        }

        #if CORECLR
        internal static string NonWindowsGetFolderPath(SpecialFolder folder)
        {
            return Unix.GetFolderPath(folder);
        }
        #endif

        internal static string NonWindowsInternalGetLinkType(FileSystemInfo fileInfo)
        {
            if (NonWindowsIsSymLink(fileInfo))
            {
                return "SymbolicLink";
            }

            if (NonWindowsIsHardLink(fileInfo))
            {
                return "HardLink";
            }

            return null;
        }

        internal static bool NonWindowsCreateSymbolicLink(string path, string strTargetPath, bool isDirectory)
        {
            // Linux doesn't care if target is a directory or not
            return Unix.CreateSymbolicLink(path, strTargetPath);
        }

        internal static bool NonWindowsCreateHardLink(string path, string strTargetPath)
        {
            return Unix.CreateHardLink(path, strTargetPath);
        }

        internal static void NonWindowsSetDate(DateTime dateToUse)
        {
            Unix.SetDateInfoInternal date = new Unix.SetDateInfoInternal(dateToUse);
            Unix.SetDate(date);
        }

        internal static string NonWindowsGetDomainName()
        {
            string fullyQualifiedName = Unix.NativeMethods.GetFullyQualifiedName();
            if (string.IsNullOrEmpty(fullyQualifiedName))
            {
                int lastError = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Unix.NonWindowsGetDomainName error: " + lastError);
            }

            int index = fullyQualifiedName.IndexOf('.');
            if (index >= 0)
            {
                return fullyQualifiedName.Substring(index + 1);
            }

            return "";
        }

        internal static string NonWindowsGetUserName()
        {
            return Unix.UserName;
        }

        // Hostname in this context seems to be the FQDN
        internal static string NonWindowsGetHostName()
        {
            string hostName = Unix.NativeMethods.GetFullyQualifiedName();
            if (string.IsNullOrEmpty(hostName))
            {
                int lastError = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Unix.NonWindowsHostName error: " + lastError);
            }
            return hostName;
        }

        internal static bool NonWindowsIsFile(string path)
        {
            return Unix.NativeMethods.IsFile(path);
        }

        internal static bool NonWindowsIsDirectory(string path)
        {
            return Unix.NativeMethods.IsDirectory(path);
        }

        internal static bool NonWindowsIsExecutable(string path)
        {
            return Unix.IsExecutable(path);
        }

        internal static uint NonWindowsGetThreadId()
        {
            // TODO:PSL clean this up
            return 0;
        }

        /// <summary>
        /// This models the native call CommandLineToArgvW in managed code.
        /// </summary>
        public static string[] CommandLineToArgv(string command)
        {
            StringBuilder arguments  = new StringBuilder();
            int len                  = command.Length;
            IList<string> returnList = new List<string>();
            bool inquote             = false;
            char current             = '\0';

            for (int argIndex = 0; argIndex < len; argIndex++)
            {
                current = command[argIndex];

                if (current.Equals('"'))
                {
                    // Treat anything in quotes as a single argument
                    // Because C# treats quotes differently than C++, instead of counting the slashes
                    // it makes more sense to count the quotes.
                    inquote = !inquote;
                    arguments.Append('"');
                    continue;
                }

                // If we're inside a quote, add the current character and cycle through
                if (inquote && !current.Equals('"'))
                {
                    arguments.Append(current.ToString());
                    continue;
                }

                if (current.Equals("\\"))
                {
                    arguments.Append("\\");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(current.ToString()))
                {
                    if (arguments.Length > 0)
                    {
                        returnList.Add(arguments.ToString());
                        arguments.Clear();
                    }
                    continue;
                }

                // All other exclusion scenarios being exhausted, append the current character.
                arguments.Append(current.ToString());
            }

            // add the final object to the arguments list
            returnList.Add(arguments.ToString());
            arguments.Clear();

            return returnList.ToArray();
        }
    }

    internal static class Unix
    {
        private static string _userName;
        public static string UserName
        {
            get
            {
                if (string.IsNullOrEmpty(_userName))
                {
                    _userName = NativeMethods.GetUserName();
                    if (string.IsNullOrEmpty(_userName))
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        throw new InvalidOperationException("Unix.UserName error: " + lastError);
                    }
                }
                return _userName;
            }
        }

        public static string TemporaryDirectory
        {
            get
            {
                // POSIX temporary directory environment variables
                string[] environmentVariables = { "TMPDIR", "TMP", "TEMP", "TEMPDIR" };
                string dir = string.Empty;
                foreach (string s in environmentVariables)
                {
                    dir = System.Environment.GetEnvironmentVariable(s);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        return dir;
                    }
                }
                return "/tmp";
            }
        }

        #if CORECLR
        public static string GetFolderPath(SpecialFolder folder)
        {
            string s = null;
            switch (folder)
            {
                case SpecialFolder.ProgramFiles:
                    s = "/bin";
                    break;
                case SpecialFolder.ProgramFilesX86:
                    s = "/usr/bin";
                    break;
                case SpecialFolder.System:
                    s = "/sbin";
                    break;
                case SpecialFolder.SystemX86:
                    s = "/sbin";
                    break;
                case SpecialFolder.Personal:
                    s = System.Environment.GetEnvironmentVariable("HOME");
                    break;
                case SpecialFolder.LocalApplicationData:
                    s = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("HOME"), ".config");
                    if (!System.IO.Directory.Exists(s))
                    {
                        System.IO.Directory.CreateDirectory(s);
                    }
                    break;
                default:
                    throw new NotSupportedException();
            }
            return s;
        }
        #endif

        public static bool IsHardLink(ref IntPtr handle)
        {
            // TODO:PSL implement using fstat to query inode refcount to see if it is a hard link
            return false;
        }


        public static bool IsHardLink(FileSystemInfo fs)
        {
            if (!fs.Exists || (fs.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
            {
                return false;
            }

            int count;
            string filePath = fs.FullName;
            int ret = NativeMethods.GetLinkCount(filePath, out count);
            if (ret == 1)
            {
                return count > 1;
            }
            else
            {
                int lastError = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Unix.IsHardLink error: " + lastError);
            }

        }

        public static bool IsSymLink(FileSystemInfo fs)
        {
            return NativeMethods.IsSymLink(fs.FullName);
        }

        public static bool IsExecutable(string filePath)
        {
            return NativeMethods.IsExecutable(filePath);
        }

        public static void SetDate(SetDateInfoInternal info)
        {
            int ret = NativeMethods.SetDate(info);
            if (ret == -1)
            {
                int lastError = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Unix.NonWindowsSetDate error: " + lastError);
            }
        }

        public static bool CreateSymbolicLink(string path, string strTargetPath)
        {
            int ret = NativeMethods.CreateSymLink(path, strTargetPath);
            return ret == 1 ? true : false;
        }

        public static bool CreateHardLink(string path, string strTargetPath)
        {
            int ret = NativeMethods.CreateHardLink(path, strTargetPath);
            return ret == 1 ? true : false;
        }

        public static string FollowSymLink(string path)
        {
            return NativeMethods.FollowSymLink(path);
        }

        public static string GetUserFromPid(int pid)
        {
            return NativeMethods.GetUserFromPid(pid);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal class SetDateInfoInternal
        {
            public int Year;
            public int Month;
            public int Day;
            public int Hour;
            public int Minute;
            public int Second;
            public int Millisecond;
            public int DST;

            public SetDateInfoInternal(DateTime d)
            {
                Year = d.Year;
                Month = d.Month;
                Day = d.Day;
                Hour = d.Hour;
                Minute = d.Minute;
                Second = d.Second;
                Millisecond = d.Millisecond;
                DST = d.IsDaylightSavingTime() ? 1 : 0;
            }

            public override string ToString()
            {
                string ret = String.Format("Year = {0}; Month = {1}; Day = {2}; Hour = {3}; Minute = {4}; Second = {5}; Millisec = {6}; DST = {7}", Year, Month, Day, Hour, Minute, Second, Millisecond, DST);
                return ret;
            }
        }

        internal static class NativeMethods
        {
            const string psLib = "libpsl-native";

            // Ansi is a misnomer, it is hardcoded to UTF-8 on Linux and OS X

            // C bools are 1 byte and so must be marshaled as I1

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            internal static extern string GetUserName();

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            internal static extern int GetLinkCount([MarshalAs(UnmanagedType.LPStr)]string filePath, out int linkCount);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool IsSymLink([MarshalAs(UnmanagedType.LPStr)]string filePath);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool IsExecutable([MarshalAs(UnmanagedType.LPStr)]string filePath);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            internal static extern string GetFullyQualifiedName();

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            internal static extern int SetDate(SetDateInfoInternal info);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            internal static extern int CreateSymLink([MarshalAs(UnmanagedType.LPStr)]string filePath,
                                                     [MarshalAs(UnmanagedType.LPStr)]string target);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            internal static extern int CreateHardLink([MarshalAs(UnmanagedType.LPStr)]string filePath,
                                                      [MarshalAs(UnmanagedType.LPStr)]string target);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            internal static extern string FollowSymLink([MarshalAs(UnmanagedType.LPStr)]string filePath);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            internal static extern string GetUserFromPid(int pid);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool IsFile([MarshalAs(UnmanagedType.LPStr)]string filePath);

            [DllImport(psLib, CharSet = CharSet.Ansi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool IsDirectory([MarshalAs(UnmanagedType.LPStr)]string filePath);

        }
    }

} // namespace System.Management.Automation
