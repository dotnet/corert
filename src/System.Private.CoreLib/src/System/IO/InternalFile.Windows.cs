// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace System.IO
{
    internal static partial class InternalFile
    {
        internal static bool InternalExists(String path)
        {
            Interop.Kernel32.WIN32_FILE_ATTRIBUTE_DATA data = new Interop.Kernel32.WIN32_FILE_ATTRIBUTE_DATA();
            int errorCode = FillAttributeInfo(path, ref data, false, true);

            return (errorCode == 0) && (data.fileAttributes != -1)
                    && ((data.fileAttributes & Interop.Kernel32.FileAttributes.FILE_ATTRIBUTE_DIRECTORY) == 0);
        }

        // Returns 0 on success, otherwise a Win32 error code.  Note that
        // classes should use -1 as the uninitialized state for dataInitialized.
        internal static int FillAttributeInfo(String path, ref Interop.Kernel32.WIN32_FILE_ATTRIBUTE_DATA data, bool tryagain, bool returnErrorOnNotFound)
        {
            int errorCode = 0;
            if (tryagain) // someone has a handle to the file open, or other error
            {
                Interop.Kernel32.WIN32_FIND_DATA findData;
                findData = new Interop.Kernel32.WIN32_FIND_DATA();

                // Remove trailing slash since this can cause grief to FindFirstFile. You will get an invalid argument error
                String tempPath = path.TrimEnd(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

                // For floppy drives, normally the OS will pop up a dialog saying
                // there is no disk in drive A:, please insert one.  We don't want that.
                // SetThreadErrorMode will let us disable this, but we should set the error
                // mode back, since this may have wide-ranging effects.
                uint oldMode;
                bool setThreadErrorModeSuccess = Interop.Kernel32.SetThreadErrorMode(Interop.Kernel32.SEM_FAILCRITICALERRORS, out oldMode);
                try
                {
                    bool error = false;
                    SafeFindHandle handle = Interop.Kernel32.FindFirstFile(tempPath, ref findData);
                    try
                    {
                        if (handle.IsInvalid)
                        {
                            error = true;
                            errorCode = Marshal.GetLastWin32Error();

                            if (errorCode == Interop.Errors.ERROR_FILE_NOT_FOUND ||
                                errorCode == Interop.Errors.ERROR_PATH_NOT_FOUND ||
                                errorCode == Interop.Errors.ERROR_NOT_READY)  // floppy device not ready
                            {
                                if (!returnErrorOnNotFound)
                                {
                                    // Return default value for backward compatibility
                                    errorCode = 0;
                                    data.fileAttributes = -1;
                                }
                            }
                            return errorCode;
                        }
                    }
                    finally
                    {
                        // Close the Win32 handle
                        try
                        {
                            handle.Dispose();
                        }
                        catch
                        {
                            // if we're already returning an error, don't throw another one. 
                            if (!error)
                            {
                                throw Win32Marshal.GetExceptionForLastWin32Error();
                            }
                        }
                    }
                }
                finally
                {
                    if (setThreadErrorModeSuccess)
                        Interop.Kernel32.SetThreadErrorMode(oldMode, out oldMode);
                }

                // Copy the information to data
                data.PopulateFrom(ref findData);
            }
            else
            {
                // For floppy drives, normally the OS will pop up a dialog saying
                // there is no disk in drive A:, please insert one.  We don't want that.
                // SetThreadErrorMode will let us disable this, but we should set the error
                // mode back, since this may have wide-ranging effects.
                bool success = false;
                uint oldMode;
                bool setThreadErrorModeSuccess = Interop.Kernel32.SetThreadErrorMode(Interop.Kernel32.SEM_FAILCRITICALERRORS, out oldMode);
                try
                {
                    success = Interop.Kernel32.GetFileAttributesEx(path, Interop.Kernel32.GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, ref data);
                }
                finally
                {
                    if (setThreadErrorModeSuccess)
                        Interop.Kernel32.SetThreadErrorMode(oldMode, out oldMode);
                }

                if (!success)
                {
                    errorCode = Marshal.GetLastWin32Error();
                    if (errorCode != Interop.Errors.ERROR_FILE_NOT_FOUND &&
                        errorCode != Interop.Errors.ERROR_PATH_NOT_FOUND &&
                        errorCode != Interop.Errors.ERROR_NOT_READY)  // floppy device not ready
                    {
                        // In case someone latched onto the file. Take the perf hit only for failure
                        return FillAttributeInfo(path, ref data, true, returnErrorOnNotFound);
                    }
                    else
                    {
                        if (!returnErrorOnNotFound)
                        {
                            // Return default value for backward compatibility
                            errorCode = 0;
                            data.fileAttributes = -1;
                        }
                    }
                }
            }

            return errorCode;
        }
    }
}
