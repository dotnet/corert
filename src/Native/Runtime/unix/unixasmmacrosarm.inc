// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.macro NESTED_ENTRY Name, Section, Handler
        LEAF_ENTRY \Name, \Section
        .ifnc \Handler, NoHandler
        .personality C_FUNC(\Handler)
        .endif
.endm

.macro NESTED_END Name, Section
        LEAF_END \Name, \Section
.endm

.macro PATCH_LABEL Name
        .thumb_func
        .global C_FUNC(\Name)
C_FUNC(\Name):
.endm

.macro ALTERNATE_ENTRY Name
        .global C_FUNC(\Name)
C_FUNC(\Name):
.endm

.macro LEAF_ENTRY Name, Section
        .thumb_func
        .global C_FUNC(\Name)
        .type \Name, %function
C_FUNC(\Name):
        .fnstart
.endm

.macro LEAF_END Name, Section
        .size \Name, .-\Name
        .fnend
.endm

.macro PREPARE_EXTERNAL_VAR Name, HelperReg
        ldr \HelperReg, [pc, #C_FUNC(\Name)@GOTPCREL]
.endm

.macro push_nonvol_reg Register
        push \Register
        .save \Register
.endm

.macro pop_nonvol_reg Register
        pop \Register
.endm

.macro vpush_nonvol_reg Register
        vpush \Register
        .vsave \Register
.endm

.macro vpop_nonvol_reg Register
        vpop \Register
.endm

.macro alloc_stack Size
        sub sp, sp, (\Size)
        .pad #(\Size)
.endm

.macro free_stack Size
        add sp, sp, (\Size)
        .pad #-(\Size)
.endm

.macro POP_CALLEE_SAVED_REGISTERS
        pop_nonvol_reg "{r4-r11, lr}"
.endm

.macro PUSH_CALLEE_SAVED_REGISTERS
        push_nonvol_reg "{r4-r11, lr}"
.endm

.macro push_register Reg
        push \Reg
.endm

.macro push_argument_register Reg
        push_register \Reg
.endm

.macro PUSH_ARGUMENT_REGISTERS
        push {r0-r3}
.endm

.macro pop_register Reg
        pop \Reg
.endm

.macro pop_argument_register Reg
        pop_register \Reg
.endm

.macro POP_ARGUMENT_REGISTERS
        pop {r0-r3}
.endm

.macro EMIT_BREAKPOINT
        .inst.w 0xde01
.endm

.macro PROLOG_PUSH RegList
        push_nonvol_reg "\RegList"
.endm

.macro PROLOG_VPUSH RegList
        vpush_nonvol_reg "\RegList"
.endm

.macro PROLOG_STACK_SAVE Register
        .setfp \Register, sp
        mov \Register, sp
.endm

.macro EPILOG_STACK_RESTORE Register
        mov sp, \Register
.endm

.macro EPILOG_POP RegList
        pop_nonvol_reg "\RegList"
.endm

.macro EPILOG_VPOP RegList
        vpop_nonvol_reg "\RegList"
.endm

//-----------------------------------------------------------------------------
// Macro used to check (in debug builds only) whether the stack is 64-bit aligned (a requirement before calling
// out into C++/OS code). Invoke this directly after your prolog (if the stack frame size is fixed) or directly
// before a call (if you have a frame pointer and a dynamic stack). A breakpoint will be invoked if the stack
// is misaligned.
//
.macro CHECK_STACK_ALIGNMENT

#ifdef _DEBUG
        push {r0}
        add r0, sp, #4
        tst r0, #7
        pop {r0}
        beq 0f
        EMIT_BREAKPOINT
0:
#endif
.endm

.macro EXPORT_POINTER_TO_ADDRESS Name

1:

        .data
        .align      4
C_FUNC(\Name):
        .word       1b
        .global     C_FUNC(\Name)
        .text

.endm

#ifdef _DEBUG
.macro GEN_ASSERT message, fileName, funcName
        ldr r0, =\message
        ldr r1, =\fileName
        ldr r2, =\funcName
        bl C_FUNC(NYI_Assert)
.endm
#endif

// thumb with PIC version
.macro INLINE_GET_TLS_VAR Var
        ldr     r0, 2f
1:
        add     r0, pc, r0
        bl      __tls_get_addr(PLT)
2:
        .4byte  \Var(TLSGD) + (. - 1b - 4)
.endm

.macro INLINE_GETTHREAD
        // Inlined version of call C_FUNC(RhpGetThread)
        INLINE_GET_TLS_VAR tls_CurrentThread
.endm
