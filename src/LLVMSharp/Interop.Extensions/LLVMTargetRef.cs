// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMTargetRef : IEquatable<LLVMTargetRef>
    {
        public LLVMTargetRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMTargetRef(LLVMTarget* value)
        {
            return new LLVMTargetRef((IntPtr)value);
        }

        public static implicit operator LLVMTarget*(LLVMTargetRef value)
        {
            return (LLVMTarget*)value.Handle;
        }

        public static string DefaultTriple
        {
            get
            {
                var pDefaultTriple = LLVM.GetDefaultTargetTriple();

                if (pDefaultTriple is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pDefaultTriple, int.MaxValue);
                return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }
        }

        public static LLVMTargetRef First => LLVM.GetFirstTarget();

        public static IEnumerable<LLVMTargetRef> Targets
        {
            get
            {
                var target = First;

                while (target != null)
                {
                    yield return target;
                    target = target.GetNext();
                }
            }
        }

        public string Name
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var pName = LLVM.GetTargetName(this);

                if (pName is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pName, int.MaxValue);
                return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }
        }

        public static bool operator ==(LLVMTargetRef left, LLVMTargetRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMTargetRef left, LLVMTargetRef right) => !(left == right);

        public override bool Equals(object obj) => obj is LLVMTargetRef other && Equals(other);

        public bool Equals(LLVMTargetRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();

        public LLVMTargetRef GetNext() => LLVM.GetNextTarget(this);

        public LLVMTargetMachineRef CreateTargetMachine(string triple, string cpu, string features, LLVMCodeGenOptLevel level, LLVMRelocMode reloc, LLVMCodeModel codeModel)
        {
            using var marshaledTriple = new MarshaledString(triple);
            using var marshaledCPU = new MarshaledString(cpu);
            using var marshaledFeatures = new MarshaledString(features);
            return LLVM.CreateTargetMachine(this, marshaledTriple, marshaledCPU, marshaledFeatures, level, reloc, codeModel);
        }
    }
}
