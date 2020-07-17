// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
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
            StringBuilder buffer = new StringBuilder(Interop.Kernel32.MAX_PATH);
            while (true)
            {
                int size = Interop.mincore.GetModuleFileName(IntPtr.Zero, buffer, buffer.Capacity);
                if (size == 0)
                {
                    throw Win32Marshal.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
                }

                if (Marshal.GetLastWin32Error() == Interop.mincore.ERROR_INSUFFICIENT_BUFFER)
                {
                    // Enlarge the buffer and try again.
                    buffer.EnsureCapacity(buffer.Capacity * 2);
                    continue;
                }

                // Return path to the executable image including the terminating slash
                string fileName = buffer.ToString();
                return fileName.Substring(0, fileName.LastIndexOf('\\') + 1);
            }
        }
    }
}
