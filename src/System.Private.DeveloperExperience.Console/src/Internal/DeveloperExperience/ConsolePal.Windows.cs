// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.DeveloperExperience
{
    internal static class ConsolePal
    {
        private static IntPtr s_consoleErrorHandle = Interop.mincore.GetStdHandle(Interop.mincore.HandleTypes.STD_ERROR_HANDLE);

        internal unsafe static void WriteError(string errorMessage)
        {
            if (s_consoleErrorHandle == new IntPtr(Interop.mincore.HandleTypes.INVALID_HANDLE_VALUE))
            {
                // ensure we have a valid handle before writing to it
                return; 
            }

            fixed (char *pBuffer = errorMessage)
            {
                int numberOfCharsWritten;
                Interop.mincore.WriteConsole(s_consoleErrorHandle, (byte*)pBuffer, errorMessage.Length, out numberOfCharsWritten, IntPtr.Zero);
            }

            // Write new line
            fixed (char* pBuffer = "\r\n")
            {
                int numberOfCharsWritten;
                Interop.mincore.WriteConsole(s_consoleErrorHandle, (byte*)pBuffer, 2, out numberOfCharsWritten, IntPtr.Zero);
            }
        }
    }
}
