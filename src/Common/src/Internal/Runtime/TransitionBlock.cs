// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This file is a line by line port of callingconvention.h from the CLR with the intention that we may wish to merge
// changes from the CLR in at a later time. As such, the normal coding conventions are ignored.
//

//
#if ARM
#define _TARGET_ARM_
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define FEATURE_HFA
#elif ARM64
#define _TARGET_ARM64_
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define ENREGISTERED_PARAMTYPE_MAXSIZE
#define FEATURE_HFA
#elif X86
#define _TARGET_X86_
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#elif AMD64
#if PLATFORM_UNIX
#define UNIX_AMD64_ABI
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#else
#endif
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define _TARGET_AMD64_
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define ENREGISTERED_PARAMTYPE_MAXSIZE
#else
#error Unknown architecture!
#endif

// Provides an abstraction over platform specific calling conventions (specifically, the calling convention
// utilized by the JIT on that platform). The caller enumerates each argument of a signature in turn, and is 
// provided with information mapping that argument into registers and/or stack locations.

using System;

namespace Internal.Runtime
{
#if _TARGET_AMD64_
#pragma warning disable 0169
#if UNIX_AMD64_ABI
    struct ReturnBlock
    {
        IntPtr returnValue;
        IntPtr returnValue2;
    }

    struct ArgumentRegisters
    {
        IntPtr rdi;
        IntPtr rsi;
        IntPtr rdx;
        IntPtr rcx;
        IntPtr r8;
        IntPtr r9;
    }
#else // UNIX_AMD64_ABI
    struct ReturnBlock
    {
        IntPtr returnValue;
    }

    struct ArgumentRegisters
    {
        IntPtr rdx;
        IntPtr rcx;
        IntPtr r8;
        IntPtr r9;
    }
#endif // UNIX_AMD64_ABI
#pragma warning restore 0169

#pragma warning disable 0169
    struct M128A
    {
        IntPtr a;
        IntPtr b;
    }
    struct FloatArgumentRegisters
    {
        M128A d0;
        M128A d1;
        M128A d2;
        M128A d3;
#if UNIX_AMD64_ABI
        M128A d4;
        M128A d5;
        M128A d6;
        M128A d7;
#endif
    }
#pragma warning restore 0169

    struct ArchitectureConstants
    {
        // To avoid corner case bugs, limit maximum size of the arguments with sufficient margin
        public const int MAX_ARG_SIZE = 0xFFFFFF;

#if UNIX_AMD64_ABI
        public const int NUM_ARGUMENT_REGISTERS = 6;
#else
        public const int NUM_ARGUMENT_REGISTERS = 4;
#endif
        public const int ARGUMENTREGISTERS_SIZE = NUM_ARGUMENT_REGISTERS * 8;
        public const int ENREGISTERED_RETURNTYPE_MAXSIZE = 8;
        public const int ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE = 8;
        public const int ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE_PRIMITIVE = 8;
        public const int ENREGISTERED_PARAMTYPE_MAXSIZE = 8;
        public const int STACK_ELEM_SIZE = 8;
        public static int StackElemSize(int size) { return (((size) + STACK_ELEM_SIZE - 1) & ~(STACK_ELEM_SIZE - 1)); }
    }
#elif _TARGET_ARM64_
#pragma warning disable 0169
    struct ReturnBlock
    {
        IntPtr returnValue;
        IntPtr returnValue2;
        IntPtr returnValue3;
        IntPtr returnValue4;
        IntPtr returnValue5;
        IntPtr returnValue6;
        IntPtr returnValue7;
        IntPtr returnValue8;
    }

    struct CalleeSavedRegisters
    {
        IntPtr x29;
        public IntPtr m_ReturnAddress; // Also known as x30
        IntPtr x19;
        IntPtr x20;
        IntPtr x21;
        IntPtr x22;
        IntPtr x23;
        IntPtr x24;
        IntPtr x25;
        IntPtr x26;
        IntPtr x27;
        IntPtr x28;
    }

    struct ArgumentRegisters
    {
        IntPtr x0;
        IntPtr x1;
        IntPtr x2;
        IntPtr x3;
        IntPtr x4;
        IntPtr x5;
        IntPtr x6;
        IntPtr x7;
    }
#pragma warning restore 0169

#pragma warning disable 0169
    struct FloatArgumentRegisters
    {
        double d0;
        double d1;
        double d2;
        double d3;
        double d4;
        double d5;
        double d6;
        double d7;
    }
#pragma warning restore 0169

    struct ArchitectureConstants
    {
        // To avoid corner case bugs, limit maximum size of the arguments with sufficient margin
        public const int MAX_ARG_SIZE = 0xFFFFFF;

        public const int NUM_ARGUMENT_REGISTERS = 8;
        public const int ARGUMENTREGISTERS_SIZE = NUM_ARGUMENT_REGISTERS * 8;
        public const int ENREGISTERED_RETURNTYPE_MAXSIZE = 64; //(maximum HFA size is 8 doubles)
        public const int ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE = 8;
        public const int ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE_PRIMITIVE = 8;
        public const int ENREGISTERED_PARAMTYPE_MAXSIZE = 16; //(max value type size than can be passed by value)
        public const int STACK_ELEM_SIZE = 8;
        public static int StackElemSize(int size) { return (((size) + STACK_ELEM_SIZE - 1) & ~(STACK_ELEM_SIZE - 1)); }
    }
#elif _TARGET_X86_
#pragma warning disable 0169, 0649
    struct ReturnBlock
    {
        public IntPtr returnValue;
        public IntPtr returnValue2;
    }

    struct ArgumentRegisters
    {
        public IntPtr edx;
        public static unsafe int GetOffsetOfEdx()
        {
            return 0;
        }
        public IntPtr ecx;
        public static unsafe int GetOffsetOfEcx()
        {
            return sizeof(IntPtr);
        }
    }
    // This struct isn't used by x86, but exists for compatibility with the definition of the CallDescrData struct
    struct FloatArgumentRegisters
    {
    }
#pragma warning restore 0169, 0649

    struct ArchitectureConstants
    {
        // To avoid corner case bugs, limit maximum size of the arguments with sufficient margin
        public const int MAX_ARG_SIZE = 0xFFFFFF;

        public const int NUM_ARGUMENT_REGISTERS = 2;
        public const int ARGUMENTREGISTERS_SIZE = NUM_ARGUMENT_REGISTERS * 4;
        public const int ENREGISTERED_RETURNTYPE_MAXSIZE = 8;
        public const int ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE = 4;
        public const int ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE_PRIMITIVE = 4;
        public const int STACK_ELEM_SIZE = 4;
        public static int StackElemSize(int size) { return (((size) + STACK_ELEM_SIZE - 1) & ~(STACK_ELEM_SIZE - 1)); }
    }
#elif _TARGET_ARM_
#pragma warning disable 0169
    struct ReturnBlock
    {
        IntPtr returnValue;
        IntPtr returnValue2;
        IntPtr returnValue3;
        IntPtr returnValue4;
        IntPtr returnValue5;
        IntPtr returnValue6;
        IntPtr returnValue7;
        IntPtr returnValue8;
    }

    struct ArgumentRegisters
    {
        IntPtr r0;
        IntPtr r1;
        IntPtr r2;
        IntPtr r3;
    }

    struct FloatArgumentRegisters
    {
        double d0;
        double d1;
        double d2;
        double d3;
        double d4;
        double d5;
        double d6;
        double d7;
    }
#pragma warning restore 0169

    struct ArchitectureConstants
    {
        // To avoid corner case bugs, limit maximum size of the arguments with sufficient margin
        public const int MAX_ARG_SIZE = 0xFFFFFF;

        public const int NUM_ARGUMENT_REGISTERS = 4;
        public const int ARGUMENTREGISTERS_SIZE = NUM_ARGUMENT_REGISTERS * 4;
        public const int ENREGISTERED_RETURNTYPE_MAXSIZE = 32;
        public const int ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE = 4;
        public const int ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE_PRIMITIVE = 8;
        public const int STACK_ELEM_SIZE = 4;
        public static int StackElemSize(int size) { return (((size) + STACK_ELEM_SIZE - 1) & ~(STACK_ELEM_SIZE - 1)); }
    }
#endif

    //
    // TransitionBlock is layout of stack frame of method call, saved argument registers and saved callee saved registers. Even though not 
    // all fields are used all the time, we use uniform form for simplicity.
    //
    struct TransitionBlock
    {
#pragma warning disable 0169,0649

#if _TARGET_X86_
        public ArgumentRegisters m_argumentRegisters;
        public static unsafe int GetOffsetOfArgumentRegisters()
        {
            return 0;
        }
        public ReturnBlock m_returnBlock;
        public static unsafe int GetOffsetOfReturnValuesBlock()
        {
            return sizeof(ArgumentRegisters);
        }
        IntPtr m_ebp;
        IntPtr m_ReturnAddress;
#elif _TARGET_AMD64_

#if UNIX_AMD64_ABI
        public ReturnBlock m_returnBlock;
        public static unsafe int GetOffsetOfReturnValuesBlock()
        {
            return 0;
        }

        public ArgumentRegisters m_argumentRegisters;
        public static unsafe int GetOffsetOfArgumentRegisters()
        {
            return sizeof(ReturnBlock);
        }

        IntPtr m_alignmentPadding;
        IntPtr m_ReturnAddress;
#else // UNIX_AMD64_ABI
        IntPtr m_returnBlockPadding;
        ReturnBlock m_returnBlock;
        public static unsafe int GetOffsetOfReturnValuesBlock()
        {
            return sizeof(IntPtr);
        }
        IntPtr m_alignmentPadding;
        IntPtr m_ReturnAddress;
        public static unsafe int GetOffsetOfArgumentRegisters()
        {
            return sizeof(TransitionBlock);
        }
#endif // UNIX_AMD64_ABI

#elif _TARGET_ARM_
        public ReturnBlock m_returnBlock;
        public static unsafe int GetOffsetOfReturnValuesBlock()
        {
            return 0;
        }

        public ArgumentRegisters m_argumentRegisters;
        public static unsafe int GetOffsetOfArgumentRegisters()
        {
            return sizeof(ReturnBlock);
        }
#elif _TARGET_ARM64_
        public ReturnBlock m_returnBlock;
        public static unsafe int GetOffsetOfReturnValuesBlock()
        {
            return 0;
        }

        public ArgumentRegisters m_argumentRegisters;
        public static unsafe int GetOffsetOfArgumentRegisters()
        {
            return sizeof(ReturnBlock);
        }
#else
#error Portability problem
#endif
#pragma warning restore 0169,0649

        // The transition block should define everything pushed by callee. The code assumes in number of places that
        // end of the transition block is caller's stack pointer.

        public static unsafe byte GetOffsetOfArgs()
        {
            return (byte)sizeof(TransitionBlock);
        }


        public static bool IsStackArgumentOffset(int offset)
        {
            int ofsArgRegs = GetOffsetOfArgumentRegisters();

            return offset >= (int)(ofsArgRegs + ArchitectureConstants.ARGUMENTREGISTERS_SIZE);
        }

        static bool IsArgumentRegisterOffset(int offset)
        {
            int ofsArgRegs = GetOffsetOfArgumentRegisters();

            return offset >= ofsArgRegs && offset < (int)(ofsArgRegs + ArchitectureConstants.ARGUMENTREGISTERS_SIZE);
        }

#if !_TARGET_X86_
        public static unsafe int GetArgumentIndexFromOffset(int offset)
        {
            return ((offset - GetOffsetOfArgumentRegisters()) / IntPtr.Size);
        }
#endif

#if CALLDESCR_FPARGREGS
        public static bool IsFloatArgumentRegisterOffset(int offset)
        {
            return offset < 0;
        }

        public static int GetOffsetOfFloatArgumentRegisters()
        {
            return -GetNegSpaceSize();
        }
#endif

        public static unsafe int GetNegSpaceSize()
        {
            int negSpaceSize = 0;
#if CALLDESCR_FPARGREGS
            negSpaceSize += sizeof(FloatArgumentRegisters);
#endif
            return negSpaceSize;
        }

        public static int GetThisOffset()
        {
            // This pointer is in the first argument register by default
            int ret = TransitionBlock.GetOffsetOfArgumentRegisters();

#if _TARGET_X86_
            // x86 is special as always
            ret += ArgumentRegisters.GetOffsetOfEcx();
#endif

            return ret;
        }

        public const int InvalidOffset = -1;
    };
}
