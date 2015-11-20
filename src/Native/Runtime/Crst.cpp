//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "holder.h"
#include "Crst.h"

#ifndef DACCESS_COMPILE
bool EEThreadId::IsSameThread()
{
    return PalGetCurrentThreadId() == m_uiId;
}
#endif // DACCESS_COMPILE

void CrstStatic::Init(CrstType eType, CrstFlags eFlags)
{
    UNREFERENCED_PARAMETER(eType);
    UNREFERENCED_PARAMETER(eFlags);
#ifndef DACCESS_COMPILE
#if defined(_DEBUG)
    m_uiOwnerId = UNOWNED;
#endif // _DEBUG
    PalInitializeCriticalSectionEx(&m_sCritSec, 0, 0);
#endif // !DACCESS_COMPILE
}

void CrstStatic::Destroy()
{
#ifndef DACCESS_COMPILE
    PalDeleteCriticalSection(&m_sCritSec);
#endif // !DACCESS_COMPILE
}

// static 
void CrstStatic::Enter(CrstStatic *pCrst)
{
#ifndef DACCESS_COMPILE
    PalEnterCriticalSection(&pCrst->m_sCritSec);
#if defined(_DEBUG)
    pCrst->m_uiOwnerId = PalGetCurrentThreadId();
#endif // _DEBUG
#else
    UNREFERENCED_PARAMETER(pCrst);
#endif // !DACCESS_COMPILE
}

// static 
void CrstStatic::Leave(CrstStatic *pCrst)
{
#ifndef DACCESS_COMPILE
#if defined(_DEBUG)
    pCrst->m_uiOwnerId = UNOWNED;
#endif // _DEBUG
    PalLeaveCriticalSection(&pCrst->m_sCritSec);
#else
    UNREFERENCED_PARAMETER(pCrst);
#endif // !DACCESS_COMPILE
}

#if defined(_DEBUG)
bool CrstStatic::OwnedByCurrentThread()
{
#ifndef DACCESS_COMPILE
    return m_uiOwnerId == PalGetCurrentThreadId();
#else
    return false;
#endif
}

EEThreadId CrstStatic::GetHolderThreadId()
{
    return EEThreadId(m_uiOwnerId);
}
#endif // _DEBUG
