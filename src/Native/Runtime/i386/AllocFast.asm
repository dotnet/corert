;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

        .586
        .model  flat
        option  casemap:none
        .code


include AsmMacros.inc

;; Allocate non-array, non-finalizable object. If the allocation doesn't fit into the current thread's
;; allocation context then automatically fallback to the slow allocation path.
;;  ECX == EEType
FASTCALL_FUNC   RhpNewFast, 4

        ;; edx = GetThread(), TRASHES eax
        INLINE_GETTHREAD edx, eax

        ;;
        ;; ecx contains EEType pointer
        ;;
        mov         eax, [ecx + OFFSETOF__EEType__m_uBaseSize]

        ;;
        ;; eax: base size
        ;; ecx: EEType pointer
        ;; edx: Thread pointer
        ;;

        add         eax, [edx + OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        cmp         eax, [edx + OFFSETOF__Thread__m_alloc_context__alloc_limit]
        ja          AllocFailed

        ;; set the new alloc pointer
        mov         [edx + OFFSETOF__Thread__m_alloc_context__alloc_ptr], eax

        ;; calc the new object pointer
        sub         eax, [ecx + OFFSETOF__EEType__m_uBaseSize]

        ;; set the new object's EEType pointer
        mov         [eax], ecx
        ret

AllocFailed:

        ;;
        ;; SLOW PATH, call RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        ;;
        ;; ecx: EEType pointer
        ;;
        push        ebp
        mov         ebp, esp

        PUSH_COOP_PINVOKE_FRAME edx

        ;; Preserve EEType in ESI.
        mov         esi, ecx

        ;; Push alloc helper arguments
        push        edx                                             ; transition frame
        push        dword ptr [ecx + OFFSETOF__EEType__m_uBaseSize] ; Size
        xor         edx, edx                                        ; Flags
        ;; Passing EEType in ecx

        ;; void* RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        call        RhpGcAlloc

        ;; Set the new object's EEType pointer on success.
        test        eax, eax
        jz          NewFast_OOM
        mov         [eax + OFFSETOF__Object__m_pEEType], esi

        ;; If the object is bigger than RH_LARGE_OBJECT_SIZE, we must publish it to the BGC
        mov         edx, [esi + OFFSETOF__EEType__m_uBaseSize]
        cmp         edx, RH_LARGE_OBJECT_SIZE
        jb          NewFast_SkipPublish
        mov         ecx, eax            ;; ecx: object
                                        ;; edx: already contains object size
        call        RhpPublishObject    ;; eax: this function returns the object that was passed-in
NewFast_SkipPublish: 

        POP_COOP_PINVOKE_FRAME

        pop         ebp
        ret

NewFast_OOM:
        ;; This is the failure path. We're going to tail-call to a managed helper that will throw
        ;; an out of memory exception that the caller of this allocator understands.

        mov         eax, esi            ; Preserve EEType pointer over POP_COOP_PINVOKE_FRAME

        POP_COOP_PINVOKE_FRAME

        ;; Cleanup our ebp frame
        pop         ebp

        mov         ecx, eax            ; EEType pointer
        xor         edx, edx            ; Indicate that we should throw OOM.
        jmp         RhExceptionHandling_FailedAllocation

FASTCALL_ENDFUNC

;; Allocate non-array object with finalizer.
;;  ECX == EEType
FASTCALL_FUNC   RhpNewFinalizable, 4
        ;; Create EBP frame.
        push        ebp
        mov         ebp, esp

        PUSH_COOP_PINVOKE_FRAME edx

        ;; Preserve EEType in ESI
        mov         esi, ecx

        ;; Push alloc helper arguments
        push        edx                                             ; transition frame
        push        dword ptr [ecx + OFFSETOF__EEType__m_uBaseSize] ; Size
        mov         edx, GC_ALLOC_FINALIZE                          ; Flags
        ;; Passing EEType in ecx

        ;; void* RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        call        RhpGcAlloc

        ;; Set the new object's EEType pointer on success.
        test        eax, eax
        jz          NewFinalizable_OOM
        mov         [eax + OFFSETOF__Object__m_pEEType], esi

        ;; If the object is bigger than RH_LARGE_OBJECT_SIZE, we must publish it to the BGC
        mov         edx, [esi + OFFSETOF__EEType__m_uBaseSize]
        cmp         edx, RH_LARGE_OBJECT_SIZE
        jb          NewFinalizable_SkipPublish
        mov         ecx, eax            ;; ecx: object
                                        ;; edx: already contains object size
        call        RhpPublishObject    ;; eax: this function returns the object that was passed-in
NewFinalizable_SkipPublish: 

        POP_COOP_PINVOKE_FRAME

        ;; Collapse EBP frame and return
        pop         ebp
        ret
        
NewFinalizable_OOM:
        ;; This is the failure path. We're going to tail-call to a managed helper that will throw
        ;; an out of memory exception that the caller of this allocator understands.

        mov         eax, esi            ; Preserve EEType pointer over POP_COOP_PINVOKE_FRAME

        POP_COOP_PINVOKE_FRAME

        ;; Cleanup our ebp frame
        pop         ebp
        
        mov         ecx, eax            ; EEType pointer
        xor         edx, edx            ; Indicate that we should throw OOM.
        jmp         RhExceptionHandling_FailedAllocation
        
FASTCALL_ENDFUNC

;; Allocate a new string.
;;  ECX == EEType
;;  EDX == element count
FASTCALL_FUNC   RhNewString, 8

        push        ecx
        push        edx

        ;; Make sure computing the aligned overall allocation size won't overflow
        cmp         edx, MAX_STRING_LENGTH
        ja          StringSizeOverflow

        ; Compute overall allocation size (align(base size + (element size * elements), 4)).
        lea         eax, [(edx * STRING_COMPONENT_SIZE) + (STRING_BASE_SIZE + 3)]
        and         eax, -4

        ; ECX == EEType
        ; EAX == allocation size
        ; EDX == scratch

        INLINE_GETTHREAD    edx, ecx        ; edx = GetThread(), TRASHES ecx

        ; ECX == scratch
        ; EAX == allocation size
        ; EDX == thread

        mov         ecx, eax
        add         eax, [edx + OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        jc          StringAllocContextOverflow
        cmp         eax, [edx + OFFSETOF__Thread__m_alloc_context__alloc_limit]
        ja          StringAllocContextOverflow

        ; ECX == allocation size
        ; EAX == new alloc ptr
        ; EDX == thread

        ; set the new alloc pointer
        mov         [edx + OFFSETOF__Thread__m_alloc_context__alloc_ptr], eax

        ; calc the new object pointer
        sub         eax, ecx

        pop         edx
        pop         ecx

        ; set the new object's EEType pointer and element count
        mov         [eax + OFFSETOF__Object__m_pEEType], ecx
        mov         [eax + OFFSETOF__String__m_Length], edx
        ret

StringAllocContextOverflow:
        ; ECX == string size
        ;   original ECX pushed
        ;   original EDX pushed

        ; Re-push original ECX
        push        [esp + 4]

        ; Create EBP frame.
        mov         [esp + 8], ebp
        lea         ebp, [esp + 8]

        PUSH_COOP_PINVOKE_FRAME edx

        ; Preserve the string size in edi
        mov         edi, ecx

        ; Get the EEType and put it in ecx.
        mov         ecx, dword ptr [ebp - 8]

        ; Push alloc helper arguments (thread, size, flags, EEType).
        push        edx                                             ; transition frame
        push        edi                                             ; Size
        xor         edx, edx                                        ; Flags
        ;; Passing EEType in ecx

        ;; void* RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        call        RhpGcAlloc

        ; Set the new object's EEType pointer and length on success.
        test        eax, eax
        jz          StringOutOfMemoryWithFrame

        mov         ecx, [ebp - 8]
        mov         edx, [ebp - 4]
        mov         [eax + OFFSETOF__Object__m_pEEType], ecx
        mov         [eax + OFFSETOF__String__m_Length], edx

        ;; If the object is bigger than RH_LARGE_OBJECT_SIZE, we must publish it to the BGC
        cmp         edi, RH_LARGE_OBJECT_SIZE
        jb          NewString_SkipPublish
        mov         ecx, eax            ;; ecx: object
        mov         edx, edi            ;; edx: object size
        call        RhpPublishObject    ;; eax: this function returns the object that was passed-in
NewString_SkipPublish: 

        POP_COOP_PINVOKE_FRAME
        add         esp, 8          ; pop ecx / edx
        pop         ebp
        ret

StringOutOfMemoryWithFrame:
        ; This is the OOM failure path. We're going to tail-call to a managed helper that will throw
        ; an out of memory exception that the caller of this allocator understands.

        mov         eax, [ebp - 8]  ; Preserve EEType pointer over POP_COOP_PINVOKE_FRAME

        POP_COOP_PINVOKE_FRAME
        add         esp, 8          ; pop ecx / edx
        pop         ebp             ; restore ebp

        mov         ecx, eax        ; EEType pointer
        xor         edx, edx        ; Indicate that we should throw OOM.
        jmp         RhExceptionHandling_FailedAllocation

StringSizeOverflow:
        ;; We get here if the size of the final string object can't be represented as an unsigned 
        ;; 32-bit value. We're going to tail-call to a managed helper that will throw
        ;; an OOM exception that the caller of this allocator understands.

        add         esp, 8          ; pop ecx / edx

        ;; ecx holds EEType pointer already
        xor         edx, edx            ; Indicate that we should throw OOM.
        jmp         RhExceptionHandling_FailedAllocation

FASTCALL_ENDFUNC


;; Allocate one dimensional, zero based array (SZARRAY).
;;  ECX == EEType
;;  EDX == element count
FASTCALL_FUNC   RhpNewArray, 8

        push        ecx
        push        edx

        ; Compute overall allocation size (align(base size + (element size * elements), 4)).
        ; if the element count is <= 0x10000, no overflow is possible because the component size is
        ; <= 0xffff, and thus the product is <= 0xffff0000, and the base size for the worst case
        ; (32 dimensional MdArray) is less than 0xffff.
        movzx       eax, word ptr [ecx + OFFSETOF__EEType__m_usComponentSize]
        cmp         edx,010000h
        ja          ArraySizeBig
        mul         edx
        add         eax, [ecx + OFFSETOF__EEType__m_uBaseSize]
        add         eax, 3
ArrayAlignSize:
        and         eax, -4

        ; ECX == EEType
        ; EAX == array size
        ; EDX == scratch

        INLINE_GETTHREAD    edx, ecx        ; edx = GetThread(), TRASHES ecx

        ; ECX == scratch
        ; EAX == array size
        ; EDX == thread

        mov         ecx, eax
        add         eax, [edx + OFFSETOF__Thread__m_alloc_context__alloc_ptr]
        jc          ArrayAllocContextOverflow
        cmp         eax, [edx + OFFSETOF__Thread__m_alloc_context__alloc_limit]
        ja          ArrayAllocContextOverflow

        ; ECX == array size
        ; EAX == new alloc ptr
        ; EDX == thread

        ; set the new alloc pointer
        mov         [edx + OFFSETOF__Thread__m_alloc_context__alloc_ptr], eax

        ; calc the new object pointer
        sub         eax, ecx

        pop         edx
        pop         ecx

        ; set the new object's EEType pointer and element count
        mov         [eax + OFFSETOF__Object__m_pEEType], ecx
        mov         [eax + OFFSETOF__Array__m_Length], edx
        ret

ArraySizeBig:
        ; Compute overall allocation size (align(base size + (element size * elements), 4)).
        ; if the element count is negative, it's an overflow, otherwise it's out of memory
        cmp         edx, 0
        jl          ArraySizeOverflow
        mul         edx
        jc          ArrayOutOfMemoryNoFrame
        add         eax, [ecx + OFFSETOF__EEType__m_uBaseSize]
        jc          ArrayOutOfMemoryNoFrame
        add         eax, 3
        jc          ArrayOutOfMemoryNoFrame
        jmp         ArrayAlignSize

ArrayAllocContextOverflow:
        ; ECX == array size
        ;   original ECX pushed
        ;   original EDX pushed

        ; Re-push original ECX
        push        [esp + 4]

        ; Create EBP frame.
        mov         [esp + 8], ebp
        lea         ebp, [esp + 8]

        PUSH_COOP_PINVOKE_FRAME edx

        ; Preserve the array size in edi
        mov         edi, ecx

        ; Get the EEType and put it in ecx.
        mov         ecx, dword ptr [ebp - 8]

        ; Push alloc helper arguments (thread, size, flags, EEType).
        push        edx                                             ; transition frame
        push        edi                                             ; Size
        xor         edx, edx                                        ; Flags
        ;; Passing EEType in ecx

        ;; void* RhpGcAlloc(EEType *pEEType, UInt32 uFlags, UIntNative cbSize, void * pTransitionFrame)
        call        RhpGcAlloc

        ; Set the new object's EEType pointer and length on success.
        test        eax, eax
        jz          ArrayOutOfMemoryWithFrame

        mov         ecx, [ebp - 8]
        mov         edx, [ebp - 4]
        mov         [eax + OFFSETOF__Object__m_pEEType], ecx
        mov         [eax + OFFSETOF__Array__m_Length], edx

        ;; If the object is bigger than RH_LARGE_OBJECT_SIZE, we must publish it to the BGC
        cmp         edi, RH_LARGE_OBJECT_SIZE
        jb          NewArray_SkipPublish
        mov         ecx, eax            ;; ecx: object
        mov         edx, edi            ;; edx: object size
        call        RhpPublishObject    ;; eax: this function returns the object that was passed-in
NewArray_SkipPublish: 

        POP_COOP_PINVOKE_FRAME
        add         esp, 8          ; pop ecx / edx
        pop         ebp
        ret

ArrayOutOfMemoryWithFrame:
        ; This is the OOM failure path. We're going to tail-call to a managed helper that will throw
        ; an out of memory exception that the caller of this allocator understands.

        mov         eax, [ebp - 8]  ; Preserve EEType pointer over POP_COOP_PINVOKE_FRAME

        POP_COOP_PINVOKE_FRAME
        add         esp, 8          ; pop ecx / edx
        pop         ebp             ; restore ebp

        mov         ecx, eax        ; EEType pointer
        xor         edx, edx        ; Indicate that we should throw OOM.
        jmp         RhExceptionHandling_FailedAllocation

ArrayOutOfMemoryNoFrame:
        add         esp, 8          ; pop ecx / edx

        ; ecx holds EEType pointer already
        xor         edx, edx        ; Indicate that we should throw OOM.
        jmp         RhExceptionHandling_FailedAllocation

ArraySizeOverflow:
        ; We get here if the size of the final array object can't be represented as an unsigned 
        ; 32-bit value. We're going to tail-call to a managed helper that will throw
        ; an overflow exception that the caller of this allocator understands.

        add         esp, 8          ; pop ecx / edx

        ; ecx holds EEType pointer already
        mov         edx, 1          ; Indicate that we should throw OverflowException
        jmp         RhExceptionHandling_FailedAllocation

FASTCALL_ENDFUNC

        end
