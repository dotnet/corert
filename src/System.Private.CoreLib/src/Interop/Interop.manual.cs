// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal enum Constants : uint
    {
        WaitObject0 = 0x0u,
        FailFastGenerateExceptionAddress = 0x1u,
        ExceptionNonContinuable = 0x1u,
        DuplicateSameAccess = 0x2u,
        CreateSuspended = 0x4u,
        WaitAbandoned0 = 0x80u,
        WaitTimeout = 0x102u,
        StackSizeParamIsAReservation = 0x10000u,
        WaitFailed = 0xFFFFFFFFu,
    }

    internal static IntPtr InvalidHandleValue => new IntPtr(-1);

#pragma warning disable 649
    internal unsafe struct _EXCEPTION_RECORD
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
        [DllImport("api-ms-win-core-debug-l1-1-0.dll", EntryPoint = "IsDebuggerPresent", CharSet = CharSet.Unicode)]
        internal extern static bool IsDebuggerPresent();

        [DllImport("api-ms-win-core-debug-l1-1-0.dll", EntryPoint = "OutputDebugStringW", CharSet = CharSet.Unicode)]
        internal extern static void OutputDebugString(string lpOutputString);

        //
        // Wrapper for calling RaiseFailFastException
        //
        internal static unsafe void ExitProcess(uint exitCode)
        {
            _EXCEPTION_RECORD exceptionRecord;

            exceptionRecord.ExceptionCode = exitCode;
            exceptionRecord.ExceptionFlags = (uint)Constants.ExceptionNonContinuable;
            exceptionRecord.ExceptionRecord = IntPtr.Zero;
            exceptionRecord.ExceptionAddress = IntPtr.Zero;
            exceptionRecord.NumberParameters = 0;
            // don't care about exceptionRecord.ExceptionInformation as we set exceptionRecord.NumberParameters to zero

            PInvoke_RaiseFailFastException(
                &exceptionRecord,
                IntPtr.Zero,
                (uint)Constants.FailFastGenerateExceptionAddress);
        }

        //
        // Wrapper for calling RaiseFailFastException
        //
        internal static unsafe void RaiseFailFastException(uint faultCode, IntPtr pExAddress, IntPtr pExContext)
        {
            _EXCEPTION_RECORD exceptionRecord;
            exceptionRecord.ExceptionCode = faultCode;
            exceptionRecord.ExceptionFlags = (uint)Constants.ExceptionNonContinuable;
            exceptionRecord.ExceptionRecord = IntPtr.Zero;
            exceptionRecord.ExceptionAddress = pExAddress;
            exceptionRecord.NumberParameters = 0;
            // don't care about exceptionRecord.ExceptionInformation as we set exceptionRecord.NumberParameters to zero

            PInvoke_RaiseFailFastException(
                &exceptionRecord,
                pExContext,
                pExAddress == IntPtr.Zero ? (uint)Constants.FailFastGenerateExceptionAddress : 0);
        }

        [DllImport("api-ms-win-core-kernel32-legacy-l1-1-0.dll", EntryPoint = "RaiseFailFastException")]
        private extern static unsafe void PInvoke_RaiseFailFastException(
            _EXCEPTION_RECORD* pExceptionRecord,
            IntPtr pContextRecord,
            uint dwFlags);
    }
}
