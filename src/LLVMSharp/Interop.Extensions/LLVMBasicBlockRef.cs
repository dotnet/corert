// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMBasicBlockRef : IEquatable<LLVMBasicBlockRef>
    {
        public LLVMBasicBlockRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static explicit operator LLVMBasicBlockRef(LLVMOpaqueValue* value)
        {
            return new LLVMBasicBlockRef((IntPtr)value);
        }

        public static implicit operator LLVMBasicBlockRef(LLVMOpaqueBasicBlock* value)
        {
            return new LLVMBasicBlockRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueBasicBlock*(LLVMBasicBlockRef value)
        {
            return (LLVMOpaqueBasicBlock*)value.Handle;
        }

        public static implicit operator LLVMOpaqueValue*(LLVMBasicBlockRef value)
        {
            return (LLVMOpaqueValue*)value.Handle;
        }

        public LLVMValueRef FirstInstruction => (Handle != IntPtr.Zero) ? LLVM.GetFirstInstruction(this) : default;

        public LLVMValueRef LastInstruction => (Handle != IntPtr.Zero) ? LLVM.GetLastInstruction(this) : default;

        public LLVMBasicBlockRef Next => (Handle != IntPtr.Zero) ? LLVM.GetNextBasicBlock(this) : default;

        public LLVMValueRef Parent => (Handle != IntPtr.Zero) ? LLVM.GetBasicBlockParent(this) : default;

        public LLVMBasicBlockRef Previous => (Handle != IntPtr.Zero) ? LLVM.GetPreviousBasicBlock(this) : default;

        public LLVMValueRef Terminator => (Handle != IntPtr.Zero) ? LLVM.GetBasicBlockTerminator(this) : default;

        public static bool operator ==(LLVMBasicBlockRef left, LLVMBasicBlockRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMBasicBlockRef left, LLVMBasicBlockRef right) => !(left == right);

        public LLVMValueRef AsValue() => LLVM.BasicBlockAsValue(this);

        public void Delete() => LLVM.DeleteBasicBlock(this);

        public void Dump() => LLVM.DumpValue(this);

        public override bool Equals(object obj) => obj is LLVMBasicBlockRef other && Equals(other);

        public bool Equals(LLVMBasicBlockRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();

        public LLVMBasicBlockRef InsertBasicBlock(string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.InsertBasicBlock(this, marshaledName);
        }

        public void MoveAfter(LLVMBasicBlockRef MovePos) => LLVM.MoveBasicBlockAfter(this, MovePos);

        public void MoveBefore(LLVMBasicBlockRef MovePos) => LLVM.MoveBasicBlockBefore(this, MovePos);

        public string PrintToString()
        {
            var pStr = LLVM.PrintValueToString(this);

            if (pStr is null)
            {
                return string.Empty;
            }
            var span = new ReadOnlySpan<byte>(pStr, int.MaxValue);

            var result = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            LLVM.DisposeMessage(pStr);
            return result;
        }

        public void RemoveFromParent() => LLVM.RemoveBasicBlockFromParent(this);

        public override string ToString() => (Handle != IntPtr.Zero) ? PrintToString() : string.Empty;
    }
}
