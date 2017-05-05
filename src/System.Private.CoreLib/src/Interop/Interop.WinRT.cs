// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal partial class WinRT
    {
        private const string CORE_WINRT = "api-ms-win-core-winrt-l1-1-0.dll";

        private const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

        private enum RO_INIT_TYPE : uint
        {
            RO_INIT_MULTITHREADED = 1
        }

        internal static void RoInitialize()
        {
            int hr = RoInitialize((uint)RO_INIT_TYPE.RO_INIT_MULTITHREADED);

            // RPC_E_CHANGED_MODE indicates this thread has been already initialized with a different
            // concurrency model. That is fine; we just need to skip the RoUninitialize call on shutdown.
            if ((hr < 0) && (hr != RPC_E_CHANGED_MODE))
            {
                throw new OutOfMemoryException();
            }
        }

        [DllImport(CORE_WINRT, ExactSpelling = true)]
        private static extern int RoInitialize(uint initType);
    }
}
