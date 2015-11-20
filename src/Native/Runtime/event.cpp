//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "event.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "holder.h"
#include "Crst.h"
#include "RWLock.h"
#include "threadstore.h"

//
// -----------------------------------------------------------------------------------------------------------
//
// CLR wrapper around events. This version directly uses Win32 events (there's no support for host
// interception). 
//

void CLREventStatic::CreateManualEvent(bool bInitialState) 
{ 
    m_hEvent = PalCreateEventW(NULL, TRUE, bInitialState, NULL); 
    m_fInitialized = true;
}

void CLREventStatic::CreateAutoEvent(bool bInitialState) 
{ 
    m_hEvent = PalCreateEventW(NULL, FALSE, bInitialState, NULL); 
    m_fInitialized = true;
}

void CLREventStatic::CreateOSManualEvent(bool bInitialState) 
{ 
    m_hEvent = PalCreateEventW(NULL, TRUE, bInitialState, NULL); 
    m_fInitialized = true;
}

void CLREventStatic::CreateOSAutoEvent (bool bInitialState) 
{ 
    m_hEvent = PalCreateEventW(NULL, FALSE, bInitialState, NULL); 
    m_fInitialized = true;
}

void CLREventStatic::CloseEvent() 
{ 
    if (m_fInitialized && m_hEvent != INVALID_HANDLE_VALUE)
    { 
        PalCloseHandle(m_hEvent);
        m_hEvent = INVALID_HANDLE_VALUE;
    }
}

bool CLREventStatic::IsValid() const 
{ 
    return m_fInitialized && m_hEvent != INVALID_HANDLE_VALUE; 
}

bool CLREventStatic::Set() 
{ 
    if (!m_fInitialized)
        return false;
    return PalSetEvent(m_hEvent); 
}

bool CLREventStatic::Reset() 
{ 
    if (!m_fInitialized)
        return false;
    return PalResetEvent(m_hEvent); 
}

uint32_t CLREventStatic::Wait(uint32_t dwMilliseconds, bool bAlertable, bool bAllowReentrantWait)
{
    UInt32 result = WAIT_FAILED;

    if (m_fInitialized)
    {
        bool        disablePreemptive = false;
        Thread *    pCurThread  = ThreadStore::GetCurrentThreadIfAvailable();

        if (NULL != pCurThread)
        {
            if (pCurThread->PreemptiveGCDisabled())
            {
                pCurThread->EnablePreemptiveGC();
                disablePreemptive = true;
            }
        }

        result = PalCompatibleWaitAny(bAlertable, dwMilliseconds, 1, &m_hEvent, bAllowReentrantWait); 

        if (disablePreemptive)
        {
            pCurThread->DisablePreemptiveGC();
        }
    }

    return result;
}

HANDLE CLREventStatic::GetOSEvent()
{
    if (!m_fInitialized)
        return INVALID_HANDLE_VALUE;
    return m_hEvent;
}
