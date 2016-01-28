// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// File: AMD64/VirtualCallStubCpu.hpp
//

//
// See code:VirtualCallStubManager for details
//
// ============================================================================

#ifndef _VIRTUAL_CALL_STUB_AMD64_H
#define _VIRTUAL_CALL_STUB_AMD64_H

#ifndef FEATURE_REDHAWK
#include "dbginterface.h"
#endif // FEATURE_REDHAWK

//#define STUB_LOGGING

#pragma pack(push)
#pragma pack(1)   // since we are placing code, we want byte packing of the structs

#define USES_ENTRYPOINT_STUBS   0

/*********************************************************************************************
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
struct DispatchStubShort;
struct DispatchStubLong;
struct DispatchHolder;

/*DispatchStub**************************************************************************************
The structure of a full dispatch stub in memory is a DispatchStub followed contiguously in memory
by either a DispatchStubShort of a DispatchStubLong. DispatchStubShort is used when the resolve
stub (failTarget()) is reachable by a rel32 (DISPL) jump. We make a pretty good effort to make sure
that the stub heaps are set up so that this is the case. If we allocate enough stubs that the heap
end up allocating in a new block that is further away than a DISPL jump can go, then we end up using
a DispatchStubLong which is bigger but is a full 64-bit jump. */

/*DispatchStubShort*********************************************************************************
This is the logical continuation of DispatchStub for the case when the failure target is within
a rel32 jump (DISPL). */
struct DispatchStubShortCode
{
    UInt8       part1 [2];      // 0f 85                    jne                                     
    DISPL       _failDispl;     // xx xx xx xx                     failEntry         ;must be forward jmp for perf reasons
    UInt8       part2 [2];      // 48 B8                    mov    rax, 
    UIntNative  _implTarget;    // xx xx xx xx xx xx xx xx              64-bit address
    UInt8       part3 [2];      // FF E0                    jmp    rax
};

struct DispatchStubShort : private DispatchStubShortCode
{
    friend struct DispatchHolder;
    friend struct DispatchStub;

    static bool isShortStub(UInt8 const * pCode);
    inline PTR_Code implTarget() const { return (PTR_Code) _implTarget; }
    inline PTR_Code failTarget() const { return (PTR_Code) &_failDispl + sizeof(DISPL) + _failDispl; }

    inline DispatchStubShort& operator=(DispatchStubShortCode const & code) { *this = *((DispatchStubShort*)&code); return *this; }
};

inline bool DispatchStubShort::isShortStub(UInt8 const * pCode)
{
    return reinterpret_cast<DispatchStubShort const *>(pCode)->part1[0] == 0x0f;
}


/*DispatchStubLong**********************************************************************************
This is the logical continuation of DispatchStub for the case when the failure target is not
reachable by a rel32 jump (DISPL). */
struct DispatchStubLongCode
{
    UInt8       part1 [1];            // 75                       jne
    UInt8       _failDispl;           //    xx                           failLabel
    UInt8       part2 [2];            // 48 B8                    mov    rax, 
    UIntNative  _implTarget;          // xx xx xx xx xx xx xx xx              64-bit address
    UInt8       part3 [2];            // FF E0                    jmp    rax
    // failLabel:
    UInt8       part4 [2];            // 48 B8                    mov    rax,
    UIntNative  _failTarget;          // xx xx xx xx xx xx xx xx              64-bit address
    UInt8       part5 [2];            // FF E0                    jmp    rax
};

struct DispatchStubLong : private DispatchStubLongCode
{
    friend struct DispatchHolder;
    friend struct DispatchStub;

    static inline bool isLongStub(UInt8 const * pCode);
    inline PTR_Code implTarget() const { return (PTR_Code) _implTarget; }
    inline PTR_Code failTarget() const { return (PTR_Code) _failTarget; }

    inline DispatchStubLong& operator=(DispatchStubLongCode const & code) { *this = *((DispatchStubLong*)&code); return *this; }
};

inline bool DispatchStubLong::isLongStub(UInt8 const * pCode)
{
    return reinterpret_cast<DispatchStubLong const *>(pCode)->part1[0] == 0x75;
}

/*DispatchStub**************************************************************************************
Monomorphic and mostly monomorphic call sites eventually point to DispatchStubs.
A dispatch stub has an expected type (expectedMT), target address (target) and fail address (failure).  
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
    UInt8      _entryPoint [2];      // 48 B8                    mov    rax, 
    UIntNative _expectedType;        // xx xx xx xx xx xx xx xx              64-bit address
                                     //                                         _expectedType is required to be aligned, as it is also used as the SLink
                                     //                                         value in stub freelists; this is statically asserted in
                                     //                                         DispatchHolder::InitializeStatic
    UInt8      part1 [3];            // 48 39 01/02              cmp    [rcx/rdx], rax

    // Followed by either DispatchStubShort or DispatchStubLong, depending
    // on whether we were able to make a rel32 or had to make an abs64 jump
    // to the resolve stub on failure.
};

typedef DPTR(DPTR(DispatchStub)) PTR_PTR_DispatchStub;
struct DispatchStub : private DispatchStubCode
{
    enum DispatchStubType
    {
        e_TYPE_SHORT,
        e_TYPE_LONG,
    };

    inline DispatchStubType const type() const
    {
        ASSERT(DispatchStubShort::isShortStub(reinterpret_cast<UInt8 const *>(this + 1))
               || DispatchStubLong::isLongStub(reinterpret_cast<UInt8 const *>(this + 1)));
        return DispatchStubShort::isShortStub((UInt8 *)(this + 1)) ? e_TYPE_SHORT : e_TYPE_LONG;
    }

    inline static UIntNative size(DispatchStubType type)
    {
        return sizeof(DispatchStub) +
            ((type == e_TYPE_SHORT) ? sizeof(DispatchStubShort) : sizeof(DispatchStubLong));
    }

    inline PTR_Code     entryPoint() const { return (PTR_Code) &_entryPoint[0]; }
    inline UIntNative   expectedType() const { return _expectedType;     }
    inline UIntNative   size()       const { return size(type()); }

    inline PTR_Code implTarget() const
    {
        if (type() == e_TYPE_SHORT)
            return getShortStub()->implTarget();
        else
            return getLongStub()->implTarget();
    }

    inline PTR_Code failTarget() const
    {
        if (type() == e_TYPE_SHORT)
            return getShortStub()->failTarget();
        else
            return getLongStub()->failTarget();
    }

    inline DispatchStub& operator=(DispatchStubCode const & code) { *this = *((DispatchStub*)&code); return *this; }

private:
    inline DispatchStubShort const *getShortStub() const
        { return reinterpret_cast<DispatchStubShort const *>(this + 1); }

    inline DispatchStubLong const *getLongStub() const
        { return reinterpret_cast<DispatchStubLong const *>(this + 1); }

     inline PTR_PTR_DispatchStub SList_GetNextPtr()
    {
        ASSERT(((dac_cast<TADDR>(this) + offsetof(DispatchStub, _expectedType)) % sizeof(void*)) == 0);
        return dac_cast<PTR_PTR_DispatchStub>(dac_cast<TADDR>(this) + offsetof(DispatchStub, _expectedType));
    }

    friend struct DispatchHolder;
    friend struct VSDStubSListTraits<DispatchStub>;
};

/* DispatchHolders are the containers for DispatchStubs, they provide for any alignment of 
stubs as necessary.  DispatchStubs are placed in a hashtable and in a cache.  The keys for both
are the pair expectedMT and token.  Efficiency of the of the hash table is not a big issue,
since lookups in it are fairly rare.  Efficiency of the cache is paramount since it is accessed frequently
(see ResolveStub below).  Currently we are storing both of these fields in the DispatchHolder to simplify
alignment issues.  If inlineMT in the stub itself was aligned, then it could be the expectedMT field.
While the token field can be logically gotten by following the failure target to the failEntryPoint 
of the ResolveStub and then to the token over there, for perf reasons of cache access, it is duplicated here.
This allows us to use DispatchStubs in the cache.  The alternative is to provide some other immutable struct
for the cache composed of the triplet (expectedMT, token, target) and some sort of reclaimation scheme when
they are thrown out of the cache via overwrites (since concurrency will make the obvious approaches invalid).
*/

/* @hack for ee resolution - Since the EE does not currently have a resolver function that
does what we want, see notes in implementation of VirtualCallStubManager::Resolver, we are 
using dispatch stubs to siumulate what we want.  That means that inlineTarget, which should be immutable
is in fact written.  Hence we have moved target out into the holder and aligned it so we can 
atomically update it.  When we get a resolver function that does what we want, we can drop this field,
and live with just the inlineTarget field in the stub itself, since immutability will hold.*/
struct DispatchHolder
{
    static void InitializeStatic();

    void  Initialize(UInt8 const * implTarget, UInt8 const * failTarget, size_t expectedMT, 
                     DispatchStub::DispatchStubType type);

    static size_t GetHolderSize(DispatchStub::DispatchStubType type)
        { return sizeof(DispatchHolder) + DispatchStub::size(type); }

    static bool CanShortJumpDispatchStubReachFailTarget(UInt8 const * failTarget, UInt8 const * stubMemory)
    {
        UInt8 const * pFrom = stubMemory + sizeof(DispatchHolder) + sizeof(DispatchStub) + offsetof(DispatchStubShortCode, part2[0]);
        size_t cbRelJump = failTarget - pFrom;
        return FitsInI4(cbRelJump);
    }

    DispatchStub* stub()
        { return reinterpret_cast<DispatchStub *>(reinterpret_cast<UInt8*>(this) + sizeof(align)); }

    static DispatchHolder* FromStub(DispatchStub *stub);
    static DispatchHolder* FromDispatchEntryPoint(PTR_Code dispatchEntry);

private:
    //force expectedType to be aligned since used as key in hash tables.
    UInt8 align[(sizeof(void*)-(offsetof(DispatchStub,_expectedType)%sizeof(void*)))%sizeof(void*)];
    // DispatchStub follows here. It is dynamically sized on allocation
    // because it could be a DispatchStubLong or a DispatchStubShort
};

struct ResolveStub;
struct ResolveHolder;

/*ResolveStub**************************************************************************************
Polymorphic call sites and monomorphic calls that fail end up in a ResolverStub.  There is only 
one resolver stub built for any given token, even though there may be many call sites that
use that token and many distinct <this> types that are used in the calling call frames.  A resolver stub 
actually has two entry points, one for polymorphic call sites and one for dispatch stubs that fail on their
expectedMT test.  There is a third part of the resolver stub that enters the ee when a decision should
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
is important.

On entry
    - R11 contains the indirection cell address for shared code interface call sites (since
      this must be based on a dictionary lookup).
    - RCX contains the "this" object address.
*/

struct ResolveStubCode
{
                                        //                failStub:
    UInt8       _failEntryPoint [2];    // 48 B8                    mov    rax, 
    Int32*      _pCounter;              // xx xx xx xx xx xx xx xx              64-bit address
    UInt8       part0 [4];              // 83 00 FF                 add    dword ptr [rax], -1
                                        // 7c                       jl
    UInt8       toPatcher;              // xx                              backpatcher

    UInt8       _resolveEntryPoint[15]; //                resolveStub:
                                        // 48 8B 01/02              mov    rax, [rcx/rdx]      ; Compute hash = ((MT + MT>>12) ^ prehash)
                                        // 4C 8B D0                 mov    r10, rax            ; r10 <- current EEType*
                                        // 48 C1 E8 0C              shr    rax, 12
                                        // 49 03 C2                 add    rax, r10
                                        // 48 35                    xor    rax,
    UInt32      _hashedToken;           // xx xx xx xx                          hashedtoken    ; xor with pre-hashed token
    UInt8       part1 [2];              // 48 25                    and    rax, 
    UInt32      mask;                   // xx xx xx xx                          cache_mask     ; and with cache mask
    UInt8       part2 [2];              // 49 BA                    mov    r10, 
    UIntNative  _cacheAddress;          // xx xx xx xx xx xx xx xx              64-bit address
    UInt8       part3 [4];              // 4A 8B 04 10              mov    rax, [r10 + rax]    ; get cache entry address

    // Compare cache entry against incoming type
    UInt8       part4 [6];              // 4C 8B 11/12              mov    r10, [rcx/rdx]      ; reload EEType* of this
                                        // 4C 3B 50                 cmp    r10, [rax+          ; compare this EEType* vs. cache EEType*
    UInt8       mtOffset;               // xx                                        ResolverCacheElem.pTgtType]
    UInt8       part5 [1];              // 75                       jne             
    UInt8       toMiss1;                // xx                              miss                ; must be forward jump, for perf reasons

    // Compare cache entry against desired interface EEType*
    UInt8       part6 [2];              // 49 BA                    mov    r10, 
    UIntNative  _itfType;               // xx xx xx xx xx xx xx xx              64-bit EEType address
                                        //                                                       _itfType is required to be aligned, as it is also used as the SLink
                                        //                                                       value in stub freelists; this is statically asserted in
                                        //                                                       ResolveHolder::InitializeStatic
    UInt8       part7 [3];              // 4C 3B 50                 cmp    r10, [rax+          ; compare our itfType vs. the cache itfType
    UInt8       targetInfopItfOffset;   // xx                                        ResolverCacheElem.targetInfo.m_pItf]
    UInt8       part8 [1];              // 75                       jne             
    UInt8       toMiss2;                // xx                              miss                ; must be forward jump, for perf reasons

    // Compare cache entry against desired interface slot number
    UInt8       part9 [3];              // 66 81 78                 cmp    [rax+
    UInt8       targetInfoSlotOffset;   // xx                                        ResolveCacheElem.targetInfo.m_slotNumber],
    UInt16      _itfSlotNumber;         // xx xx                                16-bit slot number
    UInt8       part10 [1];             // 75                       jne
    UInt8       toMiss3;                // xx                              miss                ;must be forward jump, for perf reasons

    UInt8       part11 [3];             // 48 8B 40                 mov    rax, [rax+          ; setup rax with method impl address
    UInt8       targetOffset;           // xx                                        ResolverCacheElem.target]
    UInt8       part12 [2];             // FF E0                    jmp    rax
    
    UInt8       miss [2];               //                miss:
                                        // 48 B8                    mov    rax, 
    UIntNative  _resolveWorker;         // xx xx xx xx xx xx xx xx              64-bit address
    UInt8       part13 [2];             // FF E0                    jmp    rax

                                        //                backpatcher:
    UInt8       patch [2];              // 48 B8                    mov    rax,
    UIntNative  _backpatcher;           // xx xx xx xx xx xx xx xx              64-bit address
    UInt8       part14 [3];             // FF D0                    call   rax
                                        // EB                       jmp
    UInt8       toResolveStub;          // xx                              resolveStub, i.e. go back to _resolveEntryPoint

    UInt8       alignPad [1];           // cc
};

typedef DPTR(DPTR(struct ResolveStub)) PTR_PTR_ResolveStub;
struct ResolveStub : private ResolveStubCode
{
    inline PTR_Code failEntryPoint()     { return &_failEntryPoint[0];    }
    inline PTR_Code resolveEntryPoint()  { return &_resolveEntryPoint[0]; }

    inline Int32*  pCounter()                { return _pCounter; }
    inline UInt32  hashedToken()             { return _hashedToken >> LOG2_PTRSIZE;    }
    inline size_t  cacheAddress()            { return _cacheAddress;   }
    inline size_t  size()                    { return sizeof(ResolveStub); }
    inline EEType* tgtItfType()              { return reinterpret_cast<EEType*>(_itfType); }
    inline UInt16  tgtItfSlotNumber()        { return _itfSlotNumber; }
    VSDInterfaceTargetInfo tgtItfInfo()      { return VSDInterfaceTargetInfo(tgtItfType(), tgtItfSlotNumber()); }

    inline ResolveStub& operator=(ResolveStubCode const & code) { *this = *((ResolveStub*)&code); return *this; }

private:
    inline PTR_PTR_ResolveStub SList_GetNextPtr()
    {
        ASSERT(((dac_cast<TADDR>(this) + offsetof(ResolveStub, _itfType)) % sizeof(void*)) == 0);
        return dac_cast<PTR_PTR_ResolveStub>(dac_cast<TADDR>(this) + offsetof(ResolveStub, _itfType));
    }

    friend struct ResolveHolder;
    friend struct VSDStubSListTraits<ResolveStub>;
};

/* ResolveHolders are the containers for ResolveStubs,  They provide 
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

    static ResolveHolder* FromStub(ResolveStub * pStub);
    static ResolveHolder* FromFailEntryPoint(UInt8 * resolveEntry);
    static ResolveHolder* FromResolveEntryPoint(UInt8 * resolveEntry);

private:
    //align _itfType in resolve stub
    UInt8 align[(sizeof(void*)-((offsetof(ResolveStub,_itfType))%sizeof(void*)))%sizeof(void*)];

    ResolveStub _stub;
};
#pragma pack(pop)

#ifdef DECLARE_DATA

#define INSTR_INT3 0xcc
#define INSTR_CALL_IND              0x15FF      // call dword ptr[addr32]
#define INSTR_CALL_IND_BP           0x15CC      // call dword ptr[addr32] with a breakpoint set on the instruction

#ifdef _DEBUG
#define INSTR_NOP3_1                0x1F0F      // 1st word of 3-byte nop ( 0F 1F 00 -> nop dword ptr [eax] )
#define INSTR_NOP3_1_BP             0x1FCC      // 1st word of 3-byte nop ( 0F 1F 00 -> nop dword ptr [eax] ) with a breakpoint set on the instruction
#define INSTR_NOP3_3                0x00        // 3rd byte of 3-byte nop
#define INSTR_CALL_IND_R11_1        0xff41      // 1st word of 3-byte call qword ptr [r11]
#define INSTR_CALL_IND_R11_3        0x13        // 3rd byte of 3-byte call qword ptr [r11]
#endif

#ifndef DACCESS_COMPILE

// #include "asmconstants.h"

#ifdef STUB_LOGGING
extern size_t g_lookup_inline_counter;
extern size_t g_call_inline_counter;
extern size_t g_miss_inline_counter;
extern size_t g_call_cache_counter;
extern size_t g_miss_cache_counter;
#endif

/* Template used to generate the stub.  We generate a stub by allocating a block of 
   memory and copy the template over it and just update the specific fields that need 
   to be changed.
*/ 

const DispatchStubCode dispatchTemplate = 
{
    /* UInt8      _entryPoint [2];  */ { 0x48, 0xB8 },
    /* UIntNative _expectedType;    */ 0xcccccccccccccccc,
    /* UInt8      part1 [3];        */ { 0x48, 0x39, 0x01 }
};

const DispatchStubShortCode dispatchShortTemplate =
{
    /* UInt8       part1 [2];    */ { 0x0F, 0x85 },
    /* DISPL       _failDispl;   */ 0xcccccccc,
    /* UInt8       part2 [2];    */ { 0x48, 0xb8 },
    /* UIntNative  _implTarget;  */ 0xcccccccccccccccc,
    /* UInt8       part3 [2];    */ { 0xFF, 0xE0 },
};

const DispatchStubLongCode dispatchLongTemplate = 
{
    /* UInt8       part1 [1];    */ { 0x75 },
    /* UInt8       _failDispl;   */ offsetof(DispatchStubLongCode, part4) - offsetof(DispatchStubLongCode, part2),
    /* UInt8       part2 [2];    */ { 0x48, 0xb8 },
    /* UIntNative  _implTarget;  */ 0xcccccccccccccccc, 
    /* UInt8       part3 [2];    */ { 0xFF, 0xE0 },
    // failLabel:
    /* UInt8       part4 [2];    */ { 0x48, 0xb8 },
    /* UIntNative  _failTarget;  */ 0xcccccccccccccccc,
    /* UInt8       part5 [2];    */ { 0xFF, 0xE0 },
};

void DispatchHolder::InitializeStatic()
{
    // Check that _expectedType is aligned in the DispatchHolder
    static_assert(((sizeof(DispatchHolder) + offsetof(DispatchStub, _expectedType)) % sizeof(void*)) == 0, "_expectedType is misaligned");
};

void  DispatchHolder::Initialize(UInt8 const * implTarget, UInt8 const * failTarget, size_t expectedType,
                               DispatchStub::DispatchStubType type)
{
    //
    // Initialize the common area
    //

    // initialize the static data
    *stub() = dispatchTemplate;

    // fill in the dynamic data
    stub()->_expectedType  = expectedType;

    //
    // Initialize the short/long areas
    //
    if (type == DispatchStub::e_TYPE_SHORT)
    {
        DispatchStubShort *shortStub = const_cast<DispatchStubShort *>(stub()->getShortStub());

        // initialize the static data
        *shortStub = dispatchShortTemplate;

        // fill in the dynamic data
        shortStub->_failDispl   = (DISPL)(failTarget - ((UInt8 const *) &shortStub->_failDispl + sizeof(DISPL)));
        shortStub->_implTarget  = (size_t) implTarget;
        ASSERT((UInt8 *)&shortStub->_failDispl + sizeof(DISPL) + shortStub->_failDispl == failTarget);
    }
    else
    {
        ASSERT(type == DispatchStub::e_TYPE_LONG);
        DispatchStubLong *longStub = const_cast<DispatchStubLong *>(stub()->getLongStub());

        // initialize the static data
        *longStub = dispatchLongTemplate;

        // fill in the dynamic data
        longStub->_implTarget = reinterpret_cast<size_t>(implTarget);
        longStub->_failTarget = reinterpret_cast<size_t>(failTarget);
    }
}

DispatchHolder* DispatchHolder::FromStub(DispatchStub * pStub)
{
    DispatchHolder* dispatchHolder =
        reinterpret_cast<DispatchHolder*>(reinterpret_cast<UInt8*>(pStub) - sizeof(DispatchHolder));
    ASSERT(dispatchHolder->stub()->_entryPoint[1] == dispatchTemplate._entryPoint[1]);
    return dispatchHolder;
}

/* Template used to generate the stub.  We generate a stub by allocating a block of 
   memory and copy the template over it and just update the specific fields that need 
   to be changed.
*/ 
const ResolveStubCode resolveTemplate = 
{
    /* UInt8       _failEntryPoint [2]; */ { 0x48, 0xB8 },
    /* Int32*      _pCounter;           */ (Int32*) 0xcccccccccccccccc,
    /* UInt8       part0 [4];           */ { 0x83, 0x00, 0xFF, 0x7C },
    /* UInt8       toPatcher;           */ offsetof(ResolveStubCode,patch)-(offsetof(ResolveStubCode,toPatcher)+1) & 0xFF,
    /* UInt8       _resolveEntryPoint[4]*/ { 0x48, 0x8B, 0x01, 0x4C, 0x8B, 0xD0, 0x48, 0xC1, 0xE8, CALL_STUB_CACHE_NUM_BITS, 0x49, 0x03, 0xC2, 0x48, 0x35 },
    /* UInt32      _hashedToken;        */ 0xcccccccc,
    /* UInt8       part1 [2];           */ { 0x48, 0x25 },
    /* UInt32      mask;                */ CALL_STUB_CACHE_MASK * sizeof(void *),
    /* Uint8       part2 [2];           */ { 0x49, 0xBA },
    /* UIntNative  _cacheAddress;       */ 0xcccccccccccccccc,
    /* UInt8       part3 [6];           */ { 0x4A, 0x8B, 0x04, 0x10 },
    /* UInt8       part4 [6];           */ { 0x4C, 0x8B, 0x11, 0x4C, 0x3B, 0x50 },
    /* UInt8       mtOffset;            */ offsetof(ResolveCacheElem,pTgtType) & 0xFF,
    /* UInt8       part5 [1];           */ { 0x75 },
    /* UInt8       toMiss1;             */ offsetof(ResolveStubCode,miss)-(offsetof(ResolveStubCode,toMiss1)+1) & 0xFF,
    /* Uint8       part6 [2];           */ { 0x49, 0xBA },
    /* UIntNative  _itfType;            */ 0xcccccccccccccccc,
    /* UInt8       part7 [3];           */ { 0x4C, 0x3B, 0x50 },
    /* UInt8       targetInfopItfOffset;*/ offsetof(ResolveCacheElem,targetInfo.m_pItf) & 0xFF,
    /* UInt8       part8 [1];           */ { 0x75 },
    /* UInt8       toMiss2;             */ offsetof(ResolveStubCode,miss)-(offsetof(ResolveStubCode,toMiss2)+1) & 0xFF,
    /* UInt8       part9 [3];           */ { 0x66, 0x81, 0x78 },
    /* UInt8       targetInfoSlotOffset;*/ offsetof(ResolveCacheElem,targetInfo.m_slotNumber) & 0xFF,
    /* UInt16      _itfSlotNumber;      */ 0xcccc,
    /* UInt8       part10 [1];          */ { 0x75 },
    /* UInt8       toMiss3;             */ offsetof(ResolveStubCode,miss)-(offsetof(ResolveStubCode,toMiss3)+1) & 0xFF,
    /* UInt8       part11 [3];          */ { 0x48, 0x8B, 0x40 },
    /* UInt8       targetOffset;        */ offsetof(ResolveCacheElem,target) & 0xFF,
    /* UInt8       part12 [2];          */ { 0xFF, 0xE0 },
    /* UInt8       miss [2];            */ { 0x48, 0xB8 },
    /* UIntNative  _resolveWorker;      */ 0xcccccccccccccccc,
    /* UInt8       part13 [2];          */ { 0xFF, 0xE0 },
    /* UInt8       patch [2];           */ { 0x48, 0xB8 },
    /* UIntNative  _backpatcher;        */ 0xcccccccccccccccc,
    /* UInt8       part14 [3];          */ { 0xFF, 0xD0, 0xEB },
    /* UInt8       toResolveStub        */ offsetof(ResolveStubCode,_resolveEntryPoint)-(offsetof(ResolveStubCode,toResolveStub)+1) & 0xFF,
    /* UInt8       alignPad [1];        */ { INSTR_INT3 }
};

void ResolveHolder::InitializeStatic()
{
    // Check that _itfType is aligned in ResolveHolder
    static_assert(((offsetof(ResolveHolder, _stub) + offsetof(ResolveStub, _itfType)) % sizeof(void*)) == 0, "_itfType is misaligned");
};

void  ResolveHolder::Initialize(
                        const UInt8 * resolveWorkerTarget,
                        const UInt8 * patcherTarget, 
                        EEType * pItfType,
                        UInt16 itfSlotNumber,
                        UInt32 hashedToken,
                        void * cacheAddr,
                        Int32 * counterAddr)
{
    _stub = resolveTemplate;

    //fill in the stub specific fields
    _stub._cacheAddress       = (size_t) cacheAddr;
    _stub._hashedToken        = hashedToken << LOG2_PTRSIZE;
    _stub._itfType            = (size_t) pItfType;
    _stub._itfSlotNumber      = itfSlotNumber;
    _stub._resolveWorker      = (size_t) resolveWorkerTarget;
    _stub._pCounter           = counterAddr;
    _stub._backpatcher        = (size_t) patcherTarget;
}

ResolveHolder* ResolveHolder::FromStub(ResolveStub * pStub)
{
    ResolveHolder *resolveHolder =
        reinterpret_cast<ResolveHolder*>(reinterpret_cast<UInt8*>(pStub) - offsetof(ResolveHolder, _stub));
    ASSERT(resolveHolder->_stub._resolveEntryPoint[1] == resolveTemplate._resolveEntryPoint[1]);
    return resolveHolder;
}

ResolveHolder* ResolveHolder::FromFailEntryPoint(UInt8 * failEntry)
{ 
    ResolveStub *pStub = reinterpret_cast<ResolveStub*>(failEntry - offsetof(ResolveStub, _failEntryPoint));
    return FromStub(pStub);
}

#endif // DACCESS_COMPILE

DispatchHolder* DispatchHolder::FromDispatchEntryPoint(PTR_Code dispatchEntry)
{ 
    DispatchStub *pStub = reinterpret_cast<DispatchStub*>(dispatchEntry - offsetof(DispatchStub, _entryPoint));
    return FromStub(pStub);
}


ResolveHolder* ResolveHolder::FromResolveEntryPoint(UInt8 * resolveEntry)
{
    ResolveStub *pStub = reinterpret_cast<ResolveStub*>(resolveEntry - offsetof(ResolveStub, _resolveEntryPoint));
    return FromStub(pStub);
}

VirtualCallStubManager::StubKind VirtualCallStubManager::DecodeStubKind(PTR_Code stubStartAddress)
{
    StubKind stubKind = SK_LOOKUP;

    UInt16 firstWord = *((UInt16*) stubStartAddress);
    UInt8 firstByte = *((UInt8*) stubStartAddress);

    if (firstWord == 0xb848)
    {
        stubKind = SK_DISPATCH;
    }
    else if (firstWord == 0x8b48)
    {
        stubKind = SK_RESOLVE;
    }
    else if (firstByte == 0xcc)
    {
        stubKind = SK_BREAKPOINT;
    }

    return stubKind;
}

//-----------------------------------------------------------------------------------------------------------
/* static */
const UInt8 **StubCallSite::ComputeIndirCellAddr(const UInt8 *returnAddr, const UInt8 **indirCellAddrForRegisterIndirect)
{ 
    if ((*reinterpret_cast<const UInt16*>(&returnAddr[-6]) == INSTR_CALL_IND) ||
        (*reinterpret_cast<const UInt16*>(&returnAddr[-6]) == INSTR_CALL_IND_BP))
    {
        return (const UInt8 **)(returnAddr + *((DISPL*)(returnAddr - sizeof(DISPL))));
    }
    else
    {
        ASSERT( ((*reinterpret_cast<const UInt16*>(&returnAddr[-6]) == INSTR_NOP3_1) || (*reinterpret_cast<const UInt16*>(&returnAddr[-5]) == INSTR_NOP3_1_BP))
              && (*reinterpret_cast<const UInt8*> (&returnAddr[-4]) == INSTR_NOP3_3)
              && (*reinterpret_cast<const UInt16*>(&returnAddr[-3]) == INSTR_CALL_IND_R11_1)
              && (*reinterpret_cast<const UInt8*>(&returnAddr[-1]) == INSTR_CALL_IND_R11_3));
        return indirCellAddrForRegisterIndirect;
    }
}

#ifndef X86_INSTR_JMP_IND
#define X86_INSTR_JMP_IND 0x25FF      // jmp dword ptr[addr32]
#endif

void * DecodeJumpStubTarget(UInt8 const * pModuleJumpStub)
{
    UInt8 const * pbCode = pModuleJumpStub;
    ASSERT(X86_INSTR_JMP_IND == *((UInt16 *)pbCode));
    pbCode += 2;
    Int32 displacement = *((Int32 *)pbCode);
    pbCode += 4;
    return *(void**)(pbCode + displacement);
}

#endif //DECLARE_DATA

#endif // _VIRTUAL_CALL_STUB_AMD64_H

