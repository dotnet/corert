// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
                Interop.Sys.Write2(Interop.Sys.FileDescriptors.STDERR_FILENO, pBuffer, errorMessageAsBytes.Length);
            }

            // Write new line
            byte newLine = (byte) '\n';
            Interop.Sys.Write2(Interop.Sys.FileDescriptors.STDERR_FILENO, &newLine, 1);

        }
    }
}
