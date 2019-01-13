// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifndef _GCHEAPWALKHELPER_H_
#define _GCHEAPWALKHELPER_H_


// These two functions are utilized to scan the heap if requested by ETW
// or a profiler. The implementations of these two functions are in profheapwalkhelper.cpp.
#if defined(FEATURE_EVENT_TRACE) || defined(GC_PROFILING)
void ScanRootsHelper(Object* pObj, Object** ppRoot, ScanContext* pSC, DWORD dwFlags);
BOOL HeapWalkHelper(Object* pBO, void* pvContext);
#endif


#endif // _GCHEAPWALKHELPER_H_
