// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
** 
** 
**
**
** Purpose: Centralized error methods for the IO package.  
** Mostly useful for translating Win32 HRESULTs into meaningful
** error strings & exceptions.
**
**
===========================================================*/

using System;
using System.Runtime.InteropServices;
using Win32Native = Microsoft.Win32.Win32Native;
using System.Text;
using System.Globalization;
using System.Security;
using System.Diagnostics.Contracts;

namespace System.IO {
    [Pure]
    internal static class __Error
    {
        // Given a possible fully qualified path, ensure that we have path
        // discovery permission to that path.  If we do not, return just the 
        // file name.  If we know it is a directory, then don't return the 
        // directory name.
        internal static String GetDisplayablePath(String path, bool isInvalidPath)
        {
            
            if (String.IsNullOrEmpty(path))
                return String.Empty;

            // Is it a fully qualified path?
            bool isFullyQualified = false;
            if (path.Length < 2)
                return path;
            if (Path.IsDirectorySeparator(path[0]) && Path.IsDirectorySeparator(path[1]))
                isFullyQualified = true;
            else if (path[1] == Path.VolumeSeparatorChar) {
                isFullyQualified = true;
            }

            if (!isFullyQualified && !isInvalidPath)
                return path;

            bool safeToReturn = false;
            try {
                if (!isInvalidPath) {
                    safeToReturn = true;
                }
            }
            catch (SecurityException) {
            }
            catch (ArgumentException) {
                // ? and * characters cause ArgumentException to be thrown from HasIllegalCharacters
                // inside FileIOPermission.AddPathList
            }
            catch (NotSupportedException) {
                // paths like "!Bogus\\dir:with/junk_.in it" can cause NotSupportedException to be thrown
                // from Security.Util.StringExpressionSet.CanonicalizePath when ':' is found in the path
                // beyond string index position 1.  
            }
            
            if (!safeToReturn) {
                if (Path.IsDirectorySeparator(path[path.Length - 1]))
                    path = SR.IO_NoPermissionToDirectoryName;
                else
                    path = Path.GetFileName(path);
            }

            return path;
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        internal static void WinIOError() {
            int errorCode = Microsoft.Win32.UnsafeNativeMethods.GetLastError();
            WinIOError(errorCode, String.Empty);
        }
    
        // After calling GetLastWin32Error(), it clears the last error field,
        // so you must save the HResult and pass it to this method.  This method
        // will determine the appropriate exception to throw dependent on your 
        // error, and depending on the error, insert a string into the message 
        // gotten from the ResourceManager.
        internal static void WinIOError(int errorCode, String maybeFullPath) {
            // This doesn't have to be perfect, but is a perf optimization.
            bool isInvalidPath = errorCode == Win32Native.ERROR_INVALID_NAME || errorCode == Win32Native.ERROR_BAD_PATHNAME;
            String str = GetDisplayablePath(maybeFullPath, isInvalidPath);

            switch (errorCode) {
            case Win32Native.ERROR_FILE_NOT_FOUND:
                if (str.Length == 0)
                    throw new FileNotFoundException(SR.IO_FileNotFound);
                else
                    throw new FileNotFoundException(String.Format(SR.IO_FileNotFound_FileName, str), str);
                
            case Win32Native.ERROR_PATH_NOT_FOUND:
                if (str.Length == 0)
                    throw new DirectoryNotFoundException(SR.IO_PathNotFound_NoPathName);
                else
                    throw new DirectoryNotFoundException(String.Format(SR.IO_PathNotFound_Path, str));

            case Win32Native.ERROR_ACCESS_DENIED:
                if (str.Length == 0)
                    throw new UnauthorizedAccessException(SR.UnauthorizedAccess_IODenied_NoPathName);
                else
                    throw new UnauthorizedAccessException(String.Format(SR.UnauthorizedAccess_IODenied_Path, str));

            case Win32Native.ERROR_ALREADY_EXISTS:
                if (str.Length == 0)
                    goto default;
                throw new IOException(String.Format(SR.IO_AlreadyExists_Name, str), Win32Native.MakeHRFromErrorCode(errorCode));

            case Win32Native.ERROR_FILENAME_EXCED_RANGE:
                throw new PathTooLongException(SR.IO_PathTooLong);

            case Win32Native.ERROR_INVALID_DRIVE:
                throw new DriveNotFoundException(String.Format(SR.IO_DriveNotFound_Drive, str));

            case Win32Native.ERROR_INVALID_PARAMETER:
                throw new IOException(Win32Native.GetMessage(errorCode), Win32Native.MakeHRFromErrorCode(errorCode));

            case Win32Native.ERROR_SHARING_VIOLATION:
                if (str.Length == 0)
                    throw new IOException(SR.IO_SharingViolation_NoFileName, Win32Native.MakeHRFromErrorCode(errorCode));
                else
                    throw new IOException(String.Format(SR.IO_SharingViolation_File, str), Win32Native.MakeHRFromErrorCode(errorCode));

            case Win32Native.ERROR_FILE_EXISTS:
                if (str.Length == 0)
                    goto default;
                throw new IOException(String.Format(SR.IO_FileExists_Name, str), Win32Native.MakeHRFromErrorCode(errorCode));

            case Win32Native.ERROR_OPERATION_ABORTED:
                throw new OperationCanceledException();

            default:
                throw new IOException(Win32Native.GetMessage(errorCode), Win32Native.MakeHRFromErrorCode(errorCode));
            }
        }
    
        internal static void WriteNotSupported() {
            throw new NotSupportedException(SR.NotSupported_UnwritableStream);
        }

        internal static void WriterClosed() {
            throw new ObjectDisposedException(null, SR.ObjectDisposed_WriterClosed);
        }

        // From WinError.h
        internal const int ERROR_FILE_NOT_FOUND = Win32Native.ERROR_FILE_NOT_FOUND;
        internal const int ERROR_PATH_NOT_FOUND = Win32Native.ERROR_PATH_NOT_FOUND;
        internal const int ERROR_ACCESS_DENIED  = Win32Native.ERROR_ACCESS_DENIED;
        internal const int ERROR_INVALID_PARAMETER = Win32Native.ERROR_INVALID_PARAMETER;
    }
}
