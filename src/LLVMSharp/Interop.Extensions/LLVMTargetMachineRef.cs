// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMTargetMachineRef : IEquatable<LLVMTargetMachineRef>
    {
        public LLVMTargetMachineRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMTargetMachineRef(LLVMOpaqueTargetMachine* value)
        {
            return new LLVMTargetMachineRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueTargetMachine*(LLVMTargetMachineRef value)
        {
            return (LLVMOpaqueTargetMachine*)value.Handle;
        }

        public static bool operator ==(LLVMTargetMachineRef left, LLVMTargetMachineRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMTargetMachineRef left, LLVMTargetMachineRef right) => !(left == right);

        public string CreateTargetDataLayout()
        {
            var pDataLayout = LLVM.CreateTargetDataLayout(this);

            if (pDataLayout is null)
            {
                return string.Empty;
            }

            var span = new ReadOnlySpan<byte>(pDataLayout, int.MaxValue);
            return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
        }

        public void EmitToFile(LLVMModuleRef module, string fileName, LLVMCodeGenFileType codegen)
        {
            if (!TryEmitToFile(module, fileName, codegen, out string Error))
            {
                throw new ExternalException(Error);
            }
        }

        public override bool Equals(object obj) => obj is LLVMTargetMachineRef other && Equals(other);

        public bool Equals(LLVMTargetMachineRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();

        public bool TryEmitToFile(LLVMModuleRef module, string fileName, LLVMCodeGenFileType codegen, out string message)
        {
            using var marshaledFileName = new MarshaledString(fileName);

            sbyte* errorMessage;
            int result = LLVM.TargetMachineEmitToFile(this, module, marshaledFileName, codegen, &errorMessage);

            if (errorMessage is null)
            {
                message = string.Empty;
            }
            else
            {
                var span = new ReadOnlySpan<byte>(errorMessage, int.MaxValue);
                message = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
                LLVM.DisposeErrorMessage(errorMessage);
            }

            return result == 0;
        }
    }
}
