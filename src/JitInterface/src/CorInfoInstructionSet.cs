
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// DO NOT EDIT THIS FILE! IT IS AUTOGENERATED
// FROM /src/coreclr/src/tools/Common/JitInterface/ThunkGenerator/InstructionSetDesc.txt
// using /src/coreclr/src/tools/Common/JitInterface/ThunkGenerator/gen.bat

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Internal.TypeSystem;

namespace Internal.JitInterface
{
    public enum InstructionSet
    {
        ILLEGAL = 0,
        NONE = 63,
        ARM64_ArmBase=1,
        ARM64_ArmBase_Arm64=2,
        ARM64_AdvSimd=3,
        ARM64_AdvSimd_Arm64=4,
        ARM64_Aes=5,
        ARM64_Crc32=6,
        ARM64_Crc32_Arm64=7,
        ARM64_Sha1=8,
        ARM64_Sha256=9,
        ARM64_Atomics=10,
        ARM64_Vector64=11,
        ARM64_Vector128=12,
        X64_X86Base=1,
        X64_SSE=2,
        X64_SSE2=3,
        X64_SSE3=4,
        X64_SSSE3=5,
        X64_SSE41=6,
        X64_SSE42=7,
        X64_AVX=8,
        X64_AVX2=9,
        X64_AES=10,
        X64_BMI1=11,
        X64_BMI2=12,
        X64_FMA=13,
        X64_LZCNT=14,
        X64_PCLMULQDQ=15,
        X64_POPCNT=16,
        X64_Vector128=17,
        X64_Vector256=18,
        X64_X86Base_X64=19,
        X64_BMI1_X64=20,
        X64_BMI2_X64=21,
        X64_LZCNT_X64=22,
        X64_POPCNT_X64=23,
        X64_SSE_X64=24,
        X64_SSE2_X64=25,
        X64_SSE41_X64=26,
        X64_SSE42_X64=27,
        X86_X86Base=1,
        X86_SSE=2,
        X86_SSE2=3,
        X86_SSE3=4,
        X86_SSSE3=5,
        X86_SSE41=6,
        X86_SSE42=7,
        X86_AVX=8,
        X86_AVX2=9,
        X86_AES=10,
        X86_BMI1=11,
        X86_BMI2=12,
        X86_FMA=13,
        X86_LZCNT=14,
        X86_PCLMULQDQ=15,
        X86_POPCNT=16,
        X86_Vector128=17,
        X86_Vector256=18,
        X86_X86Base_X64=19,
        X86_BMI1_X64=20,
        X86_BMI2_X64=21,
        X86_LZCNT_X64=22,
        X86_POPCNT_X64=23,
        X86_SSE_X64=24,
        X86_SSE2_X64=25,
        X86_SSE41_X64=26,
        X86_SSE42_X64=27,

    }

    public struct InstructionSetFlags : IEnumerable<InstructionSet>
    {
        ulong _flags;
        
        public void AddInstructionSet(InstructionSet instructionSet)
        {
            _flags = _flags | (((ulong)1) << (int)instructionSet);
        }

        public void RemoveInstructionSet(InstructionSet instructionSet)
        {
            _flags = _flags & ~(((ulong)1) << (int)instructionSet);
        }

        public bool HasInstructionSet(InstructionSet instructionSet)
        {
            return (_flags & (((ulong)1) << (int)instructionSet)) != 0;
        }

        public bool Equals(InstructionSetFlags other)
        {
            return _flags == other._flags;
        }

        public void Add(InstructionSetFlags other)
        {
            _flags |= other._flags;
        }

        public void IntersectionWith(InstructionSetFlags other)
        {
            _flags &= other._flags;
        }

        public void Remove(InstructionSetFlags other)
        {
            _flags &= ~other._flags;
        }

        public bool IsEmpty()
        {
            return _flags == 0;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<InstructionSet> GetEnumerator()
        {
            for (int i = 1; i < (int)InstructionSet.NONE; i ++)
            {
                InstructionSet instructionSet = (InstructionSet)i;
                if (HasInstructionSet(instructionSet))
                {
                    yield return instructionSet;
                }
            }
        }

        public void ExpandInstructionSetByImplication(TargetArchitecture architecture)
        {
            this = ExpandInstructionSetByImplicationHelper(architecture, this);
        }

        public static InstructionSetFlags ExpandInstructionSetByImplicationHelper(TargetArchitecture architecture, InstructionSetFlags input)
        {
            InstructionSetFlags oldflags = input;
            InstructionSetFlags resultflags = input;
            do
            {
                oldflags = resultflags;
                switch(architecture)
                {

                case TargetArchitecture.ARM64:
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_ArmBase))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_ArmBase_Arm64);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_ArmBase_Arm64))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_ArmBase);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_AdvSimd))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_AdvSimd_Arm64);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_AdvSimd_Arm64))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_AdvSimd);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_Crc32))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_Crc32_Arm64);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_Crc32_Arm64))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_Crc32);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_AdvSimd))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_ArmBase);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_Aes))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_ArmBase);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_Crc32))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_ArmBase);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_Sha1))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_ArmBase);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_Sha256))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_ArmBase);
                    break;

                case TargetArchitecture.X64:
                    if (resultflags.HasInstructionSet(InstructionSet.X64_X86Base))
                        resultflags.AddInstructionSet(InstructionSet.X64_X86Base_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_X86Base_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_X86Base);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE2_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE2_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE41))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE41_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE41_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE41);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE42))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE42_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE42_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE42);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_BMI1))
                        resultflags.AddInstructionSet(InstructionSet.X64_BMI1_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_BMI1_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_BMI1);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_BMI2))
                        resultflags.AddInstructionSet(InstructionSet.X64_BMI2_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_BMI2_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_BMI2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_LZCNT))
                        resultflags.AddInstructionSet(InstructionSet.X64_LZCNT_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_LZCNT_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_LZCNT);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_POPCNT))
                        resultflags.AddInstructionSet(InstructionSet.X64_POPCNT_X64);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_POPCNT_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_POPCNT);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE))
                        resultflags.AddInstructionSet(InstructionSet.X64_X86Base);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE3))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSSE3))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE3);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE41))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSSE3);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE42))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE41);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE42);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_AVX2))
                        resultflags.AddInstructionSet(InstructionSet.X64_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_AES))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_BMI1))
                        resultflags.AddInstructionSet(InstructionSet.X64_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_BMI2))
                        resultflags.AddInstructionSet(InstructionSet.X64_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_FMA))
                        resultflags.AddInstructionSet(InstructionSet.X64_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_PCLMULQDQ))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_POPCNT))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE42);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_Vector256))
                        resultflags.AddInstructionSet(InstructionSet.X64_AVX);
                    break;

                case TargetArchitecture.X86:
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE))
                        resultflags.AddInstructionSet(InstructionSet.X86_X86Base);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE3))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSSE3))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE3);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE41))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSSE3);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE42))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE41);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE42);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_AVX2))
                        resultflags.AddInstructionSet(InstructionSet.X86_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_AES))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_BMI1))
                        resultflags.AddInstructionSet(InstructionSet.X86_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_BMI2))
                        resultflags.AddInstructionSet(InstructionSet.X86_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_FMA))
                        resultflags.AddInstructionSet(InstructionSet.X86_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_PCLMULQDQ))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_POPCNT))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE42);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_Vector256))
                        resultflags.AddInstructionSet(InstructionSet.X86_AVX);
                    break;

                }
            } while (!oldflags.Equals(resultflags));
            return resultflags;
        }

        public void ExpandInstructionSetByReverseImplication(TargetArchitecture architecture)
        {
            this = ExpandInstructionSetByReverseImplicationHelper(architecture, this);
        }

        private static InstructionSetFlags ExpandInstructionSetByReverseImplicationHelper(TargetArchitecture architecture, InstructionSetFlags input)
        {
            InstructionSetFlags oldflags = input;
            InstructionSetFlags resultflags = input;
            do
            {
                oldflags = resultflags;
                switch(architecture)
                {

                case TargetArchitecture.ARM64:
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_ArmBase_Arm64))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_ArmBase);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_AdvSimd_Arm64))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_AdvSimd);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_Crc32_Arm64))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_Crc32);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_ArmBase))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_AdvSimd);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_ArmBase))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_Aes);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_ArmBase))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_Crc32);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_ArmBase))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_Sha1);
                    if (resultflags.HasInstructionSet(InstructionSet.ARM64_ArmBase))
                        resultflags.AddInstructionSet(InstructionSet.ARM64_Sha256);
                    break;

                case TargetArchitecture.X64:
                    if (resultflags.HasInstructionSet(InstructionSet.X64_X86Base_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_X86Base);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE2_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE41_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE41);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE42_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE42);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_BMI1_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_BMI1);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_BMI2_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_BMI2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_LZCNT_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_LZCNT);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_POPCNT_X64))
                        resultflags.AddInstructionSet(InstructionSet.X64_POPCNT);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_X86Base))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE3);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE3))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSSE3);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSSE3))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE41);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE41))
                        resultflags.AddInstructionSet(InstructionSet.X64_SSE42);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE42))
                        resultflags.AddInstructionSet(InstructionSet.X64_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X64_AVX2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X64_AES);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X64_BMI1);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X64_BMI2);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X64_FMA);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X64_PCLMULQDQ);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_SSE42))
                        resultflags.AddInstructionSet(InstructionSet.X64_POPCNT);
                    if (resultflags.HasInstructionSet(InstructionSet.X64_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X64_Vector256);
                    break;

                case TargetArchitecture.X86:
                    if (resultflags.HasInstructionSet(InstructionSet.X86_X86Base))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE2);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE3);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE3))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSSE3);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSSE3))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE41);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE41))
                        resultflags.AddInstructionSet(InstructionSet.X86_SSE42);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE42))
                        resultflags.AddInstructionSet(InstructionSet.X86_AVX);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X86_AVX2);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X86_AES);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X86_BMI1);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X86_BMI2);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X86_FMA);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE2))
                        resultflags.AddInstructionSet(InstructionSet.X86_PCLMULQDQ);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_SSE42))
                        resultflags.AddInstructionSet(InstructionSet.X86_POPCNT);
                    if (resultflags.HasInstructionSet(InstructionSet.X86_AVX))
                        resultflags.AddInstructionSet(InstructionSet.X86_Vector256);
                    break;

                }
            } while (!oldflags.Equals(resultflags));
            return resultflags;
        }

        public struct InstructionSetInfo
        {
            public readonly string Name;
            public readonly string ManagedName;
            public readonly InstructionSet InstructionSet;
            public readonly bool Specifiable;

            public InstructionSetInfo(string name, string managedName, InstructionSet instructionSet, bool specifiable)
            {
                Name = name;
                ManagedName = managedName;
                InstructionSet = instructionSet;
                Specifiable = specifiable;
            }
        }

        public static IEnumerable<InstructionSetInfo> ArchitectureToValidInstructionSets(TargetArchitecture architecture)
        {
            switch (architecture)
            {

                case TargetArchitecture.ARM64:
                    yield return new InstructionSetInfo("base", "ArmBase", InstructionSet.ARM64_ArmBase, true);
                    yield return new InstructionSetInfo("neon", "AdvSimd", InstructionSet.ARM64_AdvSimd, true);
                    yield return new InstructionSetInfo("aes", "Aes", InstructionSet.ARM64_Aes, true);
                    yield return new InstructionSetInfo("crc", "Crc32", InstructionSet.ARM64_Crc32, true);
                    yield return new InstructionSetInfo("sha1", "Sha1", InstructionSet.ARM64_Sha1, true);
                    yield return new InstructionSetInfo("sha2", "Sha256", InstructionSet.ARM64_Sha256, true);
                    yield return new InstructionSetInfo("lse", "", InstructionSet.ARM64_Atomics, true);
                    yield return new InstructionSetInfo("Vector64", "", InstructionSet.ARM64_Vector64, false);
                    yield return new InstructionSetInfo("Vector128", "", InstructionSet.ARM64_Vector128, false);
                    break;

                case TargetArchitecture.X64:
                    yield return new InstructionSetInfo("base", "X86Base", InstructionSet.X64_X86Base, true);
                    yield return new InstructionSetInfo("sse", "Sse", InstructionSet.X64_SSE, true);
                    yield return new InstructionSetInfo("sse2", "Sse2", InstructionSet.X64_SSE2, true);
                    yield return new InstructionSetInfo("sse3", "Sse3", InstructionSet.X64_SSE3, true);
                    yield return new InstructionSetInfo("ssse3", "Ssse3", InstructionSet.X64_SSSE3, true);
                    yield return new InstructionSetInfo("sse4.1", "Sse41", InstructionSet.X64_SSE41, true);
                    yield return new InstructionSetInfo("sse4.2", "Sse42", InstructionSet.X64_SSE42, true);
                    yield return new InstructionSetInfo("avx", "Avx", InstructionSet.X64_AVX, true);
                    yield return new InstructionSetInfo("avx2", "Avx2", InstructionSet.X64_AVX2, true);
                    yield return new InstructionSetInfo("aes", "Aes", InstructionSet.X64_AES, true);
                    yield return new InstructionSetInfo("bmi", "Bmi1", InstructionSet.X64_BMI1, true);
                    yield return new InstructionSetInfo("bmi2", "Bmi2", InstructionSet.X64_BMI2, true);
                    yield return new InstructionSetInfo("fma", "Fma", InstructionSet.X64_FMA, true);
                    yield return new InstructionSetInfo("lzcnt", "Lzcnt", InstructionSet.X64_LZCNT, true);
                    yield return new InstructionSetInfo("pclmul", "Pclmulqdq", InstructionSet.X64_PCLMULQDQ, true);
                    yield return new InstructionSetInfo("popcnt", "Popcnt", InstructionSet.X64_POPCNT, true);
                    yield return new InstructionSetInfo("Vector128", "", InstructionSet.X64_Vector128, false);
                    yield return new InstructionSetInfo("Vector256", "", InstructionSet.X64_Vector256, false);
                    break;

                case TargetArchitecture.X86:
                    yield return new InstructionSetInfo("base", "X86Base", InstructionSet.X86_X86Base, true);
                    yield return new InstructionSetInfo("sse", "Sse", InstructionSet.X86_SSE, true);
                    yield return new InstructionSetInfo("sse2", "Sse2", InstructionSet.X86_SSE2, true);
                    yield return new InstructionSetInfo("sse3", "Sse3", InstructionSet.X86_SSE3, true);
                    yield return new InstructionSetInfo("ssse3", "Ssse3", InstructionSet.X86_SSSE3, true);
                    yield return new InstructionSetInfo("sse4.1", "Sse41", InstructionSet.X86_SSE41, true);
                    yield return new InstructionSetInfo("sse4.2", "Sse42", InstructionSet.X86_SSE42, true);
                    yield return new InstructionSetInfo("avx", "Avx", InstructionSet.X86_AVX, true);
                    yield return new InstructionSetInfo("avx2", "Avx2", InstructionSet.X86_AVX2, true);
                    yield return new InstructionSetInfo("aes", "Aes", InstructionSet.X86_AES, true);
                    yield return new InstructionSetInfo("bmi", "Bmi1", InstructionSet.X86_BMI1, true);
                    yield return new InstructionSetInfo("bmi2", "Bmi2", InstructionSet.X86_BMI2, true);
                    yield return new InstructionSetInfo("fma", "Fma", InstructionSet.X86_FMA, true);
                    yield return new InstructionSetInfo("lzcnt", "Lzcnt", InstructionSet.X86_LZCNT, true);
                    yield return new InstructionSetInfo("pclmul", "Pclmulqdq", InstructionSet.X86_PCLMULQDQ, true);
                    yield return new InstructionSetInfo("popcnt", "Popcnt", InstructionSet.X86_POPCNT, true);
                    yield return new InstructionSetInfo("Vector128", "", InstructionSet.X86_Vector128, false);
                    yield return new InstructionSetInfo("Vector256", "", InstructionSet.X86_Vector256, false);
                    break;

            }
        }

        public void Set64BitInstructionSetVariants(TargetArchitecture architecture)
        {
            switch (architecture)
            {

                case TargetArchitecture.ARM64:
                    if (HasInstructionSet(InstructionSet.ARM64_ArmBase))
                        AddInstructionSet(InstructionSet.ARM64_ArmBase_Arm64);
                    if (HasInstructionSet(InstructionSet.ARM64_AdvSimd))
                        AddInstructionSet(InstructionSet.ARM64_AdvSimd_Arm64);
                    if (HasInstructionSet(InstructionSet.ARM64_Crc32))
                        AddInstructionSet(InstructionSet.ARM64_Crc32_Arm64);
                    break;

                case TargetArchitecture.X64:
                    if (HasInstructionSet(InstructionSet.X64_X86Base))
                        AddInstructionSet(InstructionSet.X64_X86Base_X64);
                    if (HasInstructionSet(InstructionSet.X64_SSE))
                        AddInstructionSet(InstructionSet.X64_SSE_X64);
                    if (HasInstructionSet(InstructionSet.X64_SSE2))
                        AddInstructionSet(InstructionSet.X64_SSE2_X64);
                    if (HasInstructionSet(InstructionSet.X64_SSE41))
                        AddInstructionSet(InstructionSet.X64_SSE41_X64);
                    if (HasInstructionSet(InstructionSet.X64_SSE42))
                        AddInstructionSet(InstructionSet.X64_SSE42_X64);
                    if (HasInstructionSet(InstructionSet.X64_BMI1))
                        AddInstructionSet(InstructionSet.X64_BMI1_X64);
                    if (HasInstructionSet(InstructionSet.X64_BMI2))
                        AddInstructionSet(InstructionSet.X64_BMI2_X64);
                    if (HasInstructionSet(InstructionSet.X64_LZCNT))
                        AddInstructionSet(InstructionSet.X64_LZCNT_X64);
                    if (HasInstructionSet(InstructionSet.X64_POPCNT))
                        AddInstructionSet(InstructionSet.X64_POPCNT_X64);
                    break;

                case TargetArchitecture.X86:
                    break;

            }
        }
    }
}
