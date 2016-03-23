// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
#include "slist.h"
#include "holder.h"
#include "Crst.h"
#include "rhbinder.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "gcrhinterface.h"
#include "shash.h"
#include "module.h"

// Macro nonsense to get around limitations of the C++ preprocessor.
#define MAKE_WIDE_STRING(_str) L ## _str
#define WIDE_STRING(_str) MAKE_WIDE_STRING(_str)

#define MAIN_RH_MODULE_NAME_W          WIDE_STRING(RH_BASE_NAME)



#ifdef FEATURE_PROFILING
#ifndef APP_LOCAL_RUNTIME //need to sort out how to get this thread started, where to log, etc., without violating the WACK
UInt32 __stdcall ProfileThread(void *pv)
{
    RuntimeInstance *runtimeInstance = (RuntimeInstance *)pv;
    for (;;)
    {
        PalSleep(10*1000);
        runtimeInstance->WriteProfileInfo();
    }
}
#endif

void RuntimeInstance::InitProfiling(ModuleHeader *pModuleHeader)
{
#ifdef APP_LOCAL_RUNTIME //need to sort out how to get this thread started, where to log, etc., without violating the WACK
    UNREFERENCED_PARAMETER(pModuleHeader);
#else
    if (!m_fProfileThreadCreated && pModuleHeader->GetProfilingEntries() != NULL)
    {
        // this module has profile data, and we don't have a profile-writing thread yet
        // so let's create one
        UInt32 threadId;
        PalCreateThread(NULL, 4096, ProfileThread, this, 0, &threadId);

        m_fProfileThreadCreated = true;
    }
#endif
}


void RuntimeInstance::WriteProfileInfo()
{
#ifndef APP_LOCAL_RUNTIME //need to sort out how to get this thread started, where to log, etc., without violating the WACK
    FOREACH_MODULE(pModule)
    {
        PTR_ModuleHeader pModuleHeader = pModule->GetModuleHeader();
        if (pModuleHeader->GetProfilingEntries() != NULL)
        {
            // our general error handling strategy is just to give up writing the profile
            // if we encounter any errors or the names get insanely long
            WCHAR *wzModuleFileName;
            const size_t MAX_PATH = 260;
            size_t moduleFileNameLength = PalGetModuleFileName(&wzModuleFileName, pModule->GetOsModuleHandle());
            if (moduleFileNameLength >= MAX_PATH)
                continue;
            const UInt32 BUFFER_SIZE = 512;
            WCHAR profileName[BUFFER_SIZE];
            WCHAR *basicName = wcsrchr(wzModuleFileName, L'\\');
            if (basicName == NULL)
                basicName = wzModuleFileName;
            else
                basicName += 1; // skip past the '\'
            size_t basicNameLength = wcslen(basicName);
            size_t dirNameLength = PalGetEnvironmentVariable(L"LOCALAPPDATA", profileName, MAX_PATH);

            // make sure the names are not so long as to cause trouble
            const size_t MAX_SAFE_LENGTH = MAX_PATH - 50;
            if ( basicNameLength >= MAX_SAFE_LENGTH
              || dirNameLength >= MAX_SAFE_LENGTH
              || basicNameLength + dirNameLength >= MAX_SAFE_LENGTH)
            {
                continue;
            }

            // make sure %LOCALAPPDATA%\Microsoft\mrt100\ProfileData exists
            wcscat_s(profileName, L"\\Microsoft");
            if (!PalCreateDirectoryW(profileName, NULL) && GetLastError() != ERROR_ALREADY_EXISTS)
                continue;

            wcscat_s(profileName, L"\\");
            wcscat_s(profileName, MAIN_RH_MODULE_NAME_W);
            if (!PalCreateDirectoryW(profileName, NULL) && GetLastError() != ERROR_ALREADY_EXISTS)
                continue;

            wcscat_s(profileName, L"\\ProfileData");
            if (!PalCreateDirectoryW(profileName, NULL) && GetLastError() != ERROR_ALREADY_EXISTS)
                continue;

            // final filename is %LOCALAPPDATA%\Microsoft\mrt100\ProfileData\<basicName>.profile
            wcscat_s(profileName, L"\\");
            wcscat_s(profileName, basicName);
            wcscat_s(profileName, L".profile");
            HANDLE fileHandle = PalCreateFileW(profileName, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, 0, NULL);
            if (fileHandle == INVALID_HANDLE_VALUE)
            {
                continue;
            }
            else
            {
                ProfilingEntry *profilingEntry = (ProfilingEntry *)pModuleHeader->GetProfilingEntries();
                ProfilingEntry *profilingEntryLast = profilingEntry + pModuleHeader->CountOfProfilingEntries;
                for( ; profilingEntry < profilingEntryLast; profilingEntry++)
                {
                    if (profilingEntry->m_count != 0)
                    {
                        UInt32 bytesWritten = 0;
                        if (!PalWriteFile(fileHandle, profilingEntry, sizeof(ProfilingEntry), &bytesWritten, NULL))
                            break;
                    }
                }
                PalCloseHandle(fileHandle);
            }
        }
    }
    END_FOREACH_MODULE;
#endif //!APP_LOCAL_RUNTIME
}
#endif // FEATURE_PROFILING

