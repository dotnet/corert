// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"

#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "regdisplay.h"
#include "config.h"

#if !HAVE_LIBUNWIND_H
#error Don't know how to unwind on this platform
#endif

#include <libunwind.h>

#if HAVE_UCONTEXT_T
#include <ucontext.h>
#endif  // HAVE_UCONTEXT_T

#include "UnixContext.h"

#ifdef __APPLE__

#define MCREG_Rip(mc)       ((mc)->__ss.__rip)
#define MCREG_Rsp(mc)       ((mc)->__ss.__rsp)
#define MCREG_Rax(mc)       ((mc)->__ss.__rax)
#define MCREG_Rbx(mc)       ((mc)->__ss.__rbx)
#define MCREG_Rcx(mc)       ((mc)->__ss.__rcx)
#define MCREG_Rdx(mc)       ((mc)->__ss.__rdx)
#define MCREG_Rsi(mc)       ((mc)->__ss.__rsi)
#define MCREG_Rdi(mc)       ((mc)->__ss.__rdi)
#define MCREG_Rbp(mc)       ((mc)->__ss.__rbp)
#define MCREG_R8(mc)        ((mc)->__ss.__r8)
#define MCREG_R9(mc)        ((mc)->__ss.__r9)
#define MCREG_R10(mc)       ((mc)->__ss.__r10)
#define MCREG_R11(mc)       ((mc)->__ss.__r11)
#define MCREG_R12(mc)       ((mc)->__ss.__r12)
#define MCREG_R13(mc)       ((mc)->__ss.__r13)
#define MCREG_R14(mc)       ((mc)->__ss.__r14)
#define MCREG_R15(mc)       ((mc)->__ss.__r15)

#else

#if HAVE___GREGSET_T

#ifdef BIT64
#define MCREG_Rip(mc)       ((mc).__gregs[_REG_RIP])
#define MCREG_Rsp(mc)       ((mc).__gregs[_REG_RSP])
#define MCREG_Rax(mc)       ((mc).__gregs[_REG_RAX])
#define MCREG_Rbx(mc)       ((mc).__gregs[_REG_RBX])
#define MCREG_Rcx(mc)       ((mc).__gregs[_REG_RCX])
#define MCREG_Rdx(mc)       ((mc).__gregs[_REG_RDX])
#define MCREG_Rsi(mc)       ((mc).__gregs[_REG_RSI])
#define MCREG_Rdi(mc)       ((mc).__gregs[_REG_RDI])
#define MCREG_Rbp(mc)       ((mc).__gregs[_REG_RBP])
#define MCREG_R8(mc)        ((mc).__gregs[_REG_R8])
#define MCREG_R9(mc)        ((mc).__gregs[_REG_R9])
#define MCREG_R10(mc)       ((mc).__gregs[_REG_R10])
#define MCREG_R11(mc)       ((mc).__gregs[_REG_R11])
#define MCREG_R12(mc)       ((mc).__gregs[_REG_R12])
#define MCREG_R13(mc)       ((mc).__gregs[_REG_R13])
#define MCREG_R14(mc)       ((mc).__gregs[_REG_R14])
#define MCREG_R15(mc)       ((mc).__gregs[_REG_R15])

#else // BIT64

#define MCREG_Eip(mc)       ((mc).__gregs[_REG_EIP])
#define MCREG_Esp(mc)       ((mc).__gregs[_REG_ESP])
#define MCREG_Eax(mc)       ((mc).__gregs[_REG_EAX])
#define MCREG_Ebx(mc)       ((mc).__gregs[_REG_EBX])
#define MCREG_Ecx(mc)       ((mc).__gregs[_REG_ECX])
#define MCREG_Edx(mc)       ((mc).__gregs[_REG_EDX])
#define MCREG_Esi(mc)       ((mc).__gregs[_REG_ESI])
#define MCREG_Edi(mc)       ((mc).__gregs[_REG_EDI])
#define MCREG_Ebp(mc)       ((mc).__gregs[_REG_EBP])

#endif // BIT64

#elif HAVE_GREGSET_T

#ifdef BIT64
#define MCREG_Rip(mc)       ((mc).gregs[REG_RIP])
#define MCREG_Rsp(mc)       ((mc).gregs[REG_RSP])
#define MCREG_Rax(mc)       ((mc).gregs[REG_RAX])
#define MCREG_Rbx(mc)       ((mc).gregs[REG_RBX])
#define MCREG_Rcx(mc)       ((mc).gregs[REG_RCX])
#define MCREG_Rdx(mc)       ((mc).gregs[REG_RDX])
#define MCREG_Rsi(mc)       ((mc).gregs[REG_RSI])
#define MCREG_Rdi(mc)       ((mc).gregs[REG_RDI])
#define MCREG_Rbp(mc)       ((mc).gregs[REG_RBP])
#define MCREG_R8(mc)        ((mc).gregs[REG_R8])
#define MCREG_R9(mc)        ((mc).gregs[REG_R9])
#define MCREG_R10(mc)       ((mc).gregs[REG_R10])
#define MCREG_R11(mc)       ((mc).gregs[REG_R11])
#define MCREG_R12(mc)       ((mc).gregs[REG_R12])
#define MCREG_R13(mc)       ((mc).gregs[REG_R13])
#define MCREG_R14(mc)       ((mc).gregs[REG_R14])
#define MCREG_R15(mc)       ((mc).gregs[REG_R15])

#else // BIT64

#define MCREG_Eip(mc)       ((mc).gregs[REG_EIP])
#define MCREG_Esp(mc)       ((mc).gregs[REG_ESP])
#define MCREG_Eax(mc)       ((mc).gregs[REG_EAX])
#define MCREG_Ebx(mc)       ((mc).gregs[REG_EBX])
#define MCREG_Ecx(mc)       ((mc).gregs[REG_ECX])
#define MCREG_Edx(mc)       ((mc).gregs[REG_EDX])
#define MCREG_Esi(mc)       ((mc).gregs[REG_ESI])
#define MCREG_Edi(mc)       ((mc).gregs[REG_EDI])
#define MCREG_Ebp(mc)       ((mc).gregs[REG_EBP])

#endif // BIT64

#else // HAVE_GREGSET_T

#ifdef BIT64

#if defined(_ARM64_)

#define MCREG_Pc(mc)      ((mc).pc)
#define MCREG_Sp(mc)      ((mc).sp)
#define MCREG_Lr(mc)      ((mc).regs[30])
#define MCREG_X0(mc)      ((mc).regs[0])
#define MCREG_X1(mc)      ((mc).regs[1])
#define MCREG_X19(mc)     ((mc).regs[19])
#define MCREG_X20(mc)     ((mc).regs[20])
#define MCREG_X21(mc)     ((mc).regs[21])
#define MCREG_X22(mc)     ((mc).regs[22])
#define MCREG_X23(mc)     ((mc).regs[23])
#define MCREG_X24(mc)     ((mc).regs[24])
#define MCREG_X25(mc)     ((mc).regs[25])
#define MCREG_X26(mc)     ((mc).regs[26])
#define MCREG_X27(mc)     ((mc).regs[27])
#define MCREG_X28(mc)     ((mc).regs[28])
#define MCREG_Fp(mc)      ((mc).regs[29])

#else

// For FreeBSD, as found in x86/ucontext.h
#define MCREG_Rip(mc)       ((mc).mc_rip)
#define MCREG_Rsp(mc)       ((mc).mc_rsp)
#define MCREG_Rax(mc)       ((mc).mc_rax)
#define MCREG_Rbx(mc)       ((mc).mc_rbx)
#define MCREG_Rcx(mc)       ((mc).mc_rcx)
#define MCREG_Rdx(mc)       ((mc).mc_rdx)
#define MCREG_Rsi(mc)       ((mc).mc_rsi)
#define MCREG_Rdi(mc)       ((mc).mc_rdi)
#define MCREG_Rbp(mc)       ((mc).mc_rbp)
#define MCREG_R8(mc)        ((mc).mc_r8)
#define MCREG_R9(mc)        ((mc).mc_r9)
#define MCREG_R10(mc)       ((mc).mc_r10)
#define MCREG_R11(mc)       ((mc).mc_r11)
#define MCREG_R12(mc)       ((mc).mc_r12)
#define MCREG_R13(mc)       ((mc).mc_r13)
#define MCREG_R14(mc)       ((mc).mc_r14)
#define MCREG_R15(mc)       ((mc).mc_r15)

#endif

#else // BIT64

#if defined(_ARM_)

#define MCREG_Pc(mc)        ((mc).arm_pc)
#define MCREG_Sp(mc)        ((mc).arm_sp)
#define MCREG_Lr(mc)        ((mc).arm_lr)
#define MCREG_R0(mc)        ((mc).arm_r0)
#define MCREG_R1(mc)        ((mc).arm_r1)
#define MCREG_R4(mc)        ((mc).arm_r4)
#define MCREG_R5(mc)        ((mc).arm_r5)
#define MCREG_R6(mc)        ((mc).arm_r6)
#define MCREG_R7(mc)        ((mc).arm_r7)
#define MCREG_R8(mc)        ((mc).arm_r8)
#define MCREG_R9(mc)        ((mc).arm_r9)
#define MCREG_R10(mc)       ((mc).arm_r10)
#define MCREG_R11(mc)       ((mc).arm_fp)

#elif defined(_X86_)

#define MCREG_Eip(mc)       ((mc).mc_eip)
#define MCREG_Esp(mc)       ((mc).mc_esp)
#define MCREG_Eax(mc)       ((mc).mc_eax)
#define MCREG_Ebx(mc)       ((mc).mc_ebx)
#define MCREG_Ecx(mc)       ((mc).mc_ecx)
#define MCREG_Edx(mc)       ((mc).mc_edx)
#define MCREG_Esi(mc)       ((mc).mc_esi)
#define MCREG_Edi(mc)       ((mc).mc_edi)
#define MCREG_Ebp(mc)       ((mc).mc_ebp)

#else
#error "Unsupported arch"
#endif

#endif // BIT64

#endif // HAVE_GREGSET_T

#endif // __APPLE__

#if UNWIND_CONTEXT_IS_UCONTEXT_T

#if defined(_AMD64_)
#define ASSIGN_UNWIND_REGS     \
    ASSIGN_REG(Rip, IP)        \
    ASSIGN_REG(Rsp, SP)        \
    ASSIGN_REG_PTR(Rbp, Rbp)   \
    ASSIGN_REG_PTR(Rbx, Rbx)   \
    ASSIGN_REG_PTR(R12, R12)   \
    ASSIGN_REG_PTR(R13, R13)   \
    ASSIGN_REG_PTR(R14, R14)   \
    ASSIGN_REG_PTR(R15, R15)
#elif defined(_ARM64_)
#define ASSIGN_UNWIND_REGS     \
    ASSIGN_REG(Pc, IP)
    // ASSIGN_REG(Sp, SP)         \
    // ASSIGN_REG_PTR(Fp, FP)     \
    // ASSIGN_REG_PTR(Lr, LR)     \
    // ASSIGN_REG_PTR(X19, X19)   \
    // ASSIGN_REG_PTR(X20, X20)   \
    // ASSIGN_REG_PTR(X21, X21)   \
    // ASSIGN_REG_PTR(X22, X22)   \
    // ASSIGN_REG_PTR(X23, X23)   \
    // ASSIGN_REG_PTR(X24, X24)   \
    // ASSIGN_REG_PTR(X25, X25)   \
    // ASSIGN_REG_PTR(X26, X26)   \
    // ASSIGN_REG_PTR(X27, X27)   \
    // ASSIGN_REG_PTR(X28, X28)
#else
#error unsupported architecture
#endif

// Convert REGDISPLAY to unw_context_t
static void RegDisplayToUnwindContext(REGDISPLAY* regDisplay, unw_context_t *unwContext)
{
#define ASSIGN_REG(regName1, regName2) \
    MCREG_##regName1(unwContext->uc_mcontext) = regDisplay->regName2;

#define ASSIGN_REG_PTR(regName1, regName2) \
    if (regDisplay->p##regName2 != NULL) \
        MCREG_##regName1(unwContext->uc_mcontext) = *(regDisplay->p##regName2);

    ASSIGN_UNWIND_REGS

#undef ASSIGN_REG
#undef ASSIGN_REG_PTR
}

#else // UNWIND_CONTEXT_IS_UCONTEXT_T

// Update unw_context_t from REGDISPLAY
static void RegDisplayToUnwindContext(REGDISPLAY* regDisplay, unw_context_t *unwContext)
{
#if defined(_ARM_)
    // Assuming that unw_set_reg() on cursor will point the cursor to the
    // supposed stack frame is dangerous for libunwind-arm in Linux.
    // It is because libunwind's unw_cursor_t has other data structure
    // initialized by unw_init_local(), which are not updated by
    // unw_set_reg().

#define ASSIGN_REG(regIndex, regName) \
    unwContext->regs[regIndex] = regDisplay->regName2;

#define ASSIGN_REG_PTR(regIndex, regName) \
    if (regDisplay->p##regName2 != NULL) \
        unwContext->regs[regIndex] = *(regDisplay->p##regName2);

    ASSIGN_REG_PTR(4, R4);
    ASSIGN_REG_PTR(5, R5);
    ASSIGN_REG_PTR(6, R6);
    ASSIGN_REG_PTR(7, R7);
    ASSIGN_REG_PTR(8, R8);
    ASSIGN_REG_PTR(9, R9);
    ASSIGN_REG_PTR(10, R10);
    ASSIGN_REG_PTR(11, R11);
    ASSIGN_REG(13, SP);
    ASSIGN_REG_PTR(14, LR);
    ASSIGN_REG(15, IP);

#undef ASSIGN_REG
#undef ASSIGN_REG_PTR
#endif // _ARM_
}

// Update unw_cursor_t from REGDISPLAY
static void RegDisplayToUnwindCursor(REGDISPLAY* regDisplay, unw_cursor_t *cursor)
{
#if defined(_AMD64_)
#define ASSIGN_REG(regName1, regName2) \
    unw_set_reg(cursor, regName1, regDisplay->regName2);

#define ASSIGN_REG_PTR(regName1, regName2) \
    if (regDisplay->p##regName2 != NULL) \
        unw_set_reg(cursor, regName1, *(regDisplay->p##regName2));

    ASSIGN_REG(UNW_REG_IP, IP)
    ASSIGN_REG(UNW_REG_SP, SP)
    ASSIGN_REG_PTR(UNW_X86_64_RBP, Rbp)
    ASSIGN_REG_PTR(UNW_X86_64_RBX, Rbx)
    ASSIGN_REG_PTR(UNW_X86_64_R12, R12)
    ASSIGN_REG_PTR(UNW_X86_64_R13, R13)
    ASSIGN_REG_PTR(UNW_X86_64_R14, R14)
    ASSIGN_REG_PTR(UNW_X86_64_R15, R15)

#undef ASSIGN_REG
#undef ASSIGN_REG_PTR
#endif // _AMD64_
}
#endif // UNWIND_CONTEXT_IS_UCONTEXT_T

// Initialize unw_cursor_t and unw_context_t from REGDISPLAY
bool InitializeUnwindContextAndCursor(REGDISPLAY* regDisplay, unw_cursor_t* cursor, unw_context_t* unwContext)
{
    int st;

#if !UNWIND_CONTEXT_IS_UCONTEXT_T
    st = unw_getcontext(unwContext);
    if (st < 0)
    {
        return false;
    }
#endif

    RegDisplayToUnwindContext(regDisplay, unwContext);

    st = unw_init_local(cursor, unwContext);
    if (st < 0)
    {
        return false;
    }

#if !UNWIND_CONTEXT_IS_UCONTEXT_T
    // Set the unwind context to the specified windows context
    RegDisplayToUnwindCursor(regDisplay, cursor);
#endif

    return true;
}

// Update context pointer for a register from the unw_cursor_t.
static void GetContextPointer(unw_cursor_t *cursor, unw_context_t *unwContext, int reg, PTR_UIntNative *contextPointer)
{
#if defined(HAVE_UNW_GET_SAVE_LOC)
    unw_save_loc_t saveLoc;
    unw_get_save_loc(cursor, reg, &saveLoc);
    if (saveLoc.type == UNW_SLT_MEMORY)
    {
        PTR_UIntNative pLoc = (PTR_UIntNative)saveLoc.u.addr;
        // Filter out fake save locations that point to unwContext
        if (unwContext == NULL || (pLoc < (PTR_UIntNative)unwContext) || ((PTR_UIntNative)(unwContext + 1) <= pLoc))
            *contextPointer = (PTR_UIntNative)saveLoc.u.addr;
    }
#else
    // Returning NULL indicates that we don't have context pointers available
    *contextPointer = NULL;
#endif
}

#if defined(_AMD64_)
#define GET_CONTEXT_POINTERS                    \
    GET_CONTEXT_POINTER(UNW_X86_64_RBP, Rbp)	\
    GET_CONTEXT_POINTER(UNW_X86_64_RBX, Rbx)    \
    GET_CONTEXT_POINTER(UNW_X86_64_R12, R12)    \
    GET_CONTEXT_POINTER(UNW_X86_64_R13, R13)    \
    GET_CONTEXT_POINTER(UNW_X86_64_R14, R14)    \
    GET_CONTEXT_POINTER(UNW_X86_64_R15, R15)
#elif defined(_ARM_)
#define GET_CONTEXT_POINTERS                    \
    GET_CONTEXT_POINTER(UNW_ARM_R4, R4)	        \
    GET_CONTEXT_POINTER(UNW_ARM_R5, R5)	        \
    GET_CONTEXT_POINTER(UNW_ARM_R6, R6)	        \
    GET_CONTEXT_POINTER(UNW_ARM_R7, R7)	        \
    GET_CONTEXT_POINTER(UNW_ARM_R8, R8)	        \
    GET_CONTEXT_POINTER(UNW_ARM_R9, R9)	        \
    GET_CONTEXT_POINTER(UNW_ARM_R10, R10)       \
    GET_CONTEXT_POINTER(UNW_ARM_R11, R11)
#elif defined(_ARM64_)
#define GET_CONTEXT_POINTERS                    \
    GET_CONTEXT_POINTER(UNW_AARCH64_X19, 19)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X20, 20)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X21, 21)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X22, 22)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X23, 23)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X24, 24)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X25, 25)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X26, 26)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X27, 27)	\
    GET_CONTEXT_POINTER(UNW_AARCH64_X28, 28)
#else
#error unsupported architecture
#endif

// Update REGDISPLAY from the unw_cursor_t and unw_context_t
void UnwindCursorToRegDisplay(unw_cursor_t *cursor, unw_context_t *unwContext, REGDISPLAY *regDisplay)
{
#define GET_CONTEXT_POINTER(unwReg, rdReg) GetContextPointer(cursor, unwContext, unwReg, &regDisplay->p##rdReg);
    GET_CONTEXT_POINTERS
#undef GET_CONTEXT_POINTER

    unw_get_reg(cursor, UNW_REG_IP, (unw_word_t *) &regDisplay->IP);
    unw_get_reg(cursor, UNW_REG_SP, (unw_word_t *) &regDisplay->SP);

#if defined(_AMD64_)
    regDisplay->pIP = PTR_PCODE(regDisplay->SP - sizeof(TADDR));
#endif

#if defined(_ARM_) || defined(_ARM64_)
    regDisplay->IP |= 1;
#endif
}

#if defined(_AMD64_)
#define ASSIGN_CONTROL_REGS \
    ASSIGN_REG(Rip, IP)     \
    ASSIGN_REG(Rsp, Rsp)

#define ASSIGN_INTEGER_REGS  \
    ASSIGN_REG(Rbx, Rbx)     \
    ASSIGN_REG(Rbp, Rbp)     \
    ASSIGN_REG(R12, R12)     \
    ASSIGN_REG(R13, R13)     \
    ASSIGN_REG(R14, R14)     \
    ASSIGN_REG(R15, R15)

#define ASSIGN_TWO_ARGUMENT_REGS(arg0Reg, arg1Reg) \
    MCREG_Rdi(nativeContext->uc_mcontext) = arg0Reg;      \
    MCREG_Rsi(nativeContext->uc_mcontext) = arg1Reg;

#elif defined(_ARM_)

#define ASSIGN_CONTROL_REGS  \
    ASSIGN_REG(Pc, IP)       \
    ASSIGN_REG(Sp, SP)       \
    ASSIGN_REG(Lr, LR)

#define ASSIGN_INTEGER_REGS  \
    ASSIGN_REG(R4, R4)       \
    ASSIGN_REG(R5, R5)       \
    ASSIGN_REG(R6, R6)       \
    ASSIGN_REG(R7, R7)       \
    ASSIGN_REG(R8, R8)       \
    ASSIGN_REG(R9, R9)       \
    ASSIGN_REG(R10, R10)     \
    ASSIGN_REG(R11, R11)

#define ASSIGN_TWO_ARGUMENT_REGS(arg0Reg, arg1Reg) \
    MCREG_R0(nativeContext->uc_mcontext) = arg0Reg;       \
    MCREG_R1(nativeContext->uc_mcontext) = arg1Reg;

#elif defined(_ARM64_)
#define ASSIGN_CONTROL_REGS  \
    ASSIGN_REG(Pc, IP)
    // ASSIGN_REG(Sp, SP)    \
    // ASSIGN_REG(Fp, FP)    \
    // ASSIGN_REG(Lr, LR)    \

#define ASSIGN_INTEGER_REGS
    // ASSIGN_REG(X19, X19)   \
    // ASSIGN_REG(X20, X20)   \
    // ASSIGN_REG(X21, X21)   \
    // ASSIGN_REG(X22, X22)   \
    // ASSIGN_REG(X23, X23)   \
    // ASSIGN_REG(X24, X24)   \
    // ASSIGN_REG(X25, X25)   \
    // ASSIGN_REG(X26, X26)   \
    // ASSIGN_REG(X27, X27)   \
    // ASSIGN_REG(X28, X28)

#define ASSIGN_TWO_ARGUMENT_REGS
    // MCREG_X0(nativeContext->uc_mcontext) = arg0Reg;       \
    // MCREG_X1(nativeContext->uc_mcontext) = arg1Reg;

#else
#error unsupported architecture
#endif

// Convert Unix native context to PAL_LIMITED_CONTEXT
void NativeContextToPalContext(const void* context, PAL_LIMITED_CONTEXT* palContext)
{
    ucontext_t *nativeContext = (ucontext_t*)context;
#define ASSIGN_REG(regNative, regPal) palContext->regPal = MCREG_##regNative(nativeContext->uc_mcontext);
    ASSIGN_CONTROL_REGS
    ASSIGN_INTEGER_REGS
#undef ASSIGN_REG
}

// Redirect Unix native context to the PAL_LIMITED_CONTEXT and also set the first two argument registers
void RedirectNativeContext(void* context, const PAL_LIMITED_CONTEXT* palContext, UIntNative arg0Reg, UIntNative arg1Reg)
{
    ucontext_t *nativeContext = (ucontext_t*)context;

#define ASSIGN_REG(regNative, regPal) MCREG_##regNative(nativeContext->uc_mcontext) = palContext->regPal;
    ASSIGN_CONTROL_REGS
#undef ASSIGN_REG
    ASSIGN_TWO_ARGUMENT_REGS(arg0Reg, arg1Reg);
}

#ifdef _AMD64_
// Get value of a register from the native context
// Parameters:
//  void* context  - context containing the registers
//  uint32_t index - index of the register
//                   Rax = 0, Rcx = 1, Rdx = 2, Rbx = 3
//                   Rsp = 4, Rbp = 5, Rsi = 6, Rdi = 7
//                   R8  = 8, R9  = 9, R10 = 10, R11 = 11
//                   R12 = 12, R13 = 13, R14 = 14, R15 = 15
uint64_t GetRegisterValueByIndex(void* context, uint32_t index)
{
    ucontext_t *nativeContext = (ucontext_t*)context;
    switch (index)
    {
        case 0:
            return MCREG_Rax(nativeContext->uc_mcontext);
        case 1:
            return MCREG_Rcx(nativeContext->uc_mcontext);
        case 2:
            return MCREG_Rdx(nativeContext->uc_mcontext);
        case 3:
            return MCREG_Rbx(nativeContext->uc_mcontext);
        case 4:
            return MCREG_Rsp(nativeContext->uc_mcontext);
        case 5:
            return MCREG_Rbp(nativeContext->uc_mcontext);
        case 6:
            return MCREG_Rsi(nativeContext->uc_mcontext);
        case 7:
            return MCREG_Rdi(nativeContext->uc_mcontext);
        case 8:
            return MCREG_R8(nativeContext->uc_mcontext);
        case 9:
            return MCREG_R9(nativeContext->uc_mcontext);
        case 10:
            return MCREG_R10(nativeContext->uc_mcontext);
        case 11:
            return MCREG_R11(nativeContext->uc_mcontext);
        case 12:
            return MCREG_R12(nativeContext->uc_mcontext);
        case 13:
            return MCREG_R13(nativeContext->uc_mcontext);
        case 14:
            return MCREG_R14(nativeContext->uc_mcontext);
        case 15:
            return MCREG_R15(nativeContext->uc_mcontext);
    }

    ASSERT(false);
    return 0;
}

// Get value of the program counter from the native context
uint64_t GetPC(void* context)
{
    ucontext_t *nativeContext = (ucontext_t*)context;
    return MCREG_Rip(nativeContext->uc_mcontext);
}

#endif // _AMD64_

// Find LSDA and start address for a function at address controlPC
bool FindProcInfo(UIntNative controlPC, UIntNative* startAddress, UIntNative* lsda)
{
    unw_context_t unwContext;
    unw_cursor_t cursor;
    REGDISPLAY regDisplay;
    memset(&regDisplay, 0, sizeof(REGDISPLAY));

    regDisplay.SetIP((PCODE)controlPC);

    if (!InitializeUnwindContextAndCursor(&regDisplay, &cursor, &unwContext))
    {
        return false;
    }

    unw_proc_info_t procInfo;
    int st = unw_get_proc_info(&cursor, &procInfo);
    if (st < 0)
    {
        return false;
    }

    assert((procInfo.start_ip <= controlPC) && (controlPC < procInfo.end_ip));

    *lsda = procInfo.lsda;
    *startAddress = procInfo.start_ip;

    return true;
}

// Virtually unwind stack to the caller of the context specified by the REGDISPLAY
bool VirtualUnwind(REGDISPLAY* pRegisterSet)
{
    unw_context_t unwContext;
    unw_cursor_t cursor;

    if (!InitializeUnwindContextAndCursor(pRegisterSet, &cursor, &unwContext))
    {
        return false;
    }

#if defined(__APPLE__) || defined(__FreeBSD__) || defined(__NetBSD__)  || defined(_ARM64_) || defined(_ARM_)
    // FreeBSD, NetBSD and OSX appear to do two different things when unwinding
    // 1: If it reaches where it cannot unwind anymore, say a
    // managed frame.  It wil return 0, but also update the $pc
    // 2: If it unwinds all the way to _start it will return
    // 0 from the step, but $pc will stay the same.
    // The behaviour of libunwind from nongnu.org is to null the PC
    // So we bank the original PC here, so we can compare it after
    // the step
    uintptr_t curPc = pRegisterSet->GetIP();
#endif

    int st = unw_step(&cursor);
    if (st < 0)
    {
        return false;
    }

    // Update the REGDISPLAY to reflect the unwind
    UnwindCursorToRegDisplay(&cursor, &unwContext, pRegisterSet);

#if defined(__APPLE__) || defined(__FreeBSD__) || defined(__NetBSD__)  || defined(_ARM64_) || defined(_ARM_)
    if (st == 0 && pRegisterSet->GetIP() == curPc)
    {
        // TODO: is this correct for CoreRT? Should we return false instead?
        pRegisterSet->SetIP(0);
    }
#endif

    return true;
}
