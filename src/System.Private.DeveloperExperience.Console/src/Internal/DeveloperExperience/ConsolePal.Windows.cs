// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Internal.DeveloperExperience
{
    internal static class ConsolePal
    {
        private static IntPtr s_stdErrorHandle = Interop.mincore.GetStdHandle(Interop.mincore.HandleTypes.STD_ERROR_HANDLE);

        internal unsafe static void WriteError(string errorMessage)
        {
            if (s_stdErrorHandle == new IntPtr(Interop.mincore.HandleTypes.INVALID_HANDLE_VALUE))
            {
                // ensure we have a valid handle before writing to it
                return; 
            }

            // Ensure that the encoding we use matches the current code page.
            int currentCodePage = (int)Interop.mincore.GetConsoleOutputCP();
            Encoding encoding;
            try
            {
                encoding = Encoding.GetEncoding(currentCodePage);
            }
            catch (Exception)
            {
                encoding = Encoding.UTF8;
            }

            byte[] errorMessageBytes = encoding.GetBytes(errorMessage + "\r\n");

            fixed (byte *pBuffer = errorMessageBytes)
            {
                int numberOfBytesWritten;
                Interop.mincore.WriteFile(s_stdErrorHandle, pBuffer, errorMessageBytes.Length, out numberOfBytesWritten, IntPtr.Zero);
            }
        }
    }
}
