// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifndef _GCHEAPUTILITIES_H_
#define _GCHEAPUTILITIES_H_

#include "gcinterface.h"

// The singular heap instance.
GPTR_DECL(IGCHeap, g_pGCHeap);

#ifndef DACCESS_COMPILE
extern "C" {
#endif // !DACCESS_COMPILE
GPTR_DECL(uint8_t,g_lowest_address);
GPTR_DECL(uint8_t,g_highest_address);
GPTR_DECL(uint32_t,g_card_table);
#ifndef DACCESS_COMPILE
}
#endif // !DACCESS_COMPILE

#ifdef FEATURE_MANUALLY_MANAGED_CARD_BUNDLES
extern "C" uint32_t* g_card_bundle_table;
#endif // FEATURE_MANUALLY_MANAGED_CARD_BUNDLES

extern "C" uint8_t* g_ephemeral_low;
extern "C" uint8_t* g_ephemeral_high;

// g_gc_dac_vars is a structure of pointers to GC globals that the
// DAC uses. It is not exposed directly to the DAC.
extern GcDacVars g_gc_dac_vars;

// Instead of exposing g_gc_dac_vars to the DAC, a pointer to it
// is exposed here (g_gcDacGlobals). The reason for this is to avoid
// a problem in which a debugger attaches to a program while the program
// is in the middle of initializing the GC DAC vars - if the "publishing"
// of DAC vars isn't atomic, the debugger could see a partially initialized
// GcDacVars structure.
//
// Instead, the debuggee "publishes" GcDacVars by assigning a pointer to g_gc_dac_vars
// to this global, and the DAC will read this global.
typedef DPTR(GcDacVars) PTR_GcDacVars;
GPTR_DECL(GcDacVars, g_gcDacGlobals);

// GCHeapUtilities provides a number of static methods
// that operate on the global heap instance. It can't be
// instantiated.
class GCHeapUtilities {
public:
    // Retrieves the GC heap.
    inline static IGCHeap* GetGCHeap() 
    {
        assert(g_pGCHeap != nullptr);
        return g_pGCHeap;
    }

    // Returns true if the heap has been initialized, false otherwise.
    inline static bool IsGCHeapInitialized()
    {
        return g_pGCHeap != nullptr;
    }

    // Returns true if a the heap is initialized and a garbage collection
    // is in progress, false otherwise.
    inline static BOOL IsGCInProgress(BOOL bConsiderGCStart = FALSE)
    {
        return GetGCHeap()->IsGCInProgressHelper(bConsiderGCStart);
    }

    // Returns true if the held GC heap is a Server GC heap, false otherwise.
    inline static bool IsServerHeap()
    {
        LIMITED_METHOD_CONTRACT;
#ifdef FEATURE_SVR_GC
        _ASSERTE(IGCHeap::gcHeapType != IGCHeap::GC_HEAP_INVALID);
        return (IGCHeap::gcHeapType == IGCHeap::GC_HEAP_SVR);
#else // FEATURE_SVR_GC
        return false;
#endif // FEATURE_SVR_GC
    }


private:
    // This class should never be instantiated.
    GCHeapUtilities() = delete;
};

#endif // _GCHEAPUTILITIES_H_
