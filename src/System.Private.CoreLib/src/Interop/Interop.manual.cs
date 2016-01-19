// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
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
        FileTypeChar = 0x2u,
        WaitAbandoned0 = 0x80u,
        WaitTimeout = 0x102u,
        MaxPath = 0x104u,
        Synchronize = 0x100000u,
        MutexAllAccess = 0x1F0001u,
        EventAllAccess = 0x1F0003u,
        EFail = 0x80004005u,
        CoENotInitialized = 0x800401F0u,
        WaitFailed = 0xFFFFFFFFu,
    }

    // MCG doesn't currently support constants that are not uint.
    internal static IntPtr InvalidHandleValue = new IntPtr(-1);

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

    internal struct _FILETIME
    {
        internal uint dwLowDateTime;
        internal uint dwHighDateTime;
    }

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

    internal unsafe struct _CONTEXT
    {
        internal uint ContextFlags;
        internal uint Dr0;
        internal uint Dr1;
        internal uint Dr2;
        internal uint Dr3;
        internal uint Dr6;
        internal uint Dr7;
        internal _FLOATING_SAVE_AREA FloatSave;
        internal uint SegGs;
        internal uint SegFs;
        internal uint SegEs;
        internal uint SegDs;
        internal uint Edi;
        internal uint Esi;
        internal uint Ebx;
        internal uint Edx;
        internal uint Ecx;
        internal uint Eax;
        internal uint Ebp;
#if AMD64
        internal uint Rip;
#elif ARM
        internal uint Pc;
#else
        internal uint Eip;
#endif
        internal uint SegCs;
        internal uint EFlags;
        internal uint Esp;
        internal uint SegSs;
        internal fixed byte ExtendedRegisters[512];
    }

    internal unsafe struct _FLOATING_SAVE_AREA
    {
        internal uint ControlWord;
        internal uint StatusWord;
        internal uint TagWord;
        internal uint ErrorOffset;
        internal uint ErrorSelector;
        internal uint DataOffset;
        internal uint DataSelector;
        internal fixed byte RegisterArea[80];
        internal uint Cr0NpxState;
    }

#pragma warning restore 649
    internal partial class mincore
    {
        [DllImport("api-ms-win-core-handle-l1-1-0.dll", EntryPoint = "CloseHandle", CharSet = CharSet.Unicode)]
        internal extern static bool CloseHandle(IntPtr hObject);

        [DllImport("api-ms-win-core-com-l1-1-0.dll")]
        internal extern static int CoGetApartmentType(out _APTTYPE pAptType, out _APTTYPEQUALIFIER pAptQualifier);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateEventExW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr CreateEventEx(IntPtr lpEventAttributes, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateMutexExW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr CreateMutexEx(IntPtr lpMutexAttributes, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateSemaphoreExW", CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateSemaphoreEx(IntPtr lpSemaphoreAttributes, int lInitialCount, int lMaximumCount, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-processthreads-l1-1-0.dll")]
        internal extern static uint GetCurrentThreadId();

        [DllImport("api-ms-win-core-errorhandling-l1-1-0.dll")]
        internal extern static uint GetLastError();

        [DllImport("api-ms-win-core-heap-l1-1-0.dll")]
        internal extern static IntPtr GetProcessHeap();

        [DllImport("api-ms-win-core-sysinfo-l1-1-0.dll")]
        internal extern static void GetSystemTimeAsFileTime(out _FILETIME lpSystemTimeAsFileTime);

        [DllImport("api-ms-win-core-sysinfo-l1-1-0.dll")]
        internal extern static ulong GetTickCount64();

        [DllImport("api-ms-win-core-heap-l1-1-0.dll")]
        internal extern static IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwBytes);

        [DllImport("api-ms-win-core-debug-l1-1-0.dll", EntryPoint = "IsDebuggerPresent", CharSet = CharSet.Unicode)]
        internal extern static bool IsDebuggerPresent();

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenEventW", CharSet = CharSet.Unicode)]
        private extern static IntPtr OpenEvent(uint dwDesiredAccess, int bInheritHandle, string lpName);

        internal static IntPtr OpenEvent(uint dwDesiredAccess, bool bInheritHandle, string lpName)
        {
            return OpenEvent(dwDesiredAccess, bInheritHandle ? 1 : 0, lpName);
        }

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenMutexW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr OpenMutex(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenSemaphoreW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr OpenSemaphore(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("api-ms-win-core-debug-l1-1-0.dll", EntryPoint = "OutputDebugStringW", CharSet = CharSet.Unicode)]
        internal extern static void OutputDebugString(string lpOutputString);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "ReleaseMutex", CharSet = CharSet.Unicode)]
        internal extern static bool ReleaseMutex(IntPtr hMutex);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "ReleaseSemaphore", CharSet = CharSet.Unicode)]
        internal extern static bool ReleaseSemaphore(IntPtr hSemaphore, int lReleaseCount, out int lpPreviousCount);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "ResetEvent", CharSet = CharSet.Unicode)]
        internal extern static bool ResetEvent(IntPtr hEvent);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "SetEvent", CharSet = CharSet.Unicode)]
        internal extern static bool SetEvent(IntPtr hEvent);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll")]
        internal extern static uint WaitForMultipleObjectsEx(uint nCount, IntPtr lpHandles, bool bWaitAll, uint dwMilliseconds, bool bAlertable);

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
                                null,
                                (uint)Constants.FailFastGenerateExceptionAddress);
        }

        //
        // Wrapper for calling RaiseFailFastException
        //
        internal static unsafe void RaiseFailFastException(uint faultCode, IntPtr pExContext)
        {
            long ctxIP = 0;
            Interop._CONTEXT* pContext = (Interop._CONTEXT*)pExContext.ToPointer();
            if (pExContext != IntPtr.Zero)
            {
#if AMD64
                ctxIP = (long)pContext->Rip;
#elif ARM
                ctxIP = (long)pContext->Pc;
#elif X86
                ctxIP = (long)pContext->Eip;
#else
                System.Diagnostics.Debug.Assert(false, "Unsupported architecture");
#endif
            }

            _EXCEPTION_RECORD exceptionRecord;
            exceptionRecord.ExceptionCode = faultCode;
            exceptionRecord.ExceptionFlags = (uint)Constants.ExceptionNonContinuable;
            exceptionRecord.ExceptionRecord = IntPtr.Zero;
            exceptionRecord.ExceptionAddress = new IntPtr(ctxIP);  // use the IP set in context record as the exception address
            exceptionRecord.NumberParameters = 0;
            // don't care about exceptionRecord.ExceptionInformation as we set exceptionRecord.NumberParameters to zero

            PInvoke_RaiseFailFastException(
                &exceptionRecord,
                pContext,
                ctxIP == 0 ? (uint)Constants.FailFastGenerateExceptionAddress : 0);
        }

        [DllImport("api-ms-win-core-kernel32-legacy-l1-1-0.dll", EntryPoint = "RaiseFailFastException")]
        private extern static unsafe void PInvoke_RaiseFailFastException(
                    _EXCEPTION_RECORD* pExceptionRecord,
                    _CONTEXT* pContextRecord,
                    uint dwFlags);

        private readonly static System.Threading.WaitHandle s_sleepHandle = new System.Threading.ManualResetEvent(false);
        static internal void Sleep(uint milliseconds)
        {
            if (milliseconds == 0)
                System.Threading.SpinWait.Yield();
            else
                s_sleepHandle.WaitOne((int)milliseconds);
        }
    }
    internal unsafe partial class WinRT
    {
        internal const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

        internal enum RO_INIT_TYPE : uint
        {
            RO_INIT_MULTITHREADED = 1
        }

        static internal unsafe void RoInitialize(RO_INIT_TYPE initType)
        {
            int hr = RoInitialize((uint)initType);

            // RPC_E_CHANGED_MODE can occur if someone else has already initialized the thread.  This is
            // legal - we just need to make sure not to uninitialize the thread later in this case.
            if (hr < 0 && hr != RPC_E_CHANGED_MODE)
            {
                throw new Exception();
            }
        }

#if TARGET_CORE_API_SET
        [DllImport(Interop.CORE_WINRT)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        static internal extern unsafe int RoInitialize(uint initType);
#else
        // Right now do what is necessary to ensure that the tools still work on pre-Win8 platforms
        static internal unsafe int RoInitialize(uint initType)
        {
            // RoInitialize gets called on startup so it can't throw a not implemented exception
            return 0;
        }
#endif
    }
}
