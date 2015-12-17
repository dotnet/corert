//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

// Abstracted thread ID.  This doesn't really belong in this file, but there is not currently any better place
// for it.  
class EEThreadId
{
public:
    EEThreadId(uint32_t uiId) : m_uiId(uiId) {}
#ifndef DACCESS_COMPILE
    bool IsSameThread();
#endif

private:
    uint32_t m_uiId;
};


//
// -----------------------------------------------------------------------------------------------------------
//
// Minimal Crst implementation based on CRITICAL_SECTION. Doesn't support much except for the basic locking
// functionality (in particular there is no rank violation checking).
//

enum CrstType
{
    CrstHandleTable,
    CrstDispatchCache,
    CrstAllocHeap,
    CrstGenericInstHashtab,
    CrstMemAccessMgr,
    CrstInterfaceDispatchGlobalLists,
    CrstStressLog,
    CrstRestrictedCallouts,
    CrstGcStressControl,
    CrstSuspendEE,
};

enum CrstFlags
{
    CRST_DEFAULT            = 0x0,
    CRST_REENTRANCY         = 0x0,
    CRST_UNSAFE_SAMELEVEL   = 0x0,
    CRST_UNSAFE_ANYMODE     = 0x0,
    CRST_DEBUGGER_THREAD    = 0x0,
};

// Static version of Crst with no default constructor (user must call Init() before use).
class CrstStatic
{
public:
    void Init(CrstType eType, CrstFlags eFlags = CRST_DEFAULT);
    bool InitNoThrow(CrstType eType, CrstFlags eFlags = CRST_DEFAULT) { Init(eType, eFlags); return true; }
    void Destroy();
    void Enter() { CrstStatic::Enter(this); }
    void Leave() { CrstStatic::Leave(this); }
    static void Enter(CrstStatic *pCrst);
    static void Leave(CrstStatic *pCrst);
#if defined(_DEBUG)
    bool OwnedByCurrentThread();
    EEThreadId GetHolderThreadId();
#endif // _DEBUG

private:
    CRITICAL_SECTION    m_sCritSec;
#if defined(_DEBUG)
    uint32_t            m_uiOwnerId;
    static const uint32_t UNOWNED = 0;
#endif // _DEBUG
};

// Non-static version that will initialize itself during construction.
class Crst : public CrstStatic
{
public:
    Crst(CrstType eType, CrstFlags eFlags = CRST_DEFAULT)
        : CrstStatic()
    { Init(eType, eFlags); }
};

// Holder for a Crst instance.
class CrstHolder
{
    CrstStatic * m_pLock;

public:
    CrstHolder(CrstStatic * pLock)
        : m_pLock(pLock)
    {
        m_pLock->Enter();
    }

    ~CrstHolder()
    {
        m_pLock->Leave();
    }
};

class CrstHolderWithState
{
    CrstStatic * m_pLock;
    bool m_fAcquired;

public:
    CrstHolderWithState(CrstStatic * pLock, bool fAcquire = true)
        : m_pLock(pLock), m_fAcquired(fAcquire)
    {
        if (fAcquire)
            m_pLock->Enter();
    }

    ~CrstHolderWithState()
    {
        if (m_fAcquired)
            m_pLock->Leave();
    }

    void Acquire()
    {
        if (!m_fAcquired)
        {
            m_pLock->Enter();
            m_fAcquired = true;
        }
    }

    void Release()
    {
        if (m_fAcquired)
        {
            m_pLock->Leave();
            m_fAcquired = false;
        }
    }

    CrstStatic * GetValue()
    {
        return m_pLock;
    }
};
