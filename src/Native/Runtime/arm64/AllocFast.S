// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <unixasmmacros.inc>
#include "AsmOffsets.inc"

// GC type flags
GC_ALLOC_FINALIZE               = 1

//
// Rename fields of nested structs
//
OFFSETOF__Thread__m_alloc_context__alloc_ptr        = OFFSETOF__Thread__m_rgbAllocContextBuffer + OFFSETOF__gc_alloc_context__alloc_ptr
OFFSETOF__Thread__m_alloc_context__alloc_limit      = OFFSETOF__Thread__m_rgbAllocContextBuffer + OFFSETOF__gc_alloc_context__alloc_limit



// Allocate non-array, non-finalizable object. If the allocation doesn't fit into the current thread's
// allocation context then automatically fallback to the slow allocation path.
//  x0 == EEType
    LEAF_ENTRY RhpNewFast, _TEXT

        // x1 = GetThread()
#ifdef FEATURE_EMULATED_TLS
        GETTHREAD_ETLS_1
#else
        INLINE_GETTHREAD x1
#endif

        //
        // x0 contains EEType pointer
        //
        ldr         w2, [x0, #OFFSETOF__EEType__m_uBaseSize]

        //
        // x0: EEType pointer
        // x1: Thread pointer
        // x2: base size
        //

        // Load potential new object address into x12.
        ldr         x12, [x1, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]

        // Determine whether the end of the object would lie outside of the current allocation context. If so,
        // we abandon the attempt to allocate the object directly and fall back to the slow helper.
        add         x2, x2, x12
        ldr         x13, [x1, #OFFSETOF__Thread__m_alloc_context__alloc_limit]
        cmp         x2, x13
        bhi         RhpNewFast_RarePath

        // Update the alloc pointer to account for the allocation.
        str         x2, [x1, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]

        // Set the new objects EEType pointer
        str         x0, [x12, #OFFSETOF__Object__m_pEEType]

        mov         x0, x12
        ret

RhpNewFast_RarePath:
        mov         x1, #0
        b           RhpNewObject
    LEAF_END RhpNewFast, _TEXT

// Allocate non-array object with finalizer.
//  x0 == EEType
    LEAF_ENTRY RhpNewFinalizable, _TEXT
        mov         x1, #GC_ALLOC_FINALIZE
        b           RhpNewObject
    LEAF_END RhpNewFinalizable, _TEXT

// Allocate non-array object.
//  x0 == EEType
//  x1 == alloc flags
    NESTED_ENTRY RhpNewObject, _TEXT, NoHandler

        PUSH_COOP_PINVOKE_FRAME x3

        // x3: transition frame

        // Preserve the EEType in x19
        mov         x19, x0

        ldr         w2, [x0, #OFFSETOF__EEType__m_uBaseSize]

        // Call the rest of the allocation helper.
        // void* RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        bl          RhpGcAlloc

        // Set the new objects EEType pointer on success.
        cbz         x0, NewOutOfMemory
        str         x19, [x0, #OFFSETOF__Object__m_pEEType]

        // If the object is bigger than RH_LARGE_OBJECT_SIZE, we must publish it to the BGC
        ldr         w1, [x19, #OFFSETOF__EEType__m_uBaseSize]
        movz        x2, #(RH_LARGE_OBJECT_SIZE & 0xFFFF)
        movk        x2, #(RH_LARGE_OBJECT_SIZE >> 16), lsl #16
        cmp         x1, x2
        blo         New_SkipPublish

        // x0: object
        // x1: already contains object size
        bl          RhpPublishObject    // x0: this function returns the object that was passed-in

New_SkipPublish:

        POP_COOP_PINVOKE_FRAME
        EPILOG_RETURN

NewOutOfMemory:
        // This is the OOM failure path. We are going to tail-call to a managed helper that will throw
        // an out of memory exception that the caller of this allocator understands.

        mov         x0, x19            // EEType pointer
        mov         x1, 0              // Indicate that we should throw OOM.

        POP_COOP_PINVOKE_FRAME
        b RhExceptionHandling_FailedAllocation

    NESTED_END RhpNewObject, _TEXT

// Allocate a string.
//  x0 == EEType
//  x1 == element/character count
    LEAF_ENTRY RhNewString, _TEXT
        // Make sure computing the overall allocation size wont overflow
        movz        x2, MAX_STRING_LENGTH & 0xFFFF
        movk        x2, MAX_STRING_LENGTH >> 16, lsl 16
        cmp         x1, x2
        bhi         StringSizeOverflow

        // Compute overall allocation size (align(base size + (element size * elements), 8)).
        mov         w2, #STRING_COMPONENT_SIZE
        mov         x3, #(STRING_BASE_SIZE + 7)
        umaddl      x2, w1, w2, x3          // x2 = w1 * w2 + x3
        and         x2, x2, #-8

        // x0 == EEType
        // x1 == element count
        // x2 == string size

#ifdef FEATURE_EMULATED_TLS
        GETTHREAD_ETLS_3
#else
        INLINE_GETTHREAD x3
#endif

        // Load potential new object address into x12.
        ldr         x12, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]

        // Determine whether the end of the object would lie outside of the current allocation context. If so,
        // we abandon the attempt to allocate the object directly and fall back to the slow helper.
        add         x2, x2, x12
        ldr         x12, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_limit]
        cmp         x2, x12
        bhi         RhpNewArrayRare

        // Reload new object address into r12.
        ldr         x12, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]

        // Update the alloc pointer to account for the allocation.
        str         x2, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]

        // Set the new objects EEType pointer and element count.
        str         x0, [x12, #OFFSETOF__Object__m_pEEType]
        str         x1, [x12, #OFFSETOF__Array__m_Length]

        // Return the object allocated in x0.
        mov         x0, x12

        ret

StringSizeOverflow:
        // We get here if the length of the final string object can not be represented as an unsigned
        // 32-bit value. We are going to tail-call to a managed helper that will throw
        // an OOM exception that the caller of this allocator understands.

        // x0 holds EEType pointer already
        mov         x1, #1                  // Indicate that we should throw OverflowException
        b           RhExceptionHandling_FailedAllocation
    LEAF_END    RhNewString, _Text

// Allocate one dimensional, zero based array (SZARRAY).
//  x0 == EEType
//  x1 == element count
    LEAF_ENTRY RhpNewArray, _Text

        // We want to limit the element count to the non-negative 32-bit int range.
        // If the element count is <= 0x7FFFFFFF, no overflow is possible because the component
        // size is <= 0xffff (it is an unsigned 16-bit value), and the base size for the worst
        // case (32 dimensional MdArray) is less than 0xffff, and thus the product fits in 64 bits.
        mov         x2, #0x7FFFFFFF
        cmp         x1, x2
        bhi         ArraySizeOverflow

        ldrh        w2, [x0, #OFFSETOF__EEType__m_usComponentSize]
        umull       x2, w1, w2
        ldr         w3, [x0, #OFFSETOF__EEType__m_uBaseSize]
        add         x2, x2, x3
        add         x2, x2, #7
        and         x2, x2, #-8

        // x0 == EEType
        // x1 == element count
        // x2 == array size

#ifdef FEATURE_EMULATED_TLS
        GETTHREAD_ETLS_3
#else
        INLINE_GETTHREAD x3
#endif

        // Load potential new object address into x12.
        ldr         x12, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]

        // Determine whether the end of the object would lie outside of the current allocation context. If so,
        // we abandon the attempt to allocate the object directly and fall back to the slow helper.
        add         x2, x2, x12
        ldr         x12, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_limit]
        cmp         x2, x12
        bhi         RhpNewArrayRare

        // Reload new object address into x12.
        ldr         x12, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]

        // Update the alloc pointer to account for the allocation.
        str         x2, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]

        // Set the new objects EEType pointer and element count.
        str         x0, [x12, #OFFSETOF__Object__m_pEEType]
        str         x1, [x12, #OFFSETOF__Array__m_Length]

        // Return the object allocated in r0.
        mov         x0, x12

        ret

ArraySizeOverflow:
        // We get here if the size of the final array object can not be represented as an unsigned
        // 32-bit value. We are going to tail-call to a managed helper that will throw
        // an overflow exception that the caller of this allocator understands.

        // x0 holds EEType pointer already
        mov         x1, #1                  // Indicate that we should throw OverflowException
        b           RhExceptionHandling_FailedAllocation
    LEAF_END    RhpNewArray, _TEXT

// Allocate one dimensional, zero based array (SZARRAY) using the slow path that calls a runtime helper.
//  x0 == EEType
//  x1 == element count
//  x2 == array size + Thread::m_alloc_context::alloc_ptr
//  x3 == Thread
    NESTED_ENTRY RhpNewArrayRare, _TEXT, NoHandler

        // Recover array size by subtracting the alloc_ptr from x2.
        ldr x12, [x3, #OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        sub x2, x2, x12

        PUSH_COOP_PINVOKE_FRAME x3

        // Preserve data we will need later into the callee saved registers
        mov         x19, x0             // Preserve EEType
        mov         x20, x1             // Preserve element count
        mov         x21, x2             // Preserve array size

        mov         x1, #0

        // void* RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        bl          RhpGcAlloc

        // Set the new objects EEType pointer and length on success.
        cbz         x0, ArrayOutOfMemory

        // Success, set the array type and element count in the new object.
        str         x19, [x0, #OFFSETOF__Object__m_pEEType]
        str         x20, [x0, #OFFSETOF__Array__m_Length]

        // If the object is bigger than RH_LARGE_OBJECT_SIZE, we must publish it to the BGC
        movz        x2, #(RH_LARGE_OBJECT_SIZE & 0xFFFF)
        movk        x2, #(RH_LARGE_OBJECT_SIZE >> 16), lsl #16
        cmp         x21, x2
        blo         NewArray_SkipPublish

        // x0 = newly allocated array. x1 = size
        mov         x1, x21
        bl          RhpPublishObject

NewArray_SkipPublish:

        POP_COOP_PINVOKE_FRAME
        EPILOG_RETURN

ArrayOutOfMemory:
        // This is the OOM failure path. We are going to tail-call to a managed helper that will throw
        // an out of memory exception that the caller of this allocator understands.

        mov         x0, x19             // EEType Pointer
        mov         x1, 0               // Indicate that we should throw OOM.

        POP_COOP_PINVOKE_FRAME
        b RhExceptionHandling_FailedAllocation

    NESTED_END RhpNewArrayRare, _TEXT
