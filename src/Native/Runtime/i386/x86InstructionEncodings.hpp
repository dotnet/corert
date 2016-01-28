// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifndef _INSTRUCTIONENCONDINGS_HPP_
#define _INSTRUCTIONENCONDINGS_HPP_

//=======================================================================

#define X86_INSTR_CALL_REL32            0xE8        // call rel32
#define X86_INSTR_CALL_IND              0x15FF      // call dword ptr[addr32]
#ifdef _DEBUG
#define X86_INSTR_CALL_IND_BP           0x15CC      // call dword ptr[addr32] with a breakpoint set on the instruction
#endif
#define X86_INSTR_CALL_IND_EAX          0x10FF      // call dword ptr[eax]
#define X86_INSTR_CALL_IND_EAX_OFFSET   0x50FF  // call dword ptr[eax + offset] ; where offset follows these 2 bytes
#define X86_INSTR_CALL_EAX              0xD0FF      // call eax
#define X86_INSTR_JMP_REL32             0xE9        // jmp rel32
#define X86_INSTR_JMP_IND               0x25FF      // jmp dword ptr[addr32]
#define X86_INSTR_JMP_EAX               0xE0FF      // jmp eax
#define X86_INSTR_MOV_EAX_IMM32         0xB8        // mov eax, imm32
#define X86_INSTR_MOV_EAX_IND           0x058B      // mov eax, dword ptr[addr32]
#define X86_INSTR_MOV_EAX_ECX_IND       0x018b    // mov eax, [ecx]        
#define X86_INSTR_MOV_ECX_ECX_OFFSET    0x498B    // mov ecx, [ecx + offset] ; where offset follows these 2 bytes
#define X86_INSTR_MOV_ECX_EAX_OFFSET    0x488B    // mov ecx, [eax + offset] ; where offset follows these 2 bytes
#define X86_INSTR_CMP_IND_ECX_IMM32     0x3981  // cmp [ecx], imm32
#define X86_INSTR_MOV_RM_R              0x89        // mov r/m,reg

#define X86_INSTR_MOV_AL                0xB0        // mov al, imm8
#define X86_INSTR_JMP_REL8              0xEB        // jmp short rel8

#define X86_INSTR_NOP                   0x90        // nop
#define X86_INSTR_NOP3_1                0x1F0F      // 1st word of 3-byte nop ( 0F 1F 00 -> nop dword ptr [eax] )
#define X86_INSTR_NOP3_3                0x00        // 3rd byte of 3-byte nop
#define X86_INSTR_INT3                  0xCC        // int 3
#define X86_INSTR_HLT                   0xF4        // hlt

//------------------------------------------------------------------------
// The following must be a distinguishable set of instruction sequences for
// various stub dispatch calls.
//
// An x86 JIT which uses full stub dispatch must generate only
// the following stub dispatch calls:
//
// (1) isCallRelativeIndirect:
//        call dword ptr [rel32]  ;  FF 15 ---rel32----
// (2) isCallRelative:
//        call abc                ;     E8 ---rel32----
// (3) isCallRegisterIndirect:
//     3-byte nop
//     call dword ptr [eax]       ;     0F 1F 00  FF 10
//
// NOTE: You must be sure that pRetAddr is a true return address for
// a stub dispatch call.

bool isCallRelativeIndirect(const UInt8 *pRetAddr);
bool isCallRelative(const UInt8 *pRetAddr);
bool isCallRegisterIndirect(const UInt8 *pRetAddr);

inline bool isCallRelativeIndirect(const UInt8 *pRetAddr)
{
    bool fRet = *reinterpret_cast<const UInt16*>(&pRetAddr[-6]) == X86_INSTR_CALL_IND;
#ifdef _DEBUG
    fRet = fRet || *reinterpret_cast<const UInt16*>(&pRetAddr[-6]) == X86_INSTR_CALL_IND_BP;
#endif
    ASSERT(!fRet || !isCallRelative(pRetAddr));
    ASSERT(!fRet || !isCallRegisterIndirect(pRetAddr));
    return fRet;
}

inline bool isCallRelative(const UInt8 *pRetAddr)
{
    bool fRet = *reinterpret_cast<const UInt8*>(&pRetAddr[-5]) == X86_INSTR_CALL_REL32;
    ASSERT(!fRet || !isCallRelativeIndirect(pRetAddr));
    ASSERT(!fRet || !isCallRegisterIndirect(pRetAddr));
    return fRet;
}

inline bool isCallRegisterIndirect(const UInt8 *pRetAddr)
{
    bool fRet = (*reinterpret_cast<const UInt16*>(&pRetAddr[-5]) == X86_INSTR_NOP3_1)
             && (*reinterpret_cast<const UInt8*>(&pRetAddr[-3]) == X86_INSTR_NOP3_3)
             && (*reinterpret_cast<const UInt16*>(&pRetAddr[-2]) == X86_INSTR_CALL_IND_EAX);
    ASSERT(!fRet || !isCallRelative(pRetAddr));
    ASSERT(!fRet || !isCallRelativeIndirect(pRetAddr));
    return fRet;
}


#endif // _INSTRUCTIONENCONDINGS_HPP_
