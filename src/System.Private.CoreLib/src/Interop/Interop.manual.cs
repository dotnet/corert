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
        DuplicateSameAccess = 0x2u,
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal unsafe struct InlineArray_WCHAR_32
    {
        // Copies characters from this buffer, up to the first null or end of buffer, into a new string.
        internal string CopyToString()
        {
            fixed (char* ptr = _buffer)
            {
                char* end = ptr;
                char* limit = (ptr + 32);
                while (end < limit
                            && (*(end)) != 0)
                {
                    end = end + 1;
                }
                return new string(ptr, 0, ((int)(end - ptr)));
            }
        }

        internal char this[uint index]
        {
            get
            {
                if (index < 0
                            || index >= 32)
                    throw new IndexOutOfRangeException();
                fixed (char* pBuffer = _buffer)
                    return (pBuffer)[index];
            }
            set
            {
                if (index < 0
                            || index >= 32)
                    throw new IndexOutOfRangeException();
                fixed (char* pBuffer = _buffer)
                    (pBuffer)[index] = value;
            }
        }

        private fixed char _buffer[32];
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal unsafe struct InlineArray_WCHAR_128
    {
        // Copies characters from this buffer, up to the first null or end of buffer, into a new string.
        internal string CopyToString()
        {
            fixed (char* ptr = _buffer)
            {
                char* end = ptr;
                char* limit = (ptr + 128);
                while (end < limit
                            && (*(end)) != 0)
                {
                    end = end + 1;
                }
                return new string(ptr, 0, ((int)(end - ptr)));
            }
        }

        internal char this[uint index]
        {
            get
            {
                if (index < 0
                            || index >= 128)
                    throw new IndexOutOfRangeException();
                fixed (char* pBuffer = _buffer)
                    return (pBuffer)[index];
            }
            set
            {
                if (index < 0
                            || index >= 128)
                    throw new IndexOutOfRangeException();
                fixed (char* pBuffer = _buffer)
                    (pBuffer)[index] = value;
            }
        }

        private fixed char _buffer[128];
    }

#pragma warning disable 649

    internal struct _TIME_DYNAMIC_ZONE_INFORMATION
    {
        internal int Bias;
        internal InlineArray_WCHAR_32 StandardName;
        internal _SYSTEMTIME StandardDate;
        internal int StandardBias;
        internal InlineArray_WCHAR_32 DaylightName;
        internal _SYSTEMTIME DaylightDate;
        internal int DaylightBias;
        internal InlineArray_WCHAR_128 TimeZoneKeyName;
        internal byte DynamicDaylightTimeDisabled;
    }

    internal struct _SYSTEMTIME
    {
        internal ushort wYear;
        internal ushort wMonth;
        internal ushort wDayOfWeek;
        internal ushort wDay;
        internal ushort wHour;
        internal ushort wMinute;
        internal ushort wSecond;
        internal ushort wMilliseconds;
    }

    internal struct _TIME_ZONE_INFORMATION
    {
        internal int Bias;
        internal InlineArray_WCHAR_32 StandardName;
        internal _SYSTEMTIME StandardDate;
        internal int StandardBias;
        internal InlineArray_WCHAR_32 DaylightName;
        internal _SYSTEMTIME DaylightDate;
        internal int DaylightBias;
    }

    internal struct _FILETIME
    {
        internal uint dwLowDateTime;
        internal uint dwHighDateTime;
    }

    internal unsafe struct InlineArray_ULONG_PTR_15
    {
        internal UIntPtr this[uint index]
        {
            get
            {
                if (index < 0
                            || index >= 15)
                    throw new IndexOutOfRangeException();
                fixed (InlineArray_ULONG_PTR_15* pThis = &(this))
                    return ((UIntPtr*)pThis)[index];
            }
            set
            {
                if (index < 0
                            || index >= 15)
                    throw new IndexOutOfRangeException();
                fixed (InlineArray_ULONG_PTR_15* pThis = &(this))
                    ((UIntPtr*)pThis)[index] = value;
            }
        }
        internal const int Length = 15; private UIntPtr _elem_0; private UIntPtr _elem_1; private UIntPtr _elem_2; private UIntPtr _elem_3; private UIntPtr _elem_4; private UIntPtr _elem_5; private UIntPtr _elem_6; private UIntPtr _elem_7; private UIntPtr _elem_8; private UIntPtr _elem_9; private UIntPtr _elem_10; private UIntPtr _elem_11; private UIntPtr _elem_12; private UIntPtr _elem_13; private UIntPtr _elem_14;
    }

    internal struct _EXCEPTION_RECORD
    {
        internal uint ExceptionCode;
        internal uint ExceptionFlags;
        internal IntPtr ExceptionRecord;
        internal IntPtr ExceptionAddress;
        internal uint NumberParameters;
        internal InlineArray_ULONG_PTR_15 ExceptionInformation;
    }

    internal unsafe struct InlineArray_BYTE_512
    {
        internal byte this[uint index]
        {
            get
            {
                if (index < 0
                            || index >= 512)
                    throw new IndexOutOfRangeException();
                fixed (byte* pBuffer = _buffer)
                    return (pBuffer)[index];
            }
            set
            {
                if (index < 0
                            || index >= 512)
                    throw new IndexOutOfRangeException();
                fixed (byte* pBuffer = _buffer)
                    (pBuffer)[index] = value;
            }
        }

        private fixed byte _buffer[512];
    }

    internal struct _CONTEXT
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
        internal InlineArray_BYTE_512 ExtendedRegisters;
    }

    internal unsafe struct InlineArray_BYTE_80
    {
        internal byte this[uint index]
        {
            get
            {
                if (index < 0
                            || index >= 80)
                    throw new IndexOutOfRangeException();
                fixed (byte* pBuffer = _buffer)
                    return (pBuffer)[index];
            }
            set
            {
                if (index < 0
                            || index >= 80)
                    throw new IndexOutOfRangeException();
                fixed (byte* pBuffer = _buffer)
                    (pBuffer)[index] = value;
            }
        }

        private fixed byte _buffer[80];
    }

    internal struct _FLOATING_SAVE_AREA
    {
        internal uint ControlWord;
        internal uint StatusWord;
        internal uint TagWord;
        internal uint ErrorOffset;
        internal uint ErrorSelector;
        internal uint DataOffset;
        internal uint DataSelector;
        internal InlineArray_BYTE_80 RegisterArea;
        internal uint Cr0NpxState;
    }

    internal unsafe struct InlineArray_byte_8
    {
        internal byte this[uint index]
        {
            get
            {
                if (index < 0
                            || index >= 8)
                    throw new IndexOutOfRangeException();
                fixed (byte* pBuffer = _buffer)
                    return (pBuffer)[index];
            }
            set
            {
                if (index < 0
                            || index >= 8)
                    throw new IndexOutOfRangeException();
                fixed (byte* pBuffer = _buffer)
                    (pBuffer)[index] = value;
            }
        }

        private fixed byte _buffer[8];
    }

    internal struct _GUID
    {
        internal uint Data1;
        internal ushort Data2;
        internal ushort Data3;
        internal InlineArray_byte_8 Data4;
    }
#pragma warning restore 649
    internal partial class mincore
    {
        // Currently the generated GetCommandLine tries to call HeapFree which is incorrect for this API so to workaround it we
        // are making the call manually. It would be nice to have MCG understand how to free or not free memory from a given API 
        // but it currently doesn't and will likely not.
        internal unsafe static string GetCommandLine()
        {
            string retval;
            char* unsafe_retval;
            // Marshalling
            // Call to native method
            unsafe_retval = PInvoke_GetCommandLine();
            // Unmarshalling
            retval = (unsafe_retval != null) ? new string(unsafe_retval) : null;
            // Return
            return retval;
        }

        [DllImport("api-ms-win-core-processenvironment-l1-1-0.dll", EntryPoint = "GetCommandLineW")]
        private unsafe extern static char* PInvoke_GetCommandLine();

        // Wrappers around the GetLocaleInfoEx APIs which handle marshalling the returned
        // data as either and Int or String.
        internal static unsafe String GetLocaleInfoEx(String localeName, uint field)
        {
            // REVIEW: Determine the maximum size for the buffer
            const int BUFFER_SIZE = 530;

            char* pBuffer = stackalloc char[BUFFER_SIZE];
            int resultCode = Interop.mincore.GetLocaleInfoEx(localeName, field, pBuffer, BUFFER_SIZE);
            if (resultCode > 0)
            {
                return new String(pBuffer);
            }

            return "";
        }

        [DllImport("api-ms-win-core-handle-l1-1-0.dll", EntryPoint = "CloseHandle", CharSet = CharSet.Unicode)]
        internal extern static int PInvoke_CloseHandle(IntPtr hObject);

        internal static bool CloseHandle(IntPtr hObject)
        {
            return PInvoke_CloseHandle(hObject) != 0;
        }

        [DllImport("api-ms-win-core-com-l1-1-0.dll")]
        internal extern static int CoCreateGuid(out _GUID pguid);

        [DllImport("api-ms-win-core-com-l1-1-0.dll")]
        internal extern static int CoGetApartmentType(out _APTTYPE pAptType, out _APTTYPEQUALIFIER pAptQualifier);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateEventExW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr CreateEventEx(IntPtr lpEventAttributes, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateMutexExW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr CreateMutexEx(IntPtr lpMutexAttributes, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "CreateSemaphoreExW", CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateSemaphoreEx(IntPtr lpSemaphoreAttributes, int lInitialCount, int lMaximumCount, string lpName, uint dwFlags, uint dwDesiredAccess);

        [DllImport("api-ms-win-core-handle-l1-1-0.dll", EntryPoint = "DuplicateHandle", CharSet = CharSet.Unicode)]
        internal static extern int PInvoke_DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        internal static bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions)
        {
            return PInvoke_DuplicateHandle(hSourceProcessHandle, hSourceHandle, hTargetProcessHandle, out lpTargetHandle, dwDesiredAccess, bInheritHandle, dwOptions) != 0;
        }

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint EnumDynamicTimeZoneInformation(uint dwIndex, out _TIME_DYNAMIC_ZONE_INFORMATION lpTimeZoneInformation);

        [DllImport("api-ms-win-core-processthreads-l1-1-0.dll")]
        internal extern static uint GetCurrentThreadId();

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint GetDynamicTimeZoneInformation(out _TIME_DYNAMIC_ZONE_INFORMATION pTimeZoneInformation);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint GetDynamicTimeZoneInformationEffectiveYears(ref _TIME_DYNAMIC_ZONE_INFORMATION lpTimeZoneInformation, out uint FirstYear, out uint LastYear);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static bool GetTimeZoneInformationForYear(ushort wYear, ref _TIME_DYNAMIC_ZONE_INFORMATION pdtzi, out _TIME_ZONE_INFORMATION ptzi);

        [DllImport("api-ms-win-core-errorhandling-l1-1-0.dll")]
        internal extern static uint GetLastError();

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetLocaleInfoEx(string lpLocaleName, uint LCType, IntPtr lpLCData, int cchData);

        [DllImport("api-ms-win-core-heap-l1-1-0.dll")]
        internal extern static IntPtr GetProcessHeap();

        [DllImport("api-ms-win-core-sysinfo-l1-1-0.dll")]
        internal extern static void GetSystemTimeAsFileTime(out _FILETIME lpSystemTimeAsFileTime);

        [DllImport("api-ms-win-core-sysinfo-l1-1-0.dll")]
        internal extern static ulong GetTickCount64();

        [DllImport("api-ms-win-core-heap-l1-1-0.dll")]
        internal extern static IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwBytes);

        [DllImport("api-ms-win-core-debug-l1-1-0.dll", EntryPoint = "IsDebuggerPresent", CharSet = CharSet.Unicode)]
        private extern static int PInvoke_IsDebuggerPresent();

        internal static bool IsDebuggerPresent()
        {
            return PInvoke_IsDebuggerPresent() != 0;
        }

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenEventW", CharSet = CharSet.Unicode)]
        private extern static IntPtr OpenEvent(uint dwDesiredAccess, int bInheritHandle, string lpName);

        internal static IntPtr OpenEvent(uint dwDesiredAccess, bool bInheritHandle, string lpName)
        {
            return OpenEvent(dwDesiredAccess, bInheritHandle ? 1 : 0, lpName);
        }

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenMutexW", CharSet = CharSet.Unicode)]
        internal extern static IntPtr OpenMutex(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "OpenSemaphoreW", CharSet = CharSet.Unicode)]
        private extern static IntPtr OpenSemaphore(uint dwDesiredAccess, int bInheritHandle, string lpName);

        internal static IntPtr OpenSemaphore(uint dwDesiredAccess, bool bInheritHandle, string lpName)
        {
            return OpenSemaphore(dwDesiredAccess, bInheritHandle ? 1 : 0, lpName);
        }

        [DllImport("api-ms-win-core-debug-l1-1-0.dll", EntryPoint = "OutputDebugStringW", CharSet = CharSet.Unicode)]
        internal extern static void OutputDebugString(string lpOutputString);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "ReleaseMutex", CharSet = CharSet.Unicode)]
        private extern static int PInvoke_ReleaseMutex(IntPtr hMutex);

        internal static bool ReleaseMutex(IntPtr hMutex)
        {
            return PInvoke_ReleaseMutex(hMutex) != 0;
        }

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "ReleaseSemaphore", CharSet = CharSet.Unicode)]
        private extern static int PInvoke_ReleaseSemaphore(IntPtr hSemaphore, int lReleaseCount, out int lpPreviousCount);

        internal static bool ReleaseSemaphore(IntPtr hSemaphore, int lReleaseCount, out int lpPreviousCount)
        {
            return PInvoke_ReleaseSemaphore(hSemaphore, lReleaseCount, out lpPreviousCount) != 0;
        }

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "ResetEvent", CharSet = CharSet.Unicode)]
        private extern static int PInvoke_ResetEvent(IntPtr hEvent);

        internal static bool ResetEvent(IntPtr hEvent)
        {
            return PInvoke_ResetEvent(hEvent) != 0;
        }

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int ResolveLocaleName(string lpNameToResolve, IntPtr lpLocaleName, int cchLocaleName);

        [DllImport("api-ms-win-core-synch-l1-1-0.dll", EntryPoint = "SetEvent", CharSet = CharSet.Unicode)]
        private extern static int PInvoke_SetEvent(IntPtr hEvent);

        internal static bool SetEvent(IntPtr hEvent)
        {
            return PInvoke_SetEvent(hEvent) != 0;
        }

        [DllImport("api-ms-win-core-synch-l1-1-0.dll")]
        internal extern static uint WaitForMultipleObjectsEx(uint nCount, IntPtr lpHandles, bool bWaitAll, uint dwMilliseconds, bool bAlertable);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int GetCalendarInfoEx(string lpLocaleName, uint Calendar, IntPtr lpReserved, uint CalType, IntPtr lpCalData, int cchData, out int lpValue);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int GetCalendarInfoEx(string lpLocaleName, uint Calendar, IntPtr lpReserved, uint CalType, IntPtr lpCalData, int cchData, IntPtr lpValue);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int LCMapStringEx(string lpLocaleName, uint dwMapFlags, string lpSrcStr, int cchSrc, IntPtr lpDestStr, int cchDest, IntPtr lpVersionInformation, IntPtr lpReserved, IntPtr sortHandle);

        [DllImport("api-ms-win-core-localization-l2-1-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int EnumCalendarInfoExEx(IntPtr pCalInfoEnumProcExEx, string lpLocaleName, uint Calendar, string lpReserved, uint CalType, IntPtr lParam);

        [DllImport("api-ms-win-core-localization-l2-1-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int EnumTimeFormatsEx(IntPtr lpTimeFmtEnumProcEx, string lpLocaleName, uint dwFlags, IntPtr lParam);

        internal static unsafe int GetLocaleInfoExInt(String localeName, uint field)
        {
            const uint LOCALE_RETURN_NUMBER = 0x20000000;
            const int BUFFER_SIZE = 2; // sizeof(int) / sizeof(char)

            field |= LOCALE_RETURN_NUMBER;

            char* pBuffer = stackalloc char[BUFFER_SIZE];
            Interop.mincore.GetLocaleInfoEx(localeName, field, pBuffer, BUFFER_SIZE);

            return *(int*)pBuffer;
        }

        internal static unsafe int GetLocaleInfoEx(string lpLocaleName, uint lcType, char* lpLCData, int cchData)
        {
            return GetLocaleInfoEx(lpLocaleName, lcType, (IntPtr)lpLCData, cchData);
        }

        internal static unsafe int ResolveLocaleName(string lpNameToResolve, char* lpLocaleName, int cchLocaleName)
        {
            return ResolveLocaleName(lpNameToResolve, (IntPtr)lpLocaleName, cchLocaleName);
        }

        //
        // Wrappers to the private unsafe generated PInvoke 
        //
        internal static unsafe int FindNLSStringEx(
                    string lpLocaleName,
                    uint dwFindNLSStringFlags,
                    string lpStringSource,
                    int startSource,
                    int cchSource,
                    string lpStringValue,
                    int startValue,
                    int cchValue,
                    IntPtr sortHandle)
        {
            fixed (char* pLocaleName = lpLocaleName)
            fixed (char* pSource = lpStringSource)
            fixed (char* pValue = lpStringValue)
            {
                char* pS = pSource + startSource;
                char* pV = pValue + startValue;

                return PInvoke_FindNLSStringEx(
                                    (short*)pLocaleName,
                                    dwFindNLSStringFlags,
                                    (short*)pS,
                                    cchSource,
                                    (short*)pV,
                                    cchValue,
                                    null,
                                    null,
                                    null,
                                    sortHandle);
            }
        }

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", EntryPoint = "FindNLSStringEx")]
        private extern static unsafe int PInvoke_FindNLSStringEx(
                    short* lpLocaleName,
                    uint dwFindNLSStringFlags,
                    short* lpStringSource,
                    int cchSource,
                    short* lpStringValue,
                    int cchValue,
                    int* pcchFound,
                    void* lpVersionInformation,
                    void* lpReserved,
                    IntPtr sortHandle);


        internal static unsafe int CompareStringOrdinal(
                    char* lpString1,
                    int cchCount1,
                    char* lpString2,
                    int cchCount2,
                    bool bIgnoreCase)
        {
            return PInvoke_CompareStringOrdinal(
                        (short*)lpString1,
                        cchCount1,
                        (short*)lpString2,
                        cchCount2,
                        bIgnoreCase ? 1 : 0);
        }

        [DllImport("api-ms-win-core-string-l1-1-0.dll", EntryPoint = "CompareStringOrdinal")]
        private extern static unsafe int PInvoke_CompareStringOrdinal(
                    short* lpString1,
                    int cchCount1,
                    short* lpString2,
                    int cchCount2,
                    int bIgnoreCase);

        internal static unsafe int FindStringOrdinal(
            uint dwFindStringOrdinalFlags,
            string stringSource,
            int offset,
            int cchSource,
            string value,
            int cchValue,
            bool bIgnoreCase)
        {
            fixed (char* pSource = stringSource)
            fixed (char* pValue = value)
            {
                char* pS = pSource + offset;
                int ret = PInvoke_FindStringOrdinal(
                            dwFindStringOrdinalFlags,
                            (short*)pS,
                            cchSource,
                            (short*)pValue,
                            cchValue,
                            bIgnoreCase ? 1 : 0);
                return ret < 0 ? ret : ret + offset;
            }
        }

        [DllImport("api-ms-win-core-libraryloader-l1-1-0.dll", EntryPoint = "FindStringOrdinal")]
        private extern static unsafe int PInvoke_FindStringOrdinal(
                    uint dwFindStringOrdinalFlags,
                    short* lpStringSource,
                    int cchSource,
                    short* lpStringValue,
                    int cchValue,
                    int bIgnoreCase);

        internal static unsafe int CompareStringEx(
                        string lpLocaleName,
                        int dwCmpFlags,
                        string string1,
                        int offset1,
                        int cchCount1,
                        string string2,
                        int offset2,
                        int cchCount2,
                        IntPtr lParam)
        {
            fixed (char* pLocaleName = lpLocaleName)
            fixed (char* pString1 = string1)
            fixed (char* pString2 = string2)
            {
                char* pS1 = pString1 + offset1;
                char* pS2 = pString2 + offset2;

                return PInvoke_CompareStringEx(
                                    (short*)pLocaleName,
                                    (uint)dwCmpFlags,
                                    (short*)pS1,
                                    cchCount1,
                                    (short*)pS2,
                                    cchCount2,
                                    null,
                                    null,
                                    lParam);
            }
        }

        [DllImport("api-ms-win-core-string-l1-1-0.dll", EntryPoint = "CompareStringEx")]
        private extern static unsafe int PInvoke_CompareStringEx(
                    short* lpLocaleName,
                    uint dwCmpFlags,
                    short* lpString1,
                    int cchCount1,
                    short* lpString2,
                    int cchCount2,
                    void* lpVersionInformation,
                    void* lpReserved,
                    IntPtr lParam);


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


        //
        // GetCurrentProcess and GetCurrentThread just return constants.  To avoid making an expensive p/invoke just to 
        // get a constant, we implement these manually here.
        //
        internal static IntPtr GetCurrentProcess()
        {
            return (IntPtr)(-1);
        }

        internal static IntPtr GetCurrentThread()
        {
            return (IntPtr)(-2);
        }

        //
        // Wrappers for encoding 
        //
        internal const uint ERROR_INSUFFICIENT_BUFFER = 122;

        [DllImport("api-ms-win-core-string-l1-1-0.dll")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal extern unsafe static int WideCharToMultiByte(
                    int CodePage,
                    uint dwFlags,
                    char* lpWideCharStr,
                    int cchWideChar,
                    byte* lpMultiByteStr,
                    int cbMultiByte,
                    byte* lpDefaultChar,
                    int* lpUsedDefaultChar);

        [DllImport("api-ms-win-core-string-l1-1-0.dll")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal extern unsafe static int MultiByteToWideChar(
                    int CodePage,
                    uint dwFlags,
                    byte* lpMultiByteStr,
                    int cbMultiByte,
                    char* lpWideCharStr,
                    int cchWideChar);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal extern unsafe static int GetCPInfoExW(int codepage, uint flag, byte* pCodePageInfo);

        private static unsafe int GetByteCount(int codepage, Char* pChars, int count)
        {
            if (count == 0) return 0;

            int result = WideCharToMultiByte(codepage, 0, pChars, count, null, 0, null, null);
            if (result <= 0)
                throw new ArgumentException(SR.Argument_InvalidCharSequenceNoIndex);
            return result;
        }

        internal static unsafe int GetByteCount(int codepage, char[] chars, int index, int count)
        {
            fixed (char* pChars = &chars[index])
            {
                return GetByteCount(codepage, pChars, count);
            }
        }

        internal static unsafe int GetByteCount(int codepage, string s)
        {
            fixed (char* pChars = s)
            {
                return GetByteCount(codepage, pChars, s.Length);
            }
        }

        private static unsafe int GetBytes(int codepage, char* pChars, int charCount, byte* pBytes, int byteCount)
        {
            if (charCount == 0)
                return 0;

            int result = WideCharToMultiByte(codepage, 0, pChars, charCount, pBytes, byteCount, null, null);
            if (result <= 0)
            {
                uint lastErroro = Interop.mincore.GetLastError();
                if (lastErroro == ERROR_INSUFFICIENT_BUFFER)
                    throw new ArgumentOutOfRangeException(SR.Argument_EncodingConversionOverflowBytes);
                throw new ArgumentException(SR.Argument_InvalidCharSequenceNoIndex);
            }
            return result;
        }

        internal unsafe static int GetBytes(int codepage, char[] chars, int index, int count, byte[] bytes, int byteIndex)
        {
            fixed (byte* pBytes = &bytes[byteIndex])
            fixed (char* pChars = &chars[index])
            {
                return GetBytes(codepage, pChars, count, pBytes, bytes.Length - byteIndex);
            }
        }

        internal unsafe static int GetBytes(int codepage, string s, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            fixed (byte* pBytes = &bytes[byteIndex])
            fixed (char* pChars = s)
            {
                return GetBytes(codepage, pChars + charIndex, charCount, pBytes, bytes.Length - byteIndex);
            }
        }

        internal static unsafe int GetCharCount(int codepage, byte* pBytes, int count)
        {
            if (count == 0) return 0;

            int result = MultiByteToWideChar(codepage, 0, pBytes, count, null, 0);
            if (result <= 0)
                throw new ArgumentException(SR.Argument_InvalidCharSequenceNoIndex);
            return result;
        }

        internal static unsafe int GetCharCount(int codepage, byte[] bytes, int index, int count)
        {
            fixed (byte* pBytes = &bytes[index])
            {
                return GetCharCount(codepage, pBytes, count);
            }
        }

        internal static unsafe int GetChars(int codepage, byte* bytes, int byteCount, char* chars, int charsCount)
        {
            if (byteCount == 0)
                return 0;

            int result = MultiByteToWideChar(codepage, 0, bytes, byteCount, chars, charsCount);
            if (result <= 0)
            {
                uint lastErroro = Interop.mincore.GetLastError();
                if (lastErroro == ERROR_INSUFFICIENT_BUFFER)
                    throw new ArgumentOutOfRangeException(SR.Argument_EncodingConversionOverflowChars);
                throw new ArgumentException(SR.Argument_InvalidCharSequenceNoIndex);
            }
            return result;
        }

        internal static unsafe int GetChars(int codepage, byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            if (byteCount == 0)
                return 0;

            fixed (byte* pBytes = &bytes[byteIndex])
            fixed (char* pChars = &chars[charIndex])
            {
                int result = MultiByteToWideChar(codepage, 0, pBytes, byteCount, pChars, chars.Length - charIndex);
                if (result <= 0)
                {
                    uint lastErroro = Interop.mincore.GetLastError();
                    if (lastErroro == ERROR_INSUFFICIENT_BUFFER)
                        throw new ArgumentOutOfRangeException(SR.Argument_EncodingConversionOverflowChars);
                    throw new ArgumentException(SR.Argument_InvalidCharSequenceNoIndex);
                }
                return result;
            }
        }

        private readonly static System.Threading.WaitHandle s_sleepHandle = new System.Threading.ManualResetEvent(false);
        static internal void Sleep(uint milliseconds)
        {
            if (milliseconds == 0)
                System.Threading.SpinWait.Yield();
            else
                s_sleepHandle.WaitOne((int)milliseconds);
        }
    }

    internal partial class mincore_obsolete
    {
        [DllImport("api-ms-win-core-localization-l1-2-1.dll")]
        internal extern static bool EnumSystemLocalesEx(EnumLocalesProcEx lpLocaleEnumProcEx, uint dwFlags, mincore_private.LParamCallbackContext lParam, IntPtr lpReserved);

        internal delegate bool EnumLocalesProcEx(IntPtr arg0, uint arg1, mincore_private.LParamCallbackContext arg2);
    }
    // declare the specialized callback context type, deriving from the CallbackContext type that MCG expects the class library to implement.  
    internal partial class mincore_private
    {
#pragma warning disable 649
        internal class LParamCallbackContext : CallbackContext
        {
            // Put any user data to pass to the callback here.  The user code being called back will get the instance of this class that was passed to the API originally.
            internal IntPtr lParam;
        }

        [DllImport("api-ms-win-core-localization-l2-1-0.dll", CharSet = CharSet.Unicode)]
        internal extern static bool EnumCalendarInfoExEx(EnumCalendarInfoExExCallback pCalInfoEnumProcExEx, string lpLocaleName, uint Calendar, string lpReserved, uint CalType, mincore_private.LParamCallbackContext lParam);

        [DllImport("api-ms-win-core-localization-l2-1-0.dll", CharSet = CharSet.Unicode)]
        internal extern static bool EnumTimeFormatsEx(EnumTimeFormatsProcEx lpTimeFmtEnumProcEx, string lpLocaleName, uint dwFlags, mincore_private.LParamCallbackContext lParam);

        internal delegate bool EnumCalendarInfoExExCallback(IntPtr arg0, uint arg1, IntPtr arg2, mincore_private.LParamCallbackContext arg3);

        // Unmanaged Function Pointer - Calling Convention StdCall
        internal delegate bool EnumTimeFormatsProcEx(IntPtr arg0, mincore_private.LParamCallbackContext arg1);
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
