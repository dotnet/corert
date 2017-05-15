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
        SOk = 0x0u,
        FailFastGenerateExceptionAddress = 0x1u,
        ExceptionNonContinuable = 0x1u,
        CreateMutexInitialOwner = 0x1u,
        CreateEventManualReset = 0x1u,
        MutexModifyState = 0x1u,
        CreateEventInitialSet = 0x2u,
        SemaphoreModifyState = 0x2u,
        EventModifyState = 0x2u,
        DuplicateSameAccess = 0x2u,
        FileTypeChar = 0x2u,
        CreateSuspended = 0x4u,
        WaitAbandoned0 = 0x80u,
        WaitTimeout = 0x102u,
        MaxPath = 0x104u,
        StackSizeParamIsAReservation = 0x10000u,
        Synchronize = 0x100000u,
        MaximumAllowed = 0x02000000u,
        EFail = 0x80004005u,
        CoENotInitialized = 0x800401F0u,
        WaitFailed = 0xFFFFFFFFu,
    }

    // MCG doesn't currently support constants that are not uint.
    internal static IntPtr InvalidHandleValue => new IntPtr(-1);

    internal enum _APTTYPE : uint
    {
        APTTYPE_STA = 0x0u,
        APTTYPE_MTA = 0x1u,
        APTTYPE_NA = 0x2u,
        APTTYPE_MAINSTA = 0x3u,
        APTTYPE_CURRENT = 0xFFFFFFFFu,
    }

    internal enum _APTTYPEQUALIFIER : uint
    {
        APTTYPEQUALIFIER_NONE = 0x0u,
        APTTYPEQUALIFIER_IMPLICIT_MTA = 0x1u,
        APTTYPEQUALIFIER_NA_ON_MTA = 0x2u,
        APTTYPEQUALIFIER_NA_ON_STA = 0x3u,
        APTTYPEQUALIFIER_NA_ON_IMPLICIT_MTA = 0x4u,
        APTTYPEQUALIFIER_NA_ON_MAINSTA = 0x5u,
        APTTYPEQUALIFIER_APPLICATION_STA = 0x6u,
    }

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
        [DllImport("api-ms-win-core-com-l1-1-0.dll")]
        internal extern static int CoGetApartmentType(out _APTTYPE pAptType, out _APTTYPEQUALIFIER pAptQualifier);

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

namespace System.Runtime.InteropServices
{
    internal class Marshal
    {
        public static int GetLastWin32Error()
        {
            return PInvokeMarshal.GetLastWin32Error();
        }

        public static unsafe IntPtr AllocHGlobal(IntPtr cb)
        {
            return PInvokeMarshal.AllocHGlobal(cb);
        }

        public static unsafe IntPtr AllocHGlobal(int cb)
        {
            return PInvokeMarshal.AllocHGlobal(cb);
        }

        public static void FreeHGlobal(IntPtr hglobal)
        {
            PInvokeMarshal.FreeHGlobal(hglobal);
        }

        public static unsafe IntPtr AllocCoTaskMem(int cb)
        {
            return PInvokeMarshal.AllocCoTaskMem(cb);
        }

        public static void FreeCoTaskMem(IntPtr ptr)
        {
            PInvokeMarshal.FreeCoTaskMem(ptr);
        }

        public static void Copy(IntPtr source, byte[] destination, int startIndex, int length)
        {
            InteropExtensions.CopyToManaged(source, destination, startIndex, length);
        }

#if PLATFORM_UNIX
        public static unsafe String PtrToStringAnsi(IntPtr ptr)
        {
            return PInvokeMarshal.PtrToStringAnsi(ptr);
        }
#endif
    }
}
