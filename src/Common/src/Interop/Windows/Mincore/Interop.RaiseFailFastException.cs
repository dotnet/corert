// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
#pragma warning disable 649
    internal unsafe struct EXCEPTION_RECORD
    {
        internal uint ExceptionCode;
        internal uint ExceptionFlags;
        internal IntPtr ExceptionRecord;
        internal IntPtr ExceptionAddress;
        internal uint NumberParameters;
#if BIT64
        internal fixed ulong ExceptionInformation[15];
#else
        internal fixed uint ExceptionInformation[15];
#endif
    }
#pragma warning restore 649

    internal partial class mincore
    {
        internal const uint EXCEPTION_NONCONTINUABLE = 0x1;

        internal const uint FAIL_FAST_GENERATE_EXCEPTION_ADDRESS = 0x1;

        //
        // Wrapper for calling RaiseFailFastException
        //
        internal static unsafe void RaiseFailFastException(uint faultCode, IntPtr pExAddress, IntPtr pExContext)
        {
            EXCEPTION_RECORD exceptionRecord;
            exceptionRecord.ExceptionCode = faultCode;
            exceptionRecord.ExceptionFlags = EXCEPTION_NONCONTINUABLE;
            exceptionRecord.ExceptionRecord = IntPtr.Zero;
            exceptionRecord.ExceptionAddress = pExAddress;
            exceptionRecord.NumberParameters = 0;
            // don't care about exceptionRecord.ExceptionInformation as we set exceptionRecord.NumberParameters to zero

            RaiseFailFastException(
                &exceptionRecord,
                pExContext,
                pExAddress == IntPtr.Zero ? FAIL_FAST_GENERATE_EXCEPTION_ADDRESS : 0);
        }

        [DllImport("api-ms-win-core-kernel32-legacy-l1-1-0.dll", EntryPoint = "RaiseFailFastException")]
        private extern static unsafe void RaiseFailFastException(
            EXCEPTION_RECORD* pExceptionRecord,
            IntPtr pContextRecord,
            uint dwFlags);
    }
}
