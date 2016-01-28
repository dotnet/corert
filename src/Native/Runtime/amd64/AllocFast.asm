;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

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
        mov         eax, [rcx + OFFSETOF__EEType__m_uBaseSize]

        ;;
        ;; eax: base size
        ;; rcx: EEType pointer
        ;; rdx: Thread pointer
        ;;

        add         rax, [rdx + OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        cmp         rax, [rdx + OFFSETOF__Thread__m_alloc_context__alloc_limit]
        ja          RhpNewFast_RarePath

        ;; set the new alloc pointer
        mov         [rdx + OFFSETOF__Thread__m_alloc_context__alloc_ptr], rax

        ;; calc the new object pointer
        mov         edx, dword ptr [rcx + OFFSETOF__EEType__m_uBaseSize]
        sub         rax, rdx

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

        INLINE_GETTHREAD        rax, r10        ; rax <- Thread pointer, r10 <- trashed
        mov                     r11, rax        ; r11 <- Thread pointer
        PUSH_COOP_PINVOKE_FRAME rax, r10, no_extraStack ; rax <- in: Thread, out: trashed, r10 <- trashed
        END_PROLOGUE

        ; RCX: EEType
        ; EDX: alloc flags
        ; R11: Thread *

        ;; Preserve the EEType in RSI
        mov         rsi, rcx

        mov         r9, rcx                                         ; pEEType
        mov         r8d, edx                                        ; uFlags
        mov         edx, [rsi + OFFSETOF__EEType__m_uBaseSize]      ; cbSize
        mov         rcx, r11                                        ; pThread

        ;; Call the rest of the allocation helper.
        ;; void* RedhawkGCInterface::Alloc(Thread *pThread, UIntNative cbSize, UInt32 uFlags, EEType *pEEType)
        call        REDHAWKGCINTERFACE__ALLOC

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

        POP_COOP_PINVOKE_FRAME  no_extraStack
        ret

NewOutOfMemory:
        ;; This is the OOM failure path. We're going to tail-call to a managed helper that will throw
        ;; an out of memory exception that the caller of this allocator understands.

        mov         rcx, r9             ; EEType pointer
        xor         edx, edx            ; Indicate that we should throw OOM.

        POP_COOP_PINVOKE_FRAME  no_extraStack

        jmp         RhExceptionHandling_FailedAllocation
NESTED_END RhpNewObject, _TEXT


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

        ; rax == array size
        ; rcx == EEType
        ; rdx == scratch
        ; r8  == element count

        INLINE_GETTHREAD        rdx, r9

        mov         r9, rax
        add         rax, [rdx + OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        jc          RhpNewArrayRare

        ; rax == new alloc ptr
        ; rcx == EEType
        ; rdx == thread
        ; r8 == element count
        ; r9 == array size
        cmp         rax, [rdx + OFFSETOF__Thread__m_alloc_context__alloc_limit]
        ja          RhpNewArrayRare

        mov         [rdx + OFFSETOF__Thread__m_alloc_context__alloc_ptr], rax

        ; calc the new object pointer
        sub         rax, r9

        mov         [rax + OFFSETOF__Object__m_pEEType], rcx
        mov         [rax + OFFSETOF__Array__m_Length], r8d

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
        ; rdx == thread
        ; r8 == element count
        ; r9 == array size

        mov                     r11, rdx        ; r11 <- Thread pointer
        PUSH_COOP_PINVOKE_FRAME rdx, r10, no_extraStack ; rdx <- in: Thread, out: trashed, r10 <- trashed
        END_PROLOGUE

        ; R11: Thread *

        ; Preserve the EEType in RSI
        mov         rsi, rcx
        ; Preserve the size in RDI
        mov         rdi, r9
        
        ; Preserve the element count in RBX
        mov         rbx, r8

        mov         rcx, r11        ; pThread
        mov         rdx, r9         ; cbSize
        xor         r8d, r8d        ; uFlags
        mov         r9, rsi         ; pEEType

        ; Call the rest of the allocation helper.
        ; void* RedhawkGCInterface::Alloc(Thread *pThread, UIntNative cbSize, UInt32 uFlags, EEType *pEEType)
        call        REDHAWKGCINTERFACE__ALLOC

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

        POP_COOP_PINVOKE_FRAME no_extraStack
        ret

ArrayOutOfMemory:     
        ;; This is the OOM failure path. We're going to tail-call to a managed helper that will throw
        ;; an out of memory exception that the caller of this allocator understands.

        mov         rcx, rsi            ; EEType pointer
        xor         edx, edx            ; Indicate that we should throw OOM.

        POP_COOP_PINVOKE_FRAME no_extraStack

        jmp         RhExceptionHandling_FailedAllocation

NESTED_END RhpNewArrayRare, _TEXT


        END
