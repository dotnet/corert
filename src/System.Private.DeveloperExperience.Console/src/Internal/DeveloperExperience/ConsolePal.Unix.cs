// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Internal.DeveloperExperience
{
    internal static class ConsolePal
    {
        internal unsafe static void WriteError(string errorMessage)
        {
            byte[] errorMessageAsBytes = Encoding.UTF8.GetBytes(errorMessage);
            fixed (byte* pBuffer = errorMessageAsBytes)
            {
                Interop.Sys.Write(Interop.Sys.FileDescriptors.STDERR_FILENO, pBuffer, errorMessageAsBytes.Length);
            }

            // Write new line
            byte newLine = (byte) '\n';
            Interop.Sys.Write(Interop.Sys.FileDescriptors.STDERR_FILENO, &newLine, 1);

        }
    }
}
