//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//
// File: virtualcallstubcpu.hpp
//
// ============================================================================

#ifndef _VIRTUAL_CALL_STUB_X86_H
#define _VIRTUAL_CALL_STUB_X86_H

//#define STUB_LOGGING

#include <pshpack1.h>  // Since we are placing code, we want byte packing of the structs

#define USES_ENTRYPOINT_STUBS	0

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
    // DispatchStub:: _entryPoint expects:
    //       ecx: object (the "this" pointer)
    //       eax: siteAddrForRegisterIndirect if this is a RegisterIndirect dispatch call
#ifndef STUB_LOGGING
    UInt8 _entryPoint [2];      // 81 39        cmp  [ecx],                   ; This is the place where we are going to fault on null this.
    UIntNative _expectedType;   // xx xx xx xx              expectedType      ; If you change it, change also AdjustContextForVirtualStub in excep.cpp!!!
                                //                                              _expectedType is required to be aligned, as it is also used as the SLink
                                //                                              value in stub freelists; this is statically asserted in
                                //                                              DispatchHolder::InitializeStatic
    UInt8 jmpOp1[2];            // 0f 85        jne                 
    DISPL _failDispl;           // xx xx xx xx              failEntry         ;must be forward jmp for perf reasons
    UInt8 jmpOp2;               // e9           jmp     
    DISPL _implDispl;           // xx xx xx xx              implTarget
#else //STUB_LOGGING
    UInt8 _entryPoint [2];      // ff 05        inc
    UIntNative* d_call;         // xx xx xx xx              [call_mono_counter]
    UInt8 cmpOp [2];            // 81 39        cmp  [ecx],
    UIntNative _expectedType;   // xx xx xx xx              expectedType
    UInt8 jmpOp1[2];            // 0f 84        je 
    DISPL _implDispl;           // xx xx xx xx              implTarget        ;during logging, perf is not so important               
    UInt8 fail [2];             // ff 05        inc 
    UIntNative* d_miss;         // xx xx xx xx      [miss_mono_counter]
    UInt8 jmpFail;              // e9           jmp     
    DISPL _failDispl;           // xx xx xx xx              failEntry 
#endif //STUB_LOGGING 
};

typedef DPTR(DPTR(DispatchStub)) PTR_PTR_DispatchStub;
struct DispatchStub : private DispatchStubCode
{
    inline PTR_Code entryPoint()            { return (PTR_Code) &_entryPoint[0]; }

    inline UIntNative    expectedType()     { return _expectedType;     }
    inline PTR_Code implTarget()            { return (PTR_Code) &_implDispl + sizeof(DISPL) + _implDispl; }
    inline PTR_Code failTarget()            { return (PTR_Code) &_failDispl + sizeof(DISPL) + _failDispl; }
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
    //force expectedType to be aligned since used as key in hash tables.
#ifndef STUB_LOGGING
    UInt8 align[(sizeof(void*)-(offsetof(DispatchStub,_expectedType)%sizeof(void*)))%sizeof(void*)];
#endif
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
    // ResolveStub::_failEntryPoint expects:
    //       ecx: object (the "this" pointer)
    //       eax: siteAddrForRegisterIndirect if this is a RegisterIndirect dispatch call
    UInt8 _failEntryPoint [2];       // 83 2d        sub
    Int32* _pCounter;                // xx xx xx xx          [counter],
    UInt8 part0 [2];                 // 01                   01
                                     // 7c           jl
    UInt8 toPatcher;                 // xx                   backpatcher     ;must be forward jump, for perf reasons
                                     //                                      ;fall into the resolver stub

    // ResolveStub::_resolveEntryPoint expects:
    //       ecx: object (the "this" pointer)
    //       eax: siteAddrForRegisterIndirect if this is a RegisterIndirect dispatch call
    UInt8    _resolveEntryPoint[8];  // 39 09        cmp     [ecx],ecx       ;force an early AV while stack can be walked to ensure good
                                     //                                      ;    watson bucketing.
                                     // 50           push    eax             ;save siteAddrForRegisterIndirect - this may be an indirect call
                                     // 8b 01        mov     eax,[ecx]       ;get the method table from the "this" pointer. This is the place
                                     //                                      ;    where we are going to fault on null this. If you change it,
                                     //                                      ;    change also AdjustContextForVirtualStub in excep.cpp!!!
                                     // 52           push    edx            
                                     // 8b d0        mov     edx, eax
    UInt8    part1 [6];              // c1 e8 0C     shr     eax,12          ;we are adding upper bits into lower bits of mt
                                     // 03 c2        add     eax,edx
                                     // 35           xor     eax,
    UInt32 _hashedToken;             // xx xx xx xx              hashedToken ;along with pre-hashed token
    UInt8 part2 [1];                 // 25           and     eax,
    UIntNative mask;                 // xx xx xx xx              cache_mask
    UInt8 part3 [2];                 // 8b 80        mov     eax, [eax+
    void* _cacheAddress;             // xx xx xx xx                lookupCache]
#ifdef STUB_LOGGING
    UInt8 cntr1[2];                  // ff 05        inc
    UIntNative* c_call;              // xx xx xx xx          [call_cache_counter]
#endif //STUB_LOGGING 

    // Compare cache entry against incoming type
    UInt8 part4 [2];                 // 3b 10        cmp     edx,[eax+
    // UInt8 mtOffset;               //                          ResolveCacheElem.pMT]
    UInt8 part5 [1];                 // 75           jne
    UInt8 toMiss1;                   // xx                   miss            ;must be forward jump, for perf reasons

    // Compare cache entry against desired interface EEType*
    UInt8 part6 [2];                 // 81 78        cmp     [eax+
    UInt8 itfTypeOffset;             // xx                        ResolveCacheElem.targetInfo.m_pItf],
    void* _itfType;                  // xx xx xx xx          EEType*
                                     //                      _itfType is required to be aligned, as it is also used as the SLink
                                     //                      value in stub freelists; this is statically asserted in
                                     //                      ResolveHolder::InitializeStatic
    UInt8 part7 [1];                 // 75           jne
    UInt8 toMiss2;                   // xx                   miss            ;must be forward jump, for perf reasons

    // Compare cache entry against desired interface slot number
    UInt8 part8 [3];                 // 66 81 78     cmp     [eax+
    UInt8 slotNumberOffset;          // xx                        ResolveCacheElem.targetInfo.m_slotNumber],
    UInt16 _slotNumber;              // xx xx                slotNumber
    UInt8 part9 [1];                 // 75           jne
    UInt8 toMiss3;                   // xx                   miss            ;must be forward jump, for perf reasons

    UInt8 part10 [2];                // 8B 40 xx     mov     eax,[eax+
    UInt8 targetOffset;              //                          ResolveCacheElem.target]
    UInt8 part11 [6];                // 5a           pop     edx
                                     // 83 c4 04     add     esp,4           ;throw away siteAddrForRegisterIndirect - we don't need it now
                                     // ff e0        jmp     eax
                                     //         miss:
    UInt8    miss [2];               // 5a           pop     edx
                                     // 58           pop     eax             ;restore  siteAddrForRegisterIndirect - this may be an indirect call
#ifdef STUB_LOGGING
    UInt8 cntr2[2];                  // ff 05        inc
    UIntNative* c_miss;              // xx xx xx xx          [miss_cache_counter]
#endif //STUB_LOGGING
    UInt8 part12 [1];                // e9           jmp
    DISPL _resolveWorkerDispl;       // xx xx xx xx          resolveWorker == VSDResolveWorkerChainLookupAsmStub or VSDResolveWorkerAsmStub
    UInt8 patch[1];                  // e8           call
    DISPL _backpatcherDispl;         // xx xx xx xx          backpatcherWorker  == VSDBackPatchWorkerAsmStub
    UInt8 part13 [1];                // eb           jmp
    UInt8 toResolveStub;             // xx                   resolveStub, i.e. go back to _resolveEntryPoint
};

typedef DPTR(DPTR(struct ResolveStub)) PTR_PTR_ResolveStub;
struct ResolveStub : private ResolveStubCode
{
    inline PTR_Code failEntryPoint()            { return (PTR_Code)&_failEntryPoint[0];    }
    inline const PTR_Code resolveEntryPoint()   { return (PTR_Code)&_resolveEntryPoint[0]; }

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
    //align _itfType in resolve stub
    UInt8 align[(sizeof(void*)-((offsetof(ResolveStub,_itfType))%sizeof(void*)))%sizeof(void*)];

    ResolveStub _stub;

    // Tail alignment is not needed, as stubs are allocated using AllocHeap::AllocAligned,
    // which arranges that the start of the stub is properly aligned.
};

#include <poppack.h>

#ifdef DECLARE_DATA

#ifndef DACCESS_COMPILE

#ifdef _MSC_VER

MSVC_SAVE_WARNING_STATE()
MSVC_DISABLE_WARNING(4414) // disable "short jump to function converted to near"

#ifdef CHAIN_LOOKUP
/*-----------------------------------------------------------------------------------------------------------
   This will perform a chained lookup of the entry if the initial cache lookup fails

   Entry stack:
            dispatch token
            siteAddrForRegisterIndirect (used only if this is a RegisterIndirect dispatch call)
            return address of caller to stub
        Also, EAX contains the pointer to the first ResolveCacheElem pointer for the calculated
        bucket in the cache table.
*/
__declspec (naked) void VSDResolveWorkerChainLookupAsmStub()
{
    enum
    {
        e_token_size                = 4,
        e_indirect_addr_size        = 4,
        e_caller_ret_addr_size      = 4,
    };
    enum
    {
        // this is the part of the stack that is present as we enter this function:
        e_token                     = 0,
        e_indirect_addr             = e_token + e_token_size,
        e_caller_ret_addr           = e_indirect_addr + e_indirect_addr_size,
        e_ret_esp                   = e_caller_ret_addr + e_caller_ret_addr_size,
    };
    enum
    {
        e_spilled_reg_size          = 8,
    };

    // main loop setup
    __asm {
#ifdef STUB_LOGGING
        inc     g_chained_lookup_call_counter
#endif
        // spill regs
        push    edx
        push    ecx
        // move the token into edx
        mov     edx,[esp+e_spilled_reg_size+e_token]
        // move the MT into ecx
        mov     ecx,[ecx]
    }
    main_loop:
    __asm {
        // get the next entry in the chain (don't bother checking the first entry again)
        mov     eax,[eax+e_resolveCacheElem_offset_next]
        // test if we hit a terminating NULL
        test    eax,eax
        jz      fail
        // compare the MT of the ResolveCacheElem
        cmp     ecx,[eax+e_resolveCacheElem_offset_mt]
        jne     main_loop
        // compare the token of the ResolveCacheElem
        cmp     edx,[eax+e_resolveCacheElem_offset_token]
        jne     main_loop
        // success
        // decrement success counter and move entry to start if necessary
        sub     g_dispatch_cache_chain_success_counter,1
        //@TODO: Perhaps this should be a jl for better branch prediction?
        jge     nopromote
        // be quick to reset the counter so we don't get a bunch of contending threads
        add     g_dispatch_cache_chain_success_counter,CALL_STUB_CACHE_INITIAL_SUCCESS_COUNT
        // promote the entry to the beginning of the chain
        mov     ecx,eax
        call    VirtualCallStubManager::PromoteChainEntry
    }
    nopromote:
    __asm {
        // clean up the stack and jump to the target
        pop     ecx
        pop     edx
        add     esp,(e_caller_ret_addr - e_token)
        mov     eax,[eax+e_resolveCacheElem_offset_target]
        jmp     eax
    }
    fail:
    __asm {
#ifdef STUB_LOGGING
        inc     g_chained_lookup_miss_counter
#endif
        // restore registers
        pop     ecx
        pop     edx
        jmp     VSDResolveWorkerAsmStub
    }
}
#endif 

MSVC_RESTORE_WARNING_STATE() // 4414: disable "short jump to function converted to near"

#else

//-----------------------------------------------------------------------------------------------------------
//@ROTORTODO: implement these in virtualcallstubx86.s
void InContextTPDispatchAsmStub()
{
    ASSERT(!"NYI");
}

//-----------------------------------------------------------------------------------------------------------
void InContextTPQuickDispatchAsmStub()
{
    ASSERT(!"NYI");
}

//-----------------------------------------------------------------------------------------------------------
void TransparentProxyWorkerAsmStub()
{
    ASSERT(!"NYI");
}

//-----------------------------------------------------------------------------------------------------------
void DispatchInterfaceCallWorkerAsmStub()
{
    ASSERT(!"NYI");
}

#endif // _MSC_VER

#endif // #ifndef DACCESS_COMPILE

#ifndef DACCESS_COMPILE

//-----------------------------------------------------------------------------------------------------------
/* static */
const UInt8 **StubCallSite::ComputeIndirCellAddr(const UInt8 *returnAddr, const UInt8 **indirCellAddrForRegisterIndirect)
{ 
    if (isCallRelativeIndirect(returnAddr))
        return *(const UInt8 ***) (returnAddr - sizeof(DISPL));
    else
    {
        ASSERT(isCallRegisterIndirect(returnAddr));
        return indirCellAddrForRegisterIndirect;
    }
}


#ifdef STUB_LOGGING
extern UIntNative g_lookup_inline_counter;
extern UIntNative g_mono_call_counter;
extern UIntNative g_mono_miss_counter;
extern UIntNative g_poly_call_counter;
extern UIntNative g_poly_miss_counter;
#endif

//-----------------------------------------------------------------------------------------------------------
/* Template used to generate the stub.  We generate a stub by allocating a block of 
   memory and copy the template over it and just update the specific fields that need 
   to be changed.
*/ 
const DispatchStubCode dispatchTemplate = 
{
#ifndef STUB_LOGGING
    /* UInt8       _entryPoint [2]; */ { 0x81, 0x39 },
    /* UIntNative  _expectedType;   */ 0xcccccccc,
    /* UInt8       jmpOp1[2];       */ { 0x0f, 0x85 },
    /* DISPL       _failDispl;      */ 0xcccccccc,
    /* UInt8       jmpOp2;          */ 0xe9,
    /* DISPL       _implDispl;      */ 0xcccccccc
#else //STUB_LOGGING
    /* UInt8       _entryPoint [2]; */ { 0xff, 0x05 },
    /* UIntNative* d_call;          */ &g_mono_call_counter,
    /* UInt8       cmpOp [2];       */ { 0x81, 0x39 },
    /* UIntNative  _expectedType;   */ 0xcccccccc,
    /* UInt8       jmpOp1[2];       */ { 0x0f, 0x84 }
    /* DISPL       _implDispl;      */ 0xcccccccc,
    /* UInt8       fail [2];        */ { 0xff, 0x05 }
    /* UIntNative* d_miss;          */ &g_mono_miss_counter,
    /* UInt8       jmpFail;         */ 0xe9,
    /* DISPL       _failDispl;      */ 0xcccccccc
#endif //STUB_LOGGING 
};

//-----------------------------------------------------------------------------------------------------------
void DispatchHolder::InitializeStatic()
{
    // Check that _expectedType is aligned in the DispatchHolder
    STATIC_ASSERT(((offsetof(DispatchHolder, _stub) + offsetof(DispatchStub,_expectedType)) % sizeof(void*)) == 0);
};

//-----------------------------------------------------------------------------------------------------------
void  DispatchHolder::Initialize(
    PTR_Code implTarget,
    PTR_Code failTarget,
    UIntNative expectedType)
{
    _stub = dispatchTemplate;

    //fill in the stub specific fields
    _stub._expectedType  = (UIntNative) expectedType;
    _stub._failDispl   = failTarget - ((PTR_Code) &_stub._failDispl + sizeof(DISPL));
    _stub._implDispl   = implTarget - ((PTR_Code) &_stub._implDispl + sizeof(DISPL));
}

//-----------------------------------------------------------------------------------------------------------
DispatchHolder* DispatchHolder::FromStub(DispatchStub * pStub)
{
    DispatchHolder *dispatchHolder = 
        reinterpret_cast<DispatchHolder*>(reinterpret_cast<UInt8*>(pStub) - offsetof(DispatchHolder, _stub));
    ASSERT(dispatchHolder->stub()->_entryPoint[1] == dispatchTemplate._entryPoint[1]);
    return dispatchHolder;
}

//-----------------------------------------------------------------------------------------------------------
DispatchHolder* DispatchHolder::FromDispatchEntryPoint(PTR_Code dispatchEntry)
{ 
    DispatchStub *pStub = reinterpret_cast<DispatchStub*>(dispatchEntry - offsetof(DispatchStub, _entryPoint));
    return FromStub(pStub);
}


//-----------------------------------------------------------------------------------------------------------
/* Template used to generate the stub.  We generate a stub by allocating a block of 
   memory and copy the template over it and just update the specific fields that need 
   to be changed.
*/ 

const ResolveStubCode resolveTemplate = 
{
    /* UInt8       _failEntryPoint [2]  */ { 0x83, 0x2d },
    /* Int32*      _pCounter            */ (Int32 *) 0xcccccccc,
    /* UInt8       part0 [2]            */ { 0x01, 0x7c },
    /* UInt8       toPatcher            */ offsetof(ResolveStubCode, patch) - (offsetof(ResolveStubCode, toPatcher) + 1),
    /* UInt8       _resolveEntryPoint[8]*/ { 0x39, 0x09, 0x50, 0x8b, 0x01, 0x52, 0x8b, 0xd0 },
    /* UInt8       part1 [6]            */ { 0xc1, 0xe8, CALL_STUB_CACHE_NUM_BITS, 0x03, 0xc2, 0x35 },
    /* UInt32      _hashedToken         */ 0xcccccccc,
    /* UInt8       part2 [1];           */ { 0x25 },
    /* UIntNative  mask;                */ (CALL_STUB_CACHE_MASK << LOG2_PTRSIZE),
    /* UInt8       part3 [2];           */ { 0x8b, 0x80 },
    /* void*       _cacheAddress;       */ (void *) 0xcccccccc,
#ifdef STUB_LOGGING
    /* UInt8       cntr1[2];            */ { 0xff, 0x05 },
    /* UIntNative* c_call;              */ &g_poly_call_counter,
#endif //STUB_LOGGING 
    /* UInt8       part4 [2];           */ { 0x3b, 0x10 },
    /* UInt8       part5 [1];           */ { 0x75 },
    /* UInt8       toMiss1;             */ offsetof(ResolveStubCode,miss) - (offsetof(ResolveStubCode,toMiss1) + 1), 
    /* UInt8       part6 [2];           */ { 0x81, 0x78 },
    /* UInt8       itfTypeOffset;       */ offsetof(ResolveCacheElem,targetInfo) + offsetof(VSDInterfaceTargetInfo,m_pItf),
    /* void*       _itfType;            */ (void *) 0xcccccccc,
    /* UInt8       part7 [1];           */ { 0x75 },
    /* UInt8       toMiss2;             */ offsetof(ResolveStubCode,miss) - (offsetof(ResolveStubCode,toMiss2) + 1),
    /* UInt8       part8 [3];           */ { 0x66, 0x81, 0x78 },
    /* UInt8       slotNumberOffset;    */ offsetof(ResolveCacheElem,targetInfo) + offsetof(VSDInterfaceTargetInfo,m_slotNumber),
    /* UInt16      _slotNumber;         */ 0xcccc,
    /* UInt8       part9 [1];           */ { 0x75 },
    /* UInt8       toMiss3;             */ offsetof(ResolveStubCode,miss) - (offsetof(ResolveStubCode,toMiss3) + 1),
    /* UInt8       part10 [2];          */ { 0x8b, 0x40 },
    /* UInt8       targetOffset;        */ offsetof(ResolveCacheElem,target),
    /* UInt8       part11 [6];          */ { 0x5a, 0x83, 0xc4, 0x04, 0xff, 0xe0 },
    /* UInt8       miss [2];            */ { 0x5a, 0x58 },
#ifdef STUB_LOGGING
    /* UInt8       cntr2[2];            */ { 0xff, 0x05 },
    /* UIntNative* c_miss;              */ &g_poly_miss_counter,
#endif //STUB_LOGGING 
    /* UInt8       part12 [1];          */ { 0xe9 },
    /* DISPL       _resolveWorkerDispl; */ 0xcccccccc,
    /* UInt8       patch[1];            */ { 0xe8 },
    /* DISPL       _backpatcherDispl;   */ 0xcccccccc,
    /* UInt8       part13 [1];          */ { 0xeb },
    /* UInt8       toResolveStub;       */ (offsetof(ResolveStubCode, _resolveEntryPoint) - (offsetof(ResolveStubCode, toResolveStub) + 1)) & 0xFF
};


//-----------------------------------------------------------------------------------------------------------
void ResolveHolder::InitializeStatic()
{
    // Check that _itfType is aligned in ResolveHolder
    STATIC_ASSERT(((offsetof(ResolveHolder, _stub) + offsetof(ResolveStub, _itfType)) % sizeof(void*)) == 0);
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
    _stub = resolveTemplate;

    //fill in the stub specific fields
    _stub._pCounter           = counterAddr;
    _stub._hashedToken        = hashedToken << 2;
    _stub._cacheAddress       = cacheAddr;
    _stub._itfType            = pItfType;
    _stub._slotNumber         = itfSlotNumber;
    _stub._resolveWorkerDispl = resolveWorkerTarget - ((const UInt8 *) &_stub._resolveWorkerDispl + sizeof(DISPL));
    _stub._backpatcherDispl   = patcherTarget       - ((const UInt8 *) &_stub._backpatcherDispl   + sizeof(DISPL));
}

//-----------------------------------------------------------------------------------------------------------
ResolveHolder* ResolveHolder::FromStub(ResolveStub * pStub)
{
    ResolveHolder *resolveHolder =
        reinterpret_cast<ResolveHolder*>(reinterpret_cast<UInt8*>(pStub) - offsetof(ResolveHolder, _stub));
    ASSERT(resolveHolder->_stub._resolveEntryPoint[1] == resolveTemplate._resolveEntryPoint[1]);
    return resolveHolder;
}

//-----------------------------------------------------------------------------------------------------------
ResolveHolder* ResolveHolder::FromFailEntryPoint(PTR_Code failEntry)
{ 
    ResolveStub *pStub = reinterpret_cast<ResolveStub*>(failEntry - offsetof(ResolveStub, _failEntryPoint));
    return FromStub(pStub);
}

//-----------------------------------------------------------------------------------------------------------
ResolveHolder* ResolveHolder::FromResolveEntryPoint(PTR_Code resolveEntry)
{ 
    ResolveStub *pStub = reinterpret_cast<ResolveStub*>(resolveEntry - offsetof(ResolveStub, _resolveEntryPoint));
    return FromStub(pStub);
}

#endif // DACCESS_COMPILE

//-----------------------------------------------------------------------------------------------------------
/* static */
VirtualCallStubManager::StubKind VirtualCallStubManager::DecodeStubKind(PTR_Code stubStartAddress)
{
    StubKind stubKind = SK_LOOKUP;

    UInt16 firstWord = *((UInt16*) stubStartAddress);
    UInt8 firstByte = *((UInt8*) stubStartAddress);

    ASSERT(*((UInt16*) &dispatchTemplate._entryPoint) != *((UInt16*) &resolveTemplate._resolveEntryPoint));

    if (firstWord == *((UInt16*) &dispatchTemplate._entryPoint))
    {
        stubKind = SK_DISPATCH;
    }
    else if (firstWord == *((UInt16*) &resolveTemplate._resolveEntryPoint))
    {
        stubKind = SK_RESOLVE;
    }
    else if (firstByte == 0xcc)
    {
        stubKind = SK_BREAKPOINT;
    }

    return stubKind;
}

#ifndef X86_INSTR_JMP_IND
#define X86_INSTR_JMP_IND 0x25FF      // jmp dword ptr[addr32]
#endif

void * DecodeJumpStubTarget(UInt8 const * pModuleJumpStub)
{
    UInt8 const * pbCode = pModuleJumpStub;
    ASSERT(X86_INSTR_JMP_IND == *((UInt16 *)pbCode));
    pbCode += 2;
    return **((void***)pbCode);
}

#endif //DECLARE_DATA

#endif // _VIRTUAL_CALL_STUB_X86_H
