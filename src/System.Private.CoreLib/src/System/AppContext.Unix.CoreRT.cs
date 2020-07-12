// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System
{
    public static partial class AppContext
    {
        /// <summary>
        /// Return the directory of the executable image for the current process
        /// as the default value for AppContext.BaseDirectory
        /// </summary>
        private static string GetBaseDirectoryCore()
        {
            // Start with a relatively small buffer
            int currentSize = 256;
            for (;;)
            {
                char[] buffer = ArrayPool<char>.Shared.Rent(currentSize);

                // Get full path to the executable image
                int actualSize = Interop.Sys.GetExecutableAbsolutePath(buffer, buffer.Length);

                if (actualSize < 0)
                {
                    // The call to GetExecutableAbsolutePath function failed.
                    Interop.ErrorInfo error = Interop.Sys.GetLastErrorInfo();
                    ArrayPool<char>.Shared.Return(buffer);
                    throw Interop.GetExceptionForIoErrno(error);
                }

                Debug.Assert(actualSize > 0);
                if (actualSize <= buffer.Length)
                {
                    string fileName = new string(buffer, 0, actualSize);
                    ArrayPool<char>.Shared.Return(buffer);

                    // Return path to the executable image including the terminating slash
                    return fileName.Substring(0, fileName.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                }

                ArrayPool<char>.Shared.Return(buffer);
                currentSize = actualSize;
            }
        }
    }
}
