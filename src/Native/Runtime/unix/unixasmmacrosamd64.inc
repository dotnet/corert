//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#define C_VAR(Name) rip + C_FUNC(Name)

.macro NESTED_ENTRY Name, Section, Handler
        LEAF_ENTRY \Name, \Section
        .ifnc \Handler, NoHandler
#if defined(__APPLE__)
        .cfi_personality 0x9b, C_FUNC(\Handler) // 0x9b == DW_EH_PE_indirect | DW_EH_PE_pcrel | DW_EH_PE_sdata4
#else
        .cfi_personality 0, C_FUNC(\Handler) // 0 == DW_EH_PE_absptr
#endif
        .endif
.endm

.macro NESTED_END Name, Section
        LEAF_END \Name, \Section
#if defined(__APPLE__)
        .section __LD,__compact_unwind,regular,debug
        .quad C_FUNC(\Name)
        .set C_FUNC(\Name\()_Size), C_FUNC(\Name\()_End) - C_FUNC(\Name)
        .long C_FUNC(\Name\()_Size)
        .long 0x04000000 # DWARF
        .quad 0
        .quad 0
#endif
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
#if defined(__APPLE__)
        .text
#else
        .global C_FUNC(_\Name)
        .type \Name, %function
#endif
C_FUNC(\Name):
        .cfi_startproc
.endm

.macro LEAF_END_MARKED Name, Section
C_FUNC(\Name\()_End):
        .global C_FUNC(\Name\()_End)
#if !defined(__APPLE__)
        .size \Name, .-\Name
#endif
        .cfi_endproc
.endm

.macro push_nonvol_reg Register
        push \Register
        .cfi_adjust_cfa_offset 8
        .cfi_rel_offset \Register, 0
.endm

.macro pop_nonvol_reg Register
        pop \Register
        .cfi_adjust_cfa_offset -8
        .cfi_restore \Register
.endm

.macro alloc_stack Size
.att_syntax
        lea -\Size(%rsp), %rsp
.intel_syntax noprefix
        .cfi_adjust_cfa_offset \Size
.endm

.macro free_stack Size
.att_syntax
        lea \Size(%rsp), %rsp
.intel_syntax noprefix
        .cfi_adjust_cfa_offset -\Size
.endm

.macro set_cfa_register Reg, Offset
        .cfi_def_cfa_register \Reg
        .cfi_def_cfa_offset \Offset
.endm

.macro save_reg_postrsp Reg, Offset
        __Offset = \Offset
        mov     qword ptr [rsp + __Offset], \Reg
        .cfi_rel_offset \Reg, __Offset
.endm

.macro restore_reg Reg, Offset
        __Offset = \Offset
        mov             \Reg, [rsp + __Offset]
        .cfi_restore \Reg
.endm

.macro save_xmm128_postrsp Reg, Offset
        __Offset = \Offset
        movdqa  xmmword ptr [rsp + __Offset], \Reg
        // NOTE: We cannot use ".cfi_rel_offset \Reg, __Offset" here, 
        // the xmm registers are not supported by the libunwind
.endm

.macro restore_xmm128 Reg, ofs
        __Offset = \ofs
        movdqa          \Reg, xmmword ptr [rsp + __Offset]
        // NOTE: We cannot use ".cfi_restore \Reg" here, 
        // the xmm registers are not supported by the libunwind
        
.endm

.macro PUSH_CALLEE_SAVED_REGISTERS

        push_register rbp
        push_register rbx
        push_register r15
        push_register r14
        push_register r13
        push_register r12

.endm

.macro POP_CALLEE_SAVED_REGISTERS

        pop_nonvol_reg r12
        pop_nonvol_reg r13
        pop_nonvol_reg r14
        pop_nonvol_reg r15
        pop_nonvol_reg rbx
        pop_nonvol_reg rbp

.endm

.macro push_register Reg
        push            \Reg
        .cfi_adjust_cfa_offset 8
.endm

.macro push_eflags
        pushfq
        .cfi_adjust_cfa_offset 8
.endm

.macro push_argument_register Reg
        push_register \Reg
.endm

.macro PUSH_ARGUMENT_REGISTERS

        push_argument_register r9
        push_argument_register r8
        push_argument_register rcx
        push_argument_register rdx
        push_argument_register rsi
        push_argument_register rdi

.endm

.macro pop_register Reg
        pop            \Reg
        .cfi_adjust_cfa_offset -8
.endm

.macro pop_eflags
        popfq
        .cfi_adjust_cfa_offset -8
.endm

.macro pop_argument_register Reg
        pop_register \Reg
.endm

.macro POP_ARGUMENT_REGISTERS

        pop_argument_register rdi
        pop_argument_register rsi
        pop_argument_register rdx
        pop_argument_register rcx
        pop_argument_register r8
        pop_argument_register r9

.endm

.macro SAVE_FLOAT_ARGUMENT_REGISTERS ofs

        save_xmm128_postrsp xmm0, \ofs
        save_xmm128_postrsp xmm1, \ofs + 0x10
        save_xmm128_postrsp xmm2, \ofs + 0x20
        save_xmm128_postrsp xmm3, \ofs + 0x30
        save_xmm128_postrsp xmm4, \ofs + 0x40
        save_xmm128_postrsp xmm5, \ofs + 0x50
        save_xmm128_postrsp xmm6, \ofs + 0x60
        save_xmm128_postrsp xmm7, \ofs + 0x70

.endm

.macro RESTORE_FLOAT_ARGUMENT_REGISTERS ofs

        restore_xmm128  xmm0, \ofs
        restore_xmm128  xmm1, \ofs + 0x10
        restore_xmm128  xmm2, \ofs + 0x20
        restore_xmm128  xmm3, \ofs + 0x30
        restore_xmm128  xmm4, \ofs + 0x40
        restore_xmm128  xmm5, \ofs + 0x50
        restore_xmm128  xmm6, \ofs + 0x60
        restore_xmm128  xmm7, \ofs + 0x70

.endm

// Note: these must match the defs in PInvokeTransitionFrameFlags
#define PTFF_SAVE_RBX            00000001h
#define PTFF_SAVE_R12            00000010h
#define PTFF_SAVE_R13            00000020h
#define PTFF_SAVE_R14            00000040h
#define PTFF_SAVE_R15            00000080h
#define PTFF_SAVE_ALL_PRESERVED  000000F1h   // NOTE: RBP is not included in this set!
#define PTFF_SAVE_RSP            00008000h
#define PTFF_SAVE_RAX            00000100h   // RAX is saved if it contains a GC ref and we're in hijack handler
#define PTFF_SAVE_ALL_SCRATCH    00007F00h
#define PTFF_RAX_IS_GCREF        00010000h   // iff PTFF_SAVE_RAX: set -> eax is Object, clear -> eax is scalar
#define PTFF_RAX_IS_BYREF        00020000h   // iff PTFF_SAVE_RAX: set -> eax is ByRef, clear -> eax is Object or scalar

