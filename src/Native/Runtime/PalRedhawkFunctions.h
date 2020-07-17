// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern "C" UInt16 __stdcall CaptureStackBackTrace(UInt32, UInt32, void*, UInt32*);
inline UInt16 PalCaptureStackBackTrace(UInt32 arg1, UInt32 arg2, void* arg3, UInt32* arg4)
{
    return CaptureStackBackTrace(arg1, arg2, arg3, arg4);
}

extern "C" UInt32_BOOL __stdcall CloseHandle(HANDLE);
inline UInt32_BOOL PalCloseHandle(HANDLE arg1)
{
    return CloseHandle(arg1);
}

extern "C" UInt32_BOOL __stdcall CreateDirectoryW(LPCWSTR, LPSECURITY_ATTRIBUTES);
inline UInt32_BOOL PalCreateDirectoryW(LPCWSTR arg1, LPSECURITY_ATTRIBUTES arg2)
{
    return CreateDirectoryW(arg1, arg2);
}

extern "C" void __stdcall DeleteCriticalSection(CRITICAL_SECTION *);
inline void PalDeleteCriticalSection(CRITICAL_SECTION * arg1)
{
    DeleteCriticalSection(arg1);
}

extern "C" UInt32_BOOL __stdcall DuplicateHandle(HANDLE, HANDLE, HANDLE, HANDLE *, UInt32, UInt32_BOOL, UInt32);
inline UInt32_BOOL PalDuplicateHandle(HANDLE arg1, HANDLE arg2, HANDLE arg3, HANDLE * arg4, UInt32 arg5, UInt32_BOOL arg6, UInt32 arg7)
{
    return DuplicateHandle(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
}

extern "C" void __stdcall EnterCriticalSection(CRITICAL_SECTION *);
inline void PalEnterCriticalSection(CRITICAL_SECTION * arg1)
{
    EnterCriticalSection(arg1);
}

extern "C" UInt32 __stdcall EventRegister(const GUID *, void *, void *, REGHANDLE *);
inline UInt32 PalEventRegister(const GUID * arg1, void * arg2, void * arg3, REGHANDLE * arg4)
{
    return EventRegister(arg1, arg2, arg3, arg4);
}

extern "C" UInt32 __stdcall EventUnregister(REGHANDLE);
inline UInt32 PalEventUnregister(REGHANDLE arg1)
{
    return EventUnregister(arg1);
}

extern "C" UInt32 __stdcall EventWrite(REGHANDLE, const EVENT_DESCRIPTOR *, UInt32, EVENT_DATA_DESCRIPTOR *);
inline UInt32 PalEventWrite(REGHANDLE arg1, const EVENT_DESCRIPTOR * arg2, UInt32 arg3, EVENT_DATA_DESCRIPTOR * arg4)
{
    return EventWrite(arg1, arg2, arg3, arg4);
}

extern "C" void __stdcall FlushProcessWriteBuffers();
inline void PalFlushProcessWriteBuffers()
{
    FlushProcessWriteBuffers();
}

extern "C" HANDLE __stdcall GetCurrentProcess();
inline HANDLE PalGetCurrentProcess()
{
    return GetCurrentProcess();
}

extern "C" UInt32 __stdcall GetCurrentProcessId();
inline UInt32 PalGetCurrentProcessId()
{
    return GetCurrentProcessId();
}

extern "C" HANDLE __stdcall GetCurrentThread();
inline HANDLE PalGetCurrentThread()
{
    return GetCurrentThread();
}

#ifdef UNICODE
extern "C" UInt32 __stdcall GetEnvironmentVariableW(__in_z_opt LPCWSTR, __out_z_opt LPWSTR, UInt32);
inline UInt32 PalGetEnvironmentVariable(__in_z_opt LPCWSTR arg1, __out_z_opt LPWSTR arg2, UInt32 arg3)
{
    return GetEnvironmentVariableW(arg1, arg2, arg3);
}
#else
extern "C" UInt32 __stdcall GetEnvironmentVariableA(__in_z_opt LPCSTR, __out_z_opt LPSTR, UInt32);
inline UInt32 PalGetEnvironmentVariable(__in_z_opt LPCSTR arg1, __out_z_opt LPSTR arg2, UInt32 arg3)
{
    return GetEnvironmentVariableA(arg1, arg2, arg3);
}
#endif

extern "C" void * __stdcall GetProcAddress(HANDLE, const char *);
inline void * PalGetProcAddress(HANDLE arg1, const char * arg2)
{
    return GetProcAddress(arg1, arg2);
}

extern "C" UInt32_BOOL __stdcall InitializeCriticalSectionEx(CRITICAL_SECTION *, UInt32, UInt32);
inline UInt32_BOOL PalInitializeCriticalSectionEx(CRITICAL_SECTION * arg1, UInt32 arg2, UInt32 arg3)
{
    return InitializeCriticalSectionEx(arg1, arg2, arg3);
}

extern "C" UInt32_BOOL __stdcall IsDebuggerPresent();
inline UInt32_BOOL PalIsDebuggerPresent()
{
    return IsDebuggerPresent();
}

extern "C" void __stdcall LeaveCriticalSection(CRITICAL_SECTION *);
inline void PalLeaveCriticalSection(CRITICAL_SECTION * arg1)
{
    LeaveCriticalSection(arg1);
}

extern "C" HANDLE __stdcall LoadLibraryExW(const WCHAR *, HANDLE, UInt32);
inline HANDLE PalLoadLibraryExW(const WCHAR * arg1, HANDLE arg2, UInt32 arg3)
{
    return LoadLibraryExW(arg1, arg2, arg3);
}

extern "C" UInt32_BOOL __stdcall QueryPerformanceCounter(LARGE_INTEGER *);
inline UInt32_BOOL PalQueryPerformanceCounter(LARGE_INTEGER * arg1)
{
    return QueryPerformanceCounter(arg1);
}

extern "C" UInt32_BOOL __stdcall QueryPerformanceFrequency(LARGE_INTEGER *);
inline UInt32_BOOL PalQueryPerformanceFrequency(LARGE_INTEGER * arg1)
{
    return QueryPerformanceFrequency(arg1);
}

extern "C" void __stdcall RaiseException(UInt32, UInt32, UInt32, const UInt32 *);
inline void PalRaiseException(UInt32 arg1, UInt32 arg2, UInt32 arg3, const UInt32 * arg4)
{
    RaiseException(arg1, arg2, arg3, arg4);
}

extern "C" UInt32_BOOL __stdcall ReleaseMutex(HANDLE);
inline UInt32_BOOL PalReleaseMutex(HANDLE arg1)
{
    return ReleaseMutex(arg1);
}

extern "C" UInt32_BOOL __stdcall ResetEvent(HANDLE);
inline UInt32_BOOL PalResetEvent(HANDLE arg1)
{
    return ResetEvent(arg1);
}

extern "C" UInt32_BOOL __stdcall SetEvent(HANDLE);
inline UInt32_BOOL PalSetEvent(HANDLE arg1)
{
    return SetEvent(arg1);
}

extern "C" void __stdcall TerminateProcess(HANDLE, UInt32);
inline void PalTerminateProcess(HANDLE arg1, UInt32 arg2)
{
    TerminateProcess(arg1, arg2);
}

extern "C" UInt32 __stdcall WaitForSingleObjectEx(HANDLE, UInt32, UInt32_BOOL);
inline UInt32 PalWaitForSingleObjectEx(HANDLE arg1, UInt32 arg2, UInt32_BOOL arg3)
{
    return WaitForSingleObjectEx(arg1, arg2, arg3);
}

#ifdef PAL_REDHAWK_INCLUDED
extern "C" void __stdcall GetSystemTimeAsFileTime(FILETIME *);
inline void PalGetSystemTimeAsFileTime(FILETIME * arg1)
{
    GetSystemTimeAsFileTime(arg1);
}

extern "C" void __stdcall RaiseFailFastException(PEXCEPTION_RECORD, PCONTEXT, UInt32);
inline void PalRaiseFailFastException(PEXCEPTION_RECORD arg1, PCONTEXT arg2, UInt32 arg3)
{
    RaiseFailFastException(arg1, arg2, arg3);
}
#endif 
