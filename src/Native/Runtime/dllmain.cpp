//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "gcrhinterface.h"

#include "assert.h"
#include "slist.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "holder.h"
#include "Crst.h"
#include "event.h"

bool InitDLL(HANDLE hPalInstance);
bool UninitDLL(HANDLE hPalInstance);
void DllThreadAttach(HANDLE hPalInstance);
void DllThreadDetach();

EXTERN_C UInt32_BOOL WINAPI RtuDllMain(HANDLE hPalInstance, UInt32 dwReason, void* /*pvReserved*/)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        {
            STARTUP_TIMELINE_EVENT(PROCESS_ATTACH_BEGIN);

            if (!InitDLL(hPalInstance))
                return FALSE;

            DllThreadAttach(hPalInstance);
            STARTUP_TIMELINE_EVENT(PROCESS_ATTACH_COMPLETE);
            return TRUE;
        }
        break;

    case DLL_PROCESS_DETACH:
        UninitDLL(hPalInstance);
        break;

    case DLL_THREAD_ATTACH:
        DllThreadAttach(hPalInstance);
        break;

    case DLL_THREAD_DETACH:
        DllThreadDetach();
        break;

    }

    return TRUE;
}



