// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.TypeSystem;
using Internal.IL;
using Internal.IL.Stubs;
using Internal.JitInterface;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public static partial class HardwareIntrinsicHelpers
    {
        public static bool IsIsSupportedMethod(MethodDesc method)
        {
            return method.Name == "get_IsSupported";
        }

        public static MethodIL GetUnsupportedImplementationIL(MethodDesc method)
        {
            // The implementation of IsSupported for codegen backends that don't support hardware intrinsics
            // at all is to return 0.
            if (IsIsSupportedMethod(method))
            {
                return new ILStubMethodIL(method,
                    new byte[] {
                        (byte)ILOpcode.ldc_i4_0,
                        (byte)ILOpcode.ret
                    },
                    Array.Empty<LocalVariableDefinition>(), null);
            }

            // Other methods throw PlatformNotSupportedException
            MethodDesc throwPnse = method.Context.GetHelperEntryPoint("ThrowHelpers", "ThrowPlatformNotSupportedException");

            return new ILStubMethodIL(method,
                    new byte[] {
                        (byte)ILOpcode.call, 1, 0, 0, 0,
                        (byte)ILOpcode.br_s, unchecked((byte)-7),
                    },
                    Array.Empty<LocalVariableDefinition>(),
                    new object[] { throwPnse });
        }

        /// <summary>
        /// Generates IL for the IsSupported property that reads this information from a field initialized by the runtime
        /// at startup. Only works for intrinsics that the code generator can generate detection code for.
        /// </summary>
        public static MethodIL EmitIsSupportedIL(MethodDesc method, FieldDesc isSupportedField)
        {
            Debug.Assert(IsIsSupportedMethod(method));
            Debug.Assert(isSupportedField.IsStatic && isSupportedField.FieldType.IsWellKnownType(WellKnownType.Int32));

            string id = InstructionSetSupport.GetHardwareIntrinsicId(method.Context.Target.Architecture, method.OwningType);

            Debug.Assert(method.Context.Target.Architecture == TargetArchitecture.X64
                || method.Context.Target.Architecture == TargetArchitecture.X86);
            int flag = XArchIntrinsicConstants.FromHardwareIntrinsicId(id);

            var emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();

            codeStream.Emit(ILOpcode.ldsfld, emit.NewToken(isSupportedField));
            codeStream.EmitLdc(flag);
            codeStream.Emit(ILOpcode.and);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.cgt_un);
            codeStream.Emit(ILOpcode.ret);

            return emit.Link(method);
        }

        public static int GetRuntimeRequiredIsaFlags(InstructionSetSupport instructionSetSupport)
        {
            Debug.Assert(instructionSetSupport.Architecture == TargetArchitecture.X64 ||
                instructionSetSupport.Architecture == TargetArchitecture.X86);
            return XArchIntrinsicConstants.FromInstructionSetFlags(instructionSetSupport.SupportedFlags);
        }

        // Keep this enumeration in sync with startup.cpp in the native runtime.
        private static class XArchIntrinsicConstants
        {
            // SSE and SSE2 are baseline ISAs - they're always available
            public const int Aes = 0x0001;
            public const int Pclmulqdq = 0x0002;
            public const int Sse3 = 0x0004;
            public const int Ssse3 = 0x0008;
            public const int Sse41 = 0x0010;
            public const int Sse42 = 0x0020;
            public const int Popcnt = 0x0040;
            public const int Avx = 0x0080;
            public const int Fma = 0x0100;
            public const int Avx2 = 0x0200;
            public const int Bmi1 = 0x0400;
            public const int Bmi2 = 0x0800;
            public const int Lzcnt = 0x1000;

            public static int FromHardwareIntrinsicId(string id)
            {
                return id switch
                {
                    "Aes" => Aes,
                    "Pclmulqdq" => Pclmulqdq,
                    "Sse3" => Sse3,
                    "Ssse3" => Ssse3,
                    "Sse41" => Sse41,
                    "Sse42" => Sse42,
                    "Popcnt" => Popcnt,
                    "Avx" => Avx,
                    "Fma" => Fma,
                    "Avx2" => Avx2,
                    "Bmi1" => Bmi1,
                    "Bmi2" => Bmi2,
                    "Lzcnt" => Lzcnt,
                    _ => throw new NotSupportedException(),
                };
            }

            public static int FromInstructionSetFlags(InstructionSetFlags instructionSets)
            {
                int result = 0;

                Debug.Assert(InstructionSet.X64_AES == InstructionSet.X86_AES);
                Debug.Assert(InstructionSet.X64_SSE41 == InstructionSet.X86_SSE41);
                Debug.Assert(InstructionSet.X64_LZCNT == InstructionSet.X86_LZCNT);

                foreach (InstructionSet instructionSet in instructionSets)
                {
                    result |= instructionSet switch
                    {
                        InstructionSet.X64_AES => Aes,
                        InstructionSet.X64_PCLMULQDQ => Pclmulqdq,
                        InstructionSet.X64_SSE3 => Sse3,
                        InstructionSet.X64_SSSE3 => Ssse3,
                        InstructionSet.X64_SSE41 => Sse41,
                        InstructionSet.X64_SSE41_X64 => Sse41,
                        InstructionSet.X64_SSE42 => Sse42,
                        InstructionSet.X64_SSE42_X64 => Sse42,
                        InstructionSet.X64_POPCNT => Popcnt,
                        InstructionSet.X64_POPCNT_X64 => Popcnt,
                        InstructionSet.X64_AVX => Avx,
                        InstructionSet.X64_FMA => Fma,
                        InstructionSet.X64_AVX2 => Avx2,
                        InstructionSet.X64_BMI1 => Bmi1,
                        InstructionSet.X64_BMI1_X64 => Bmi1,
                        InstructionSet.X64_BMI2 => Bmi2,
                        InstructionSet.X64_BMI2_X64 => Bmi2,
                        InstructionSet.X64_LZCNT => Lzcnt,
                        InstructionSet.X64_LZCNT_X64 => Popcnt,

                        // SSE and SSE2 are baseline ISAs - they're always available
                        InstructionSet.X64_SSE => 0,
                        InstructionSet.X64_SSE_X64 => 0,
                        InstructionSet.X64_SSE2 => 0,
                        InstructionSet.X64_SSE2_X64 => 0,
                        InstructionSet.X64_X86Base => 0,
                        InstructionSet.X64_X86Base_X64 => 0,

                        _ => throw new NotSupportedException()
                    };
                }

                return result;
            }
        }
    }
}
