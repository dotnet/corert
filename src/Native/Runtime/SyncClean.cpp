//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
#include "slist.h"
#include "holder.h"
#include "SpinLock.h"
#include "rhbinder.h"
#ifdef FEATURE_VSD
#include "virtualcallstub.h"
#endif // FEATURE_VSD
#include "CachedInterfaceDispatch.h"

#include "SyncClean.hpp"

void SyncClean::Terminate()
{
    CleanUp();
}

void SyncClean::CleanUp ()
{
#ifdef FEATURE_VSD
    // Give others we want to reclaim during the GC sync point a chance to do it
    VirtualCallStubManager::ReclaimAll();
#elif defined(FEATURE_CACHED_INTERFACE_DISPATCH)
    // Update any interface dispatch caches that were unsafe to modify outside of this GC.
    ReclaimUnusedInterfaceDispatchCaches();
#endif
}
