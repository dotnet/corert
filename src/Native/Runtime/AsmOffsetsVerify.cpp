// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "gcenv.h"
#include "gc.h"
#include "rhassert.h"
#include "RedhawkWarnings.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "TargetPtrs.h"
#include "rhbinder.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "CachedInterfaceDispatch.h"
#include "shash.h"
#include "module.h"
#include "CallDescr.h"

class AsmOffsets
{
    static_assert(sizeof(Thread::m_rgbAllocContextBuffer) >= sizeof(alloc_context), "Thread::m_rgbAllocContextBuffer is not big enough to hold an alloc_context");

#define PLAT_ASM_OFFSET(offset, cls, member) \
    static_assert((offsetof(cls, member) == 0x##offset) || (offsetof(cls, member) > 0x##offset), "Bad asm offset for '" #cls "." #member "', the actual offset is smaller than 0x" #offset "."); \
    static_assert((offsetof(cls, member) == 0x##offset) || (offsetof(cls, member) < 0x##offset), "Bad asm offset for '" #cls "." #member "', the actual offset is larger than 0x" #offset ".");

#define PLAT_ASM_SIZEOF(size,   cls        ) \
    static_assert((sizeof(cls) == 0x##size) || (sizeof(cls) > 0x##size), "Bad asm size for '" #cls "', the actual size is smaller than 0x" #size "."); \
    static_assert((sizeof(cls) == 0x##size) || (sizeof(cls) < 0x##size), "Bad asm size for '" #cls "', the actual size is larger than 0x" #size ".");

#define PLAT_ASM_CONST(constant, expr) \
    static_assert(((expr) == 0x##constant) || ((expr) > 0x##constant), "Bad asm constant for '" #expr "', the actual value is smaller than 0x" #constant "."); \
    static_assert(((expr) == 0x##constant) || ((expr) < 0x##constant), "Bad asm constant for '" #expr "', the actual value is larger than 0x" #constant ".");

#include "AsmOffsets.h"

};
