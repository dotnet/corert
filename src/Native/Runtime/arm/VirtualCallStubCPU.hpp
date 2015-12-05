//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//
// File: virtualcallstubcpu.hpp
//
// ============================================================================

#ifndef _VIRTUAL_CALL_STUB_ARM_H
#define _VIRTUAL_CALL_STUB_ARM_H

// On various types of control flow redirection (calls, branches, direct moves to the PC etc.) the ARM uses
// the low-order bit of the target PC to determine the instruction set mode for the destination: 0 == ARM, 1
// == Thumb. Windows on ARM supports only Thumb mode, so we must ensure target addresses used in such control
// flow situations (known as interworking branches) have the low-order bit set. This is done for us when
// taking the address of a function in C++, but in situations where we manipulate a target address as both a
// data address and an entrypoint, we need to be careful with out bookkeeping (masking out the low order bit
// when using the address for data access purposes, setting the bit in cases where we may branch to it). The
// following functions encapsulate these operations.
template <typename ResultType, typename SourceType>
inline ResultType DataPointerToThumbCode(SourceType pCode)
{
    return (ResultType)(((UIntNative)pCode) | 1);
}

template <typename ResultType, typename SourceType>
inline ResultType ThumbCodeToDataPointer(SourceType pCode)
{
    return (ResultType)(((UIntNative)pCode) & ~1);
}

/*-----------------------------------------------------------------------------------------------------------
Stubs that contain code are all part of larger structs called Holders.  There is a
Holder for each kind of stub, i.e XXXStub is contained with XXXHolder.  Holders are
essentially an implementation trick that allowed rearranging the code sequences more
easily while trying out different alternatives, and for dealing with any alignment 
issues in a way that was mostly immune to the actually code sequences.  These Holders
should be revisited when the stub code sequences are fixed, since in many cases they
add extra space to a stub that is not really needed.  

Stubs are placed in cache and hash tables.  Since unaligned access of data in memory
is very slow, the keys used in those tables should be aligned.  The things used as keys
typically also occur in the generated code, e.g. a token as an immediate part of an instruction.
For now, to avoid alignment computations as different code strategies are tried out, the key
fields are all in the Holders.  Eventually, many of these fields should be dropped, and the instruction
streams aligned so that the immediate fields fall on aligned boundaries.  
*/

struct DispatchStub;
struct DispatchHolder;

/*--DispatchStub---------------------------------------------------------------------------------------------
Monomorphic and mostly monomorphic call sites eventually point to DispatchStubs.
A dispatch stub has an expected type (expectedType), target address (target) and fail address (failure).  
If the calling frame does in fact have the <this> type be of the expected type, then
control is transfered to the target address, the method implementation.  If not, 
then control is transfered to the fail address, a fail stub (see below) where a polymorphic 
lookup is done to find the correct address to go to.  

implementation note: Order, choice of instructions, and branch directions
should be carefully tuned since it can have an inordinate effect on performance.  Particular
attention needs to be paid to the effects on the BTB and branch prediction, both in the small
and in the large, i.e. it needs to run well in the face of BTB overflow--using static predictions.
Note that since this stub is only used for mostly monomorphic callsites (ones that are not, get patched
to something else), therefore the conditional jump "jne failure" is mostly not taken, and hence it is important
that the branch prediction staticly predict this, which means it must be a forward jump.  The alternative 
is to reverse the order of the jumps and make sure that the resulting conditional jump "je implTarget" 
is statically predicted as taken, i.e a backward jump. The current choice was taken since it was easier
to control the placement of the stubs than control the placement of the jitted code and the stubs. */
struct DispatchStubCode
{
    const static int entryPointLen = 13 + 1 /* padding */;

    UInt16      _entryPoint[entryPointLen];
    UIntNative  _expectedType;
    PTR_Code    _failTarget;
    PTR_Code    _implTarget;
};

typedef DPTR(DPTR(DispatchStub)) PTR_PTR_DispatchStub;
struct DispatchStub : private DispatchStubCode
{
    inline PTR_Code entryPoint()            { return DataPointerToThumbCode<PTR_Code>(_entryPoint); }

    inline UIntNative    expectedType()     { return _expectedType; }
    inline PTR_Code implTarget()            { return _implTarget; }
    inline PTR_Code failTarget()            { return _failTarget; }
    inline static UIntNative size()         { return sizeof(DispatchStub); }

    inline DispatchStub& operator=(DispatchStubCode const & code) { *static_cast<DispatchStubCode*>(this) = code; return *this; }

private:
    inline PTR_PTR_DispatchStub SList_GetNextPtr()
    {
        ASSERT(((dac_cast<TADDR>(this) + offsetof(DispatchStub, _expectedType)) % sizeof(void*)) == 0);
        return dac_cast<PTR_PTR_DispatchStub>(dac_cast<TADDR>(this) + offsetof(DispatchStub, _expectedType));
    }

    friend struct DispatchHolder;
    friend struct VSDStubSListTraits<DispatchStub>;
};

/*-----------------------------------------------------------------------------------------------------------
DispatchHolders are the containers for DispatchStubs, they provide for any alignment of 
stubs as necessary.  DispatchStubs are placed in a hashtable and in a cache.  The keys for both
are the pair expectedType and token.  Efficiency of the of the hash table is not a big issue,
since lookups in it are fairly rare.  Efficiency of the cache is paramount since it is accessed frequently
o(see ResolveStub below).  Currently we are storing both of these fields in the DispatchHolder to simplify
alignment issues.  If inlineMT in the stub itself was aligned, then it could be the expectedType field.
While the token field can be logically gotten by following the failure target to the failEntryPoint 
of the ResolveStub and then to the token over there, for perf reasons of cache access, it is duplicated here.
This allows us to use DispatchStubs in the cache.  The alternative is to provide some other immutable struct
for the cache composed of the triplet (expectedType, token, target) and some sort of reclaimation scheme when
they are thrown out of the cache via overwrites (since concurrency will make the obvious approaches invalid).
*/

struct DispatchHolder
{
    static void InitializeStatic();

    void  Initialize(PTR_Code implTarget,
                     PTR_Code failTarget,
                     UIntNative expectedType);

    DispatchStub* stub()
        { return &_stub; }

    static DispatchHolder*  FromStub(DispatchStub *stub);
    static DispatchHolder*  FromDispatchEntryPoint(PTR_Code dispatchEntry);

private:
    DispatchStub _stub;

    // Tail alignment is not needed, as stubs are allocated using AllocHeap::AllocAligned,
    // which arranges that the start of the stub is properly aligned.
};

struct ResolveStub;
struct ResolveHolder;

/*--ResolveStub----------------------------------------------------------------------------------------------
Polymorphic call sites and monomorphic calls that fail end up in a ResolverStub.  There is only 
one resolver stub built for any given token, even though there may be many call sites that
use that token and many distinct <this> types that are used in the calling call frames.  A resolver stub 
actually has two entry points, one for polymorphic call sites and one for dispatch stubs that fail on their
expectedType test.  There is a third part of the resolver stub that enters the ee when a decision should
be made about changing the callsite.  Therefore, we have defined the resolver stub as three distinct pieces,
even though they are actually allocated as a single contiguous block of memory.  These pieces are:

A ResolveStub has two entry points:

FailEntry - where the dispatch stub goes if the expected MT test fails.  This piece of the stub does
a check to see how often we are actually failing. If failures are frequent, control transfers to the 
patch piece to cause the call site to be changed from a mostly monomorphic callsite 
(calls dispatch stub) to a polymorphic callsize (calls resolve stub).  If failures are rare, control
transfers to the resolve piece (see ResolveStub).  The failEntryPoint decrements a counter 
every time it is entered.  The ee at various times will add a large chunk to the counter. 

ResolveEntry - does a lookup via in a cache by hashing the actual type of the calling frame s
<this> and the token identifying the (contract,method) pair desired.  If found, control is transfered
to the method implementation.  If not found in the cache, the token is pushed and the ee is entered via
the ResolveWorkerStub to do a full lookup and eventual transfer to the correct method implementation.  Since
there is a different resolve stub for every token, the token can be inlined and the token can be pre-hashed.
The effectiveness of this approach is highly sensitive to the effectiveness of the hashing algorithm used,
as well as its speed.  It turns out it is very important to make the hash function sensitive to all 
of the bits of the method table, as method tables are laid out in memory in a very non-random way.  Before
making any changes to the code sequences here, it is very important to measure and tune them as perf
can vary greatly, in unexpected ways, with seeming minor changes.

Implementation note - Order, choice of instructions, and branch directions
should be carefully tuned since it can have an inordinate effect on performance.  Particular
attention needs to be paid to the effects on the BTB and branch prediction, both in the small
and in the large, i.e. it needs to run well in the face of BTB overflow--using static predictions. 
Note that this stub is called in highly polymorphic cases, but the cache should have been sized
and the hash function chosen to maximize the cache hit case.  Hence the cmp/jcc instructions should
mostly be going down the cache hit route, and it is important that this be statically predicted as so.
Hence the 3 jcc instrs need to be forward jumps.  As structured, there is only one jmp/jcc that typically
gets put in the BTB since all the others typically fall straight thru.  Minimizing potential BTB entries
is important. */

struct ResolveStubCode
{
    const static int resolveEntryPointLen = 46;
    const static int failEntryPointLen = 20;

    UInt16      _resolveEntryPoint[resolveEntryPointLen];
    UInt16      _failEntryPoint[failEntryPointLen];
    Int32 *     _pCounter;
    UInt32      _hashedToken;
    void *      _cacheAddress; // lookupCache
    void *      _itfType;
    UInt16      _slotNumber;
    UInt32      _cacheMask;
    PTR_Code    _resolveWorkerTarget;
    PTR_Code    _backpatcherTarget;
};

typedef DPTR(DPTR(struct ResolveStub)) PTR_PTR_ResolveStub;
struct ResolveStub : private ResolveStubCode
{
    inline PTR_Code failEntryPoint()            { return DataPointerToThumbCode<PTR_Code>(_failEntryPoint); }
    inline const PTR_Code resolveEntryPoint()   { return DataPointerToThumbCode<PTR_Code>(_resolveEntryPoint); }

    inline Int32*  pCounter()                { return _pCounter; }
    inline UInt32  hashedToken()             { return _hashedToken >> /*LOG2_PTRSIZE*/2;    }
    inline void*  cacheAddress()             { return _cacheAddress; }
    inline EEType*  tgtItfType()             { return reinterpret_cast<EEType*>(_itfType); }
    inline UInt16 tgtItfSlotNumber()         { return _slotNumber; }
    inline static UIntNative  size()         { return sizeof(ResolveStub); }
    VSDInterfaceTargetInfo tgtItfInfo()      { return VSDInterfaceTargetInfo(tgtItfType(), tgtItfSlotNumber()); }

    inline ResolveStub& operator=(ResolveStubCode const & code) { *static_cast<ResolveStubCode*>(this) = code; return *this; }

private:
    inline PTR_PTR_ResolveStub SList_GetNextPtr()
    {
        ASSERT(((dac_cast<TADDR>(this) + offsetof(ResolveStub, _itfType)) % sizeof(void*)) == 0);
        return dac_cast<PTR_PTR_ResolveStub>(dac_cast<TADDR>(this) + offsetof(ResolveStub, _itfType));
    }

    friend struct ResolveHolder;
    friend struct VSDStubSListTraits<ResolveStub>;
};

/*-----------------------------------------------------------------------------------------------------------
ResolveHolders are the containers for ResolveStubs,  They provide 
for any alignment of the stubs as necessary. The stubs are placed in a hash table keyed by 
the token for which they are built.  Efficiency of access requires that this token be aligned.  
For now, we have copied that field into the ResolveHolder itself, if the resolve stub is arranged such that
any of its inlined tokens (non-prehashed) is aligned, then the token field in the ResolveHolder
is not needed. */ 
struct ResolveHolder
{
    static void  InitializeStatic();

    void  Initialize(
        const UInt8 * resolveWorkerTarget,
        const UInt8 * patcherTarget, 
        EEType * pItfType,
        UInt16 itfSlotNumber,
        UInt32 hashedToken,
        void * cacheAddr,
        Int32 * counterAddr);

    ResolveStub* stub()      { return &_stub; }

    static ResolveHolder*  FromStub(ResolveStub * pStub);
    static ResolveHolder*  FromFailEntryPoint(PTR_Code failEntry);
    static ResolveHolder*  FromResolveEntryPoint(PTR_Code resolveEntry);

private:
    ResolveStub _stub;

    // Tail alignment is not needed, as stubs are allocated using AllocHeap::AllocAligned,
    // which arranges that the start of the stub is properly aligned.
};

#ifdef DECLARE_DATA

#ifndef DACCESS_COMPILE

//-----------------------------------------------------------------------------------------------------------
/* static */
const UInt8 **StubCallSite::ComputeIndirCellAddr(const UInt8 *returnAddr, const UInt8 **indirCellAddrForRegisterIndirect)
{ 
    // Redhawk on ARM always generates the following code sequence for VSD callsites:
    //
    //  0xf8df 0xc<offset>  ldr r12, [pc, #offset]
    //  0xf8dc 0xc000       ldr r12, [r12]
    //  0x47e0              blx r12
    //
    // With the combination of the return PC and the 12-bit offset we can determine the address of the
    // indirection cell in the caller's local constant pool.
    //
    // Some things to bear in mind:
    //  1) The return address will have the bottom bit set to indicate a return to thumb code.
    //  2) The offset in the initial ldr instruction is relative to the start of the instruction aligned down
    //     to a 4-byte boundary + 4.

    returnAddr = (UInt8*)((UIntNative)returnAddr & ~1);

    ASSERT(*(UInt16*)(returnAddr - 2) == 0x47e0);
    ASSERT(*(UInt16*)(returnAddr - 4) == 0xc000);
    ASSERT(*(UInt16*)(returnAddr - 6) == 0xf8dc);
    ASSERT((*(UInt16*)(returnAddr - 8) & 0xf000) == 0xc000);
    ASSERT(*(UInt16*)(returnAddr - 10) == 0xf8df);

    UInt32 instrOffset = (*(UInt16*)(returnAddr - 8) & 0x0fff);
    const UInt8 *** literalAddr = (const UInt8***)(((UIntNative)(returnAddr - 10) & ~3) + 4 + instrOffset);

    return *literalAddr;
}

//-----------------------------------------------------------------------------------------------------------
void DispatchHolder::InitializeStatic()
{
    // Check that _expectedType is aligned in the DispatchHolder
    static_assert(((offsetof(DispatchHolder, _stub) + offsetof(DispatchStub, _expectedType)) % sizeof(void*)) == 0, "_expectedType is misaligned");
};

//-----------------------------------------------------------------------------------------------------------
void  DispatchHolder::Initialize(
    PTR_Code implTarget,
    PTR_Code failTarget,
    UIntNative expectedType)
{
    // Called directly by JITTED code
    // DispatchHolder._stub._entryPoint(r0:object, r1, r2, r3)
    // {
    //     if (r0.methodTable == this._expectedType) (this._implTarget)(r0, r1, r2, r3);
    //     else (this._failTarget)(r0, r1, r2, r3);
    // }

    int n = 0;
    int offset;

    // We rely on the stub entry-point being UInt32 aligned (so we can tell whether any subsequent UInt16 is
    // UInt32-aligned or not, which matters in the calculation of PC-relative offsets).
    ASSERT(((UIntNative)_stub._entryPoint & 0x3) == 0);

// Compute a PC-relative offset for use in an instruction encoding. Must call this prior to emitting the
// instruction halfword to which it applies. For thumb-2 encodings the offset must be computed before emitting
// the first of the halfwords.
#undef PC_REL_OFFSET
#define PC_REL_OFFSET(_field) (UInt16)(offsetof(DispatchStub, _field) - (offsetof(DispatchStub, _entryPoint[n + 2]) & 0xfffffffc))

    // ldr r12, [pc + #_expectedType]
    offset = PC_REL_OFFSET(_expectedType);
    _stub._entryPoint[n++] = 0xf8df;
    _stub._entryPoint[n++] = (UInt16)(0xc000 | offset);

    // push {r5}
    _stub._entryPoint[n++] = 0xf84d;
    _stub._entryPoint[n++] = 0x5d04;

    // ldr r5, [r0 + #Object.m_pMethTab]
    _stub._entryPoint[n++] = 0x6805;

    // cmp r5, r12
    _stub._entryPoint[n++] = 0x4565;

    // pop {r5}
    _stub._entryPoint[n++] = 0xf85d;
    _stub._entryPoint[n++] = 0x5b04;

    // it eq
    _stub._entryPoint[n++] = 0xbf08;

    // ldr[eq] pc, [pc + #_implTarget]
    offset = PC_REL_OFFSET(_implTarget);
    _stub._entryPoint[n++] = 0xf8df;
    _stub._entryPoint[n++] = (UInt16)(0xf000 | offset);

    // ldr pc, [pc + #_failTarget]
    offset = PC_REL_OFFSET(_failTarget);
    _stub._entryPoint[n++] = 0xf8df;
    _stub._entryPoint[n++] = (UInt16)(0xf000 | offset);

    // nop - insert padding
    _stub._entryPoint[n++] = 0xbf00;

    ASSERT(n == DispatchStub::entryPointLen);

    // Make sure that the data members below are aligned
    ASSERT((n & 1) == 0);

    _stub._expectedType = expectedType;
    _stub._failTarget = failTarget;
    _stub._implTarget = implTarget;
}

//-----------------------------------------------------------------------------------------------------------
DispatchHolder* DispatchHolder::FromStub(DispatchStub * pStub)
{
    DispatchHolder *dispatchHolder = 
        reinterpret_cast<DispatchHolder*>(reinterpret_cast<UInt8*>(pStub) - offsetof(DispatchHolder, _stub));
    return dispatchHolder;
}

//-----------------------------------------------------------------------------------------------------------
DispatchHolder* DispatchHolder::FromDispatchEntryPoint(PTR_Code dispatchEntry)
{ 
    DispatchStub *pStub = reinterpret_cast<DispatchStub*>(ThumbCodeToDataPointer<UInt8*>(dispatchEntry) - offsetof(DispatchStub, _entryPoint));
    return FromStub(pStub);
}


//-----------------------------------------------------------------------------------------------------------
void ResolveHolder::InitializeStatic()
{
    // Check that _itfType is aligned in ResolveHolder
    static_assert(((offsetof(ResolveHolder, _stub) + offsetof(ResolveStub, _itfType)) % sizeof(void*)) == 0, "_itfType is misaligned");
};

//-----------------------------------------------------------------------------------------------------------
void  ResolveHolder::Initialize(
    const UInt8 * resolveWorkerTarget,
    const UInt8 * patcherTarget, 
    EEType * pItfType,
    UInt16 itfSlotNumber,
    UInt32 hashedToken,
    void * cacheAddr,
    Int32 * counterAddr)
{
    // Called directly by Redhawk code.
    //
    // ResolveStub._resolveEntryPoint(r0:Object*, r1, r2, r3)
    // {
    //    MethodTable mt = r0.m_pMethTab;
    //    int i = ((mt + mt >> 12) ^ this._hashedToken) & this._cacheMask
    //    ResolveCacheElem e = this._cacheAddress + i
    //    do
    //    {
    //        if (mt == e.pTgtType &&
    //            this._itfType == e.targetInfo.m_pItf &&
    //            this._slotNumber == e.targetInfo.m_slotNumber)
    //        {
    //           (e.target)(r0, r1, r2, r3);
    //        }
    //        e = e.pNext;
    //    } while (e != null)
    //    VSDResolveWorkerAsmStub(r0, r1, r2, r3);
    // }
    //

    int n = 0;
    int offset;

    // We rely on the stub entry-point being UInt32 aligned (so we can tell whether any subsequent UInt16 is
    // UInt32-aligned or not, which matters in the calculation of PC-relative offsets).
    ASSERT(((UIntNative)_stub._resolveEntryPoint & 0x3) == 0);

// Compute a PC-relative offset for use in an instruction encoding. Must call this prior to emitting the
// instruction halfword to which it applies. For thumb-2 encodings the offset must be computed before emitting
// the first of the halfwords.
#undef PC_REL_OFFSET
#define PC_REL_OFFSET(_field) (UInt16)(offsetof(ResolveStub, _field) - (offsetof(ResolveStub, _resolveEntryPoint[n + 2]) & 0xfffffffc))

    // ;; We need two scratch registers, r5 and r6
    // push {r5,r6}
    _stub._resolveEntryPoint[n++] = 0xb460;

    // ;; Compute i = ((mt + mt >> 12) ^ this._hashedToken) & this._cacheMask
    // ldr r6, [r0 + #Object.m_pMethTab]
    _stub._resolveEntryPoint[n++] = 0x6806;

    // add r12, r6, r6 lsr #12
    _stub._resolveEntryPoint[n++] = 0xeb06;
    _stub._resolveEntryPoint[n++] = 0x3c16;

    // ldr r5, [pc + #_hashedToken]
    offset = PC_REL_OFFSET(_hashedToken);
    _stub._resolveEntryPoint[n++] = 0xf8df;
    _stub._resolveEntryPoint[n++] = (UInt16)(0x5000 | offset);

    // eor r12, r12, r5
    _stub._resolveEntryPoint[n++] = 0xea8c;
    _stub._resolveEntryPoint[n++] = 0x0c05;

    // ldr r5, [pc + #_cacheMask]
    offset = PC_REL_OFFSET(_cacheMask);
    _stub._resolveEntryPoint[n++] = 0xf8df;
    _stub._resolveEntryPoint[n++] = (UInt16)(0x5000 | offset);

    // and r12, r12, r5
    _stub._resolveEntryPoint[n++] = 0xea0c;
    _stub._resolveEntryPoint[n++] = 0x0c05;

    // ;; ResolveCacheElem e = this._cacheAddress + i
    // ldr r5, [pc + #_cacheAddress]
    offset = PC_REL_OFFSET(_cacheAddress);
    _stub._resolveEntryPoint[n++] = 0xf8df;
    _stub._resolveEntryPoint[n++] = (UInt16)(0x5000 | offset);

    // ldr r12, [r5 + r12] ;; r12 = e = this._cacheAddress + i
    _stub._resolveEntryPoint[n++] = 0xf855;
    _stub._resolveEntryPoint[n++] = 0xc00c;

    // ;; do {
    int loop = n;

    // ;; Check mt == e.pTgtType
    // ldr r5, [r12 + #ResolveCacheElem.pTgtType]
    offset = offsetof(ResolveCacheElem, pTgtType);
    _stub._resolveEntryPoint[n++] = 0xf8dc;
    _stub._resolveEntryPoint[n++] = (UInt16)(0x5000 | offset);

    // cmp r6, r5
    _stub._resolveEntryPoint[n++] = 0x42ae;

    // ittt eq
    _stub._resolveEntryPoint[n++] = 0xbf02;

    // ;; Check this._itfType == e.targetInfo.m_pItf
    // ldr[eq] r5, [pc + #_itfType]
    offset = PC_REL_OFFSET(_itfType);
    _stub._resolveEntryPoint[n++] = 0xf8df;
    _stub._resolveEntryPoint[n++] = (UInt16)(0x5000 | offset);

    // ldr[eq] r6, [r12 + #ResolveCacheElem.targetInfo.m_pItf]
    offset = offsetof(ResolveCacheElem, targetInfo) + offsetof(VSDInterfaceTargetInfo, m_pItf);
    _stub._resolveEntryPoint[n++] = 0xf8dc;
    _stub._resolveEntryPoint[n++] = (UInt16)(0x6000 | offset);

    // cmp[eq] r6, r5
    _stub._resolveEntryPoint[n++] = 0x42ae;

    // ittt eq
    _stub._resolveEntryPoint[n++] = 0xbf02;

    // ;; Check this._slotNumber == e.targetInfo.m_slotNumber
    // ldrh[eq] r5, [pc + #_slotNumber]
    offset = PC_REL_OFFSET(_slotNumber);
    _stub._resolveEntryPoint[n++] = 0xf8bf;
    _stub._resolveEntryPoint[n++] = (UInt16)(0x5000 | offset);

    // ldrh[eq] r6, [r12 + #ResolveCacheElem.targetInfo.m_slotNumber]
    offset = offsetof(ResolveCacheElem, targetInfo) + offsetof(VSDInterfaceTargetInfo, m_slotNumber);
    _stub._resolveEntryPoint[n++] = 0xf8bc;
    _stub._resolveEntryPoint[n++] = (UInt16)(0x6000 | offset);

    // cmp[eq] r6, r5
    _stub._resolveEntryPoint[n++] = 0x42ae;

    // itt eq
    _stub._resolveEntryPoint[n++] = 0xbf04;

    // ;; Restore r5 and r6
    // pop[eq] {r5,r6}
    _stub._resolveEntryPoint[n++] = 0xbc60;

    // ;; Conditionally branch to e.target
    // ldr[eq] pc, [r12 + #ResolveCacheElem.target] ;; (e.target)(r0,r1,r2,r3)
    offset = offsetof(ResolveCacheElem, target);
    _stub._resolveEntryPoint[n++] = 0xf8dc;
    _stub._resolveEntryPoint[n++] = (UInt16)(0xf000 | offset);

    // ;; e = e.pNext;
    // ldr r12, [r12 + #ResolveCacheElem.pNext]
    offset = offsetof(ResolveCacheElem, pNext);
    _stub._resolveEntryPoint[n++] = 0xf8dc;
    _stub._resolveEntryPoint[n++] = (UInt16)(0xc000 | offset);

    // ;; } while(e != null);
    // cmp r12, #0
    _stub._resolveEntryPoint[n++] = 0xf1bc;
    _stub._resolveEntryPoint[n++] = 0x0f00;

    // itt ne
    _stub._resolveEntryPoint[n++] = 0xbf1c;

    // ldr[ne] r6, [r0 + #Object.m_pMethTab]
    _stub._resolveEntryPoint[n++] = 0x6806;

    // b[ne] loop
    offset = (loop - (n + 2)) * sizeof(UInt16);
    ASSERT(offset > -4096);
    _stub._resolveEntryPoint[n++] = (UInt16)(0xe000 | ((offset >> 1) & 0x7ff));

    // pop {r5,r6}
    _stub._resolveEntryPoint[n++] = 0xbc60;

    // ;; VSDResolveWorkerAsmStub(r0, r1, r2, r3);
    // ldr pc, [pc + #_resolveWorkerTarget]
    offset = PC_REL_OFFSET(_resolveWorkerTarget);
    _stub._resolveEntryPoint[n++] = 0xf8df;
    _stub._resolveEntryPoint[n++] = (UInt16)(0xf000 | offset);

    // Insert a nop just to UInt32-align the slow entry point (see ASSERT below).
    // nop
    _stub._resolveEntryPoint[n++] = 0xbf00;

    // ResolveStub._failEntryPoint(r0:MethodToken, r1, r2, r3, r4:IndirectionCell)
    // {
    //     if (--(*this._pCounter) < 0)
    //       VSDBackPatchWorkerAsmStub(r0, r1, r2, r3);
    //     this._resolveEntryPoint(r0, r1, r2, r3);
    // }

    // The following macro relies on this entry point being UInt32-aligned. We've already asserted that the
    // overall stub is aligned above, just need to check that the preceding stubs occupy an even number of
    // UInt16 slots.
    ASSERT((n & 1) == 0);

#undef PC_REL_OFFSET
#define PC_REL_OFFSET(_field) (UInt16)(offsetof(ResolveStub, _field) - (offsetof(ResolveStub, _failEntryPoint[n + 2]) & 0xfffffffc))

    n = 0;

    // push {r5}
    _stub._failEntryPoint[n++] = 0xf84d;
    _stub._failEntryPoint[n++] = 0x5d04;

    // ldr r5, [pc + #_pCounter]
    offset = PC_REL_OFFSET(_pCounter);
    _stub._failEntryPoint[n++] = 0xf8df;
    _stub._failEntryPoint[n++] = (UInt16)(0x5000 | offset);

    // ldr r12, [r5]
    _stub._failEntryPoint[n++] = 0xf8d5;
    _stub._failEntryPoint[n++] = 0xc000;

    // subs r12, r12, #1
    _stub._failEntryPoint[n++] = 0xf1bc;
    _stub._failEntryPoint[n++] = 0x0c01;

    // str r12, [r5]
    _stub._failEntryPoint[n++] = 0xf8c5;
    _stub._failEntryPoint[n++] = 0xc000;

    // pop {r5}
    _stub._failEntryPoint[n++] = 0xf85d;
    _stub._failEntryPoint[n++] = 0x5b04;

    // bge _resolveEntryPoint
    offset = offsetof(ResolveStub, _resolveEntryPoint) - offsetof(ResolveStub, _failEntryPoint[n + 2]);
    ASSERT((offset & 1) == 0);
    ASSERT(offset > -512);
    _stub._failEntryPoint[n++] = (UInt16)(0xda00 | ((offset >> 1) & 0xff));

    // We need to save LR because of the upcoming call. But to maintain 8-byte stack alignment at the callsite
    // (as the ABI requires) we push R4 as well.
    // push {r4,lr}
    _stub._failEntryPoint[n++] = 0xb510;

    // ldr r12, [pc + #_backpatcherTarget]
    offset = PC_REL_OFFSET(_backpatcherTarget);
    _stub._failEntryPoint[n++] = 0xf8df;
    _stub._failEntryPoint[n++] = (UInt16)(0xc000 | offset);

    // blx r12
    _stub._failEntryPoint[n++] = 0x47e0;

    // pop {r4,lr}
    _stub._failEntryPoint[n++] = 0xe8bd;
    _stub._failEntryPoint[n++] = 0x4010;

    // b _resolveEntryPoint
    offset = offsetof(ResolveStub, _resolveEntryPoint) - offsetof(ResolveStub, _failEntryPoint[n + 2]);
    ASSERT((offset & 1) == 0);
    ASSERT(offset > -4096);
    _stub._failEntryPoint[n++] = (UInt16)(0xe000 | ((offset >> 1) & 0x7ff));

    ASSERT(n == ResolveStub::failEntryPointLen);

    //fill in the stub specific fields
    _stub._pCounter             = counterAddr;
    _stub._hashedToken          = hashedToken << 2;
    _stub._cacheAddress         = cacheAddr;
    _stub._itfType              = pItfType;
    _stub._slotNumber           = itfSlotNumber;
    _stub._cacheMask            = CALL_STUB_CACHE_MASK << LOG2_PTRSIZE;
    _stub._resolveWorkerTarget  = (PTR_Code)resolveWorkerTarget;
    _stub._backpatcherTarget    = (PTR_Code)patcherTarget;
}

//-----------------------------------------------------------------------------------------------------------
ResolveHolder* ResolveHolder::FromStub(ResolveStub * pStub)
{
    ResolveHolder *resolveHolder =
        reinterpret_cast<ResolveHolder*>(reinterpret_cast<UInt8*>(pStub) - offsetof(ResolveHolder, _stub));
    return resolveHolder;
}

//-----------------------------------------------------------------------------------------------------------
ResolveHolder* ResolveHolder::FromFailEntryPoint(PTR_Code failEntry)
{ 
    ResolveStub *pStub = reinterpret_cast<ResolveStub*>(ThumbCodeToDataPointer<UInt8*>(failEntry) - offsetof(ResolveStub, _failEntryPoint));
    return FromStub(pStub);
}

//-----------------------------------------------------------------------------------------------------------
ResolveHolder* ResolveHolder::FromResolveEntryPoint(PTR_Code resolveEntry)
{ 
    ResolveStub *pStub = reinterpret_cast<ResolveStub*>(ThumbCodeToDataPointer<UInt8*>(resolveEntry) - offsetof(ResolveStub, _resolveEntryPoint));
    return FromStub(pStub);
}

#endif // DACCESS_COMPILE

//-----------------------------------------------------------------------------------------------------------
/* static */
VirtualCallStubManager::StubKind VirtualCallStubManager::DecodeStubKind(PTR_Code stubStartAddress)
{
    StubKind stubKind = SK_LOOKUP;

    UInt16 const *codeWordPtr = ThumbCodeToDataPointer<UInt16*>(stubStartAddress);
    UInt16 firstWord = codeWordPtr[0];

    if (firstWord == 0xf8df && codeWordPtr[1] != 0xc008)
    {
        stubKind = SK_DISPATCH;
    }
    else if (firstWord == 0xb460)
    {
        stubKind = SK_RESOLVE;
    }
    else if (firstWord == 0xdefe)
    {
        stubKind = SK_BREAKPOINT;
    }

    return stubKind;
}

UInt16 DecodeMov16(const UInt16 * pwCode)
{
    return ((pwCode[0] & 0x0400) << 1) | ((pwCode[0] & 0x000f) << 12) | ((pwCode[1] & 0x7000) >> 4) | (pwCode[1] & 0x00ff); 
}

UInt32 DecodeMov32Addr(const UInt16 * pwCode)
{
    ASSERT((pwCode[0] & 0xfbf0) == 0xf240); // movw r12,...
    ASSERT((pwCode[1] & 0x8f00) == 0x0c00); // ...
    ASSERT((pwCode[2] & 0xfbf0) == 0xf2c0); // movt r12,...
    ASSERT((pwCode[3] & 0x8f00) == 0x0c00); // ...

    return (DecodeMov16(pwCode+2) << 16) | DecodeMov16(pwCode);
}

void * DecodeJumpStubTarget(UInt8 const * pModuleJumpStub)
{
    UInt16 const * pwCode = ThumbCodeToDataPointer<UInt16*>(pModuleJumpStub);
    ASSERT(pwCode[4] == 0xf8dc);            // mov pc,[r12]
    ASSERT(pwCode[5] == 0xf000);

    void **iatAddr = (void **)DecodeMov32Addr(pwCode);
    return *iatAddr;
}


#endif //DECLARE_DATA

#endif // _VIRTUAL_CALL_STUB_ARM_H
