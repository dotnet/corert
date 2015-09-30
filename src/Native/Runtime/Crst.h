//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

// Abstracted thread ID.  This doesn't really belong in this file, but there is not currently any better place
// for it.  
class EEThreadId
{
public:
    EEThreadId(UInt32 uiId) : m_uiId(uiId) {}
#ifndef DACCESS_COMPILE
    bool IsSameThread() { return PalGetCurrentThreadId() == m_uiId; }
#endif

private:
    UInt32 m_uiId;
};


//
// -----------------------------------------------------------------------------------------------------------
//
// Minimal Crst implementation based on CRITICAL_SECTION. Doesn't support much except for the basic locking
// functionality (in particular there is no rank violation checking, but then again there's only one Crst type
// used currently).
//

enum CrstType
{
    CrstHandleTable,
    CrstInstanceStore,
    CrstThreadStore,
    CrstDispatchCache,
    CrstAllocHeap,
    CrstModuleList,
    CrstGenericInstHashtab,
    CrstMemAccessMgr,
    CrstInterfaceDispatchGlobalLists,
    CrstStressLog,
    CrstRestrictedCallouts,
    CrstGcStressControl,
};

enum CrstFlags
{
    CRST_DEFAULT    = 0x0,
};

// Static version of Crst with no default constructor (user must call Init() before use).
class CrstStatic
{
public:
    void Init(CrstType eType, CrstFlags eFlags = CRST_DEFAULT);
    bool InitNoThrow(CrstType eType, CrstFlags eFlags = CRST_DEFAULT) { Init(eType, eFlags); return true; }
    void Destroy();
    static void Enter(CrstStatic *pCrst);
    static void Leave(CrstStatic *pCrst);
#if defined(_DEBUG)
    bool OwnedByCurrentThread();
    EEThreadId GetHolderThreadId();
#endif // _DEBUG

private:
    CRITICAL_SECTION    m_sCritSec;
#if defined(_DEBUG)
    UInt32              m_uiOwnerId;
    static const UInt32 UNOWNED = 0;
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
class CrstHolder : public Holder<CrstStatic*, CrstStatic::Enter, CrstStatic::Leave>
{
public:
    CrstHolder(CrstStatic *pCrst, bool fTake = true) : Holder(pCrst, fTake) {}
    ~CrstHolder() {}
};

// The CLR has split the Crst holders into CrstHolder which only supports acquire on construction/release on
// destruction semantics and CrstHolderWithState, with the old, fully flexible semantics. We don't support the
// split yet so both types are equivalent.
typedef CrstHolder CrstHolderWithState;
