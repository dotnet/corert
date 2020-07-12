;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

include asmmacros.inc


;; Allocate non-array, non-finalizable object. If the allocation doesn't fit into the current thread's
;; allocation context then automatically fallback to the slow allocation path.
;;  RCX == EEType
LEAF_ENTRY RhpNewFast, _TEXT

        ;; rdx = GetThread(), TRASHES rax
        INLINE_GETTHREAD rdx, rax

        ;;
        ;; rcx contains EEType pointer
        ;;
        mov         r8d, [rcx + OFFSETOF__EEType__m_uBaseSize]

        ;;
        ;; eax: base size
        ;; rcx: EEType pointer
        ;; rdx: Thread pointer
        ;;

        mov         rax, [rdx + OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        add         r8, rax
        cmp         r8, [rdx + OFFSETOF__Thread__m_alloc_context__alloc_limit]
        ja          RhpNewFast_RarePath

        ;; set the new alloc pointer
        mov         [rdx + OFFSETOF__Thread__m_alloc_context__alloc_ptr], r8

        ;; set the new object's EEType pointer
        mov         [rax], rcx
        ret

RhpNewFast_RarePath:
        xor         edx, edx
        jmp         RhpNewObject

LEAF_END RhpNewFast, _TEXT



;; Allocate non-array object with finalizer
;;  RCX == EEType
LEAF_ENTRY RhpNewFinalizable, _TEXT
        mov         edx, GC_ALLOC_FINALIZE
        jmp         RhpNewObject
LEAF_END RhpNewFinalizable, _TEXT



;; Allocate non-array object
;;  RCX == EEType
;;  EDX == alloc flags
NESTED_ENTRY RhpNewObject, _TEXT

        PUSH_COOP_PINVOKE_FRAME r9
        END_PROLOGUE

        ; R9: transition frame

        ;; Preserve the EEType in RSI
        mov         rsi, rcx

        mov         r8d, [rsi + OFFSETOF__EEType__m_uBaseSize]      ; cbSize

        ;; Call the rest of the allocation helper.
        ;; void* RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        call        RhpGcAlloc

        ;; Set the new object's EEType pointer on success.
        test        rax, rax
        jz          NewOutOfMemory
        mov         [rax + OFFSETOF__Object__m_pEEType], rsi

        ;; If the object is bigger than RH_LARGE_OBJECT_SIZE, we must publish it to the BGC
        mov         edx, [rsi + OFFSETOF__EEType__m_uBaseSize]
        cmp         rdx, RH_LARGE_OBJECT_SIZE
        jb          New_SkipPublish
        mov         rcx, rax            ;; rcx: object
                                        ;; rdx: already contains object size
        call        RhpPublishObject    ;; rax: this function returns the object that was passed-in
New_SkipPublish: 

        POP_COOP_PINVOKE_FRAME
        ret

NewOutOfMemory:
        ;; This is the OOM failure path. We're going to tail-call to a managed helper that will throw
        ;; an out of memory exception that the caller of this allocator understands.

        mov         rcx, rsi            ; EEType pointer
        xor         edx, edx            ; Indicate that we should throw OOM.

        POP_COOP_PINVOKE_FRAME

        jmp         RhExceptionHandling_FailedAllocation
NESTED_END RhpNewObject, _TEXT


;; Allocate a string.
;;  RCX == EEType
;;  EDX == character/element count
LEAF_ENTRY RhNewString, _TEXT

        ; we want to limit the element count to the non-negative 32-bit int range
        cmp         rdx, MAX_STRING_LENGTH
        ja          StringSizeOverflow

        ; Compute overall allocation size (align(base size + (element size * elements), 8)).
        lea         rax, [(rdx * STRING_COMPONENT_SIZE) + (STRING_BASE_SIZE + 7)]
        and         rax, -8

        ; rax == string size
        ; rcx == EEType
        ; rdx == element count

        INLINE_GETTHREAD r10, r8

        mov         r8, rax
        add         rax, [r10 + OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        jc          RhpNewArrayRare

        ; rax == new alloc ptr
        ; rcx == EEType
        ; rdx == element count
        ; r8 == array size
        ; r10 == thread
        cmp         rax, [r10 + OFFSETOF__Thread__m_alloc_context__alloc_limit]
        ja          RhpNewArrayRare

        mov         [r10 + OFFSETOF__Thread__m_alloc_context__alloc_ptr], rax

        ; calc the new object pointer
        sub         rax, r8

        mov         [rax + OFFSETOF__Object__m_pEEType], rcx
        mov         [rax + OFFSETOF__String__m_Length], edx

        ret

StringSizeOverflow:
        ; We get here if the size of the final string object can't be represented as an unsigned 
        ; 32-bit value. We're going to tail-call to a managed helper that will throw
        ; an OOM exception that the caller of this allocator understands.

        ; rcx holds EEType pointer already
        xor         edx, edx            ; Indicate that we should throw OOM.
        jmp         RhExceptionHandling_FailedAllocation
LEAF_END RhNewString, _TEXT


;; Allocate one dimensional, zero based array (SZARRAY).
;;  RCX == EEType
;;  EDX == element count
LEAF_ENTRY RhpNewArray, _TEXT

        ; we want to limit the element count to the non-negative 32-bit int range
        cmp         rdx, 07fffffffh
        ja          ArraySizeOverflow

        ; save element count
        mov         r8, rdx

        ; Compute overall allocation size (align(base size + (element size * elements), 8)).
        movzx       eax, word ptr [rcx + OFFSETOF__EEType__m_usComponentSize]
        mul         rdx
        mov         edx, [rcx + OFFSETOF__EEType__m_uBaseSize]
        add         rax, rdx
        add         rax, 7
        and         rax, -8

        mov         rdx, r8

        ; rax == array size
        ; rcx == EEType
        ; rdx == element count

        INLINE_GETTHREAD r10, r8

        mov         r8, rax
        add         rax, [r10 + OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        jc          RhpNewArrayRare

        ; rax == new alloc ptr
        ; rcx == EEType
        ; rdx == element count
        ; r8 == array size
        ; r10 == thread
        cmp         rax, [r10 + OFFSETOF__Thread__m_alloc_context__alloc_limit]
        ja          RhpNewArrayRare

        mov         [r10 + OFFSETOF__Thread__m_alloc_context__alloc_ptr], rax

        ; calc the new object pointer
        sub         rax, r8

        mov         [rax + OFFSETOF__Object__m_pEEType], rcx
        mov         [rax + OFFSETOF__Array__m_Length], edx

        ret

ArraySizeOverflow:
        ; We get here if the size of the final array object can't be represented as an unsigned 
        ; 32-bit value. We're going to tail-call to a managed helper that will throw
        ; an overflow exception that the caller of this allocator understands.

        ; rcx holds EEType pointer already
        mov         edx, 1              ; Indicate that we should throw OverflowException
        jmp         RhExceptionHandling_FailedAllocation
LEAF_END RhpNewArray, _TEXT

NESTED_ENTRY RhpNewArrayRare, _TEXT

        ; rcx == EEType
        ; rdx == element count
        ; r8 == array size

        PUSH_COOP_PINVOKE_FRAME r9
        END_PROLOGUE

        ; r9: transition frame

        ; Preserve the EEType in RSI
        mov         rsi, rcx
        ; Preserve the element count in RBX
        mov         rbx, rdx
        ; Preserve the size in RDI
        mov         rdi, r8

        ; passing EEType in rcx
        xor         rdx, rdx        ; uFlags
        ; pasing size in r8
        ; pasing pTransitionFrame in r9

        ; Call the rest of the allocation helper.
        ; void* RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        call        RhpGcAlloc

        ; Set the new object's EEType pointer and length on success.
        test        rax, rax
        jz          ArrayOutOfMemory
        mov         [rax + OFFSETOF__Object__m_pEEType], rsi
        mov         [rax + OFFSETOF__Array__m_Length], ebx

        ;; If the object is bigger than RH_LARGE_OBJECT_SIZE, we must publish it to the BGC
        cmp         rdi, RH_LARGE_OBJECT_SIZE
        jb          NewArray_SkipPublish
        mov         rcx, rax            ;; rcx: object
        mov         rdx, rdi            ;; rdx: object size
        call        RhpPublishObject    ;; rax: this function returns the object that was passed-in
NewArray_SkipPublish: 

        POP_COOP_PINVOKE_FRAME
        ret

ArrayOutOfMemory:     
        ;; This is the OOM failure path. We're going to tail-call to a managed helper that will throw
        ;; an out of memory exception that the caller of this allocator understands.

        mov         rcx, rsi            ; EEType pointer
        xor         edx, edx            ; Indicate that we should throw OOM.

        POP_COOP_PINVOKE_FRAME

        jmp         RhExceptionHandling_FailedAllocation

NESTED_END RhpNewArrayRare, _TEXT


        END
