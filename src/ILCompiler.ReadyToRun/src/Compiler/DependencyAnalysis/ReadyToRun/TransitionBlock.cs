// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Provides an abstraction over platform specific calling conventions (specifically, the calling convention
// utilized by the JIT on that platform). The caller enumerates each argument of a signature in turn, and is 
// provided with information mapping that argument into registers and/or stack locations.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    internal abstract class TransitionBlock
    {
        public TransitionBlock()
        {
        }

        public static TransitionBlock FromTarget(TargetDetails target)
        {
            switch (target.Architecture)
            {
                case TargetArchitecture.X86:
                    return X86TransitionBlock.Instance;

                case TargetArchitecture.X64:
                    return target.OperatingSystem == TargetOS.Windows ?
                        X64WindowsTransitionBlock.Instance :
                        X64UnixTransitionBlock.Instance;

                case TargetArchitecture.ARM:
                    return Arm32TransitionBlock.Instance;

                case TargetArchitecture.ARM64:
                    return Arm64TransitionBlock.Instance;

                default:
                    throw new NotImplementedException(target.Architecture.ToString());
            }
        }

        public const int MaxArgSize = 0xFFFFFF;

        // Unix AMD64 ABI: Special offset value to represent  struct passed in registers. Such a struct can span both
        // general purpose and floating point registers, so it can have two different offsets.
        public const int StructInRegsOffset = -2;

        public abstract TargetArchitecture Architecture { get; }

        public bool IsX86 => Architecture == TargetArchitecture.X86;
        public bool IsX64 => Architecture == TargetArchitecture.X64;
        public bool IsARM => Architecture == TargetArchitecture.ARM;
        public bool IsARM64 => Architecture == TargetArchitecture.ARM64;

        /// <summary>
        /// This property is only overridden in AMD64 Unix variant of the transition block.
        /// </summary>
        public virtual bool IsX64UnixABI => false;

        public abstract int PointerSize { get; }

        public int StackElemSize() => PointerSize;

        public int StackElemSize(int size) => (((size) + StackElemSize() - 1) & -StackElemSize());

        public abstract int NumArgumentRegisters { get; }

        public int SizeOfArgumentRegisters => NumArgumentRegisters * PointerSize;

        public abstract int NumCalleeSavedRegisters { get; }

        public int SizeOfCalleeSavedRegisters => NumCalleeSavedRegisters * PointerSize;

        public abstract int SizeOfTransitionBlock { get; }

        public abstract int OffsetOfArgumentRegisters { get; }

        public abstract int OffsetOfFloatArgumentRegisters { get; }

        public bool IsFloatArgumentRegisterOffset(int offset) => offset < 0;

        public abstract int EnregisteredParamTypeMaxSize { get; }

        public abstract int EnregisteredReturnTypeIntegerMaxSize { get; }

        /// <summary>
        /// Default implementation of ThisOffset; X86TransitionBlock provides a slightly different implementation.
        /// </summary>
        public virtual int ThisOffset { get { return OffsetOfArgumentRegisters;  } }

        /// <summary>
        /// Recalculate pos in GC ref map to actual offset. This is the default implementation for all architectures
        /// except for X86 where it's overridden to supply a more complex algorithm.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public virtual int OffsetFromGCRefMapPos(int pos)
        {
            return OffsetOfArgumentRegisters + pos * PointerSize;
        }

        /// <summary>
        /// The transition block should define everything pushed by callee. The code assumes in number of places that
        /// end of the transition block is caller's stack pointer.
        /// </summary>
        public int OffsetOfArgs => SizeOfTransitionBlock;

        public bool IsStackArgumentOffset(int offset)
        {
            int ofsArgRegs = OffsetOfArgumentRegisters;

            return offset >= (int)(ofsArgRegs + SizeOfArgumentRegisters);
        }

        public bool IsArgumentRegisterOffset(int offset)
        {
            int ofsArgRegs = OffsetOfArgumentRegisters;

            return offset >= ofsArgRegs && offset < (int)(ofsArgRegs + SizeOfArgumentRegisters);
        }

        public int GetArgumentIndexFromOffset(int offset)
        {
            Debug.Assert(!IsX86);
            return ((offset - OffsetOfArgumentRegisters) / PointerSize);
        }

        public int GetStackArgumentIndexFromOffset(int offset)
        {
            Debug.Assert(!IsX86);
            return (offset - OffsetOfArgs) / StackElemSize();
        }

        public const int InvalidOffset = -1;

        private sealed class X86TransitionBlock : TransitionBlock
        {
            public static TransitionBlock Instance = new X86TransitionBlock();

            public override TargetArchitecture Architecture => TargetArchitecture.X86;

            public override int PointerSize => 4;

            public override int NumArgumentRegisters => 2;
            public override int NumCalleeSavedRegisters => 4;
            // Argument registers, callee-save registers, return address
            public override int SizeOfTransitionBlock => SizeOfArgumentRegisters + SizeOfCalleeSavedRegisters + PointerSize;
            public override int OffsetOfArgumentRegisters => 0;
            // CALLDESCR_FPARGREGS is not set for X86
            public override int OffsetOfFloatArgumentRegisters => 0;
            // offsetof(ArgumentRegisters.ECX)
            public override int ThisOffset => 4;
            public override int EnregisteredParamTypeMaxSize => 0;
            public override int EnregisteredReturnTypeIntegerMaxSize => 4;

            public override int OffsetFromGCRefMapPos(int pos)
            {
                if (pos < NumArgumentRegisters)
                {
                    return OffsetOfArgumentRegisters + SizeOfArgumentRegisters - (pos + 1) * PointerSize;
                }
                else
                {
                    return OffsetOfArgs + (pos - NumArgumentRegisters) * PointerSize;
                }
            }
        }

        public const int SizeOfM128A = 16;

        private sealed class X64WindowsTransitionBlock : TransitionBlock
        {
            public static TransitionBlock Instance = new X64WindowsTransitionBlock();

            public override TargetArchitecture Architecture => TargetArchitecture.X64;
            public override int PointerSize => 8;
            // RCX, RDX, R8, R9
            public override int NumArgumentRegisters => 4;
            // RDI, RSI, RBX, RBP, R12, R13, R14, R15
            public override int NumCalleeSavedRegisters => 8;
            // Callee-saved registers, return address
            public override int SizeOfTransitionBlock => SizeOfCalleeSavedRegisters + PointerSize;
            public override int OffsetOfArgumentRegisters => SizeOfTransitionBlock;
            // CALLDESCR_FPARGREGS is not set for Amd64 on 
            public override int OffsetOfFloatArgumentRegisters => 0;
            public override int EnregisteredParamTypeMaxSize => 8;
            public override int EnregisteredReturnTypeIntegerMaxSize => 8;
        }

        private sealed class X64UnixTransitionBlock : TransitionBlock
        {
            public static readonly TransitionBlock Instance = new X64UnixTransitionBlock();

            public const int NUM_FLOAT_ARGUMENT_REGISTERS = 8;

            public override TargetArchitecture Architecture => TargetArchitecture.X64;
            public override bool IsX64UnixABI => true;
            public override int PointerSize => 8;
            // RDI, RSI, RDX, RCX, R8, R9
            public override int NumArgumentRegisters => 6;
            // R12, R13, R14, R15, RBX, RBP
            public override int NumCalleeSavedRegisters => 6;
            // Argument registers, callee-saved registers, return address
            public override int SizeOfTransitionBlock => SizeOfArgumentRegisters + SizeOfCalleeSavedRegisters + PointerSize;
            public override int OffsetOfArgumentRegisters => 0;
            public override int OffsetOfFloatArgumentRegisters => SizeOfM128A * NUM_FLOAT_ARGUMENT_REGISTERS;
            public override int EnregisteredParamTypeMaxSize => 16;
            public override int EnregisteredReturnTypeIntegerMaxSize => 16;
        }

        private sealed class Arm32TransitionBlock : TransitionBlock
        {
            public static TransitionBlock Instance = new Arm32TransitionBlock();

            public override TargetArchitecture Architecture => TargetArchitecture.ARM;
            public override int PointerSize => 4;
            // R0, R1, R2, R3
            public override int NumArgumentRegisters => 4;
            // R4, R5, R6, R7, R8, R9, R10, R11, R14
            public override int NumCalleeSavedRegisters => 9;
            // Callee-saves, argument registers
            public override int SizeOfTransitionBlock => SizeOfCalleeSavedRegisters + SizeOfArgumentRegisters;
            public override int OffsetOfArgumentRegisters => SizeOfCalleeSavedRegisters;
            // D0..D7
            public override int OffsetOfFloatArgumentRegisters => 8 * sizeof(double) + PointerSize;
            public override int EnregisteredParamTypeMaxSize => 0;
            public override int EnregisteredReturnTypeIntegerMaxSize => 4;
        }

        private sealed class Arm64TransitionBlock : TransitionBlock
        {
            public static TransitionBlock Instance = new Arm64TransitionBlock();

            public override TargetArchitecture Architecture => TargetArchitecture.ARM64;
            public override int PointerSize => 8;
            // X0 .. X7
            public override int NumArgumentRegisters => 8;
            // X29, X30, X19, X20, X21, X22, X23, X24, X25, X26, X27, X28
            public override int NumCalleeSavedRegisters => 12;
            // Callee-saves, padding, m_x8RetBuffReg, argument registers
            public override int SizeOfTransitionBlock => SizeOfCalleeSavedRegisters + 2 * PointerSize + SizeOfArgumentRegisters;
            public override int OffsetOfArgumentRegisters => SizeOfCalleeSavedRegisters + 2 * PointerSize;
            // D0..D7
            public override int OffsetOfFloatArgumentRegisters => 8 * sizeof(double) + PointerSize;
            public override int EnregisteredParamTypeMaxSize => 16;
            public override int EnregisteredReturnTypeIntegerMaxSize => 16;
        }
    };
}
