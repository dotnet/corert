// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.macro NESTED_ENTRY Name, Section, Handler
        LEAF_ENTRY \Name, \Section
        .ifnc \Handler, NoHandler
        .cfi_personality 0, C_FUNC(\Handler) // 0 == DW_EH_PE_absptr
        .endif
.endm

.macro NESTED_END Name, Section
        LEAF_END \Name, \Section
.endm

.macro PATCH_LABEL Name
        .global C_FUNC(\Name)
C_FUNC(\Name):
.endm

.macro ALTERNATE_ENTRY Name
        .global C_FUNC(\Name)
C_FUNC(\Name):
.endm

.macro LEAF_ENTRY Name, Section
        .global C_FUNC(\Name)
        .type \Name, %function
C_FUNC(\Name):
        .cfi_startproc
.endm

.macro LEAF_END Name, Section
        .size \Name, .-\Name
        .cfi_endproc
.endm

.macro PREPARE_EXTERNAL_VAR Name, HelperReg
        ldr \HelperReg, [pc, #C_FUNC(\Name)@GOTPCREL]
.endm

.macro PROLOG_STACK_ALLOC Size
        sub sp, sp, \Size
        .cfi_adjust_cfa_offset \Size
.endm

.macro EPILOG_STACK_FREE Size
        add sp, sp, \Size
.endm

.macro EPILOG_STACK_RESTORE
        mov sp, fp
.endm

.macro PROLOG_SAVE_REG reg, ofs
        str \reg, [sp, \ofs]
.endm

.macro PROLOG_SAVE_REG_PAIR reg1, reg2, ofs
        stp \reg1, \reg2, [sp, \ofs]
        .ifc \reg1, fp
        mov fp, sp
        .endif
.endm

.macro PROLOG_SAVE_REG_PAIR_INDEXED reg1, reg2, ofs
        stp \reg1, \reg2, [sp, \ofs]!
        .ifc \reg1, fp
        mov fp, sp
        .endif
.endm

.macro EPILOG_RESTORE_REG reg, ofs
        ldr \reg, [sp, \ofs]
.endm

.macro EPILOG_RESTORE_REG_PAIR reg1, reg2, ofs
        ldp \reg1, \reg2, [sp, \ofs]
.endm

.macro EPILOG_RESTORE_REG_PAIR_INDEXED reg1, reg2, ofs
        ldp \reg1, \reg2, [sp], \ofs
.endm

.macro EPILOG_RETURN
        ret
.endm

.macro EMIT_BREAKPOINT
        brk #0
.endm

//-----------------------------------------------------------------------------
// The Following sets of SAVE_*_REGISTERS expect the memory to be reserved and 
// base address to be passed in $reg
//

// Reserve 64 bytes of memory before calling  SAVE_ARGUMENT_REGISTERS
.macro SAVE_ARGUMENT_REGISTERS reg, ofs 

        stp                    x0, x1, [\reg, #(\ofs)]
        stp                    x2, x3, [\reg, #(\ofs + 16)]
        stp                    x4, x5, [\reg, #(\ofs + 32)]
        stp                    x6, x7, [\reg, #(\ofs + 48)]

.endm

// Reserve 64 bytes of memory before calling  SAVE_FLOAT_ARGUMENT_REGISTERS
.macro SAVE_FLOAT_ARGUMENT_REGISTERS reg, ofs 

        stp                    d0, d1, [\reg, #(\ofs)]
        stp                    d2, d3, [\reg, #(\ofs + 16)]
        stp                    d4, d5, [\reg, #(\ofs + 32)]
        stp                    d6, d7, [\reg, #(\ofs + 48)]

.endm

.macro RESTORE_ARGUMENT_REGISTERS reg, ofs 

        ldp                    x0, x1, [\reg, #(\ofs)]
        ldp                    x2, x3, [\reg, #(\ofs + 16)]
        ldp                    x4, x5, [\reg, #(\ofs + 32)]
        ldp                    x6, x7, [\reg, #(\ofs + 48)]

.endm

.macro RESTORE_FLOAT_ARGUMENT_REGISTERS reg, ofs 

        ldp                    d0, d1, [\reg, #(\ofs)]
        ldp                    d2, d3, [\reg, #(\ofs + 16)]
        ldp                    d4, d5, [\reg, #(\ofs + 32)]
        ldp                    d6, d7, [\reg, #(\ofs + 48)]

.endm

.macro EPILOG_BRANCH_REG reg

        br \reg

.endm
