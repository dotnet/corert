//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Overload new and delete operators to provide the required Redhawk semantics.
//

#ifndef __NEW_INCLUDED
#define __NEW_INCLUDED

#ifndef DACCESS_COMPILE

__declspec(selectany) HANDLE g_hHeap = NULL;

inline void * BaseNew(size_t cbSize)
{
    //
    // @TODO: revisit this implementation
    //
    if (NULL == g_hHeap)
    {
        // NOTE: GetProcessHeap is indempotent, so all threads racing to initialize this global will 
        // initialize it with the same value.
        g_hHeap = PalGetProcessHeap();
    }
    return PalHeapAlloc(g_hHeap, 0, cbSize);
}

inline void BaseDelete(void * pvMemory)
{
    //
    // @TODO: revisit this implementation
    //

    //ASSERT(g_hHeap != NULL);
    PalHeapFree(g_hHeap, 0, pvMemory);
}

//
// All 'operator new' variations have the same contract, which is to return NULL when out of memory.
//
inline void * __cdecl operator new(size_t cbSize) { return BaseNew(cbSize); }               // normal
inline void * __cdecl operator new[](size_t cbSize) { return BaseNew(cbSize); }             // array
inline void * __cdecl operator new(size_t cbSize, void * pvWhere) { return pvWhere; }   // placement
inline void __cdecl operator delete(void * pvMemory) { BaseDelete(pvMemory); }              // normal
inline void __cdecl operator delete[](void * pvMemory) { BaseDelete(pvMemory); }            // array
inline void __cdecl operator delete(void * pvMemory, void * pvWhere) { }                // placement

#endif // DACCESS_COMPILE

#endif // !__NEW_INCLUDED
