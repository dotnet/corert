// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Implementation of the portions of the Redhawk Platform Abstraction Layer (PAL) library that are common among
// multiple PAL variants.
//
// Note that in general we don't want to assume that Windows and Redhawk global definitions can co-exist.
// Since this code must include Windows headers to do its job we can't therefore safely include general
// Redhawk header files.
//

#include <windows.h>
#include <stdio.h>
#include <errno.h>
#include <evntprov.h>
#include "CommonTypes.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include <winternl.h>
#include "CommonMacros.h"
#include "rhassert.h"


#define REDHAWK_PALEXPORT extern "C"
#define REDHAWK_PALAPI __stdcall


// Given the OS handle of a loaded module, compute the upper and lower virtual address bounds (inclusive).
REDHAWK_PALEXPORT void REDHAWK_PALAPI PalGetModuleBounds(HANDLE hOsHandle, _Out_ UInt8 ** ppLowerBound, _Out_ UInt8 ** ppUpperBound)
{
    BYTE *pbModule = (BYTE*)hOsHandle;
    DWORD cbModule;

    IMAGE_NT_HEADERS *pNtHeaders = (IMAGE_NT_HEADERS*)(pbModule + ((IMAGE_DOS_HEADER*)hOsHandle)->e_lfanew);
    if (pNtHeaders->OptionalHeader.Magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC)
        cbModule = ((IMAGE_OPTIONAL_HEADER32*)&pNtHeaders->OptionalHeader)->SizeOfImage;
    else
        cbModule = ((IMAGE_OPTIONAL_HEADER64*)&pNtHeaders->OptionalHeader)->SizeOfImage;

    *ppLowerBound = pbModule;
    *ppUpperBound = pbModule + cbModule - 1;
}

// Reads through the PE header of the specified module, and returns
// the module's matching PDB's signature GUID, age, and build path by
// fishing them out of the last IMAGE_DEBUG_DIRECTORY of type
// IMAGE_DEBUG_TYPE_CODEVIEW.  Used when sending the ModuleLoad event
// to help profilers find matching PDBs for loaded modules.
//
// Arguments:
//
// [in] hOsHandle - OS Handle for module from which to get PDB info
// [out] pGuidSignature - PDB's signature GUID to be placed here
// [out] pdwAge - PDB's age to be placed here
// [out] wszPath - PDB's build path to be placed here
// [in] cchPath - Number of wide characters allocated in wszPath, including NULL terminator
//
// This is a simplification of similar code in desktop CLR's GetCodeViewInfo
// in eventtrace.cpp.
REDHAWK_PALEXPORT void REDHAWK_PALAPI PalGetPDBInfo(HANDLE hOsHandle, _Out_ GUID * pGuidSignature, _Out_ UInt32 * pdwAge, _Out_writes_z_(cchPath) WCHAR * wszPath, Int32 cchPath)
{
    // Zero-init [out]-params
    ZeroMemory(pGuidSignature, sizeof(*pGuidSignature));
    *pdwAge = 0;
    if (cchPath <= 0)
        return;
    wszPath[0] = L'\0';

    BYTE *pbModule = (BYTE*)hOsHandle;

    IMAGE_NT_HEADERS const * pNtHeaders = (IMAGE_NT_HEADERS*)(pbModule + ((IMAGE_DOS_HEADER*)hOsHandle)->e_lfanew);
    IMAGE_DATA_DIRECTORY const * rgDataDirectory = NULL;
    if (pNtHeaders->OptionalHeader.Magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC)
        rgDataDirectory = ((IMAGE_OPTIONAL_HEADER32 const *)&pNtHeaders->OptionalHeader)->DataDirectory;
    else
        rgDataDirectory = ((IMAGE_OPTIONAL_HEADER64 const *)&pNtHeaders->OptionalHeader)->DataDirectory;

    IMAGE_DATA_DIRECTORY const * pDebugDataDirectory = &rgDataDirectory[IMAGE_DIRECTORY_ENTRY_DEBUG];

    // In Redhawk, modules are loaded as MAPPED, so we don't have to worry about dealing
    // with FLAT files (with padding missing), so header addresses can be used as is
    IMAGE_DEBUG_DIRECTORY const *rgDebugEntries = (IMAGE_DEBUG_DIRECTORY const *) (pbModule + pDebugDataDirectory->VirtualAddress);
    DWORD cbDebugEntries = pDebugDataDirectory->Size;
    if (cbDebugEntries < sizeof(IMAGE_DEBUG_DIRECTORY))
        return;

    // Since rgDebugEntries is an array of IMAGE_DEBUG_DIRECTORYs, cbDebugEntries
    // should be a multiple of sizeof(IMAGE_DEBUG_DIRECTORY).
    if (cbDebugEntries % sizeof(IMAGE_DEBUG_DIRECTORY) != 0)
        return;

    // CodeView RSDS debug information -> PDB 7.00
    struct CV_INFO_PDB70 
    {
        DWORD          magic; 
        GUID           signature;       // unique identifier 
        DWORD          age;             // an always-incrementing value 
        _Field_z_ char  path[MAX_PATH];  // zero terminated string with the name of the PDB file 
    };

    // Temporary storage for a CV_INFO_PDB70 and its size (which could be less than
    // sizeof(CV_INFO_PDB70); see below).
    struct PdbInfo
    {
        CV_INFO_PDB70 *     m_pPdb70;
        ULONG               m_cbPdb70;
    };

    // Grab module bounds so we can do some rough sanity checking before we follow any
    // RVAs
    UInt8 * pbModuleLowerBound = NULL;
    UInt8 * pbModuleUpperBound = NULL;
    PalGetModuleBounds(hOsHandle, &pbModuleLowerBound, &pbModuleUpperBound);

    // Iterate through all debug directory entries. The convention is that debuggers &
    // profilers typically just use the very last IMAGE_DEBUG_TYPE_CODEVIEW entry.  Treat raw
    // bytes we read as untrusted.
    PdbInfo pdbInfoLast = {0};
    int cEntries = cbDebugEntries / sizeof(IMAGE_DEBUG_DIRECTORY);
    for (int i = 0; i < cEntries; i++)
    {
        if ((UInt8 *)(&rgDebugEntries[i]) + sizeof(rgDebugEntries[i]) >= pbModuleUpperBound)
        {
            // Bogus pointer
            return;
        }

        if (rgDebugEntries[i].Type != IMAGE_DEBUG_TYPE_CODEVIEW)
            continue;

        // Get raw data pointed to by this IMAGE_DEBUG_DIRECTORY

        // AddressOfRawData is generally set properly for Redhawk modules, so we don't
        // have to worry about using PointerToRawData and converting it to an RVA
        if (rgDebugEntries[i].AddressOfRawData == NULL)
            continue;

        DWORD rvaOfRawData = rgDebugEntries[i].AddressOfRawData;
        ULONG cbDebugData = rgDebugEntries[i].SizeOfData;
        if (cbDebugData < size_t(&((CV_INFO_PDB70*)0)->magic) + sizeof(((CV_INFO_PDB70*)0)->magic))
        {
            // raw data too small to contain magic number at expected spot, so its format
            // is not recognizable. Skip
            continue;
        }

        // Verify the magic number is as expected
        const DWORD CV_SIGNATURE_RSDS = 0x53445352;
        CV_INFO_PDB70 * pPdb70 = (CV_INFO_PDB70 *) (pbModule + rvaOfRawData);
        if ((UInt8 *)(pPdb70) + cbDebugData >= pbModuleUpperBound)
        {
            // Bogus pointer
            return;
        }

        if (pPdb70->magic != CV_SIGNATURE_RSDS)
        {
            // Unrecognized magic number.  Skip
            continue;
        }

        // From this point forward, the format should adhere to the expected layout of
        // CV_INFO_PDB70. If we find otherwise, then assume the IMAGE_DEBUG_DIRECTORY is
        // outright corrupt.

        // Verify sane size of raw data
        if (cbDebugData > sizeof(CV_INFO_PDB70))
            return;

        // cbDebugData actually can be < sizeof(CV_INFO_PDB70), since the "path" field
        // can be truncated to its actual data length (i.e., fewer than MAX_PATH chars
        // may be present in the PE file). In some cases, though, cbDebugData will
        // include all MAX_PATH chars even though path gets null-terminated well before
        // the MAX_PATH limit.
        
        // Gotta have at least one byte of the path
        if (cbDebugData < offsetof(CV_INFO_PDB70, path) + sizeof(char))
            return;
        
        // How much space is available for the path?
        size_t cchPathMaxIncludingNullTerminator = (cbDebugData - offsetof(CV_INFO_PDB70, path)) / sizeof(char);
        ASSERT(cchPathMaxIncludingNullTerminator >= 1);   // Guaranteed above

        // Verify path string fits inside the declared size
        size_t cchPathActualExcludingNullTerminator = strnlen_s(pPdb70->path, cchPathMaxIncludingNullTerminator);
        if (cchPathActualExcludingNullTerminator == cchPathMaxIncludingNullTerminator)
        {
            // This is how strnlen indicates failure--it couldn't find the null
            // terminator within the buffer size specified
            return;
        }

        // Looks valid.  Remember it.
        pdbInfoLast.m_pPdb70 = pPdb70;
        pdbInfoLast.m_cbPdb70 = cbDebugData;
    }

    // Take the last IMAGE_DEBUG_TYPE_CODEVIEW entry we saw, and return it to the caller
    if (pdbInfoLast.m_pPdb70 != NULL)
    {
        memcpy(pGuidSignature, &pdbInfoLast.m_pPdb70->signature, sizeof(GUID));
        *pdwAge = pdbInfoLast.m_pPdb70->age;

        // Convert build path from ANSI to UNICODE
        errno_t ret;
        size_t cchConverted;
        ret = mbstowcs_s(
            &cchConverted,
            wszPath,
            cchPath,
            pdbInfoLast.m_pPdb70->path,
            _countof(pdbInfoLast.m_pPdb70->path) - 1);
        if ((ret != 0) && (ret != STRUNCATE))
        {
            // PDB path isn't essential.  An empty string will do if we hit an error.
            ASSERT(cchPath > 0);        // Guaranteed at top of function
            wszPath[0] = L'\0';
        }
    }
}

REDHAWK_PALEXPORT Int32 REDHAWK_PALAPI PalGetProcessCpuCount()
{
    static int CpuCount = 0;

    if (CpuCount != 0)
        return CpuCount;
    else
    {
        // The concept of process CPU affinity is going away and so CoreSystem obsoletes the APIs used to
        // fetch this information. Instead we'll just return total cpu count.
        SYSTEM_INFO sysInfo;
#ifndef APP_LOCAL_RUNTIME
        ::GetSystemInfo(&sysInfo);
#else
        ::GetNativeSystemInfo(&sysInfo);
#endif
        CpuCount = sysInfo.dwNumberOfProcessors;
        return sysInfo.dwNumberOfProcessors;
    }
}

//Reads the entire contents of the file into the specified buffer, buff
//returns the number of bytes read if the file is successfully read
//returns 0 if the file is not found, size is greater than maxBytesToRead or the file couldn't be opened or read
REDHAWK_PALEXPORT UInt32 REDHAWK_PALAPI PalReadFileContents(_In_z_ const TCHAR* fileName, _Out_writes_all_(maxBytesToRead) char* buff, _In_ UInt32 maxBytesToRead)
{
    WIN32_FILE_ATTRIBUTE_DATA attrData;

    BOOL getAttrSuccess = GetFileAttributesExW(fileName, GetFileExInfoStandard, &attrData);

    //if we weren't able to get the file attributes, or the file is larger than maxBytesToRead, or the file size is zero
    if ((!getAttrSuccess) || (attrData.nFileSizeHigh != 0) || (attrData.nFileSizeLow > (DWORD)maxBytesToRead) || (attrData.nFileSizeLow == 0))
    {
        return 0;
    }

    HANDLE hFile = PalCreateFileW(fileName, GENERIC_READ, FILE_SHARE_DELETE | FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE)
    {
        return 0;
    }

    UInt32 bytesRead;

    BOOL readSuccess = ReadFile(hFile, buff, (DWORD)maxBytesToRead, (DWORD*)&bytesRead, NULL);

    CloseHandle(hFile);

    if (!readSuccess)
    {
        return 0;
    }

    return bytesRead;
}


// Retrieves the entire range of memory dedicated to the calling thread's stack.  This does
// not get the current dynamic bounds of the stack, which can be significantly smaller than 
// the maximum bounds.
REDHAWK_PALEXPORT bool REDHAWK_PALAPI PalGetMaximumStackBounds(_Out_ void** ppStackLowOut, _Out_ void** ppStackHighOut)
{
    // VirtualQuery on the address of a local variable to get the allocation 
    // base of the stack.  Then use the StackBase field in the TEB to give 
    // the highest address of the stack region.
    MEMORY_BASIC_INFORMATION mbi = { 0 };
    SIZE_T cb = VirtualQuery(&mbi, &mbi, sizeof(mbi));
    if (cb != sizeof(mbi))
        return false;

    NT_TIB* pTib = (NT_TIB*)NtCurrentTeb();
    *ppStackHighOut = pTib->StackBase;      // stack base is the highest address
    *ppStackLowOut = mbi.AllocationBase;    // allocation base is the lowest address
    return true;
}

#if !defined(_INC_WINDOWS) || defined(APP_LOCAL_RUNTIME)

typedef struct _UNICODE_STRING {
    USHORT Length;
    USHORT MaximumLength;
    PWSTR  Buffer;
} UNICODE_STRING;
typedef UNICODE_STRING *PUNICODE_STRING;
typedef const UNICODE_STRING *PCUNICODE_STRING;

typedef struct _PEB_LDR_DATA {
    BYTE Reserved1[8];
    PVOID Reserved2[3];
    LIST_ENTRY InMemoryOrderModuleList;
} PEB_LDR_DATA, *PPEB_LDR_DATA;

typedef struct _LDR_DATA_TABLE_ENTRY {
    PVOID Reserved1[2];
    LIST_ENTRY InMemoryOrderLinks;
    PVOID Reserved2[2];
    PVOID DllBase;
    PVOID Reserved3[2];
    UNICODE_STRING FullDllName;
    BYTE Reserved4[8];
    PVOID Reserved5[3];
    union {
        ULONG CheckSum;
        PVOID Reserved6;
    } DUMMYUNIONNAME;
    ULONG TimeDateStamp;
} LDR_DATA_TABLE_ENTRY, *PLDR_DATA_TABLE_ENTRY;

typedef struct _PEB {
    BYTE Reserved1[2];
    BYTE BeingDebugged;
    BYTE Reserved2[1];
    PVOID Reserved3[2];
    PPEB_LDR_DATA Ldr;
    PVOID /*PRTL_USER_PROCESS_PARAMETERS*/ ProcessParameters;
    PVOID Reserved4[3];
    PVOID AtlThunkSListPtr;
    PVOID Reserved5;
    ULONG Reserved6;
    PVOID Reserved7;
    ULONG Reserved8;
    ULONG AtlThunkSListPtr32;
    PVOID Reserved9[45];
    BYTE Reserved10[96];
    PVOID /*PPS_POST_PROCESS_INIT_ROUTINE*/ PostProcessInitRoutine;
    BYTE Reserved11[128];
    PVOID Reserved12[1];
    ULONG SessionId;
} PEB, *PPEB;

typedef struct _TEB {
    PVOID Reserved1[12];
    PPEB ProcessEnvironmentBlock;
    PVOID Reserved2[399];
    BYTE Reserved3[1952];
    PVOID TlsSlots[64];
    BYTE Reserved4[8];
    PVOID Reserved5[26];
    PVOID ReservedForOle;  // Windows 2000 only
    PVOID Reserved6[4];
    PVOID TlsExpansionSlots;
} TEB, *PTEB;

#endif // !defined(_INC_WINDOWS) || defined(APP_LOCAL_RUNTIME)

// retrieves the full path to the specified module, if moduleBase is NULL retreieves the full path to the 
// executable module of the current process.
//
// Return value:  number of characters in name string
//
//NOTE:  This implementation exists because calling GetModuleFileName is not wack compliant.  if we later decide
//       that the framework package containing mrt100_app no longer needs to be wack compliant, this should be 
//       removed and the windows implementation of GetModuleFileName should be substitued on windows.
REDHAWK_PALEXPORT Int32 PalGetModuleFileName(_Out_ const TCHAR** pModuleNameOut, HANDLE moduleBase)
{
    TEB* pTEB = NtCurrentTeb();
    LIST_ENTRY* pStartLink = &(pTEB->ProcessEnvironmentBlock->Ldr->InMemoryOrderModuleList);
    LIST_ENTRY* pCurLink = pStartLink->Flink;

    do
    {
        LDR_DATA_TABLE_ENTRY* pEntry = CONTAINING_RECORD(pCurLink, LDR_DATA_TABLE_ENTRY, InMemoryOrderLinks);

        //null moduleBase will result in the first module being returned 
        //since the module list is ordered this is the executable module of the current process
        if ((pEntry->DllBase == moduleBase) || (moduleBase == NULL))
        {
            *pModuleNameOut = pEntry->FullDllName.Buffer;
            return pEntry->FullDllName.Length / 2;
        }
        pCurLink = pCurLink->Flink;
    }
    while (pCurLink != pStartLink);

    *pModuleNameOut = NULL;
    return 0;
}

REDHAWK_PALEXPORT UInt64 __cdecl PalGetTickCount64()
{
    return GetTickCount64();
}
